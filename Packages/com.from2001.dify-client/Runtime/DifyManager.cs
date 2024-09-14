using UnityEngine;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Text.RegularExpressions;

public class DifyManager : MonoBehaviour
{
    private DifyApiClient difyClient;
    private readonly Queue<string> _stringQueue = new();

    private static readonly object audioBufferLock = new();
    private readonly Queue<AudioClip> audioClipQueue = new();
    private AudioSource difyAudioSource;

    [SerializeField]
    private string difyApiURL = "";
    [SerializeField]
    private string difyAppKey = "";
    [SerializeField]
    private string difyUserId = "";

    void Start()
    {
        Debug.Log("Starting DifyManager");
        difyClient = new DifyApiClient(this.gameObject, difyApiURL, difyAppKey, difyUserId);
        difyClient.EventReceived += EnqueueStreamingEventJsonReceivedFromDify;
        difyAudioSource = gameObject.AddComponent<AudioSource>();
        difyAudioSource.playOnAwake = false;
        difyAudioSource.loop = false;




        SendChatMessageExample_Straming("旅行に行くならどこがおすすめ？10個の候補を上げて、それぞれについて詳しく説明してください。");

        // Texture2D texture = gameObject.GetComponent<Renderer>().material.mainTexture as Texture2D;
        // SendChatMessageExample_Straming("何が写ってますか？",texture);
        

        StartCoroutine(PlayAudioClipsContinuously());
        StartCoroutine(ProcessReceivedDataFromDifyInTheMainThread());
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
            if (_stringQueue.Count == 0)
            {
                yield return null;
            }
            else
            {
                string command = _stringQueue.Dequeue();
                DifyOnStreamingEventReceivedCallBack(command);
                yield return null;
            }
        }
    }

    private void Update()
    {
    }

    private async Task SendChatMessageExample()
    {
        try
        {
            var response = await difyClient.ChatMessage_blocking("何が写ってますか？");
            // Debug.Log($"Received response: {response.answer}");
        }
        catch (Exception)
        {
            // Debug.LogError($"An error occurred: {e.Message}");
        }
    }

    private void SendChatMessageExample_Straming(string query,Texture2D texture = null)
    {
        difyClient.ChatMessage_streaming_start(query,texture);
    }

    // public void SendChatMessage(string query, ){

    // }

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
    /// Callback for when an event is received
    /// </summary> 
    private void DifyOnStreamingEventReceivedCallBack(string jsonString)
    {
        JObject json = JObject.Parse(jsonString);
        string eventValue = json["event"].ToString();

// Debug.Log(jsonString);

        switch (eventValue)
        {
            case "message":
                ProcessEvent_message(json);
                break;
            case "tts_message":
                ProcessEvent_tts_message(json);
                break;
            case "tts_message_end":
                ProcessEvent_tts_message_end(json);
                break;
            default:
                break;
        }
    }

    private void ProcessEvent_message(JObject json)
    {
        var answer = Regex.Unescape(json["answer"].ToString()).Replace("\n", "");
        Debug.Log(answer);
    }


    /// <summary>
    /// Process the TTS message event
    /// </summary>
    private void ProcessEvent_tts_message(JObject json)
    {
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

    private async Task TextureUploadExample()
    {
        try
        {
            // Get texture of this GameObject
            Texture2D texture = gameObject.GetComponent<Renderer>().material.mainTexture as Texture2D;
            var response = await difyClient.UploadTexture2D(texture);
            Debug.Log($"Received response: {response.id}");
        }
        catch (Exception e)
        {
            Debug.LogError($"An error occurred: {e.Message}");
        }
    }
}