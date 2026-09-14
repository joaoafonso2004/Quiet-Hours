using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// O quinto final: o pai veio.
    ///
    /// ---
    ///
    /// **A conta.** Quatro respostas honestas ao pai, espalhadas pelo fio das
    /// mensagens, cada uma a levantar `dad_told_N`. Tres delas chegam. Nao ha escolha
    /// nenhuma que decida isto sozinha, que e o que a §6.4 proibe — e nao ha nenhuma
    /// que se escolha por acidente, porque a alternativa e sempre mais confortavel.
    ///
    /// **Como as encontra.** Nao escreve mensagens novas: varre o `PHN_Dad` a procura
    /// de escolhas com `DialogueTone.Honest` e da `RaisesEvent` as quatro primeiras.
    /// Assim o fio continua a poder ser reescrito — quem lhe mexer nao tem de se
    /// lembrar desta ferramenta, so tem de manter respostas honestas la dentro.
    ///
    /// ---
    ///
    /// **O plano final.** Ver <see cref="EpilogueTableau"/>: ele de pe ao lado do
    /// carro, na rua debaixo da varanda onde o Tomas fumou cinco dias, com a porta
    /// aberta e os farois acesos. Camara fixa, ninguem se mexe, tres frases por cima.
    ///
    /// Re-executavel. Correr com a `Apartment_Blockout_V2` aberta.
    /// </summary>
    internal static class FatherEndingWiring
    {
        private const string ChapterPath = "Assets/Pungent/Narrative/Chapters/CH_Epilogue_Father.asset";
        private const string DadThread = "Assets/Pungent/Dialogue/Phone/PHN_Dad.asset";
        private const string CarPrefab = "Assets/Hatchback and Sedan/prefabs/SEDAN.prefab";
        private const string Root = "EPILOGUE_FATHER";
        private const string Ending = "ending_father";

        /// <summary>
        /// A cara do pai. Escolhida a olhar para as trinta e duas do pack, com os
        /// modelos normalizados a 1,80 m: o `_29` e o unico que le como um homem de
        /// cinquenta e tal — grisalho, camisa de colarinho. O `_30` le como avo e o
        /// `_16` esta ocupado, que e o Rui.
        ///
        /// A camisa faz trabalho sozinha. Um homem que se veste como deve ser para vir
        /// buscar o filho as tres da manha e um homem que decidiu que aquilo era
        /// importante.
        /// </summary>
        private const string FatherModel = "Character_29";

        /// <summary>Altura do passeio, sondada na malha da rua.</summary>
        private const float StreetY = -21.18f;

        [MenuItem("Pungent/Blockout/Wire Father Ending", false, 20)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireFatherEnding")) return;

            var scene = EditorSceneManager.GetActiveScene();
            var log = new List<string>();

            MarkHonestChoices(log);
            AddBlackboardRules(log);
            var chapter = WriteChapter();
            RegisterChapter(chapter, log);
            BuildTableau(log);

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[PaiFinal] Ligado.\n  " + string.Join("\n  ", log));
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Da `RaisesEvent` as quatro primeiras respostas honestas do fio do pai.
        ///
        /// Idempotente: uma escolha que ja tenha `RaisesEvent` nao e tocada, portanto
        /// correr isto duas vezes nao renumera nada nem gasta eventos a mais.
        /// </summary>
        private static void MarkHonestChoices(List<string> log)
        {
            var dad = AssetDatabase.LoadAssetAtPath<PhoneConversationDefinition>(DadThread);
            if (dad == null) { log.Add("AVISO: `PHN_Dad` nao encontrado — a conta fica sem quem a alimente"); return; }

            var so = new SerializedObject(dad);
            var steps = so.FindProperty("steps");

            int given = 0;
            var already = new HashSet<string>();

            // Primeira passagem: ve o que ja esta marcado, para nao repetir numeros.
            for (int s = 0; s < steps.arraySize; s++)
            {
                var choices = steps.GetArrayElementAtIndex(s).FindPropertyRelative("Choices");
                for (int c = 0; c < choices.arraySize; c++)
                {
                    string id = choices.GetArrayElementAtIndex(c).FindPropertyRelative("RaisesEvent").stringValue;
                    if (!string.IsNullOrWhiteSpace(id) && id.StartsWith("dad_told_")) already.Add(id);
                }
            }
            given = already.Count;

            for (int s = 0; s < steps.arraySize && given < 4; s++)
            {
                var choices = steps.GetArrayElementAtIndex(s).FindPropertyRelative("Choices");
                for (int c = 0; c < choices.arraySize && given < 4; c++)
                {
                    var choice = choices.GetArrayElementAtIndex(c);
                    if (choice.FindPropertyRelative("Tone").enumValueIndex != (int)DialogueTone.Honest) continue;

                    var raises = choice.FindPropertyRelative("RaisesEvent");
                    if (!string.IsNullOrWhiteSpace(raises.stringValue)) continue;

                    given++;
                    raises.stringValue = "dad_told_" + given;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dad);

            log.Add(given >= 3
                ? given + " respostas honestas do fio do pai passam a contar (`dad_told_1..` )"
                : "AVISO: so " + given + " respostas honestas no `PHN_Dad` — sao precisas " +
                  "pelo menos 3 para o final ser alcancavel. Acrescentar escolhas com " +
                  "`Tone = Honest` e correr isto outra vez.");
        }

        /// <summary>
        /// Acrescenta as regras a tabela que ja la esta, sem lhe mexer.
        ///
        /// O `EditorConfigure` do blackboard **substitui** o array inteiro. Escrever
        /// so as quatro novas apagava as que o `BlackboardWiring` montou, e o sintoma
        /// seria os outros quatro finais deixarem de ser alcancaveis — sem erro nenhum.
        /// </summary>
        private static void AddBlackboardRules(List<string> log)
        {
            var blackboard = Object.FindObjectOfType<NarrativeBlackboard>(true);
            if (blackboard == null) { log.Add("AVISO: sem NarrativeBlackboard na cena"); return; }

            var so = new SerializedObject(blackboard);
            var rules = so.FindProperty("rules");

            var existing = new HashSet<string>();
            for (int i = 0; i < rules.arraySize; i++)
                existing.Add(rules.GetArrayElementAtIndex(i).FindPropertyRelative("EventId").stringValue);

            int added = 0;
            for (int n = 1; n <= 4; n++)
            {
                string id = "dad_told_" + n;
                if (existing.Contains(id)) continue;

                rules.arraySize++;
                var rule = rules.GetArrayElementAtIndex(rules.arraySize - 1);
                rule.FindPropertyRelative("EventId").stringValue = id;
                rule.FindPropertyRelative("Target").enumValueIndex =
                    (int)NarrativeBlackboard.Variable.FatherWarned;
                rule.FindPropertyRelative("Amount").intValue = 1;
                rule.FindPropertyRelative("Once").boolValue = true;
                added++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(blackboard);
            log.Add(added + " regras novas no blackboard (dad_told_N -> FatherWarned)");
        }

        /// <summary>
        /// O capitulo. Tres frases, como os outros quatro.
        ///
        /// A terceira e a que paga o fio todo: ele nao pergunta nada porque ja nao
        /// precisa de perguntar — andou a reler as mensagens.
        /// </summary>
        private static ChapterDefinition WriteChapter()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ChapterDefinition>();
                AssetDatabase.CreateAsset(asset, ChapterPath);
            }

            var steps = new[]
            {
                Step("He was parked outside when I got down. He did not ask me anything.", 6.5f, null),
                Step("He said: I should have come on Tuesday.", 6.5f, null),
                Step("He had been reading them again.", 6.5f, new[] { "game_end" }),
            };

            asset.EditorPopulate("Epilogue - He came on Tuesday", Ending, steps,
                "THREE DAYS LATER", string.Empty);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static ChapterDefinition.Step Step(string thought, float seconds, string[] raise)
            => new ChapterDefinition.Step
            {
                Objective = string.Empty,
                SideObjective = string.Empty,
                Thought = thought,
                Ends = ChapterDefinition.Completion.Timer,
                Seconds = seconds,
                RaiseOnComplete = raise ?? System.Array.Empty<string>(),
            };

        private static void RegisterChapter(ChapterDefinition chapter, List<string> log)
        {
            var director = Object.FindObjectOfType<ChapterDirector>(true);
            if (director == null) { log.Add("AVISO: sem ChapterDirector na cena"); return; }

            var so = new SerializedObject(director);
            var list = so.FindProperty("chapters");

            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == chapter)
                {
                    log.Add("`CH_Epilogue_Father` ja estava na lista do director");
                    return;
                }

            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = chapter;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
            log.Add("`CH_Epilogue_Father` acrescentado ao director");
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// O plano: ele, o carro, a luz e a camara. Tudo desligado ate ao fim.
        /// </summary>
        private static void BuildTableau(List<string> log)
        {
            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Wire father ending");

            var set = new GameObject("SET");
            set.transform.SetParent(root.transform, false);

            // Na rua debaixo da varanda, virado para a porta do predio (-Z).
            var father = new GameObject("FATHER");
            father.transform.SetParent(set.transform, false);
            father.transform.position = new Vector3(1.60f, StreetY, 10.40f);
            father.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);

            if (PsxCharacter.Place(father.transform, FatherModel, "Father_Body") == null)
                log.Add("AVISO: sem `" + FatherModel + "` — o plano fica com o carro e sem ninguem");
            else
                log.Add("pai: " + FatherModel + " de pe em (1.60, " + StreetY.ToString("F2") + ", 10.40)");

            // O carro, atras dele e de lado, com os farois virados para a rua. E o
            // SEDAN porque e o carro que o jogador conhece — o pai conduz o mesmo
            // modelo que o filho, que e como estas coisas costumam acontecer.
            var carAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefab);
            if (carAsset != null)
            {
                var car = (GameObject)PrefabUtility.InstantiatePrefab(carAsset, set.transform);
                car.name = "FATHER_CAR";
                car.transform.position = new Vector3(3.90f, StreetY, 10.90f);
                car.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

                // Decorativo: nada aqui e simulado nem conduzido.
                foreach (var body in car.GetComponentsInChildren<Rigidbody>(true))
                { body.isKinematic = true; body.detectCollisions = false; }
                foreach (var behaviour in car.GetComponentsInChildren<MonoBehaviour>(true))
                    behaviour.enabled = false;

                log.Add("carro do pai posto ao lado, virado para a rua");
            }
            else log.Add("AVISO: sem `SEDAN.prefab` — o plano fica so com ele");

            // Os farois, deitados no chao a frente. Nao e realismo: e a unica luz da
            // imagem, e e ela que diz que o motor esta a trabalhar.
            var beam = new GameObject("HEADLIGHTS");
            beam.transform.SetParent(set.transform, false);
            beam.transform.position = new Vector3(2.60f, StreetY + 0.55f, 10.90f);
            beam.transform.rotation = Quaternion.Euler(6f, 250f, 0f);
            var light = beam.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 95f;
            light.range = 26f;
            light.intensity = 6.5f;
            light.color = new Color(1f, 0.95f, 0.86f);
            light.shadows = LightShadows.Soft;

            // A camara: da porta do predio, a altura dos olhos dele, parada.
            var camGo = new GameObject("TABLEAU_CAMERA");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.position = new Vector3(1.10f, StreetY + 1.62f, 5.20f);
            camGo.transform.rotation = Quaternion.LookRotation(
                new Vector3(1.60f, StreetY + 1.35f, 10.40f) - camGo.transform.position, Vector3.up);
            var view = camGo.AddComponent<Camera>();
            view.fieldOfView = 52f;
            view.nearClipPlane = 0.1f;
            view.farClipPlane = 220f;

            var tableau = root.AddComponent<EpilogueTableau>();
            tableau.EditorConfigure(Ending, set, view, Object.FindObjectOfType<ScreenFade>(true));
            EditorUtility.SetDirty(tableau);

            set.SetActive(false);
            camGo.SetActive(false);

            log.Add("plano montado e desligado; abre so em `" + Ending + "`");
        }
    }
}
