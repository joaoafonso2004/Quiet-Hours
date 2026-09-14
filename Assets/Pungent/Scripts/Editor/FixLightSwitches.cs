using UnityEngine;
using UnityEditor;
using Pungent.Interaction;

public static class FixLightSwitches
{
    [MenuItem("Tools/Fix Light Switches")]
    public static void Run()
    {
        var clipOn = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/Audio/light_switch_on.mp3");
        var clipOff = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/Audio/light_switch_off.mp3");

        if (clipOn == null || clipOff == null)
        {
            Debug.LogError("Could not find light_switch_on.mp3 or light_switch_off.mp3");
            return;
        }

        var switches = Object.FindObjectsOfType<LightSwitchInteractable>(true);
        Debug.Log($"Found {switches.Length} LightSwitches");
        foreach (var sw in switches)
        {
            var audioSource = sw.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = sw.gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 1f;
                audioSource.maxDistance = 15f;
            }

            var so = new SerializedObject(sw);
            so.FindProperty("clickOn").objectReferenceValue = clipOn;
            so.FindProperty("clickOff").objectReferenceValue = clipOff;
            so.ApplyModifiedProperties();
            
            Debug.Log($"Fixed LightSwitch {sw.name}");
        }
    }
}
