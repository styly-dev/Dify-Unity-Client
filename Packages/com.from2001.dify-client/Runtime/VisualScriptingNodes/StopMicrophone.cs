using Unity.VisualScripting;
using UnityEngine;

[UnitTitle("Stop Microphone")] // Title of the node in the graph
[UnitCategory("Dify")]   // The category path in the add node menu
public class StopMicrophone : Unit
{
    [DoNotSerialize]
    public ControlInput inputTrigger;

    [DoNotSerialize]
    public ValueInput inputDifyManager;

    [DoNotSerialize]
    public ControlOutput outputTrigger;

    [DoNotSerialize]
    public ValueOutput valueOutput_AudioClip;

    protected override void Definition()
    {
        inputTrigger = ControlInput("input", OnInputTriggered);
        inputDifyManager = ValueInput<DifyManager>("DifyManager", null);

        outputTrigger = ControlOutput("");
        valueOutput_AudioClip = ValueOutput<AudioClip>("AudioClip");

        // Connect ports
        Succession(inputTrigger, outputTrigger);
    }

    // Called when the input trigger is activated
    private ControlOutput OnInputTriggered(Flow flow)
    {
        DifyManager difyManager = flow.GetValue<DifyManager>(inputDifyManager);

        if (difyManager == null)
        {
            // Find the DifyManager in the scene
            difyManager = GameObject.FindObjectOfType<DifyManager>();
            if (difyManager == null)
            {
                Debug.LogError("DifyManager not found in the scene");
                return outputTrigger;
            }
        }

        AudioClip audioClip = difyManager.StopMicrophone();
        flow.SetValue(valueOutput_AudioClip, audioClip);

        return outputTrigger;
    }
}
