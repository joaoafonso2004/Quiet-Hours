using UnityEditor;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.NPC;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Liga a conversa do Rui atraves da porta, no climax.
    ///
    /// **Climax, atraves da porta** (`climax`). Comeca sozinha, porque tem de: o
    /// Rui esta do lado de fora de uma porta fechada e nao ha maneira de o jogador
    /// lhe tocar. Mede-se a partir da porta do quarto do Tomas e espera dois
    /// segundos e meio antes da primeira fala — o tempo que faz a diferenca entre
    /// um gatilho e um homem que ja ali estava.
    ///
    /// ---
    ///
    /// **Houve aqui uma segunda conversa, e foi apagada.** O `CONV_Day3_Leaving`
    /// punha o Rui a perguntar, a porta da rua, a que horas o Tomas saia, que
    /// caminho levava e se ia sozinho. E a mesma cena que o
    /// <see cref="NPC.DoorConversation"/> ja faz no mesmo dia — as mesmas
    /// perguntas de logistica, o mesmo registo, o mesmo par confrontar/calar-se —
    /// e as duas podiam acontecer na mesma manha, com dez minutos de intervalo.
    /// Duas versoes da mesma conversa nao sao o dobro da tensao: sao a primeira a
    /// desmentir a segunda.
    ///
    /// **Esta ferramenta apaga-o se o encontrar.** Nao chega deixar de o criar:
    /// quem ja tem a cena gravada com ele la dentro nunca mais se via livre dele,
    /// porque uma ferramenta que so acrescenta nao limpa nada.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class ScriptedConversationWiring
    {
        private const string DoorScript = "Assets/Pungent/Dialogue/Sequences/DLG_Rui_Climax_Door.asset";

        /// <summary>A conversa apagada. Ver a nota da classe.</summary>
        private const string Retired = "CONV_Day3_Leaving";

        [MenuItem("Pungent/Narrativa/Wire Rui Conversations", false, 61)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireConversations")) return;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.name.StartsWith("Apartment"))
            {
                Debug.LogError("[Conversas] Abre a cena do apartamento primeiro.");
                return;
            }

            var rui = ApartmentV2WiringUtil.Find(scene, "NPC_Rui");
            var convo = rui != null ? rui.GetComponent<RuiStoryConversation>() : null;
            if (convo == null) { Debug.LogError("[Conversas] Sem RuiStoryConversation no NPC_Rui."); return; }

            var door = AssetDatabase.LoadAssetAtPath<DialogueSequenceDefinition>(DoorScript);
            if (door == null)
            {
                Debug.LogError("[Conversas] Corre primeiro 'Pungent/Narrativa/Write Rui Dialogues'.");
                return;
            }

            var group = ApartmentV2WiringUtil.Find(scene, "SCRIPTED_CONVERSATIONS")
                        ?? new GameObject("SCRIPTED_CONVERSATIONS");

            // A conversa do Dia 3 saiu daqui. Ver a nota da classe: era a mesma que
            // o `DoorConversation` ja faz, no mesmo dia e com o mesmo par de
            // respostas. Apagada e nao so deixada de criar — quem ja tinha a cena
            // gravada com ela la dentro nao se via livre dela de outra maneira.
            var retired = group.transform.Find(Retired);
            bool removed = retired != null;
            if (removed) Object.DestroyImmediate(retired.gameObject);

            // Climax — atraves da porta do quarto do Tomas.
            var doorTransform = ApartmentV2WiringUtil.Find(scene, "Door_Bedroom_Tomas");
            if (doorTransform == null)
                Debug.LogWarning("[Conversas] Sem Door_Bedroom_Tomas — o climax mede a partir do grupo.");

            var b = Replace(group.transform, "CONV_Climax_Door");
            if (doorTransform != null) b.transform.position = doorTransform.transform.position;
            b.AddComponent<ScriptedConversationCue>().EditorConfigure(
                door, convo, "climax", null,
                ScriptedConversationCue.Start.WhenClose,
                doorTransform != null ? doorTransform.transform : null, 3.2f, 2.5f);

            EditorUtility.SetDirty(convo);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[Conversas] Climax ligado em 'climax', comeca sozinho a 3,2 m da porta " +
                      "do quarto. O guiao das 02:47 fica intacto ate la.\n" +
                      (removed
                        ? "[Conversas] CONV_Day3_Leaving APAGADO da cena: duplicava o "
                          + "DoorConversation no mesmo dia."
                        : "[Conversas] Sem CONV_Day3_Leaving na cena, como tem de ser."));
        }

        private static GameObject Replace(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
