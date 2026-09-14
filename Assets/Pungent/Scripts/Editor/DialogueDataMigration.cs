using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.NPC;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Tira o texto de dialogo de dentro do ficheiro .unity e poe-no em assets.
    ///
    /// As 42 falas do apartamento foram escritas no inspector, uma a uma: nao ha
    /// defaults no codigo que as reproduzam. Por isso a migracao le o que esta
    /// mesmo na cena e nao regenera nada. Correr duas vezes nao duplica: quem ja
    /// tem asset atribuido e ignorado.
    ///
    /// Depois de validada, os campos antigos (marcados HideInInspector nos
    /// componentes) podem ser apagados do codigo.
    /// </summary>
    public static class DialogueDataMigration
    {
        private const string Root = "Assets/Pungent/Dialogue";
        private const string ThoughtsFolder = Root + "/Thoughts";
        private const string SequencesFolder = Root + "/Sequences";

        [MenuItem("Pungent/Dialogue/Migrate Scene Text to Assets", false, 10)]
        public static void Migrate()
        {
            EnsureFolders();

            var scene = EditorSceneManager.GetActiveScene();
            int created = 0, linked = 0, skipped = 0;
            var log = new List<string>();

            // --- objetos de cenario que so dizem uma coisa -----------------
            // Guardado por componente para o lava-loica poder reutilizar o do irmao.
            var flavourAssets = new Dictionary<FlavourInteractable, ThoughtLineSet>();

            foreach (var flavour in Object.FindObjectsOfType<FlavourInteractable>(true))
            {
                var so = new SerializedObject(flavour);
                var target = so.FindProperty("thoughtLines");
                var legacy = so.FindProperty("thoughts");

                if (target.objectReferenceValue is ThoughtLineSet already)
                {
                    flavourAssets[flavour] = already;
                    skipped++;
                    continue;
                }

                string[] lines = ReadStringArray(legacy);
                if (lines.Length == 0) { skipped++; continue; }

                var asset = CreateThoughtSet(
                    $"{ThoughtsFolder}/THT_{Sanitise(flavour.gameObject.name)}.asset",
                    lines,
                    ThoughtLineSet.Order.SequentialLastRepeats,
                    so.FindProperty("holdSeconds").floatValue);

                target.objectReferenceValue = asset;
                so.ApplyModifiedProperties();
                flavourAssets[flavour] = asset;
                created++;
                log.Add($"  {flavour.gameObject.name}: {lines.Length} fala(s)");
            }

            // --- lava-loica -------------------------------------------------
            // Em runtime o SinkInteractable destroi o FlavourInteractable irmao para
            // ganhar a interacao. O texto vivia no irmao e era copiado por reflection.
            // Agora os dois passam a apontar ao mesmo asset.
            foreach (var sink in Object.FindObjectsOfType<SinkInteractable>(true))
            {
                var so = new SerializedObject(sink);
                var target = so.FindProperty("flavourLines");
                if (target.objectReferenceValue != null) { skipped++; continue; }

                string[] lines = ReadStringArray(so.FindProperty("flavourThoughts"));
                ThoughtLineSet asset = null;

                if (lines.Length > 0)
                {
                    asset = CreateThoughtSet(
                        $"{ThoughtsFolder}/THT_{Sanitise(sink.gameObject.name)}_Sink.asset",
                        lines,
                        ThoughtLineSet.Order.SequentialLastRepeats,
                        so.FindProperty("holdSeconds").floatValue);
                    created++;
                }
                else
                {
                    var sibling = sink.GetComponent<FlavourInteractable>();
                    if (sibling != null) flavourAssets.TryGetValue(sibling, out asset);
                    if (asset != null)
                        log.Add($"  {sink.gameObject.name}: reutiliza o asset do FlavourInteractable irmao");
                }

                if (asset == null) { skipped++; continue; }

                target.objectReferenceValue = asset;
                so.ApplyModifiedProperties();
                linked++;
            }

            // --- tarefas domesticas ----------------------------------------
            foreach (var task in Object.FindObjectsOfType<HomeTaskInteractable>(true))
            {
                var so = new SerializedObject(task);
                var target = so.FindProperty("beatLines");
                if (target.objectReferenceValue != null) { skipped++; continue; }

                string[] lines = ReadStringArray(so.FindProperty("beats"));
                if (lines.Length == 0) { skipped++; continue; }

                string id = so.FindProperty("id").stringValue;
                var asset = CreateThoughtSet(
                    $"{ThoughtsFolder}/THT_Task_{Sanitise(string.IsNullOrEmpty(id) ? task.gameObject.name : id)}.asset",
                    lines,
                    ThoughtLineSet.Order.SequentialLastRepeats,
                    3.0f);   // o HomeTask ja passava 3.0 fixo ao PlayerThoughtDirector

                target.objectReferenceValue = asset;
                so.ApplyModifiedProperties();
                created++;
                log.Add($"  {task.gameObject.name}: {lines.Length} beat(s)");
            }

            // --- conversa guionada do Rui -----------------------------------
            foreach (var convo in Object.FindObjectsOfType<RuiStoryConversation>(true))
            {
                var so = new SerializedObject(convo);
                string speaker = so.FindProperty("speaker").stringValue;
                float choiceSeconds = so.FindProperty("choiceSeconds").floatValue;
                float lineHold = so.FindProperty("lineHold").floatValue;

                var sequenceTarget = so.FindProperty("conversation");
                if (sequenceTarget.objectReferenceValue == null)
                {
                    var beats = ReadBeats(so.FindProperty("beats"));
                    if (beats.Length > 0)
                    {
                        var sequence = ScriptableObject.CreateInstance<DialogueSequenceDefinition>();
                        sequence.EditorPopulate(speaker, beats, choiceSeconds, lineHold);
                        string path = AssetDatabase.GenerateUniqueAssetPath(
                            $"{SequencesFolder}/DLG_{Sanitise(convo.gameObject.name)}_Opening.asset");
                        AssetDatabase.CreateAsset(sequence, path);
                        sequenceTarget.objectReferenceValue = sequence;
                        created++;
                        log.Add($"  {convo.gameObject.name}: {beats.Length} beat(s) guionados");
                    }
                }
                else skipped++;

                // As falas de encher deixaram de existir. Ver a nota no
                // `RuiStoryConversation`: eram uma roleta de dicas domesticas que
                // nunca se esgotava, e o que ficou foi uma linha so. Nao ha nada para
                // migrar — migrar isto seria criar um asset para uma coisa que o jogo
                // ja nao le.

                so.ApplyModifiedProperties();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[DialogueMigration] {created} asset(s) criados, {linked} ligacao(oes) " +
                      $"reutilizada(s), {skipped} ja migrado(s) ou vazio(s).\n" +
                      string.Join("\n", log));
        }

        // ------------------------------------------------------------------

        private static ThoughtLineSet CreateThoughtSet(string path, string[] lines,
            ThoughtLineSet.Order order, float hold)
        {
            var asset = ScriptableObject.CreateInstance<ThoughtLineSet>();
            asset.EditorPopulate(lines, order, Mathf.Max(0.8f, hold));
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
            return asset;
        }

        private static string[] ReadStringArray(SerializedProperty property)
        {
            if (property == null || !property.isArray) return System.Array.Empty<string>();
            var values = new string[property.arraySize];
            for (int i = 0; i < property.arraySize; i++)
                values[i] = property.GetArrayElementAtIndex(i).stringValue;
            return values.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
        }

        /// <summary>
        /// Converte os Beat antigos (Line + dois pares escolha/resposta em campos
        /// separados) para o formato novo, onde cada escolha carrega a sua propria
        /// resposta e o tom que alimenta a ameaca.
        /// </summary>
        private static DialogueSequenceDefinition.Beat[] ReadBeats(SerializedProperty property)
        {
            if (property == null || !property.isArray) return System.Array.Empty<DialogueSequenceDefinition.Beat>();

            var result = new List<DialogueSequenceDefinition.Beat>();
            for (int i = 0; i < property.arraySize; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                string line = element.FindPropertyRelative("Line").stringValue;
                string calm = element.FindPropertyRelative("ChoiceCalm").stringValue;
                string edgy = element.FindPropertyRelative("ChoiceEdgy").stringValue;
                string replyCalm = element.FindPropertyRelative("ReplyToCalm").stringValue;
                string replyEdgy = element.FindPropertyRelative("ReplyToEdgy").stringValue;

                var beat = new DialogueSequenceDefinition.Beat { Line = line };

                // Indice 0 era sempre a opcao calma e 1 a cortante; o tom passa a ser
                // explicito para a ordem no inspector deixar de ter significado.
                if (!string.IsNullOrWhiteSpace(calm) && !string.IsNullOrWhiteSpace(edgy))
                {
                    beat.Choices = new[]
                    {
                        new DialogueChoice { Text = calm, Reply = replyCalm, Tone = DialogueTone.Calm },
                        new DialogueChoice { Text = edgy, Reply = replyEdgy, Tone = DialogueTone.Edgy },
                    };
                }

                result.Add(beat);
            }
            return result.ToArray();
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Root))
                AssetDatabase.CreateFolder("Assets/Pungent", "Dialogue");
            if (!AssetDatabase.IsValidFolder(ThoughtsFolder))
                AssetDatabase.CreateFolder(Root, "Thoughts");
            if (!AssetDatabase.IsValidFolder(SequencesFolder))
                AssetDatabase.CreateFolder(Root, "Sequences");
        }

        private static string Sanitise(string name)
        {
            var clean = name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray();
            return clean.Length > 0 ? new string(clean) : "Unnamed";
        }
    }
}
