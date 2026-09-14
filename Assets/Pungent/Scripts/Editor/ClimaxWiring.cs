using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Audio;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.NPC;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Liga o Dia 5 — "Porta 3B" — ao apartamento.
    ///
    /// O `CH_Day5_Door3B` existia desde o primeiro esqueleto e esperava por quatro
    /// acontecimentos que nao tinham dono nenhum. Este ficheiro e onde cada um passa
    /// a ter um, e um so:
    ///
    /// - `keys_down` — pousar as chaves ao entrar (o movel da entrada);
    /// - `climax_door_wrong` — a fechadura do quarto do Tomas, que ja nao existe;
    /// - `climax_keys_gone` — voltar ao movel e a prateleira estar vazia;
    /// - `keys_found` — as chaves em cima da secretaria do Rui, sobre as coisas dele;
    /// - `escaped` — a porta 3B, com a chave na mao.
    ///
    /// O `caught` e do `RuiHunt` e nao fecha passo nenhum: ser apanhado nao acaba o
    /// capitulo, atira-o para tras.
    ///
    /// Re-executavel: destroi e reconstroi so o root <see cref="Root"/>, e volta a
    /// escrever os assets de texto por cima dos que ja la estao. Nao toca no
    /// blockout, no dressing, nas portas nem em nada do prologo, do Dia 2 ou do
    /// Dia 3 — o que faz e dizer ao <see cref="ClimaxStage"/> o que desligar quando
    /// a noite for esta.
    /// </summary>
    internal static class ClimaxWiring
    {
        private const string Root = "CLIMAX_DAY5";
        private const string Content = "CLIMAX_CONTENT";
        private const string ChapterPath = "Assets/Pungent/Narrative/Chapters/CH_Day5_Door3B.asset";
        private const string ThoughtFolder = "Assets/Pungent/Dialogue/Thoughts/";
        /// <summary>O contacto que esta ferramenta criou por engano, e que apaga.</summary>
        private const string StalePhonePath = "Assets/Pungent/Dialogue/Phone/PHN_Rui_Climax.asset";
        private const string SystemsPrefab = "Assets/Pungent/Prefabs/GAME_SYSTEMS.prefab";

        [MenuItem("Pungent/Blockout/Wire Climax (Day 5)", false, 15)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireClimax")) return;

            var scene = EditorSceneManager.GetActiveScene();
            var player = GameObject.Find("PlayerRoot");
            if (player == null)
            {
                Debug.LogError("[Climax] Sem PlayerRoot nesta cena. Correr primeiro " +
                               "'Add Player + Systems to Scene'.");
                return;
            }

            var rui = GameObject.Find("NPC_Rui");
            if (rui == null)
                Debug.LogWarning("[Climax] NPC_Rui nao encontrado: a caca fica por montar.");

            WriteChapter();
            var lines = WriteLines();
            WritePhoneThread();

            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Wire climax");

            // Tudo o que e desta noite vive num grupo a parte, que fica desligado.
            // O apartamento corre quatro capitulos e tres deles nao devem ver nada
            // disto: um esconderijo debaixo da cama durante o prologo era um prompt
            // a oferecer uma mecanica que ainda nao existe, e nao ha aviso nenhum
            // que apanhe isso — so se ve a jogar.
            var content = new GameObject(Content);
            content.transform.SetParent(root.transform, false);

            var doors = CollectDoors();

            BuildKeyShelf(content.transform, lines);
            BuildDoorLock(content.transform, lines);
            BuildRuiDesk(content.transform, lines);
            BuildExit(content.transform, player, doors, lines);
            BuildHidingSpots(content.transform, player);

            // O que vive no Rui e nao no grupo, e por isso tem de ser acordado a
            // mao: a caca e os filtros que lhe abafam a voz e os passos.
            var wake = new List<MonoBehaviour>();
            BuildHunt(content.transform, rui, player, doors, lines, wake);

            // Depois do conteudo: a encenacao guarda o grupo e o que ha para acordar.
            BuildStage(root.transform, content, wake, rui, doors);

            content.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Climax] Dia 5 ligado: keys_down -> climax_door_wrong -> " +
                      "climax_keys_gone -> keys_found -> escaped.");

            // Esta ferramenta destroi e reconstroi o `CLIMAX_DAY5` inteiro, e os
            // quatro esconderijos vivem la dentro. Quem lhes aponta de fora fica com
            // referencias a nulo e nao ha erro nenhum a dizer isso — so uma manha do
            // Dia 3 sem nada para fazer e um Dia 5 sem memoria.
            if (Object.FindObjectOfType<NothingHappened>(true) != null)
                Debug.LogWarning("[Climax] O beat do Dia 3 aponta para os esconderijos que " +
                                 "acabaram de ser reconstruidos. Corre agora " +
                                 "'Pungent/Narrativa/Wire Nothing Happened (Day 3)'.");
        }

        // ------------------------------------------------------------------
        // Texto
        // ------------------------------------------------------------------

        /// <summary>
        /// Os conjuntos de falas deste capitulo.
        ///
        /// Regra de escrita do projecto: o detalhe que se repetir mais tarde tem de
        /// ser banal. Aqui o detalhe e **os parafusos**. O jogador carregou uma caixa
        /// de ferramentas para dentro desta casa no prologo, com as proprias maos, e
        /// encontrou a lanterna que estava la dentro numa oficina do outro lado da
        /// cidade. Ninguem lhe diz isso outra vez. O que ele encontra e uma fechadura
        /// nova com parafusos novos, e a conta e dele.
        /// </summary>
        private sealed class Lines
        {
            public ThoughtLineSet KeysDown, KeysGone, DoorLock, RuiDesk, ExitLocked;
            public ThoughtLineSet RuiAtDoor, RuiSearching, RuiOnSight;
        }

        private static Lines WriteLines()
        {
            var lines = new Lines
            {
                KeysDown = Set("THT_Climax_KeyShelf", 3.0f, ThoughtLineSet.Order.SequentialLastRepeats,
                    "Keys on the shelf. Same as every night."),

                KeysGone = Set("THT_Climax_KeyShelf_Empty", 3.4f, ThoughtLineSet.Order.SequentialLastRepeats,
                    "They are not here.",
                    "I put them there. Four minutes ago, I put them there.",
                    "I have not been outside since."),

                // Nao acusa ninguem e nao explica nada. Tres frases sobre bricolage.
                DoorLock = Set("THT_Climax_DoorLock", 3.4f, ThoughtLineSet.Order.SequentialLastRepeats,
                    "The lock is gone. There is a plate over the hole.",
                    "The screws are new. The paint around them is not.",
                    "Somebody had a screwdriver in my room today."),

                RuiDesk = Set("THT_Climax_RuiDesk", 3.6f, ThoughtLineSet.Order.SequentialLastRepeats,
                    "My keys. On his desk. Lined up with the edge.",
                    "My charger. The box cutter from the kitchen boxes.",
                    "He has been putting my things somewhere I would never look."),

                ExitLocked = Set("THT_Climax_ExitLocked", 3.0f, ThoughtLineSet.Order.SequentialLastRepeats,
                    "Locked. It locks from the inside with the key.",
                    "The key is not on the shelf and it is not in my pocket."),

                // A fala pela porta. Calma, no tom de sempre, e sobre coisas
                // domesticas — e por isso que assusta. Um perseguidor que grita e um
                // monstro; um colega de casa que pergunta pelo jantar atraves da
                // porta do quarto onde tu estas fechado nao tem categoria nenhuma.
                // A estrada saiu daqui com o corte de 11-08: ele nao foi a lado nenhum
                // esta noite. As duas do meio passam a ser sobre a caixa e sobre a
                // lanterna — coisas que estao dentro desta casa, e que ele so pode
                // saber por ter estado ao pe delas.
                //
                // A terceira e a pior de todas: **ele nomeia a fita azul.** E o
                // detalhe que prova que a lanterna e do Tomas, e ele di-lo como quem
                // devolve um objecto perdido.
                RuiAtDoor = Set("THT_Climax_Rui_Door", 3.6f, ThoughtLineSet.Order.SequentialLastRepeats,
                    "You're up late.",
                    "You brought that box in. I heard you on the stairs.",
                    "The one with the blue tape on it. That's yours, isn't it.",
                    "Open the door, Tomas. I want to see that you're all right."),

                RuiSearching = Set("THT_Climax_Rui_Search", 3.0f, ThoughtLineSet.Order.RandomNoImmediateRepeat,
                    "There's nowhere to go, you know that.",
                    "I'm not angry.",
                    "You always come back to the kitchen.",
                    "I know this flat better than you do.",
                    "Tomas."),

                RuiOnSight = Set("THT_Climax_Rui_Sight", 2.4f, ThoughtLineSet.Order.RandomNoImmediateRepeat,
                    "There you are.",
                    "Stop.",
                    "Don't do that.")
            };

            AssetDatabase.SaveAssets();
            return lines;
        }

        private static ThoughtLineSet Set(string fileName, float hold,
            ThoughtLineSet.Order order, params string[] text)
        {
            string path = ThoughtFolder + fileName + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<ThoughtLineSet>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ThoughtLineSet>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.EditorPopulate(text, order, hold);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // ------------------------------------------------------------------
        // O capitulo
        // ------------------------------------------------------------------

        /// <summary>
        /// Reescreve os passos do `CH_Day5_Door3B`.
        ///
        /// Os que la estavam eram um esboco: esperavam por `back_inside`,
        /// `rui_room_opened` e `confrontation_resolved`, nomes de uma versao em que
        /// o capitulo acabava numa conversa. A seccao 5 diz o contrario — "o
        /// objectivo final nao e derrotar Rui: e sair" — e um passo chamado
        /// "confrontation" e uma promessa de luta que o jogo nao vai cumprir.
        /// </summary>
        private static void WriteChapter()
        {
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            if (chapter == null)
            {
                Debug.LogError("[Climax] " + ChapterPath + " nao encontrado.");
                return;
            }

            var steps = new[]
            {
                Step("OBJECTIVE: Put your keys down. Get some sleep.",
                     "The street door downstairs was open. This one was too.",
                     "keys_down"),

                // Sem objectivo: ele esta a ir para a cama e a casa e que o
                // interrompe. Por-lhe "OBJECTIVE: investigate" em cima era dizer-lhe
                // que ha aqui um jogo antes de ele ter reparado em nada.
                Step(string.Empty,
                     "His door is shut. His door is never shut.",
                     "climax_door_wrong"),

                Step("OBJECTIVE: Take your keys. You can sleep somewhere else.",
                     "Somebody had a screwdriver in my room today.",
                     "climax_keys_gone"),

                Step("OBJECTIVE: Find your keys.",
                     "There is one door in this flat I have never opened.",
                     "keys_found", "rui_awake"),

                Step("OBJECTIVE: Get out. Front door.",
                     "He is in the flat. He has been in the flat the whole time.",
                     "escaped", "epilogue")
            };

            chapter.EditorPopulate("Day 5 - Door 3B", "climax", steps, "FRIDAY", "01:56");
            EditorUtility.SetDirty(chapter);
            AssetDatabase.SaveAssets();
        }

        private static ChapterDefinition.Step Step(string objective, string thought,
            string eventId, params string[] raiseOnComplete)
        {
            return new ChapterDefinition.Step
            {
                Objective = objective,
                SideObjective = string.Empty,
                Thought = thought,
                Ends = ChapterDefinition.Completion.Event,
                EventId = eventId,
                RaiseOnComplete = raiseOnComplete ?? new string[0]
            };
        }

        // ------------------------------------------------------------------
        // O fio de mensagens
        // ------------------------------------------------------------------

        /// <summary>
        /// "O telemovel tem mensagens que tornam a ligacao inequivoca" (seccao 5).
        ///
        /// Inequivoca quer dizer que deixa de haver leitura inocente, e nao que
        /// alguem confessa. Nenhuma destas linhas diz o que ele fez. O que elas
        /// dizem e que ele sabia — do Vitor, das pecas, da estrada — e o jogador sabe
        /// que nunca lhe falou de nada disso.
        ///
        /// Chegam durante os primeiros passos, enquanto ele ainda pensa que vai
        /// dormir. Sao mensagens de colega de casa a horas de colega de casa.
        ///
        /// **Vao para o fio que ja existe.** Estas linhas viviam num asset proprio,
        /// `PHN_Rui_Climax`, e o telemovel passou a ter dois contactos chamados
        /// "Rui" — um erro que so se ve a jogar, porque em disco sao dois ficheiros
        /// com nomes diferentes. Sao mensagens da mesma pessoa e o telemovel de
        /// qualquer pessoa junta-as ao que ja la esta: por baixo da conversa do
        /// router, quatro dias depois dela.
        ///
        /// E tambem o que torna estas mensagens piores. Elas chegam ao mesmo sitio
        /// onde ele um dia escreveu "Rui here. Now you have got it."
        /// </summary>
        private static void WritePhoneThread()
        {
            var thread = PhoneThreadEditing.Load("PHN_Rui_Router");
            if (thread == null)
            {
                Debug.LogWarning("[Climax] PHN_Rui_Router nao encontrado: as mensagens da " +
                                 "noite ficam por acrescentar. Correr 'Build Phone Threads'.");
                return;
            }

            // **As mensagens da noite, sem a estrada.**
            //
            // Diziam *"vitor said you left in a hurry"* e *"that road is bad at
            // night"* — escritas para uma noite em que o Tomas voltava de carro de
            // uma oficina a quarenta minutos. Com o corte de 11-08 ele nao sai de
            // casa: passa a noite toda do outro lado do corredor, e o Rui sabe disso.
            //
            // O que se ganha e melhor do que o que se perde. **Nada aqui e uma
            // ameaca**, e continua a ser tudo verificavel a partir do lado de la
            // daquela porta — os passos ouvem-se, a luz ve-se por baixo da folha, e o
            // Vitor esteve mesmo no patamar nessa tarde. A ultima e a unica que nao
            // tem explicacao boa: ele nao pode saber que o Tomas ja nao esta na cama.
            //
            // A regra do projecto continua a mandar: banal le-se como vigilancia,
            // dramatico le-se como guiao. Nenhuma destas levanta a voz.
            var night = new[]
            {
                PhoneThreadEditing.Message("you home?", "climax", 25f, 3f),
                PhoneThreadEditing.Message("your light is on", string.Empty, 40f, 4f),
                PhoneThreadEditing.Message("vitor left something on the landing for you", "climax_door_wrong", 30f, 6f),
                PhoneThreadEditing.Message("did you take it in or is it still out there", string.Empty, 35f, 4f),
                PhoneThreadEditing.Message("youre not in bed", string.Empty, 20f, 2f, endsThread: true)
            };

            var steps = PhoneThreadEditing.ReadSteps(thread);

            // **Tirar as antigas antes de acrescentar as novas.**
            //
            // Esta ferramenta so acrescenta o que ainda nao existe — o que e certo
            // para uma execucao repetida, e errado quando o texto **muda**. As linhas
            // da estrada nao desapareciam por eu reescrever as novas: ficavam as duas
            // versoes no mesmo fio, e o jogador lia primeiro uma noite que nao houve.
            string[] retired =
            {
                "did you get the parts",
                "vitor said you left in a hurry",
                "that road is bad at night",
                "dont wake me",
            };

            int removed = steps.RemoveAll(s => s != null && System.Array.IndexOf(retired, s.Line) >= 0);
            if (removed > 0)
            {
                PhoneThreadEditing.Write(thread, thread.ContactName, steps);
                Debug.Log("[Climax] " + removed + " mensagem(ns) da estrada retirada(s) do fio do Rui.");
                steps = PhoneThreadEditing.ReadSteps(thread);
            }

            int added = 0;
            foreach (var message in night)
            {
                if (PhoneThreadEditing.HasLine(thread, message.Line)) continue;

                // O fio e linear: um passo que feche a conversa antes destes deixava
                // a noite toda por entregar, e sem erro nenhum.
                for (int i = 0; i < steps.Count; i++)
                    if (steps[i] != null) steps[i].EndsThread = false;

                steps.Add(message);
                added++;
            }

            if (added > 0) PhoneThreadEditing.Write(thread, thread.ContactName, steps);

            // O fio do Rui ja costuma estar na lista dos sistemas — mas a garantia e
            // barata, e sem ela esta ferramenta passava a depender da ordem por que
            // as outras foram corridas.
            AddThreadToSystems(thread);
            DropStaleClimaxThread();
        }

        /// <summary>
        /// Tira o `PHN_Rui_Climax` da lista do telemovel e apaga-o.
        ///
        /// Enquanto o asset existir e estiver na lista do `PhoneMessageService`, o
        /// segundo "Rui" continua a aparecer no telemovel — o campo novo de
        /// desbloqueio nao chega, porque o problema dele nao e a altura a que
        /// aparece, e ser uma segunda pessoa que nao existe.
        /// </summary>
        private static void DropStaleClimaxThread()
        {
            var stale = AssetDatabase.LoadAssetAtPath<PhoneConversationDefinition>(StalePhonePath);
            if (stale == null) return;

            var root = PrefabUtility.LoadPrefabContents(SystemsPrefab);
            if (root != null)
            {
                var service = root.GetComponentInChildren<PhoneMessageService>(true);
                if (service != null)
                {
                    var so = new SerializedObject(service);
                    var array = so.FindProperty("conversations");
                    for (int i = array.arraySize - 1; i >= 0; i--)
                        if (array.GetArrayElementAtIndex(i).objectReferenceValue == stale)
                            array.DeleteArrayElementAtIndex(i);

                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, SystemsPrefab);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.DeleteAsset(StalePhonePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Climax] PHN_Rui_Climax apagado: as mensagens da noite vivem agora no " +
                      "fio do Rui que ja existia.");
        }

        /// <summary>
        /// Poe o fio na lista do `PhoneMessageService` do prefab dos sistemas.
        ///
        /// Tem de ser no prefab e nao na cena: o telemovel atravessa cenas com o
        /// resto do `GAME_SYSTEMS`, e um fio acrescentado a instancia do apartamento
        /// desaparecia assim que o jogo passasse pela garagem.
        /// </summary>
        private static void AddThreadToSystems(PhoneConversationDefinition thread)
        {
            var root = PrefabUtility.LoadPrefabContents(SystemsPrefab);
            if (root == null)
            {
                Debug.LogWarning("[Climax] Sem " + SystemsPrefab + ": o fio do climax " +
                                 "fica por acrescentar ao telemovel.");
                return;
            }

            var service = root.GetComponentInChildren<PhoneMessageService>(true);
            if (service == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogWarning("[Climax] Sem PhoneMessageService no GAME_SYSTEMS.");
                return;
            }

            var so = new SerializedObject(service);
            var array = so.FindProperty("conversations");

            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == thread)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                    return;   // ja la esta: a ferramenta e re-executavel
                }

            array.arraySize++;
            array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = thread;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, SystemsPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------
        // A encenacao
        // ------------------------------------------------------------------

        private static void BuildStage(Transform parent, GameObject content,
            List<MonoBehaviour> wake, GameObject rui, List<DoorDragInteractable> doors)
        {
            var go = new GameObject("CLIMAX_STAGE");
            go.transform.SetParent(parent, false);
            var stage = go.AddComponent<ClimaxStage>();

            // Tudo o que pertence as outras noites. O `PrologueStage` e o pior dos
            // casos: se o `Start` dele chegar a correr, teleporta o jogador para a
            // porta, poe as caixas de volta e reposiciona o Rui na entrada a dar a
            // chave — no capitulo em que ele e a ameaca.
            var suspend = new List<MonoBehaviour>();
            Collect<PrologueStage>(suspend);
            Collect<PrologueTracker>(suspend);
            Collect<DayTwoDirector>(suspend);

            // **O `DayThreeStage` faltava aqui, e por pouco nao se notava.**
            //
            // Ele arranca no `day_two` e desmonta-se no `day3_ready`, e na noite do
            // Dia 5 o jogador ja viu os dois — por isso, na cena recarregada, o
            // `Update` dele fazia as duas coisas **no mesmo frame**: teleportava o
            // Rui para (9.5, -4, 9.5), desligava-lhe o agente, calava-lhe a rotina,
            // escancarava a porta do quarto dele e apagava as luzes; e a seguir
            // repunha tudo. Anulava-se, e por isso nunca deu erro nenhum.
            //
            // Mas anulava-se **a partir do que estivesse la nesse instante**, que e
            // o que o `ClimaxStage.Start` acabara de montar. Um frame em que o
            // antagonista esta fora do mundo, no capitulo em que ele e a ameaca, e
            // que a caca ja esteve inerte duas vezes sem avisar. Desligado, nao ha
            // frame nenhum.
            Collect<DayThreeStage>(suspend);

            Collect<OpeningQuestDirector>(suspend);
            Collect<PlayerDeskOpening>(suspend);
            Collect<IntroTextSequence>(suspend);
            Collect<HomeTaskDirector>(suspend);
            Collect<InvasionSigns>(suspend);
            Collect<PrototypeStoryDirector>(suspend);

            var deactivate = new List<GameObject>();
            Deactivate(deactivate, "DAY3_SIGNS");
            Deactivate(deactivate, "PROLOGUE_BOXES");
            Deactivate(deactivate, "BOX_DROP_ZONE");
            Deactivate(deactivate, "DESK_SETUP");
            Deactivate(deactivate, "HOME_TASKS");

            var lights = new List<Light>();
            foreach (var light in Object.FindObjectsOfType<Light>(true))
                if (light.type != LightType.Directional) lights.Add(light);

            // Fechada: a dele. Abertas: a do Tomas, e as duas por onde ele sai da
            // lavandaria — sem elas o vao fica tapado na NavMesh e ele nunca chega
            // ao corredor.
            var closed = new[] { Door(doors, "Door_Bedroom_Rui") };
            var open = new[]
            {
                Door(doors, "Door_Bedroom_Tomas"),
                Door(doors, "Door_Bathroom"),
                Door(doors, "Door_Laundry")
            };

            stage.EditorConfigure(suspend.ToArray(), deactivate.ToArray(),
                content, wake.ToArray(),
                GameObject.Find("PROLOGUE_BOXES"), lights.ToArray(), rui,
                closed, open, Door(doors, "Door_Front_3B"));

            EditorUtility.SetDirty(stage);
        }

        private static void Collect<T>(List<MonoBehaviour> into) where T : MonoBehaviour
        {
            foreach (var found in Object.FindObjectsOfType<T>(true))
                if (!into.Contains(found)) into.Add(found);
        }

        private static void Deactivate(List<GameObject> into, string name)
        {
            var go = GameObject.Find(name);
            if (go != null && !into.Contains(go)) into.Add(go);
        }

        // ------------------------------------------------------------------
        // Os cinco donos
        // ------------------------------------------------------------------

        /// <summary>
        /// O movel da entrada, dono de `keys_down` e de `climax_keys_gone`.
        ///
        /// Posicionado pelas bounds e nao pelo transform: os props deste projecto
        /// sao "holder + malha com offset local", e o movel da entrada tem o
        /// transform a 1,10 com a prateleira visivel a 1,72. O Dia 3 ja pagou este
        /// erro uma vez — o prompt aparecia no ar e as chaves ficavam por pegar.
        /// </summary>
        private static void BuildKeyShelf(Transform parent, Lines lines)
        {
            Vector3 position = BoundsCentre("ART_PASS_V2/Hall/KeyShelf_Hall",
                new Vector3(4.6f, 1.5f, -1.9f), "[Climax] KeyShelf_Hall");

            var go = Make(parent, "CLIMAX_KeyShelf", position);
            // Trigger: pontos de interaccao sem nada desenhado nao podem ser
            // paredes invisiveis, nem tapar a mira ao que esta atras deles.
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.9f, 0.9f, 0.9f);

            var shelf = go.AddComponent<ClimaxKeyShelf>();
            shelf.EditorConfigure(lines.KeysDown, lines.KeysGone);
            EditorUtility.SetDirty(shelf);
        }

        /// <summary>
        /// A fechadura que ja nao existe, dona de `climax_door_wrong`.
        ///
        /// Num objecto proprio ao lado do aro e nao na folha da porta. A folha ja
        /// tem o `DoorDragInteractable`, e dois interagiveis no mesmo objecto
        /// disputam o clique: ganha o primeiro da lista de componentes, que nao e
        /// coisa que se deva deixar ao acaso.
        ///
        /// O par `FlavourInteractable` + `ChapterEventRaiser` com `afterReading` e o
        /// mesmo molde da lanterna da oficina: quem fala e o objecto, e o
        /// acontecimento so se levanta quando ele acabou de dizer o que tem a dizer.
        /// </summary>
        private static void BuildDoorLock(Transform parent, Lines lines)
        {
            // Aro da porta do quarto do Tomas: vao em x [-5.55, -4.65], z = -0.80.
            // A fechadura fica do lado oposto a dobradica, a altura da macaneta.
            var go = Make(parent, "CLIMAX_DoorLock", new Vector3(-4.78f, 1.02f, -0.72f));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.30f, 0.35f, 0.22f);

            var flavour = go.AddComponent<FlavourInteractable>();
            var so = new SerializedObject(flavour);
            so.FindProperty("prompt").stringValue = "Look at the lock";
            so.FindProperty("thoughtLines").objectReferenceValue = lines.DoorLock;
            so.ApplyModifiedPropertiesWithoutUndo();

            var raiser = go.AddComponent<ChapterEventRaiser>();
            raiser.EditorConfigure("climax_door_wrong", ChapterEventRaiser.Trigger.Interact,
                string.Empty, string.Empty, "keys_down", flavour);
            EditorUtility.SetDirty(raiser);
        }

        /// <summary>
        /// A secretaria do Rui, dona de `keys_found`.
        ///
        /// E aqui que estao "objectos pessoais de Tomas numa zona de Rui" (seccao 5):
        /// as chaves, o carregador, o x-acto que veio na caixa da cozinha. Nao ha
        /// sangue, nao ha bilhete e o nome dele nao aparece em lado nenhum — ha
        /// coisas do Tomas arrumadas com cuidado num sitio onde ele nunca ia olhar.
        /// </summary>
        private static void BuildRuiDesk(Transform parent, Lines lines)
        {
            // Quarto do Rui: x [-3.4, -0.1], z [-5.0, -0.8]. Encostado a parede sul.
            var go = Make(parent, "CLIMAX_RuiDesk", new Vector3(-2.60f, 0.95f, -4.55f));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.10f, 0.60f, 0.70f);

            var flavour = go.AddComponent<FlavourInteractable>();
            var so = new SerializedObject(flavour);
            so.FindProperty("prompt").stringValue = "Take your keys";
            so.FindProperty("thoughtLines").objectReferenceValue = lines.RuiDesk;
            so.ApplyModifiedPropertiesWithoutUndo();

            var raiser = go.AddComponent<ChapterEventRaiser>();
            raiser.EditorConfigure("keys_found", ChapterEventRaiser.Trigger.Interact,
                string.Empty, string.Empty, "climax_keys_gone", flavour);
            EditorUtility.SetDirty(raiser);
        }

        /// <summary>A porta 3B por dentro, dona de `escaped`.</summary>
        private static void BuildExit(Transform parent, GameObject player,
            List<DoorDragInteractable> doors, Lines lines)
        {
            var front = Door(doors, "Door_Front_3B");

            // Do lado de dentro do vao, a altura da fechadura. Nao na folha: a folha
            // ja tem o `DoorDragInteractable` a dizer "The door is locked", e um
            // segundo interagivel por cima dele era outra vez dois donos a disputar
            // o mesmo objecto.
            var go = Make(parent, "CLIMAX_Exit", new Vector3(5.55f, 1.02f, -1.95f));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.40f, 0.60f, 0.90f);

            var suppress = new List<MonoBehaviour>
            {
                player.GetComponent<Pungent.Player.PlayerMotor>(),
                player.GetComponent<PlayerInteractor>(),
                player.GetComponent<PrototypePhoneUI>()
            };
            suppress.RemoveAll(c => c == null);

            var exit = go.AddComponent<ClimaxExit>();
            exit.EditorConfigure(front, lines.ExitLocked, suppress.ToArray());
            EditorUtility.SetDirty(exit);
        }

        // ------------------------------------------------------------------
        // Esconderijos
        // ------------------------------------------------------------------

        /// <summary>
        /// Quatro sitios onde caber, um por zona da casa.
        ///
        /// Quatro e nao doze de proposito: um esconderijo em cada divisao faz da
        /// casa um tabuleiro e da fuga uma sequencia de saltos seguros. Assim ha
        /// sempre um alcancavel e nunca ha um por perto.
        ///
        /// Nenhum e seguro — ver `HidingSpot`. O que eles dao e tempo, e tempo e a
        /// unica moeda deste capitulo.
        /// </summary>
        private static void BuildHidingSpots(Transform parent, GameObject player)
        {
            var suppress = new List<MonoBehaviour>
            {
                player.GetComponent<Pungent.Player.PlayerMotor>(),
                player.GetComponent<PrototypePhoneUI>()
                // O `PlayerInteractor` fica de fora: e ele que aceita o clique de sair.
            };
            suppress.RemoveAll(c => c == null);
            var array = suppress.ToArray();

            // Debaixo da cama do Tomas. Cama em (-5.90, -3.70), virada a 90.
            Spot(parent, "HIDE_UnderBed", new Vector3(-5.90f, 0.35f, -3.70f),
                "Get under the bed", new Vector3(-5.90f, 0.38f, -3.70f), 90f, array);

            // O roupeiro do Rui, no quarto dele. Esconder-se no quarto de quem te
            // procura e a decisao mais desconfortavel que a casa oferece.
            Spot(parent, "HIDE_Wardrobe", new Vector3(-0.55f, 0.9f, -3.00f),
                "Get in the wardrobe", new Vector3(-0.60f, 1.35f, -3.00f), 270f, array);

            // Atras das caixas dos arrumos.
            Spot(parent, "HIDE_Storage", new Vector3(3.30f, 0.6f, -4.60f),
                "Get behind the boxes", new Vector3(3.30f, 0.95f, -4.62f), 0f, array);

            // Atras do sofa da sala, entre ele e a janela.
            Spot(parent, "HIDE_Couch", new Vector3(1.45f, 0.6f, 3.30f),
                "Get behind the sofa", new Vector3(1.40f, 0.85f, 3.30f), 90f, array);
        }

        private static void Spot(Transform parent, string name, Vector3 position,
            string prompt, Vector3 head, float yaw, MonoBehaviour[] suppress)
        {
            var go = Make(parent, name, position);

            // Volume grande o suficiente para conter o ponto de olhar. Com a cabeca
            // do jogador dentro dele, o `PlayerInteractor` continua a encontrar o
            // esconderijo pela sobreposicao de curta distancia — que e como ele
            // consegue clicar para sair de dentro de uma cama.
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.20f, 1.10f, 1.20f);
            box.center = go.transform.InverseTransformPoint(head) * 0.5f;

            var spot = go.AddComponent<HidingSpot>();
            spot.EditorConfigure(prompt, head, yaw, suppress);
            EditorUtility.SetDirty(spot);
        }

        // ------------------------------------------------------------------
        // A caca
        // ------------------------------------------------------------------

        private static void BuildHunt(Transform parent, GameObject rui, GameObject player,
            List<DoorDragInteractable> doors, Lines lines, List<MonoBehaviour> wake)
        {
            if (rui == null) return;

            // Os pontos de procura ficam no root do climax e nao no dressing: sao
            // desta noite e nao da casa. Reconstruir o dressing nao os leva atras.
            var pointsRoot = new GameObject("CLIMAX_SEARCH");
            pointsRoot.transform.SetParent(parent, false);

            var points = new List<Transform>
            {
                Point(pointsRoot.transform, "SEARCH_Bedroom_Tomas", -5.20f, -2.90f),
                Point(pointsRoot.transform, "SEARCH_Bedroom_Rui",   -1.75f, -2.90f),
                Point(pointsRoot.transform, "SEARCH_Bathroom",       1.15f, -1.90f),
                Point(pointsRoot.transform, "SEARCH_Hall",           4.10f, -1.90f),
                Point(pointsRoot.transform, "SEARCH_Storage",        4.10f, -4.00f),
                Point(pointsRoot.transform, "SEARCH_Living",         2.90f,  2.85f),
                Point(pointsRoot.transform, "SEARCH_Dining",        -1.50f,  2.85f),
                Point(pointsRoot.transform, "SEARCH_Kitchen",       -5.00f,  2.85f),
                Point(pointsRoot.transform, "SEARCH_Corridor",      -1.60f, -0.05f)
            };

            var hunt = rui.GetComponent<RuiHunt>();
            if (hunt == null) hunt = rui.AddComponent<RuiHunt>();

            var steps = BuildFootstepSource(rui, wake);
            hunt.EditorConfigure(points.ToArray(), doors.ToArray(),
                lines.RuiAtDoor, lines.RuiSearching, lines.RuiOnSight,
                steps, BorrowFootstepClips(player));

            // Desligado. O Rui vive na cena inteira e nas outras tres noites quem
            // manda no corpo dele e o `PrototypeNpcRoutine`; quem o acorda e a
            // encenacao desta noite.
            hunt.enabled = false;
            EditorUtility.SetDirty(hunt);
            wake.Add(hunt);

            MuffleVoice(rui, wake);
        }

        private static Transform Point(Transform parent, string name, float x, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, 0f, z);
            return go.transform;
        }

        /// <summary>
        /// Os passos dele, a altura dos pes e abafados por paredes.
        ///
        /// Nao ha componente de passos para NPCs neste projecto e nao e este
        /// capitulo que precisa de um: o `RuiHunt` toca os clips ao ritmo da
        /// velocidade que ja mede para o blend tree. O que faz falta e a fonte estar
        /// no sitio certo — no chao, nao no root — e passar pelo filtro.
        /// </summary>
        private static AudioSource BuildFootstepSource(GameObject rui, List<MonoBehaviour> wake)
        {
            var existing = rui.transform.Find("Rui_Footsteps");
            var go = existing != null ? existing.gameObject : new GameObject("Rui_Footsteps");
            go.transform.SetParent(rui.transform, false);
            go.transform.localPosition = Vector3.zero;

            var source = go.GetComponent<AudioSource>();
            if (source == null) source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 1.2f;
            source.maxDistance = 16f;
            source.rolloffMode = AudioRolloffMode.Linear;

            wake.Add(Muffle(go));
            return source;
        }

        private static AudioClip[] BorrowFootstepClips(GameObject player)
        {
            var footsteps = player.GetComponentInChildren<PlayerFootstepAudio>(true);
            if (footsteps == null) return new AudioClip[0];

            var so = new SerializedObject(footsteps);
            var array = so.FindProperty("footstepClips");
            var clips = new AudioClip[array.arraySize];
            for (int i = 0; i < clips.Length; i++)
                clips[i] = array.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
            return clips;
        }

        /// <summary>
        /// A voz dele passa a ser abafada por paredes.
        ///
        /// A voz ja vive num anchor na cabeca do modelo (ver `ApartmentV2RuiWiring`),
        /// que e onde tem de estar. O que falta e o filtro: sem ele, "fala calmamente
        /// atraves da porta" soa exactamente igual a ele estar na mesma divisao, e o
        /// jogador perde a unica pista que tem para saber de que lado da porta ele
        /// esta.
        /// </summary>
        private static void MuffleVoice(GameObject rui, List<MonoBehaviour> wake)
        {
            var voice = rui.GetComponentInChildren<NpcMumbleVoice>(true);
            if (voice == null)
            {
                Debug.LogWarning("[Climax] Sem NpcMumbleVoice no Rui: a voz fica sem filtro.");
                return;
            }

            wake.Add(Muffle(voice.gameObject));
        }

        /// <summary>
        /// Poe o filtro num objecto e devolve-o **desligado**.
        ///
        /// Desligado porque a voz do Rui e a mesma nas quatro noites, e as outras
        /// tres ja estao afinadas como estao. Abafar tudo retroactivamente seria
        /// mexer em cenas que funcionam para servir uma que ainda nao foi jogada —
        /// e isso e polimento disfarcado de encanamento.
        /// </summary>
        private static MuffledThroughWalls Muffle(GameObject go)
        {
            var filter = go.GetComponent<MuffledThroughWalls>();
            if (filter == null) filter = go.AddComponent<MuffledThroughWalls>();
            filter.enabled = false;
            EditorUtility.SetDirty(filter);
            return filter;
        }

        // ------------------------------------------------------------------
        // Utilitarios
        // ------------------------------------------------------------------

        private static List<DoorDragInteractable> CollectDoors()
        {
            var doors = new List<DoorDragInteractable>();
            foreach (var door in Object.FindObjectsOfType<DoorDragInteractable>(true))
                doors.Add(door);
            return doors;
        }

        private static DoorDragInteractable Door(List<DoorDragInteractable> doors, string name)
        {
            foreach (var door in doors)
                if (door.gameObject.name == name) return door;

            Debug.LogWarning($"[Climax] Porta '{name}' nao encontrada.");
            return null;
        }

        private static Vector3 BoundsCentre(string path, Vector3 fallback, string label)
        {
            var go = GameObject.Find(path);
            if (go == null)
            {
                Debug.LogWarning($"{label} nao encontrado; fica numa posicao aproximada.");
                return fallback;
            }

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return go.transform.position;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds.center;
        }

        private static GameObject Make(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go;
        }
    }
}
