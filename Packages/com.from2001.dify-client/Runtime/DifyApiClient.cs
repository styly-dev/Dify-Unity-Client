using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Net.Http.Headers;


public class DifyApiClient
{
    public string serverUrl { get; set; }
    public string apiKey { get; set; }
    public string user { get; set; }
    private string conversation_id = "";

    private static readonly HttpClient client = new();
    private CancellationTokenSource cancellationTokenSource;
    private List<string> task_ids = new();

    public delegate void OnEventReceived(string eventData);
    public event OnEventReceived EventReceived;

    public DifyApiClient(GameObject gameObject, string serverUrl, string apiKey, string user)
    {
        this.serverUrl = serverUrl;
        this.apiKey = apiKey;
        this.user = user;
    }

    /// <summary>
    /// Send a request to Dify API
    /// </summary>
    private async Task<T> SendRequest<T>(UnityWebRequest request)
    {
        request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        var operation = request.SendWebRequest();
        while (!operation.isDone) { await Task.Yield(); }

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(request.error);
            throw new Exception($"Request failed: {request.error}");
        }
        return JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
    }

    /// <summary>
    /// Stop streaming chat messages
    /// </summary>
    public void ChatMessage_streaming_stop()
    {
        if (cancellationTokenSource != null) { cancellationTokenSource.Cancel(); }
        Mp3Handler.ClearMp3Buffer();

        // Stop generating messages
        foreach (var current_task_id in task_ids)
        {
            _ = StopGenerate(current_task_id);
        }

        // Clear task_ids
        task_ids.Clear();
    }

    /// <summary>
    /// Start a streaming chat message to Dify API
    /// </summary>
    public async Task ChatMessage_streaming_start(string query, Texture2D texture = null)
    {
        if (query == null)
        {
            Debug.LogError("Query is null");
            throw new Exception("Query is null");
        }

        try
        {
            if (texture == null)
            {
                cancellationTokenSource = new CancellationTokenSource();
                await ChatMessage_streaming(cancellationTokenSource.Token, query);
            }
            else
            {
                cancellationTokenSource = new CancellationTokenSource();
                var fileUploadResponse = await UploadTexture2D(texture);
                await ChatMessage_streaming(cancellationTokenSource.Token, query, new string[] { fileUploadResponse.id });
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.Message);
            throw (ex);
        }
    }

    /// <summary>
    /// Start a streaming chat message to Dify API
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <param name="query"></param>
    /// <param name="upload_file_ids"></param>
    /// <param name="inputs"></param>
    /// <returns></returns>//  
    public async Task ChatMessage_streaming(CancellationToken cancellationToken, string query, string[] upload_file_ids = null, Dictionary<string, string> inputs = null)
    {
        // Send request and handle Server-sent events (SSE)

        string response_mode = "streaming";
        upload_file_ids ??= new string[0];
        string url = $"{serverUrl}/chat-messages";
        var payload = new
        {
            inputs = inputs ?? new Dictionary<string, string>(),
            query = query,
            response_mode = response_mode,
            conversation_id = this.conversation_id,
            user = this.user,
            files = upload_file_ids.Select(id => new { type = "image", transfer_method = "local_file", upload_file_id = id }).ToList()
        };

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        try
        {
            var jsonBody = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var reader = new System.IO.StreamReader(stream))
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (line != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            ProcessEvent(line);
                        }
                    }
                    else
                    {
                        // SSE stream ended, we can either reconnect or handle as needed
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception in SSE connection: {ex.Message}");
            throw (ex);
        }
    }

    /// <summary>
    /// Process the event received from Dify API
    /// </summary>
    /// <param name="eventString"></param>//  
    private void ProcessEvent(string eventString)
    {
        if (eventString.StartsWith("data:"))
        {
            // Extract data from event string
            var dataJsonString = eventString.Substring(5).Trim(); // Remove "data: " prefix
            var dataJson = JObject.Parse(dataJsonString);

            // Invoke event received
            EventReceived?.Invoke(dataJsonString);

            // If task_id is received, store it
            if (dataJson.TryGetValue("task_id", out var taskId) && !task_ids.Contains(taskId.ToString()))
            {
                task_ids.Add(taskId.ToString());
            }
        }
    }

    /// <summary>
    /// Start a blocking chat message to Dify API
    /// </summary>
    /// <param name="query"></param>//  
    public async Task<ChatCompletionResponse> ChatMessage_blocking_start(string query, Texture2D texture = null)
    {
        if (texture == null)
        {
            cancellationTokenSource = new CancellationTokenSource();
            var response = await ChatMessage_blocking(query);
            return response;
        }
        else
        {
            var fileUploadResponse = await UploadTexture2D(texture);
            var response = await ChatMessage_blocking(query, new string[] { fileUploadResponse.id });
            return response;
        }
    }

    /// <summary>
    /// /// Send a chat message to Dify API
    /// </summary>
    public async Task<ChatCompletionResponse> ChatMessage_blocking(string query, string[] upload_file_ids = null, Dictionary<string, string> inputs = null)
    {
        string response_mode = "blocking";
        upload_file_ids ??= new string[0];
        string url = $"{serverUrl}/chat-messages";
        var payload = new
        {
            inputs = inputs ?? new Dictionary<string, string>(),
            query = query,
            response_mode = response_mode,
            conversation_id = this.conversation_id,
            user = this.user,
            files = upload_file_ids.Select(id => new { type = "image", transfer_method = "local_file", upload_file_id = id }).ToList()
        };

        using (UnityWebRequest webRequest = new(url, "POST"))
        {
            string jsonBody = JsonConvert.SerializeObject(payload);
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            var ret = await SendRequest<ChatCompletionResponse>(webRequest);
            this.conversation_id = ret.conversation_id;
            return ret;
        }
    }

    /// <summary>
    /// Upload a file to Dify API
    /// </summary>
    public async Task<FileUploadResponse> UploadFile(byte[] fileData, string fileName, string mimeType)
    {
        string url = $"{serverUrl}/files/upload";

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", fileData, fileName, mimeType);
        form.AddField("user", user);

        using (UnityWebRequest webRequest = UnityWebRequest.Post(url, form))
        {
            return await SendRequest<FileUploadResponse>(webRequest);
        }
    }

    /// <summary>
    /// Upload a Texture2D to Dify API
    /// </summary>
    public async Task<FileUploadResponse> UploadTexture2D(Texture2D texture)
    {
        // Convert Texture2D to JPEG byte array
        byte[] jpgData = texture.EncodeToJPG(60);
        return await UploadFile(jpgData, "upload.jpg", "image/jpeg");
    }

    /// <summary>
    /// Stop generating messages
    /// </summary> 
    public async Task<bool> StopGenerate(string taskId)
    {
        string url = $"{serverUrl}/chat-messages/{taskId}/stop";
        string jsonBody = JsonConvert.SerializeObject(new { user = user });

        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            await SendRequest<object>(webRequest);
            return true;
        }
    }

    /// <summary>
    /// Get messages of a conversation
    /// </summary>
    public async Task<ConversationMessagesResponse> GetConversationMessages(string conversationId, string firstId, int limit)
    {
        string url = $"{serverUrl}/messages?conversation_id={conversationId}&user={user}&first_id={firstId}&limit={limit}";

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            return await SendRequest<ConversationMessagesResponse>(webRequest);
        }
    }

    /// <summary>
    /// Get conversations
    /// </summary> 
    public async Task<ConversationsResponse> GetConversations(string lastId, int limit, bool? pinned)
    {
        string url = $"{serverUrl}/conversations?user={user}&last_id={lastId}&limit={limit}";
        if (pinned.HasValue)
        {
            url += $"&pinned={pinned.Value}";
        }

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            return await SendRequest<ConversationsResponse>(webRequest);
        }
    }

    /// <summary>
    ///  Delete a conversation
    /// </summary>
    public async Task<string> DeleteConversation(string conversationId)
    {
        string url = $"{serverUrl}/conversations/{conversationId}";
        string jsonBody = JsonConvert.SerializeObject(new { user = user });

        using (UnityWebRequest webRequest = new UnityWebRequest(url, "DELETE"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            await SendRequest<object>(webRequest);
            return "Conversation deleted successfully";
        }
    }

    /// <summary>
    /// Rename a conversation
    /// </summary>
    public async Task<ConversationResponse> RenameConversation(string conversationId, bool autoGenerateName, string name = "")
    {
        string url = $"{serverUrl}/conversations/{conversationId}/name";
        string jsonBody = JsonConvert.SerializeObject(new { name = name, auto_generate = autoGenerateName });

        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            return await SendRequest<ConversationResponse>(webRequest);
        }
    }

    /// <summary>
    /// Convert audio(speech) to text using Dify API
    /// </summary>
    public async Task<string> AudioToText(AudioClip audio)
    {
        string url = $"{serverUrl}/audio-to-text";

        // Convert AudioClip to WAV byte array
        byte[] wavData = Mp3Handler.ConvertAudioClipToWav(audio);

        WWWForm form = new();
        form.AddBinaryData("file", wavData, "upload.wav", "audio/wav");

        // Send request
        UnityWebRequest webRequest = UnityWebRequest.Post(url, form);
        var audioToTextResponse = await SendRequest<AudioToTextResponse>(webRequest);

        return audioToTextResponse.text;
    }

    /// <summary>
    /// Convert text to audio using Dify API
    /// Set text or message_id (set text="" if you want to use message_id)
    /// </summary>
    public async Task<AudioClip> TextToAudio(string text, string message_id = "")
    {
        string url = $"{serverUrl}/text-to-audio";
        var payload = message_id != "" ?
            (object)new { message_id = message_id, user = this.user } :
            (object)new { text = text, user = this.user };

        // Send request
        string jsonPayload = JsonConvert.SerializeObject(payload);
        UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerAudioClip(url, AudioType.MPEG);
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + apiKey);
        var operation = request.SendWebRequest();

        // Wait for response
        while (!operation.isDone) { await Task.Yield(); }

        // Handle response
        if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError(request.error);
            Debug.LogError(request.downloadHandler.text);
            throw new Exception($"Request failed: {request.error}");
        }
        else
        {
            // Get AudioClip from response
            return DownloadHandlerAudioClip.GetContent(request);
        }
    }

    // Not implemented yet
    // /parameters #Get Application Information
    // /meta #Get Application Meta Information
}

[System.Serializable]
public class FileInfo
{
    public string type;
    public string transfer_method;
    public string url;
    public string upload_file_id;
}

[System.Serializable]
public class ChatCompletionResponse
{
    public string message_id;
    public string conversation_id;
    public string mode;
    public string answer;
    public Metadata metadata;
    public long created_at;
}

[System.Serializable]
public class Metadata
{
    public Usage usage;
    public List<RetrieverResource> retriever_resources;
}

[System.Serializable]
public class Usage
{
    public int prompt_tokens;
    public string prompt_unit_price;
    public string prompt_price_unit;
    public string prompt_price;
    public int completion_tokens;
    public string completion_unit_price;
    public string completion_price_unit;
    public string completion_price;
    public int total_tokens;
    public string total_price;
    public string currency;
    public float latency;
}

[System.Serializable]
public class RetrieverResource
{
    public int position;
    public string dataset_id;
    public string dataset_name;
    public string document_id;
    public string document_name;
    public string segment_id;
    public float score;
    public string content;
}

[System.Serializable]
public class FileUploadResponse
{
    public string id;
    public string name;
    public int size;
    public string extension;
    public string mime_type;
    public string created_by;
    public long created_at;
}

[System.Serializable]
public class ConversationMessagesResponse
{
    public int limit;
    public bool has_more;
    public List<Message> data;
}

[System.Serializable]
public class Message
{
    public string id;
    public string conversation_id;
    public Dictionary<string, object> inputs;
    public string query;
    public string answer;
    public List<MessageFile> message_files;
    public Feedback feedback;
    public List<RetrieverResource> retriever_resources;
    public long created_at;
}

[System.Serializable]
public class MessageFile
{
    public string id;
    public string type;
    public string url;
    public string belongs_to;
}

[System.Serializable]
public class Feedback
{
    public string rating;
}

[System.Serializable]
public class ConversationsResponse
{
    public int limit;
    public bool has_more;
    public List<Conversation> data;
}

[System.Serializable]
public class Conversation
{
    public string id;
    public string name;
    public Dictionary<string, object> inputs;
    public string status;
    public long created_at;
}

[System.Serializable]
public class ConversationResponse
{
    public string id;
    public string name;
    public Dictionary<string, object> inputs;
    public string introduction;
    public long created_at;
}

[System.Serializable]
public class AudioToTextResponse
{
    public string text;
}
