using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

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

            difyManager.OnDifyMessageChunk.AddListener((messageChunk) =>
            {
                flow.SetValue(valueOutput_MessageChunkText, messageChunk);
                flow.Invoke(outputTrigger_OnMessageChunk);
            });

            difyManager.OnDifyMessage.AddListener((message) =>
            {
                isProcessing = false;
                flow.SetValue(valueOutput_MessageText, message);
                flow.Invoke(outputTrigger);
            });

            void InvokeOutputTriggerEvent(string eventName, Flow flow, JObject json)
            {
                fsData fsData = fsJsonParser.Parse(json.ToString());
                flow.SetValue(valueOutput_event_name, eventName);
                flow.SetValue(valueOutput_event_json, fsData);
                flow.Invoke(outputTrigger_event);
            }
            difyManager.Event_message.AddListener((json) => { InvokeOutputTriggerEvent("message", flow, json); });
            difyManager.Event_message_file.AddListener((json) => { InvokeOutputTriggerEvent("message_file", flow, json); });
            difyManager.Event_message_end.AddListener((json) => { InvokeOutputTriggerEvent("message_end", flow, json); });
            difyManager.Event_message_replace.AddListener((json) => { InvokeOutputTriggerEvent("message_replace", flow, json); });
            difyManager.Event_workflow_started.AddListener((json) => { InvokeOutputTriggerEvent("workflow_started", flow, json); });
            difyManager.Event_node_started.AddListener((json) => { InvokeOutputTriggerEvent("node_started", flow, json); });
            difyManager.Event_node_finished.AddListener((json) => { InvokeOutputTriggerEvent("node_finished", flow, json); });
            difyManager.Event_workflow_finished.AddListener((json) => { InvokeOutputTriggerEvent("workflow_finished", flow, json); });
            difyManager.Event_error.AddListener((json) => { InvokeOutputTriggerEvent("error", flow, json); isProcessing = false; });
            difyManager.Event_ping.AddListener((json) => { InvokeOutputTriggerEvent("ping", flow, json); });

            // Skip tts message events since the event trriggered after the message is finished in some cases.
            // difyManager.Event_tts_message.AddListener((json) => { InvokeOutputTriggerEvent("tts_message", flow, json); });
            // difyManager.Event_tts_message_end.AddListener((json) => { InvokeOutputTriggerEvent("tts_message_end", flow, json); });
        }

        // Send the message to Dify
        difyManager.SendChatMessage_Streaming(query, texture2D);

        // Wait until the message is finished
        while (isProcessing) { yield return null; }

        // Remove all listeners
        difyManager.Event_message.RemoveAllListeners();
        difyManager.Event_message_file.RemoveAllListeners();
        difyManager.Event_message_end.RemoveAllListeners();
        difyManager.Event_message_replace.RemoveAllListeners();
        difyManager.Event_workflow_started.RemoveAllListeners();
        difyManager.Event_node_started.RemoveAllListeners();
        difyManager.Event_node_finished.RemoveAllListeners();
        difyManager.Event_workflow_finished.RemoveAllListeners();
        difyManager.Event_error.RemoveAllListeners();
        difyManager.Event_ping.RemoveAllListeners();
    }
}





