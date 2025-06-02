# Dify Unity Client

A Unity package that provides seamless integration with [Dify](https://dify.ai/), an open-source platform for building AI applications. This client enables Unity developers to easily incorporate AI-powered chat completion, audio-to-text conversion, and other AI services into their Unity projects.

## Features

- **Chat Completion**: Send messages to Dify AI applications with both blocking and streaming modes
- **Audio-to-Text**: Convert audio recordings to text using Dify's speech recognition
- **Microphone Recording**: Built-in microphone recording and playback functionality
- **Visual Scripting Support**: No-code integration with Unity Visual Scripting nodes
- **File Upload**: Support for uploading files to Dify applications
- **Conversation Management**: Handle multiple conversations and message history
- **Unity Events**: Native Unity event system integration for reactive programming
- **Streaming Responses**: Real-time streaming of AI responses with event callbacks

## Requirements

- **Unity**: 2022.1 or later
- **Dependencies**: 
  - Newtonsoft JSON (com.unity.nuget.newtonsoft-json 3.2.1)
- **Platform**: All Unity-supported platforms

## Installation

### Via Unity Package Manager (Recommended)

1. Open Unity Package Manager (`Window > Package Manager`)
2. Click the `+` button and select `Add package from git URL...`
3. Enter the repository URL:
   ```
   https://github.com/styly-dev/Dify-Unity-Client.git?path=Packages/com.from2001.dify-client
   ```
4. Click `Add`

### Manual Installation

1. Download or clone this repository
2. Copy the `Packages/com.from2001.dify-client` folder to your Unity project's `Packages` folder
3. Unity will automatically detect and import the package

## Quick Start

### 1. Setup Dify Manager

Add the `DifyManager` component to a GameObject in your scene:

```csharp
// Get or add DifyManager component
DifyManager difyManager = gameObject.GetComponent<DifyManager>();
if (difyManager == null)
    difyManager = gameObject.AddComponent<DifyManager>();

// Configure Dify settings
difyManager.DifyApiURL = "https://your-dify-instance.com/v1";
difyManager.DifyApiKey = "app-your-api-key-here";
difyManager.DifyUserId = "your-user-id";
```

### 2. Send Chat Messages

```csharp
// Send a blocking chat message
string response = await difyManager.SendChatMessage_blocking("Hello, how are you?");
Debug.Log("AI Response: " + response);

// Send a streaming chat message with events
difyManager.OnDifyMessage.AddListener(OnMessageReceived);
difyManager.SendChatMessage_Streaming("Tell me a story");

private void OnMessageReceived(string message)
{
    Debug.Log("Received: " + message);
}
```

### 3. Audio-to-Text Conversion

```csharp
// Record audio and convert to text
difyManager.StartMicrophone();
// ... wait for user input ...
AudioClip recording = difyManager.StopMicrophone();


string transcription = await difyManager.AudioToText(recording);
Debug.Log("Transcription: " + transcription);
```

## Sample Scene

The package includes a simple example scene that demonstrates streaming chat
via Unity Visual Scripting. After installing the package, open
`Chat Sample in streaming mode.unity` from
`Packages/com.from2001.dify-client/Samples~/Chat sample in streaming mode`.
This scene shows how to send a message and display the AI response in real time.

## Visual Scripting Usage

This package includes Visual Scripting nodes for no-code development:

### Available Nodes

- **Start Microphone**: Begin microphone recording
- **Stop Microphone**: Stop recording and get AudioClip
- **Audio to Text**: Convert AudioClip to text using Dify
- **Send Chat Message (Streaming)**: Send messages with real-time streaming responses

### Using Visual Scripting Nodes

1. Open Visual Scripting graph editor
2. Add nodes from the `Dify` category
3. Connect DifyManager reference (auto-finds if not specified)
4. Wire up your logic flow

## Configuration

### DifyManager Properties

| Property | Description | Example |
|----------|-------------|---------|
| `DifyApiURL` | Your Dify instance API endpoint | `"https://api.dify.ai/v1"` |
| `DifyApiKey` | Application API key from Dify | `"app-abc123..."` |
| `DifyUserId` | Unique identifier for the user | `"user-12345"` |

### Events

The DifyManager provides several Unity Events for reactive programming:

- `OnDifyMessage`: Fired when a complete message is received
- `OnDifyMessageChunk`: Fired for each chunk in streaming mode
- `Event_message`: Raw message events from Dify
- `Event_message_end`: Message completion events
- `Event_error`: Error events from Dify API

## API Reference

### DifyManager Methods

#### Chat Operations
```csharp
// Blocking chat message
Task<string> SendChatMessage_blocking(string query, Texture2D image = null)

// Streaming chat message  
void SendChatMessage_Streaming(string query, Texture2D image = null)

// Stop streaming
void ChatMessage_streaming_stop()
```

#### Audio Operations
```csharp
// Microphone control
void StartMicrophone()
AudioClip StopMicrophone()

// Audio processing
Task<string> AudioToText(AudioClip audioClip)
void PlayAudioClip(AudioClip audioClip)
```

#### Conversation Management
```csharp
// Get conversation list
Task<ConversationsResponse> GetConversationsList(int limit = 20, string lastId = "")

// Get conversation messages
Task<ConversationMessagesResponse> GetConversationMessages(string conversationId, int limit = 20, string lastId = "")

// Rename conversation
Task<ConversationResponse> RenameConversation(string conversationId, bool autoGenerateName, string name = "")
```

### DifyApiClient Methods

The low-level API client provides direct access to Dify services:

```csharp
// File upload
Task<FileUploadResponse> UploadFile(byte[] fileData, string fileName, string user)

// Raw API calls
Task<ChatCompletionResponse> ChatCompletion(string query, string user, bool streaming, string conversationId, Dictionary<string, object> inputs, List<FileInfo> files)
```

## Examples

### Complete Chat Integration

```csharp
public class ChatExample : MonoBehaviour
{
    public DifyManager difyManager;
    public UnityEngine.UI.InputField inputField;
    public UnityEngine.UI.Text responseText;

    void Start()
    {
        // Configure Dify
        difyManager.DifyApiURL = "https://your-dify-instance.com/v1";
        difyManager.DifyApiKey = "your-api-key";
        difyManager.DifyUserId = "user-example";

        // Setup events
        difyManager.OnDifyMessage.AddListener(OnMessageReceived);
    }

    public async void SendMessage()
    {
        string userMessage = inputField.text;
        inputField.text = "";
        
        responseText.text = "Thinking...";
        
        try
        {
            string response = await difyManager.SendChatMessage_blocking(userMessage);
            responseText.text = response;
        }
        catch (Exception e)
        {
            responseText.text = "Error: " + e.Message;
        }
    }

    private void OnMessageReceived(string message)
    {
        responseText.text = message;
    }
}
```

### Voice Input Example

```csharp
public class VoiceInputExample : MonoBehaviour
{
    public DifyManager difyManager;
    public UnityEngine.UI.Button recordButton;
    public UnityEngine.UI.Text statusText;
    
    private bool isRecording = false;

    public void ToggleRecording()
    {
        if (!isRecording)
        {
            StartRecording();
        }
        else
        {
            StopRecording();
        }
    }

    private void StartRecording()
    {
        difyManager.StartMicrophone();
        isRecording = true;
        statusText.text = "Recording...";
        recordButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "Stop";
    }

    private async void StopRecording()
    {
        AudioClip recording = difyManager.StopMicrophone();
        isRecording = false;
        statusText.text = "Processing...";
        recordButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "Record";

        try
        {
            string transcription = await difyManager.AudioToText(recording);
            statusText.text = "Transcription: " + transcription;
            
            // Send transcription as chat message
            string response = await difyManager.SendChatMessage_blocking(transcription);
            statusText.text = "AI: " + response;
        }
        catch (Exception e)
        {
            statusText.text = "Error: " + e.Message;
        }
    }
}
```

## Troubleshooting

### Common Issues

**Error: "Request failed: Unauthorized"**
- Check that your API key is correct
- Ensure the API key has the necessary permissions
- Verify the Dify API URL is correct

**Error: "DifyManager not found in the scene"**
- Make sure you have a DifyManager component in your scene
- The Visual Scripting nodes will auto-find DifyManager if not explicitly connected

**Audio recording not working**
- Check microphone permissions on your target platform
- Ensure your device has a microphone available
- Verify Unity's microphone settings

### Debug Tips

- Enable debug logging in Unity Console
- Check Dify instance logs for API errors
- Verify network connectivity to your Dify instance
- Use Unity Profiler to monitor performance

## Contributing

We welcome contributions! Please see our [contributing guidelines](CONTRIBUTING.md) for details.

### Development Setup

1. Clone the repository
2. Open in Unity 2022.1 or later
3. Install required dependencies
4. Run tests to verify setup

### Submitting Changes

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Credits

- **Developer**: [STYLY, Inc.](https://styly.inc)
- **Repository**: [styly-dev/Dify-Unity-Client](https://github.com/styly-dev/Dify-Unity-Client)
- **Dify Platform**: [dify.ai](https://dify.ai/)

## Support

- **Issues**: [GitHub Issues](https://github.com/styly-dev/Dify-Unity-Client/issues)
- **Email**: info@styly.inc
- **Documentation**: [Unity Package Documentation](https://docs.unity3d.com/Manual/upm-ui.html)

---

For more information about Dify, visit [dify.ai](https://dify.ai/) or check out the [Dify documentation](https://docs.dify.ai/).
