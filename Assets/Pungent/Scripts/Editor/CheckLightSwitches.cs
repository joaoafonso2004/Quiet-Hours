using UnityEngine;
using UnityEditor;
using Pungent.Interaction;

public static class CheckLightSwitches
{
    [MenuItem("Tools/Check Light Switches")]
    public static void Run()
    {
        var switches = Object.FindObjectsOfType<LightSwitchInteractable>(true);
        Debug.Log($"Found {switches.Length} LightSwitches");
        foreach (var sw in switches)
        {
            var audioSource = sw.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                Debug.LogError($"LightSwitch {sw.name} is missing AudioSource!");
                continue;
            }

            var so = new SerializedObject(sw);
            var clickOn = so.FindProperty("clickOn").objectReferenceValue as AudioClip;
            var clickOff = so.FindProperty("clickOff").objectReferenceValue as AudioClip;
            
            Debug.Log($"LightSwitch {sw.name}: AudioSource={audioSource.name}, clickOn={clickOn?.name}, clickOff={clickOff?.name}");
        }
    }
}
