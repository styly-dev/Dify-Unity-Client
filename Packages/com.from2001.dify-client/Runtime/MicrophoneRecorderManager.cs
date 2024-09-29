using UnityEngine;
using System;

public class MicrophoneRecorderManager : MonoBehaviour
{
    public float silenceThreshold = 0.01f; // 無音と判断する閾値
    public int sampleRate = 44100; // サンプリングレート
    public string microphoneName;

    private AudioClip audioClip;

    void Start()
    {
        Debug.Log("MicrophoneRecorderManager started.");
        InitializeMicrophone();
    }

    /// <summary>
    /// マイクの初期化
    /// </summary>
    private void InitializeMicrophone()
    {
        if (Microphone.devices.Length > 0)
        {
            foreach (var device in Microphone.devices)
            {
                if (string.IsNullOrEmpty(microphoneName))
                {
                    microphoneName = device;
                    Debug.Log("microphoneName: " + device);
                }
            }
        }
        else
        {
            Debug.LogError("No microphone detected.");
        }
    }

    /// <summary>
    /// マイクの録音を開始する
    /// </summary>
    public void StartMicrophone()
    {
        audioClip = Microphone.Start(microphoneName, true, 10, sampleRate);
        Debug.Log("Microphone started: " + microphoneName);
    }

    /// <summary>
    /// マイクの録音を停止して、無音部分をトリムしたAudioClipを返す
    /// </summary> 
    public AudioClip StopMicrophone()
    {
        if (Microphone.IsRecording(microphoneName))
        {
            Microphone.End(microphoneName);
            Debug.Log("Microphone stopped: " + microphoneName);
            return TrimSilence(audioClip, silenceThreshold);
        }
        else
        {
            // throw new Exception("Microphone is not recording.");
            return null;
        }
    }

    /// <summary>
    /// AudioClipの無音部分をトリムする
    /// </summary>
    AudioClip TrimSilence(AudioClip clip, float min)
    {
        float[] samples = new float[clip.samples];
        clip.GetData(samples, 0);

        int start = -1;
        int end = -1;

        for (int i = 0; i < samples.Length; i++)
        {
            if (Mathf.Abs(samples[i]) > min)
            {
                start = i;
                break;
            }
        }

        if (start == -1) // 全てが無音の場合
        {
            return null;
        }

        for (int i = samples.Length - 1; i >= start; i--)
        {
            if (Mathf.Abs(samples[i]) > min)
            {
                end = i;
                break;
            }
        }

        int length = end - start + 1;
        float[] trimmedSamples = new float[length];
        System.Array.Copy(samples, start, trimmedSamples, 0, length);

        AudioClip trimmedClip = AudioClip.Create(clip.name + "_trimmed", length, clip.channels, clip.frequency, false);
        trimmedClip.SetData(trimmedSamples, 0);
        return trimmedClip;
    }
}
