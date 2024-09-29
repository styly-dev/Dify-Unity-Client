using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;
using UnityEngine.Events;

[UnitTitle("Send Chat Message to Dify in streaming node")] // Title of the node in the graph
[UnitCategory("Dify")]   // The category path in the add node menu
public class SendChatMessage_Streaming : Unit
{
    private bool isProcessing = false;

    // Define ports here
    [DoNotSerialize]
    public ControlInput inputTrigger;

    [DoNotSerialize]
    public ValueInput inputDifyManager;

    [DoNotSerialize]
    public ValueInput inpytQuery;

    [DoNotSerialize]
    public ValueInput inputTexture2D;


    [DoNotSerialize]
    public ControlOutput outputTrigger;

    [DoNotSerialize]
    public ValueOutput valueOutput_MessageText;


    [DoNotSerialize]
    public ControlOutput outputTrigger_OnMessageChunk;

    [DoNotSerialize]
    public ValueOutput valueOutput_MessageChunkText;


    [DoNotSerialize]
    public ControlOutput outputTrigger_event;

    [DoNotSerialize]
    public ValueOutput valueOutput_event_name;

    [DoNotSerialize]
    public ValueOutput valueOutput_event_json;

    protected override void Definition()
    {
        // Define input and output ports
        inputTrigger = ControlInputCoroutine("input", OnInputTriggered);

        inputDifyManager = ValueInput<DifyManager>("DifyManager", null);
        inpytQuery = ValueInput<string>("Query", "");
        inputTexture2D = ValueInput<Texture2D>("Texture2D", null);

        outputTrigger = ControlOutput("Message Finished");
        valueOutput_MessageText = ValueOutput<string>("Message");

        outputTrigger_OnMessageChunk = ControlOutput("Message Chunk Received");
        valueOutput_MessageChunkText = ValueOutput<string>("Message Chunk");

        outputTrigger_event = ControlOutput("Chunk Respose Event");
        valueOutput_event_name = ValueOutput<string>("Event Name");
        valueOutput_event_json = ValueOutput<fsData>("Event JSON");

        // Connect ports
        Succession(inputTrigger, outputTrigger);
    }

    // Called when the input trigger is activated
    private IEnumerator OnInputTriggered(Flow flow)
    {
        isProcessing = true;

        // Wait for the next frame
        yield return null;

        // Execute logic here when the node is triggered
        DifyManager difyManager = flow.GetValue<DifyManager>(inputDifyManager);
        string query = flow.GetValue<string>(inpytQuery);
        Texture2D texture2D = flow.GetValue<Texture2D>(inputTexture2D);

        if (difyManager == null)
        {
            // Find the DifyManager in the scene
            difyManager = GameObject.FindObjectOfType<DifyManager>();
            if (difyManager == null)
            {
                Debug.LogError("DifyManager not found in the scene");
                isProcessing = false;
                yield return outputTrigger;
            }
        }

        void InvokeOutputTriggerEvent(string eventName, Flow flow, JObject json)
        {
            fsData fsData = fsJsonParser.Parse(json.ToString());
            flow.SetValue(valueOutput_event_name, eventName);
            flow.SetValue(valueOutput_event_json, fsData);
            flow.Invoke(outputTrigger_event);
        }

        UnityAction<string> listener_OnDifyMessageChunk = (messageChunk) => { flow.SetValue(valueOutput_MessageChunkText, messageChunk); flow.Invoke(outputTrigger_OnMessageChunk); };
        UnityAction<string> listener_OnDifyMessage = (message) => { isProcessing = false; flow.SetValue(valueOutput_MessageText, message); flow.Invoke(outputTrigger); };
        UnityAction<JObject> listener_message = (json) => { InvokeOutputTriggerEvent("message", flow, json); };
        UnityAction<JObject> listener_message_file = (json) => { InvokeOutputTriggerEvent("message_file", flow, json); };
        UnityAction<JObject> listener_message_end = (json) => { InvokeOutputTriggerEvent("message_end", flow, json); };
        UnityAction<JObject> listener_message_replace = (json) => { InvokeOutputTriggerEvent("message_replace", flow, json); };
        UnityAction<JObject> listener_workflow_started = (json) => { InvokeOutputTriggerEvent("workflow_started", flow, json); };
        UnityAction<JObject> listener_node_started = (json) => { InvokeOutputTriggerEvent("node_started", flow, json); };
        UnityAction<JObject> listener_node_finished = (json) => { InvokeOutputTriggerEvent("node_finished", flow, json); };
        UnityAction<JObject> listener_workflow_finished = (json) => { InvokeOutputTriggerEvent("workflow_finished", flow, json); };
        UnityAction<JObject> listener_error = (json) => { InvokeOutputTriggerEvent("error", flow, json); isProcessing = false; };
        UnityAction<JObject> listener_ping = (json) => { InvokeOutputTriggerEvent("ping", flow, json); }; difyManager.Event_ping.AddListener(listener_ping);

        RemoveAllListeners(difyManager);

        difyManager.OnDifyMessageChunk.AddListener(listener_OnDifyMessageChunk);
        difyManager.OnDifyMessage.AddListener(listener_OnDifyMessage);
        difyManager.Event_message.AddListener(listener_message);
        difyManager.Event_message_file.AddListener(listener_message_file);
        difyManager.Event_message_end.AddListener(listener_message_end);
        difyManager.Event_message_replace.AddListener(listener_message_replace);
        difyManager.Event_workflow_started.AddListener(listener_workflow_started);
        difyManager.Event_node_started.AddListener(listener_node_started);
        difyManager.Event_node_finished.AddListener(listener_node_finished);
        difyManager.Event_workflow_finished.AddListener(listener_workflow_finished);
        difyManager.Event_error.AddListener(listener_error);


        void RemoveAllListeners(DifyManager difyManager)
        {
            difyManager.OnDifyMessage.RemoveListener(listener_OnDifyMessage);
            difyManager.OnDifyMessageChunk.RemoveListener(listener_OnDifyMessageChunk);
            difyManager.Event_message.RemoveListener(listener_message);
            difyManager.Event_message_file.RemoveListener(listener_message_file);
            difyManager.Event_message_end.RemoveListener(listener_message_end);
            difyManager.Event_message_replace.RemoveListener(listener_message_replace);
            difyManager.Event_workflow_started.RemoveListener(listener_workflow_started);
            difyManager.Event_node_started.RemoveListener(listener_node_started);
            difyManager.Event_node_finished.RemoveListener(listener_node_finished);
            difyManager.Event_workflow_finished.RemoveListener(listener_workflow_finished);
            difyManager.Event_error.RemoveListener(listener_error);
            difyManager.Event_ping.RemoveListener(listener_ping);
        }

        // Skip tts message events since the event trriggered after the message is finished in some cases.
        // difyManager.Event_tts_message.AddListener((json) => { InvokeOutputTriggerEvent("tts_message", flow, json); });
        // difyManager.Event_tts_message_end.AddListener((json) => { InvokeOutputTriggerEvent("tts_message_end", flow, json); });

        // Send the message to Dify
        Task task = difyManager.SendChatMessage_Streaming(query, texture2D);
        while (!task.IsCompleted) { yield return null; }
        if(task.IsFaulted) { isProcessing = false; }

        // Wait until the message is finished
        while (isProcessing)
        {
            Debug.Log("Processing...");

            // wait for one second
            yield return new WaitForSeconds(1);

            yield return null;
        }

        // Remove all listeners
        RemoveAllListeners(difyManager);
    }


}





