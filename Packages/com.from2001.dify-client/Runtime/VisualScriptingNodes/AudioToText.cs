using System.Collections;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

[UnitTitle("Audio to Text")] // Title of the node in the graph
[UnitCategory("Dify")]   // The category path in the add node menu
public class AudioToText : Unit
{
    [DoNotSerialize]
    public ControlInput inputTrigger;

    [DoNotSerialize]
    public ValueInput inputDifyManager;

    [DoNotSerialize]
    public ValueInput inpytAudioClip;

    [DoNotSerialize]
    public ControlOutput outputTrigger;

    [DoNotSerialize]
    public ValueOutput valueOutput_Text;

    protected override void Definition()
    {
        // Define input and output ports
        inputTrigger = ControlInputCoroutine("input", OnInputTriggered);
        inputDifyManager = ValueInput<DifyManager>("DifyManager", null);
        inpytAudioClip = ValueInput<AudioClip>("AudioClip", null);

        outputTrigger = ControlOutput("");
        valueOutput_Text = ValueOutput<string>("Text");

        // Connect ports
        Succession(inputTrigger, outputTrigger);
    }

    // Called when the input trigger is activated
    private IEnumerator OnInputTriggered(Flow flow)
    {
        DifyManager difyManager = flow.GetValue<DifyManager>(inputDifyManager);
        AudioClip audioClip = flow.GetValue<AudioClip>(inpytAudioClip);

        if (difyManager == null)
        {
            // Find the DifyManager in the scene
            difyManager = GameObject.FindObjectOfType<DifyManager>();
            if (difyManager == null)
            {
                Debug.LogError("DifyManager not found in the scene");
                yield return outputTrigger;
            }
        }
        Task<string> task = difyManager.AudioToText(audioClip);
        while (!task.IsCompleted) { yield return null; }

        flow.SetValue(valueOutput_Text, task.IsCompletedSuccessfully ? task.Result : null);

        yield return outputTrigger;
    }
}
