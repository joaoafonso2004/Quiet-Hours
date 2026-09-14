using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.NPC;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Da tosse ao Rui.
    ///
    /// Vive num objecto proprio debaixo dele, e nao no mesmo `AudioSource` da voz:
    /// a voz tem alcance de conversa (nove metros) e isto tem de atravessar a casa.
    /// Partilhar a fonte obrigava as duas a escolher um alcance so, e a tosse ficava
    /// inaudivel do quarto — que e o unico sitio de onde ela interessa ser ouvida.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class LifeSoundsWiring
    {
        private const string Holder = "RUI_LifeSounds";
        private static readonly string[] CoughClips =
        {
            "Assets/ThirdParty/Audio/coughing1.mp3",
            "Assets/ThirdParty/Audio/coughing2.mp3"
        };

        [MenuItem("Pungent/Blockout/Wire Rui Life Sounds", false, 43)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireLifeSounds")) return;

            var rui = GameObject.Find("NPC_Rui");
            if (rui == null)
            {
                Debug.LogError("[Vida] Sem `NPC_Rui` nesta cena.");
                return;
            }

            var clips = new List<AudioClip>();
            foreach (string path in CoughClips)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) clips.Add(clip);
                else Debug.LogWarning("[Vida] Sem clip em " + path + ".");
            }

            if (clips.Count == 0)
            {
                Debug.LogError("[Vida] Nenhum clip de tosse encontrado. Sem eles o " +
                               "componente fica montado e calado.");
                return;
            }

            var found = rui.transform.Find(Holder);
            GameObject host;
            if (found != null) host = found.gameObject;
            else
            {
                host = new GameObject(Holder);
                host.transform.SetParent(rui.transform, false);
                // A altura da boca: a tosse sai da cabeca dele e nao dos pes.
                host.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                Undo.RegisterCreatedObjectUndo(host, "Wire life sounds");
            }

            if (host.GetComponent<AudioSource>() == null) host.AddComponent<AudioSource>();

            var life = host.GetComponent<NpcLifeSounds>();
            if (life == null) life = host.AddComponent<NpcLifeSounds>();

            life.EditorConfigure(clips.ToArray(),
                rui.GetComponent<PrototypeNpcRoutine>(),
                Object.FindObjectOfType<WorldDialogueController>(true));
            EditorUtility.SetDirty(life);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Vida] Rui com " + clips.Count + " tosse(s), de 55 a 150 s, " +
                      "caladas durante conversas e por cima de qualquer texto no ecra.");
        }
    }
}
