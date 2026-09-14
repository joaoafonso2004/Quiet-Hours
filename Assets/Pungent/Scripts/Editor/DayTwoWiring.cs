using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Atmosphere;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Liga o dia seguinte: ciclo de luz, palpebras, acordar e as mensagens do Pai
    /// que ficaram a espera durante a noite.
    ///
    /// Re-executavel. Nao recria o que ja existe e nao duplica passos no fio.
    /// </summary>
    public static class DayTwoWiring
    {
        private const string NightSky = "Assets/Day-Night Skyboxes/Materials/SkyMidnight.mat";
        private const string MorningSky = "Assets/Day-Night Skyboxes/Materials/SkyMorning.mat";
        private const string DadThread = "Assets/Pungent/Dialogue/Phone/PHN_Dad.asset";
        private const string DayTwoEvent = "day_two";

        [MenuItem("Pungent/Blockout/Wire Day Two", false, 32)]
        public static void Wire()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var log = new List<string>();

            var player = GameObject.Find("PlayerRoot");
            if (player == null) { Debug.LogError("[DayTwo] PlayerRoot nao encontrado."); return; }

            // --- palpebras, no jogador ---
            var eyelid = player.GetComponent<ScreenEyelid>();
            if (eyelid == null) eyelid = Undo.AddComponent<ScreenEyelid>(player);

            // --- ciclo de luz, num objecto proprio ---
            var cycleHolder = GameObject.Find("DAY_CYCLE");
            if (cycleHolder == null)
            {
                cycleHolder = new GameObject("DAY_CYCLE");
                Undo.RegisterCreatedObjectUndo(cycleHolder, "Wire Day Two");
            }
            var cycle = cycleHolder.GetComponent<DayCycleController>();
            if (cycle == null) cycle = Undo.AddComponent<DayCycleController>(cycleHolder);

            var cycleSo = new SerializedObject(cycle);
            cycleSo.FindProperty("directional").objectReferenceValue = FindDirectional();
            cycleSo.FindProperty("night").FindPropertyRelative("Skybox").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>(NightSky);
            cycleSo.FindProperty("morning").FindPropertyRelative("Skybox").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>(MorningSky);
            cycleSo.ApplyModifiedProperties();
            log.Add($"  ciclo de luz: noite={System.IO.Path.GetFileName(NightSky)} " +
                    $"manha={System.IO.Path.GetFileName(MorningSky)}");

            // --- o acordar ---
            var director = player.GetComponent<DayTwoDirector>();
            if (director == null) director = Undo.AddComponent<DayTwoDirector>(player);

            var so = new SerializedObject(director);
            so.FindProperty("player").objectReferenceValue = player.transform;
            so.FindProperty("cameraPivot").objectReferenceValue = player.transform.Find("ViewYaw");

            var camera = player.GetComponentInChildren<Camera>(true);
            if (camera != null) so.FindProperty("tiltTransform").objectReferenceValue = camera.transform;

            so.FindProperty("sleep").objectReferenceValue = Object.FindObjectOfType<PlayerSleep>(true);
            so.FindProperty("eyelid").objectReferenceValue = eyelid;
            so.FindProperty("dayCycle").objectReferenceValue = cycle;
            so.FindProperty("thoughts").objectReferenceValue = player.GetComponent<PlayerThoughtDirector>();
            so.FindProperty("hud").objectReferenceValue = player.GetComponent<PrototypeHUD>();
            so.FindProperty("messages").objectReferenceValue = Object.FindObjectOfType<PhoneMessageService>(true);
            so.FindProperty("rui").objectReferenceValue = GameObject.Find("NPC_Rui");

            var heldPhone = player.GetComponentInChildren<PhoneLightController>(true);
            if (heldPhone != null) so.FindProperty("heldPhone").objectReferenceValue = heldPhone.gameObject;

            var suppressed = new List<Object>
            {
                player.GetComponent<Pungent.Player.PlayerMotor>(),
                player.GetComponent<PlayerInteractor>(),
                player.GetComponent<PrototypePhoneUI>()
            };
            suppressed.RemoveAll(c => c == null);
            var sp = so.FindProperty("suppressed");
            sp.arraySize = suppressed.Count;
            for (int i = 0; i < suppressed.Count; i++)
                sp.GetArrayElementAtIndex(i).objectReferenceValue = suppressed[i];

            // Todas as luzes do apartamento ficam apagadas de manha.
            var lights = new List<Light>();
            foreach (var name in new[] { "Bedroom_Light", "Corridor_Light", "Corridor_Light_West",
                                         "Kitchen_Light", "Living_Light", "Hall_Light",
                                         "Bathroom_Light", "Dining_Light", "Bedroom_Rui_Light" })
            {
                var go = GameObject.Find(name);
                var light = go != null ? go.GetComponent<Light>() : null;
                if (light != null) lights.Add(light);
            }
            var lp = so.FindProperty("lightsOffAtDawn");
            lp.arraySize = lights.Count;
            for (int i = 0; i < lights.Count; i++)
                lp.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];

            so.FindProperty("dayTwoEvent").stringValue = DayTwoEvent;
            so.ApplyModifiedProperties();
            log.Add($"  acordar ligado: {suppressed.Count} suprimidos, {lights.Count} luzes a apagar, " +
                    $"Rui={(GameObject.Find("NPC_Rui") != null ? "sai de casa" : "NAO ENCONTRADO")}");

            // --- as mensagens do Pai que esperaram a noite ---
            log.Add(AppendDayTwoMessages());

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[DayTwo] Dia seguinte ligado.\n" + string.Join("\n", log));
        }

        /// <summary>
        /// Acrescenta ao fio do Pai as mensagens de manha. Ficam presas ao evento
        /// day_two, portanto so caem quando o jogador acorda — mas para ele leem-se
        /// como tendo chegado durante a noite, que e o que se quer.
        /// </summary>
        private static string AppendDayTwoMessages()
        {
            var dad = AssetDatabase.LoadAssetAtPath<PhoneConversationDefinition>(DadThread);
            if (dad == null) return "  fio do Pai nao encontrado";

            var so = new SerializedObject(dad);
            var steps = so.FindProperty("steps");

            // Ja foi acrescentado antes?
            for (int i = 0; i < steps.arraySize; i++)
                if (steps.GetArrayElementAtIndex(i).FindPropertyRelative("WaitForEvent").stringValue == DayTwoEvent)
                    return "  mensagens do dia dois ja existiam";

            // O ultimo passo da noite deixa de fechar o fio: ele continua de manha.
            if (steps.arraySize > 0)
                steps.GetArrayElementAtIndex(steps.arraySize - 1)
                     .FindPropertyRelative("EndsThread").boolValue = false;

            AddStep(steps, "Morning. You were still up when I went to bed, were you not.",
                DayTwoEvent, 6f, 4f, 20f,
                "Slept fine.", "Good.", DialogueTone.Evasive,
                "Not really.", "You never do when you are worried about something.", DialogueTone.Honest);

            AddStep(steps, "Sunday still on? She has already bought too much fish for three people.",
                "", 55f, 4f, 22f,
                "Sunday works.", "I will tell her. She will pretend she forgot.", DialogueTone.Calm,
                "I might have to see.", "See what? It is Sunday.", DialogueTone.Evasive);

            // O eco, e o fim do fio. Cai tarde de proposito: a estas alturas o
            // jogador ja deu a volta a casa e nao encontrou ninguem, e a pergunta
            // banal do Pai passa a ter um peso que ele nao lhe pos.
            AddStep(steps, "Is that flatmate of yours alright? You never say anything about him.",
                "", 100f, 5f, 0f,
                null, null, DialogueTone.Calm, null, null, DialogueTone.Calm);
            steps.GetArrayElementAtIndex(steps.arraySize - 1)
                 .FindPropertyRelative("EndsThread").boolValue = true;

            so.ApplyModifiedPropertiesWithoutUndo();
            return "  3 mensagens de manha acrescentadas ao fio do Pai";
        }

        private static void AddStep(SerializedProperty steps, string line, string waitFor,
            float delay, float typing, float replyDelay,
            string calmText, string calmReply, DialogueTone calmTone,
            string edgyText, string edgyReply, DialogueTone edgyTone)
        {
            steps.arraySize++;
            var step = steps.GetArrayElementAtIndex(steps.arraySize - 1);
            step.FindPropertyRelative("Line").stringValue = line;
            step.FindPropertyRelative("WaitForEvent").stringValue = waitFor;
            step.FindPropertyRelative("DelaySeconds").floatValue = delay;
            step.FindPropertyRelative("TypingSeconds").floatValue = typing;
            step.FindPropertyRelative("ReplyDelaySeconds").floatValue = replyDelay;
            step.FindPropertyRelative("EndsThread").boolValue = false;

            var choices = step.FindPropertyRelative("Choices");
            bool hasChoices = !string.IsNullOrEmpty(calmText) && !string.IsNullOrEmpty(edgyText);
            choices.arraySize = hasChoices ? 2 : 0;
            if (!hasChoices) return;

            var first = choices.GetArrayElementAtIndex(0);
            first.FindPropertyRelative("Text").stringValue = calmText;
            first.FindPropertyRelative("Reply").stringValue = calmReply;
            first.FindPropertyRelative("Tone").enumValueIndex = (int)calmTone;

            var second = choices.GetArrayElementAtIndex(1);
            second.FindPropertyRelative("Text").stringValue = edgyText;
            second.FindPropertyRelative("Reply").stringValue = edgyReply;
            second.FindPropertyRelative("Tone").enumValueIndex = (int)edgyTone;
        }

        private static Light FindDirectional()
        {
            foreach (var light in Object.FindObjectsOfType<Light>(true))
                if (light.type == LightType.Directional) return light;
            return null;
        }
    }
}
