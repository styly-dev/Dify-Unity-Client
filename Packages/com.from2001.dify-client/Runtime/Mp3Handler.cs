using UnityEngine;
using System;
using System.IO;
using UnityEngine.Networking;
using System.Linq;
using NLayer;

public class Mp3Handler : MonoBehaviour
{

    private static byte[] MP3Buffer = new byte[0];
    private static readonly object audioBufferLock = new();

    /// <summary>
    /// Add base64 encoded MP3 data to the MP3 buffer. Then get the AudioClip if the buffer size exceeds 80kb. 
    public static AudioClip AddDataToMp3Buffer(string base64Chunk)
    {
        lock (audioBufferLock)
        {
            byte[] decodedBytes = System.Convert.FromBase64String(base64Chunk);
            MP3Buffer = MP3Buffer.Concat(decodedBytes).ToArray();

            // Convert MP3Buffer to AudioClip when its size exceeds 80kb
            if (MP3Buffer.Length > 80 * 1024)
            {
                AudioClip audioClip = Mp3Handler.GetAudioClipFromMp3Buffer();
                return audioClip;
            }
            else
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Convert MP3 buffer to AudioClip
    /// </summary>
    public static AudioClip GetAudioClipFromMp3Buffer(bool returnEvenWithoutSilent = false)
    {
        byte[] bytesToWrite = GetDataFromMp3Buffer(returnEvenWithoutSilent);
        if (bytesToWrite == null) return null;

        // Write frame data to a file
        string fileName = Guid.NewGuid().ToString() + ".mp3";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);
        Debug.Log($"Save MP3: {filePath}");
        if (bytesToWrite.Length == 0) return null;
        File.WriteAllBytes(filePath, bytesToWrite);

        // Load the mp3 with Unity web request
        UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + filePath, AudioType.MPEG);
        www.SendWebRequest();
        while (!www.isDone) { }
        if (www.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError(www.error);
            return null;
        }
        AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);

        // Delete the temporary file
        File.Delete(filePath);

        return audioClip;
    }

    /// <summary>
    /// Extract the MP3 frame data from the buffer. The returned MP3
    /// </summary>
    private static byte[] GetDataFromMp3Buffer(bool returnEvenWithoutSilent = false)
    {
        lock (audioBufferLock)
        {
            int offset = 0;

            // Find the frame header
            while (offset < MP3Buffer.Length && !IsMp3FrameHeader(MP3Buffer, offset)) { offset++; }

            // If no frame header is found, return null
            if (offset >= MP3Buffer.Length) { return null; }

            // Find the last valid frame starting from the found frame header
            int frameEnd = FindLastValidFrameWithSilent(MP3Buffer, offset, returnEvenWithoutSilent);
            if (frameEnd == -1) { return null; }

            // Get the valid frame data
            int dataSize = frameEnd - offset;
            byte[] extractedData = new byte[dataSize];

            Array.Copy(MP3Buffer, offset, extractedData, 0, dataSize);

            // Overwrite the remaining data to a new MP3Buffer
            int remainingSize = MP3Buffer.Length - frameEnd;
            byte[] newBuffer = new byte[remainingSize];
            Array.Copy(MP3Buffer, frameEnd, newBuffer, 0, remainingSize);
            MP3Buffer = newBuffer;

            return extractedData;
        }
    }

    /// <summary>
    /// Check if the buffer contains an MP3 frame header
    /// </summary> 
    private static bool IsMp3FrameHeader(byte[] data, int offset)
    {
        // Check if the frame header fits in the buffer
        if (offset + 3 >= data.Length) return false;

        // Check the Sync Word (0xFFE)
        return data[offset] == 0xFF && (data[offset + 1] & 0xE0) == 0xE0;
    }

    private static bool IsSilent(byte[] mp3FrameData)
    {
        // Convert MP3 data to a stream
        using (var mp3Stream = new MemoryStream(mp3FrameData))
        {
            // Decode the MP3 using NLayer's MpegFile class
            using (var mpegFile = new MpegFile(mp3Stream))
            {
                // Create a buffer to store the decoded PCM data
                int sampleRate = mpegFile.SampleRate;
                int channels = mpegFile.Channels;
                float[] pcmBuffer = new float[sampleRate * channels];

                int bytesRead;
                bool isSilent = true;

                // Read and check the PCM data
                while ((bytesRead = mpegFile.ReadSamples(pcmBuffer, 0, pcmBuffer.Length)) > 0)
                {
                    // Check if each sample in the PCM data is silent (close to zero)
                    for (int i = 0; i < bytesRead; i++)
                    {
                        // If a sample is not silent, set the isSilent flag to false
                        if (Math.Abs(pcmBuffer[i]) > 0.0001f)
                        {
                            isSilent = false;
                            break;
                        }
                    }

                    if (!isSilent)
                    {
                        break;
                    }
                }
                return isSilent; // Return true if silent, false otherwise
            }
        }
    }

    /// <summary>
    /// Find the last valid frame with silent data
    /// </summary>
    private static int FindLastValidFrameWithSilent(byte[] data, int startOffset, bool returnEvenWithoutSilent = false)
    {
        int offset = startOffset;
        int offsetForSilentFrame = 0;
        while (offset < data.Length)
        {
            // Check if the current offset is a frame header
            if (IsMp3FrameHeader(data, offset))
            {
                // Calculate the frame length
                int frameLength = GetFrameLength(data, offset);

                // If the frame length is invalid or exceeds the buffer length, break
                if (frameLength <= 0 || offset + frameLength > data.Length) { break; }

                try
                {
                    // Check if the frame is silent. Provide two frames to avoid decoder error.
                    bool isSilent = IsSilent(data.Skip(offset).Take(frameLength * 2).ToArray());
                    if (isSilent) { offsetForSilentFrame = offset + frameLength; }
                }
                catch (Exception) { }
                // Move to the next frame
                offset += frameLength;
            }
            else
            {
                break; // Break if there is no next frame
            }
        }
        return offsetForSilentFrame != 0 ? offsetForSilentFrame : returnEvenWithoutSilent ? offset : -1;
    }

    /// <summary>
    /// Get the length of the MP3 frame for both MPEG Version 1 and Version 2, Layer III
    /// </summary>
    private static int GetFrameLength(byte[] data, int offset)
    {
        // Extract MPEG version and Layer information from the frame header
        int mpegVersion = (data[offset + 1] & 0x18) >> 3; // 3 for Version 1, 2 for Version 2
        int layerIndex = (data[offset + 1] & 0x06) >> 1;  // 1 for Layer III

        // Debug output for version and layer information
        Console.WriteLine($"MPEG Version: {mpegVersion}, Layer Index: {layerIndex}");

        // Extract bitrate and sample rate from the frame header
        int bitrateIndex = (data[offset + 2] & 0xF0) >> 4;
        int sampleRateIndex = (data[offset + 2] & 0x0C) >> 2;
        int padding = (data[offset + 2] & 0x02) >> 1;

        // Debug output for header values
        Console.WriteLine($"Bitrate Index: {bitrateIndex}, Sample Rate Index: {sampleRateIndex}, Padding: {padding}");

        // Bitrate tables for MPEG Version 1 and Version 2
        int[] bitratesV1 = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, -1 }; // MPEG Version 1 Layer III
        int[] bitratesV2 = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160, -1 };    // MPEG Version 2 Layer III

        // Sample rate tables for MPEG Version 1 and Version 2
        int[] sampleRatesV1 = { 44100, 48000, 32000, -1 };   // MPEG Version 1 sample rates
        int[] sampleRatesV2 = { 22050, 24000, 16000, -1 };   // MPEG Version 2 sample rates

        int bitrate = 0;
        int sampleRate = 0;

        // Handle MPEG Version 1 and Version 2
        if (mpegVersion == 3 && layerIndex == 1) // MPEG Version 1, Layer III
        {
            if (bitrateIndex < 1 || bitrateIndex >= bitratesV1.Length || sampleRateIndex >= sampleRatesV1.Length)
            {
                Console.WriteLine("Invalid bitrate or sample rate index for Version 1.");
                return -1;
            }

            bitrate = bitratesV1[bitrateIndex] * 1000; // Bitrate in bits per second
            sampleRate = sampleRatesV1[sampleRateIndex];
        }
        else if (mpegVersion == 2 && layerIndex == 1) // MPEG Version 2, Layer III
        {
            if (bitrateIndex < 1 || bitrateIndex >= bitratesV2.Length || sampleRateIndex >= sampleRatesV2.Length)
            {
                Console.WriteLine("Invalid bitrate or sample rate index for Version 2.");
                return -1;
            }

            bitrate = bitratesV2[bitrateIndex] * 1000; // Bitrate in bits per second
            sampleRate = sampleRatesV2[sampleRateIndex];
        }
        else
        {
            Console.WriteLine("Unsupported MPEG version or layer.");
            return -1; // Unsupported MPEG version or layer
        }

        // Debug output for bitrate and sample rate
        Console.WriteLine($"Bitrate: {bitrate}, Sample Rate: {sampleRate}");

        if (bitrate <= 0 || sampleRate <= 0)
        {
            Console.WriteLine("Invalid bitrate or sample rate values.");
            return -1; // Invalid bitrate or sample rate
        }

        // Calculate frame length (144 * bitrate / sample rate + padding) for Layer III
        int frameLength = (144 * bitrate / sampleRate) + padding;

        Console.WriteLine($"Frame Length: {frameLength}");

        return frameLength;
    }

}
