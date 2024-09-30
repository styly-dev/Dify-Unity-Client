using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine.Events;
using UnityEngine.Networking;

public class DifyManager : MonoBehaviour
{
    [SerializeField]
    private string difyApiURL = "https://xxxxxxxxxxx/v1";
    [SerializeField]
    private string difyApiKey = "app-xxxxxxxxxxxxxxxxxx";
    [SerializeField]
    private string difyUserId = "user-id";

    [SerializeField]
    private MicrophoneRecorderManager microphoneRecorderManager;

    public string DifyApiURL
    {
        get { return difyApiURL; }
        set { difyApiURL = value; difyClient.serverUrl = value; }
    }

    public string DifyApiKey
    {
        get { return difyApiKey; }
        set { difyApiKey = value; difyClient.apiKey = value; }
    }

    public string DifyUserId
    {
        get { return difyUserId; }
        set { difyUserId = value; difyClient.user = value; }
    }

    [Serializable]
    public class StringEvent : UnityEvent<string> { }
    [Serializable]
    public class ChunkChatCompletionResponseEvent : UnityEvent<JObject> { }

    // UnityEvent for OnMessage for both SendChatMessage_blocking and SendChatMessage_Streaming
    public StringEvent OnDifyMessage;
    public StringEvent OnDifyMessageChunk;
    private string difyMessageByChunk = "";

    // UnityEvents for Dify events of SendChatMessage_Streaming
    public ChunkChatCompletionResponseEvent Event_message;
    public ChunkChatCompletionResponseEvent Event_message_file;
    public ChunkChatCompletionResponseEvent Event_message_end;
    public ChunkChatCompletionResponseEvent Event_tts_message;
    public ChunkChatCompletionResponseEvent Event_tts_message_end;
    public ChunkChatCompletionResponseEvent Event_message_replace;
    public ChunkChatCompletionResponseEvent Event_workflow_started;
    public ChunkChatCompletionResponseEvent Event_node_started;
    public ChunkChatCompletionResponseEvent Event_node_finished;
    public ChunkChatCompletionResponseEvent Event_workflow_finished;
    public ChunkChatCompletionResponseEvent Event_error;
    public ChunkChatCompletionResponseEvent Event_ping;

    private DifyApiClient difyClient;
    private readonly Queue<string> _stringQueue = new();

    private static readonly object audioBufferLock = new();
    private readonly Queue<AudioClip> audioClipQueue = new();
    private AudioSource difyAudioSource;

    // Google Text To Speech用のSentenceQueue
    private readonly Queue<string> sentenceQueue = new();
    private string difyMessageByChunkForSentenceQueue = "";

    void Awake()
    {
        Debug.Log("Starting DifyManager");

        // Initialization
        Initialization();

        // Start continuous coroutine processes
        StartCoroutine(PlayAudioClipsContinuously());
        StartCoroutine(ProcessReceivedDataFromDifyInTheMainThread());

        // Start coroutine for converting sentence to audio
        StartCoroutine(ConvertSentenceToAudio());
    }

    /// <summary>
    /// Initialization
    /// <summary>
    private void Initialization()
    {
        // Initialize Dify API client
        difyClient = new DifyApiClient(this.gameObject, difyApiURL, difyApiKey, difyUserId);
        difyClient.EventReceived += EnqueueStreamingEventJsonReceivedFromDify;

        // Initialize audio source
        difyAudioSource = gameObject.GetComponent<AudioSource>();
        if (difyAudioSource == null) difyAudioSource = gameObject.AddComponent<AudioSource>();
        difyAudioSource.playOnAwake = false;
        difyAudioSource.loop = false;

        // Initialize Microphone Recorder Manager
        microphoneRecorderManager = gameObject.GetComponent<MicrophoneRecorderManager>();
        if (microphoneRecorderManager == null) microphoneRecorderManager = gameObject.AddComponent<MicrophoneRecorderManager>();
    }

    /// <summary>
    /// Convert the sentence to audio via Google Text To Speech API
    /// </summary>
    private IEnumerator ConvertSentenceToAudio()
    {
        string TextToSpeechApiURL = "http://34.85.124.203/TextToSpeech.php?text=";

        while (true)
        {
            if (sentenceQueue.Count == 0) { yield return null; continue; }
            string sentence = sentenceQueue.Dequeue();
            string url = TextToSpeechApiURL + UnityWebRequest.EscapeURL(sentence);

            // TextToSpeechApiURLはmp3を直接返してくるAPIです。AudioClipに格納する
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
            {
                yield return www.SendWebRequest();
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError(www.error);
                }
                else
                {
                    AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                    audioClipQueue.Enqueue(audioClip);
                }
            }

            yield return null;
        }
    }


    /// <summary>
    /// Play the audio clips continuously
    /// </summary>
    private IEnumerator PlayAudioClipsContinuously()
    {
        while (true)
        {
            if (audioClipQueue.Count == 0)
            {
                yield return null;
                continue;
            }
            AudioClip currentClip = audioClipQueue.Dequeue();
            difyAudioSource.clip = currentClip;
            difyAudioSource.Play();
            yield return new WaitForSeconds(currentClip.length);
        }
    }

    /// <summary>
    /// Process the received data from Dify in the main thread
    /// </summary>
    private IEnumerator ProcessReceivedDataFromDifyInTheMainThread()
    {
        while (true)
        {
            if (_stringQueue.Count == 0) { yield return null; continue; }
            string command = _stringQueue.Dequeue();
            DifyOnStreamingEventReceivedCallBack(command);
            yield return null;
        }
    }

    /// <summary>
    /// Send a chat message to Dify in streaming mode
    /// </summary>
    public async Task SendChatMessage_Streaming(string query, Texture2D texture = null)
    {
        StopStreaming();
        difyMessageByChunk = "";
        try
        {
            await difyClient.ChatMessage_streaming_start(query, texture);
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.Message);
            throw(ex);
        }
    }

    /// <summary>
    /// Send a chat message to Dify in blocking mode
    /// </summary> 
    public void SendChatMessage_blocking(string query, Texture2D texture = null)
    {
        _ = SendChatMessage_blockingAsync(query, texture);
    }

    /// <summary>
    /// Send a chat message to Dify and wait for the response
    /// <summary>
    public async Task<string> SendChatMessage_blockingAsync(string query, Texture2D texture = null)
    {
        var response = await difyClient.ChatMessage_blocking_start(query, texture);
        Debug.Log(response.answer);
        OnDifyMessage.Invoke(response.answer);
        return response.answer;
    }

    public void StopStreaming()
    {

        Debug.Log("Stopping Streaming.....");

        // Cancel
        difyClient.ChatMessage_streaming_stop();

        // Clear queue
        _stringQueue.Clear();
        audioClipQueue.Clear();
        difyMessageByChunk = "";

        // Stop Audio
        difyAudioSource.Stop();
        difyAudioSource.clip = null;

        // Clear variables for Google Text To Speech
        sentenceQueue.Clear();
        difyMessageByChunkForSentenceQueue = "";
    }

    /// <summary>
    /// Enqueue the event received from Dify in order to process it in the main thread
    /// </summary>
    /// <param name="jsonString"></param>
    public void EnqueueStreamingEventJsonReceivedFromDify(string jsonString)
    {
        if (string.IsNullOrEmpty(jsonString)) return;
        _stringQueue.Enqueue(jsonString);
    }

    /// <summary>
    /// Convert audio to text using Dify
    /// <summary> 
    public async Task<string> AudioToText(AudioClip audioClip)
    {
        var txt = await difyClient.AudioToText(audioClip);
        return txt;
    }

    /// <summary>
    /// Callback for when an event is received
    /// </summary> 
    private void DifyOnStreamingEventReceivedCallBack(string jsonString)
    {
        JObject json = JObject.Parse(jsonString);
        string eventValue = json["event"].ToString();

        switch (eventValue)
        {
            case "message":
                Event_message.Invoke(json);
                ProcessEvent_message(json);
                break;
            case "message_file":
                Event_message_file.Invoke(json);
                break;
            case "message_end":
                Event_message_end.Invoke(json);
                ProcessEvent_message_end(json);
                break;
            case "tts_message":
                Event_tts_message.Invoke(json);
                // ProcessEvent_tts_message(json);
                break;
            case "tts_message_end":
                Event_tts_message_end.Invoke(json);
                // ProcessEvent_tts_message_end(json);
                break;
            case "message_replace":
                Event_message_replace.Invoke(json);
                break;
            case "workflow_started":
                Event_workflow_started.Invoke(json);
                break;
            case "node_started":
                Event_node_started.Invoke(json);
                break;
            case "node_finished":
                Event_node_finished.Invoke(json);
                break;
            case "workflow_finished":
                Event_workflow_finished.Invoke(json);
                break;
            case "error":
                Event_error.Invoke(json);
                break;
            case "ping":
                Event_ping.Invoke(json);
                break;
            default:
                break;
        }
    }

    private void ProcessEvent_message(JObject json)
    {
        string answer = Regex.Unescape(json["answer"].ToString()).Replace("\n", "");
        OnDifyMessageChunk.Invoke(answer);
        difyMessageByChunk += answer;

        // Google Text To Speech用のSentenceQueueに追加
        difyMessageByChunkForSentenceQueue += answer;
        // difyMessageByChunkForSentenceQueueに「。」「！」「 」「\n」などの区切り文字が含まれている場合、区切り文字を含むその文字までの文字列をSentenceQueueに追加。difyMessageByChunkForSentenceQueueには区切り文字以降の文字列を格納。
        string[] delimiters = { "。", "！", "？", "!", "?", " ", "\n" };
        foreach (string delimiter in delimiters)
        {
            if (difyMessageByChunkForSentenceQueue.Contains(delimiter))
            {
                string[] sentences = difyMessageByChunkForSentenceQueue.Split(delimiter);
                for (int i = 0; i < sentences.Length - 1; i++)
                {
                    sentenceQueue.Enqueue(sentences[i] + delimiter);
                }
                difyMessageByChunkForSentenceQueue = sentences[sentences.Length - 1];
            }
        }
    }

    private void ProcessEvent_message_end(JObject json)
    {
        OnDifyMessage.Invoke(difyMessageByChunk);

        // Google Text To Speech用のSentenceQueueに追加
        if (!string.IsNullOrEmpty(difyMessageByChunkForSentenceQueue))
        {
            sentenceQueue.Enqueue(difyMessageByChunkForSentenceQueue);
            difyMessageByChunkForSentenceQueue = "";
        }
    }

    /// <summary>
    /// Process the TTS message event
    /// </summary>
    private void ProcessEvent_tts_message(JObject json)
    {
        // Debug.Log("Processing TTS message: " + json);
        lock (audioBufferLock)
        {
            string base64Chunk = json["audio"].ToString();
            AudioClip audioClip = Mp3Handler.AddDataToMp3Buffer(base64Chunk);
            if (audioClip != null) { audioClipQueue.Enqueue(audioClip); }
        }
    }

    /// <summary>
    /// Process the end of the TTS message
    /// </summary>
    /// <param name="json"></param>
    private void ProcessEvent_tts_message_end(JObject json)
    {
        AudioClip audioClip = Mp3Handler.GetAudioClipFromMp3Buffer(true);
        if (audioClip != null) { audioClipQueue.Enqueue(audioClip); }
    }

    /// <summary>
    /// Start the microphone
    /// </summary>
    public void StartMicrophone()
    {
        Debug.Log("Starting Microphone recording.....");
        microphoneRecorderManager.StartMicrophone();
    }

    /// <summary>
    /// Stop the microphone and return the AudioClip
    /// </summary> 
    public AudioClip StopMicrophone()
    {
        Debug.Log("Stopping Microphone recording.....");
        return microphoneRecorderManager.StopMicrophone();
    }

}