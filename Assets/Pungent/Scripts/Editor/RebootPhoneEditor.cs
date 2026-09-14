using Pungent.Interaction;
using UnityEditor;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Buttons to author the two phone poses by hand.
    ///
    /// The raised pose is a framing decision, not a number: it decides how much of
    /// the room stays visible while the player reads. So the flow is drag the model
    /// in the scene view until it looks right, then press capture. Typing euler
    /// angles into the inspector and playing to check is the slow way round.
    /// </summary>
    [CustomEditor(typeof(RebootPhone))]
    public sealed class RebootPhoneEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var phone = (RebootPhone)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Compor as poses", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Pre-visualizar poe o modelo na pose gravada. Arrasta-o no Scene view " +
                "ate ficar como queres e carrega em Guardar.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Pre-visualizar baixado"))
                {
                    Undo.RecordObject(phone, "Pre-visualizar telemovel");
                    phone.EditorPreview(false);
                    MarkDirty(phone);
                }
                if (GUILayout.Button("Pre-visualizar levantado"))
                {
                    Undo.RecordObject(phone, "Pre-visualizar telemovel");
                    phone.EditorPreview(true);
                    MarkDirty(phone);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Guardar pose baixada"))
                {
                    Undo.RecordObject(phone, "Guardar pose do telemovel");
                    phone.EditorCapturePose(false);
                    MarkDirty(phone);
                }
                if (GUILayout.Button("Guardar pose levantada"))
                {
                    Undo.RecordObject(phone, "Guardar pose do telemovel");
                    phone.EditorCapturePose(true);
                    MarkDirty(phone);
                }
            }
        }

        private static void MarkDirty(RebootPhone phone)
        {
            EditorUtility.SetDirty(phone);
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(phone.gameObject.scene);
        }
    }
}
