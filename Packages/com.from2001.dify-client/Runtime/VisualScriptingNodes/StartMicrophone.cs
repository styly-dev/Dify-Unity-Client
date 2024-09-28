using Unity.VisualScripting;
using UnityEngine;

[UnitTitle("Start Microphone")] // Title of the node in the graph
[UnitCategory("Dify")]   // The category path in the add node menu
public class StartMicrophone : Unit
{
    [DoNotSerialize]
    public ControlInput inputTrigger;

    [DoNotSerialize]
    public ValueInput inputDifyManager;

    [DoNotSerialize]
    public ControlOutput outputTrigger;

    protected override void Definition()
    {
        // Define input and output ports
        inputTrigger = ControlInput("input", OnInputTriggered);
        inputDifyManager = ValueInput<DifyManager>("DifyManager", null);
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

        difyManager.StartMicrophone();

        return outputTrigger;
    }
}
