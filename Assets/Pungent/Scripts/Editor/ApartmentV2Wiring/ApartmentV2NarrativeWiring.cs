using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Pungent.Interaction;
using Pungent.NPC;

namespace Pungent.EditorTools
{
    /// <summary>
    /// A camada narrativa que corre por cima do apartamento: texto de introducao,
    /// acordar na cama, cadeia de objetivos, pensamentos e a conversa de historia
    /// com o Rui.
    /// </summary>
    internal static class ApartmentV2NarrativeWiring
    {
        /// <summary>Texto de enquadramento antes de o jogador ganhar controlo.</summary>
        internal static void WireIntro(Scene scene)
        {
            var player = GameObject.Find("PlayerRoot");
            if (player == null) return;

            var existing = ApartmentV2WiringUtil.Find(scene, "INTRO_SEQUENCE");
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject("INTRO_SEQUENCE");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = Vector3.zero;

            var intro = go.AddComponent<Pungent.Narrative.IntroTextSequence>();
            var so = new SerializedObject(intro);
            var suppressed = so.FindProperty("suppressedDuringIntro");

            var components = new List<Object>
            {
                player.GetComponent<Pungent.Player.PlayerMotor>(),
                player.GetComponent<PlayerInteractor>(),
                player.GetComponent<PrototypeHUD>(),
                player.GetComponent<PrototypePhoneUI>()
            };
            components.RemoveAll(c => c == null);

            suppressed.arraySize = components.Count;
            for (int i = 0; i < components.Count; i++)
                suppressed.GetArrayElementAtIndex(i).objectReferenceValue = components[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Liga o acordar na cama, os pensamentos e a cadeia de objetivos, mais a
        /// conversa de historia com o Rui.
        /// </summary>
        internal static void WireOpeningSequence(Scene scene)
        {
            var player = GameObject.Find("PlayerRoot");
            var rui = GameObject.Find("NPC_Rui");
            if (player == null) return;

            var dialogue = player.GetComponent<Pungent.Dialogue.WorldDialogueController>();
            var hud = player.GetComponent<PrototypeHUD>();
            var phone = player.GetComponent<PrototypePhoneUI>();

            // --- pensamentos ---
            var thoughts = player.GetComponent<Pungent.Narrative.PlayerThoughtDirector>();
            if (thoughts == null) thoughts = player.AddComponent<Pungent.Narrative.PlayerThoughtDirector>();
            var thoughtsSo = new SerializedObject(thoughts);
            thoughtsSo.FindProperty("dialogue").objectReferenceValue = dialogue;
            thoughtsSo.ApplyModifiedPropertiesWithoutUndo();

            // --- cadeia de objetivos ---
            var questHolder = ApartmentV2WiringUtil.Find(scene, "OPENING_QUEST");
            if (questHolder != null) Object.DestroyImmediate(questHolder);
            questHolder = new GameObject("OPENING_QUEST");
            questHolder.transform.SetParent(player.transform, false);

            var quest = questHolder.AddComponent<Pungent.Narrative.OpeningQuestDirector>();
            var questSo = new SerializedObject(quest);
            questSo.FindProperty("hud").objectReferenceValue = hud;
            questSo.FindProperty("phone").objectReferenceValue = phone;
            questSo.FindProperty("thoughts").objectReferenceValue = thoughts;
            questSo.FindProperty("player").objectReferenceValue = player.transform;
            if (rui != null)
                questSo.FindProperty("rui").objectReferenceValue = rui.GetComponent<PrototypeNpcRoutine>();
            questSo.ApplyModifiedPropertiesWithoutUndo();

            // O director de historia reporta o router a cadeia.
            var story = Object.FindObjectOfType<Pungent.Narrative.PrototypeStoryDirector>(true);
            if (story != null)
            {
                var storySo = new SerializedObject(story);
                storySo.FindProperty("quest").objectReferenceValue = quest;

                // O router esta na entrada (x ~ 5.6) e a Corridor_Light a sete
                // metros dali, virada a oeste: apagar so essa deixava a fala a
                // falar de uma luz que o jogador nunca viu acesa. Apaga-se a luz
                // que esta mesmo por cima dele e o corredor todo por onde ele tem
                // de voltar.
                storySo.FindProperty("corridorLight").objectReferenceValue = null;
                var dying = new List<Object>();
                foreach (var name in new[] { "Hall_Light", "Corridor_Light", "Corridor_Light_West" })
                {
                    var go = GameObject.Find(name);
                    var light = go != null ? go.GetComponent<Light>() : null;
                    if (light != null) dying.Add(light);
                }

                var dyingProperty = storySo.FindProperty("lightsThatDie");
                dyingProperty.arraySize = dying.Count;
                for (int i = 0; i < dying.Count; i++)
                    dyingProperty.GetArrayElementAtIndex(i).objectReferenceValue = dying[i];

                storySo.ApplyModifiedPropertiesWithoutUndo();
            }

            // --- abertura a secretaria ---
            // Substitui o acordar na cama: a ligacao cair so significa alguma coisa
            // se o jogador estiver a usa-la quando acontece.
            var wake = player.GetComponent<Pungent.Narrative.PlayerDeskOpening>();
            if (wake == null) wake = player.AddComponent<Pungent.Narrative.PlayerDeskOpening>();
            var pivot = player.transform.Find("ViewYaw");
            var playerCamera = player.GetComponentInChildren<Camera>(true);
            var wakeSo = new SerializedObject(wake);
            wakeSo.FindProperty("cameraPivot").objectReferenceValue = pivot;
            if (playerCamera != null)
                wakeSo.FindProperty("tiltTransform").objectReferenceValue = playerCamera.transform;
            wakeSo.FindProperty("thoughts").objectReferenceValue = thoughts;
            wakeSo.FindProperty("quest").objectReferenceValue = quest;

            var glow = GameObject.Find("Laptop_Glow") ?? GameObject.Find("Laptop_Glow (1)");
            if (glow != null)
                wakeSo.FindProperty("laptopGlow").objectReferenceValue = glow.GetComponent<Light>();
            wakeSo.FindProperty("hud").objectReferenceValue = hud;
            var heldPhone = player.GetComponentInChildren<Pungent.Interaction.PhoneLightController>(true);
            if (heldPhone != null)
                wakeSo.FindProperty("heldPhone").objectReferenceValue = heldPhone.gameObject;
            wakeSo.FindProperty("intro").objectReferenceValue =
                player.GetComponentInChildren<Pungent.Narrative.IntroTextSequence>(true);

            var suppressedList = new List<Object>
            {
                player.GetComponent<Pungent.Player.PlayerMotor>(),
                player.GetComponent<PlayerInteractor>(),
                phone
            };
            suppressedList.RemoveAll(c => c == null);
            var suppressed = wakeSo.FindProperty("suppressed");
            suppressed.arraySize = suppressedList.Count;
            for (int i = 0; i < suppressedList.Count; i++)
                suppressed.GetArrayElementAtIndex(i).objectReferenceValue = suppressedList[i];
            wakeSo.ApplyModifiedPropertiesWithoutUndo();

            // --- conversa de historia ---
            if (rui == null) return;
            var conversation = rui.GetComponent<RuiStoryConversation>();
            if (conversation == null) conversation = rui.AddComponent<RuiStoryConversation>();
            var convoSo = new SerializedObject(conversation);
            convoSo.FindProperty("dialogue").objectReferenceValue = dialogue;
            convoSo.FindProperty("voice").objectReferenceValue =
                rui.GetComponentInChildren<Pungent.Dialogue.NpcMumbleVoice>(true);
            convoSo.FindProperty("routine").objectReferenceValue = rui.GetComponent<PrototypeNpcRoutine>();
            convoSo.FindProperty("quest").objectReferenceValue = quest;
            convoSo.ApplyModifiedPropertiesWithoutUndo();

            // A conversa de historia substitui a troca binaria de prototipo:
            // as duas a responder ao mesmo clique competiam pelo dialogo.
            var oldInteractable = rui.GetComponent<NpcDialogueInteractable>();
            if (oldInteractable != null) oldInteractable.enabled = false;
        }

        /// <summary>O portatil do graybox tambem responde com um pensamento.</summary>
        internal static void WireDeskFlavour()
        {
            var laptop = GameObject.Find("Laptop_Screen");
            if (laptop == null) return;

            var flavour = laptop.GetComponent<FlavourInteractable>();
            if (flavour == null) flavour = laptop.AddComponent<FlavourInteractable>();

            var so = new SerializedObject(flavour);
            so.FindProperty("prompt").stringValue = "Look at the laptop";
            so.FindProperty("requiresEvent").stringValue = "prologue_done";
            var lines = so.FindProperty("thoughts");
            lines.arraySize = 3;
            lines.GetArrayElementAtIndex(0).stringValue =
                "The listing is still open. Those parts are far too cheap.";
            lines.GetArrayElementAtIndex(1).stringValue =
                "Still nothing. The page has not moved since it dropped.";
            lines.GetArrayElementAtIndex(2).stringValue =
                "I should close it and go to bed. I am not going to.";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
