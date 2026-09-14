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
    /// Liga o Dia 1 — "Bom negocio".
    ///
    /// Era o unico capitulo do §5 que nunca tinha sido feito: sem cena, sem asset,
    /// sem nada. E era tambem onde a corrente estava partida — o prologo levantava
    /// `prologue_done` e ninguem o ouvia, o `PrologueStage.Finish()` nao era
    /// chamado por codigo nenhum, e como e ele o unico sitio de onde a noite das
    /// 02:47 arranca, essa noite nunca comecava. Quem pegava no primeiro `DaySlept`
    /// era o `DayTwoDirector`, que saltava direito a manha do Dia 3.
    ///
    /// Duas coisas ao mesmo tempo, portanto: o capitulo que faltava, e o dono que
    /// faltava.
    ///
    /// Os quatro donos:
    ///
    /// - `day1_kitchen`  — chegar a cozinha de manha;
    /// - `day1_car_seen` — ver o carro da varanda, **e o Rui comentar**;
    /// - `day1_deal`     — o anuncio das pecas e a escolha;
    /// - `day1_desk`     — sentar-se a trabalhar, que entrega as 02:47.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class DayOneWiring
    {
        private const string Root = "DAY1_GOODDEAL";
        private const string Content = "DAY1_CONTENT";
        private const string ChapterPath = "Assets/Pungent/Narrative/Chapters/CH_Day1_GoodDeal.asset";
        private const string ThoughtFolder = "Assets/Pungent/Dialogue/Thoughts/";
        private const string PhonePath = "Assets/Pungent/Dialogue/Phone/PHN_Market.asset";
        private const string SystemsPrefab = "Assets/Pungent/Prefabs/GAME_SYSTEMS.prefab";

        /// <summary>
        /// O Rui na lavandaria — o susto do Dia 1.
        ///
        /// ---
        ///
        /// **A encenação, tal como foi pedida:** abres a porta, a camara prende-se
        /// nele, toca um sting, e dois segundos e meio depois abre a conversa em que o
        /// Tomas leva um susto e ele explica o que esta ali a fazer. Depois volta a
        /// andar pela casa.
        ///
        /// ---
        ///
        /// **Ele nao e teleportado para ali, e essa e a decisao toda.**
        ///
        /// A maneira obvia era pegar no corpo dele, po-lo na lavandaria e devolve-lo
        /// no fim. **Pedir o corpo do Rui emprestado partiu-o duas vezes num dia** —
        /// ficou especado depois do vinho e gritou na cozinha, as duas por o agente
        /// voltar sem `Warp`. Um susto nao vale um Rui partido.
        ///
        /// Em vez disso ele **vai ali por vontade propria**: um `RoutineActionAnchor`
        /// na lavandaria, como os que ele ja usa no fogao e no balcao. A rotina leva-o
        /// la, ele fica um bocado, e sai sozinho. Nao ha nada emprestado, portanto nao
        /// ha nada para devolver.
        ///
        /// **E o susto so acontece se ele estiver mesmo la**, de graca: o
        /// <see cref="AttentionSnap"/> exige linha de vista, e uma parede entre os dois
        /// e um susto que nao dispara. Se a rotina o tiver levado para outro sitio, nao
        /// acontece nada — e nao acontecer nada e um estado legitimo desta casa.
        ///
        /// **Uma vez so.** O `silencedByEvent` fecha-o depois de disparar; repetido,
        /// virava mecanica e o jogador comecava a abrir a porta a espera dele.
        /// </summary>
        private static void BuildLaundryScare(Transform parent, GameObject rui)
        {
            var machine = GameObject.Find("WashingMachine");
            if (machine == null || rui == null)
            {
                Debug.LogWarning("[Day1] Sem `WashingMachine` ou sem `NPC_Rui`: "
                               + "o susto da lavandaria fica por montar.");
                return;
            }

            var group = new GameObject("LAUNDRY_SCARE");
            group.transform.SetParent(parent, false);

            // 1. O sitio onde ele fica. Encostado a maquina, de costas para a porta —
            //    quem entra ve-o de costas antes de ele se virar.
            var spot = new GameObject("RUI_LaundryAnchor");
            spot.transform.SetParent(group.transform, false);
            spot.transform.position = machine.transform.position + new Vector3(0f, 0f, 0.62f);
            spot.transform.rotation = Quaternion.LookRotation(
                machine.transform.position - spot.transform.position, Vector3.up);

            var action = spot.AddComponent<Pungent.NPC.RoutineActionAnchor>();
            var actionSo = new SerializedObject(action);
            actionSo.FindProperty("animatorState").stringValue = "CounterLean";
            actionSo.FindProperty("seconds").floatValue = 26f;
            actionSo.FindProperty("variance").floatValue = 8f;
            actionSo.ApplyModifiedPropertiesWithoutUndo();

            // 2. O susto: camara presa nele, e o sting.
            var snapObject = new GameObject("SNAP_RuiLaundry");
            snapObject.transform.SetParent(group.transform, false);
            snapObject.transform.position = spot.transform.position + new Vector3(0f, 0f, 1.35f);

            var source = snapObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // o sting e nao-diegetico: nao vem de um sitio

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/Audio/sting.mp3");
            if (clip == null)
                Debug.LogWarning("[Day1] Sem `Assets/ThirdParty/Audio/sting.mp3`: o susto "
                               + "acontece calado.");

            var snap = snapObject.AddComponent<AttentionSnap>();
            snap.EditorConfigure(AttentionSnap.Trigger.NearBy,
                firesOn: string.Empty,
                arms: "day1_chores_open",
                silencedBy: "day1_laundry_seen",
                target: rui.transform, offset: new Vector3(0f, 1.5f, 0f),
                hold: 1.6f,
                thoughtText: "He was just standing there.",
                raises: "day1_laundry_seen",
                near: 2.2f, delay: 0f, lineOfSight: true,
                source: source, audio: clip);

            Debug.Log("[Day1] Susto da lavandaria montado: ancora de rotina em "
                    + spot.transform.position.ToString("F2")
                    + ", camara presa a 2,2 m com linha de vista, sting "
                    + (clip != null ? "ligado" : "POR PREENCHER")
                    + ". A conversa que se segue ainda nao esta escrita.");
        }

        [MenuItem("Pungent/Blockout/Wire Day 1 (Good deal)", false, 12)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireDayOne")) return;

            var scene = EditorSceneManager.GetActiveScene();
            var player = GameObject.Find("PlayerRoot");
            if (player == null)
            {
                Debug.LogError("[Day1] Sem PlayerRoot nesta cena.");
                return;
            }

            var rui = GameObject.Find("NPC_Rui");
            if (rui == null)
                Debug.LogWarning("[Day1] NPC_Rui nao encontrado: a fala do carro fica por montar.");

            var chapter = WriteChapter();
            var lines = WriteLines();
            var market = WritePhoneThread();
            AddChapterToSystems(chapter);

            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Wire day one");

            BuildLaundryScare(root.transform, rui);

            // Tudo o que e deste dia num grupo desligado, como no Dia 5. O
            // apartamento corre cinco capitulos: o volume da cozinha disparava a
            // meio do prologo, e a secretaria oferecia "Sit down and work" na noite
            // em que ele ainda estava a desfazer as caixas.
            var content = new GameObject(Content);
            content.transform.SetParent(root.transform, false);

            BuildKitchenArrival(content.transform);
            BuildBalcony(content.transform, lines);
            BuildRuiRemark(content.transform, rui, lines);
            BuildDesk(content.transform, lines);

            BuildStage(root.transform, player, market, content);
            content.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Day1] Dia 1 ligado: day1_kitchen -> day1_car_seen -> " +
                      "day1_deal -> day1_desk -> day_one_done (02:47).");
        }

        // ------------------------------------------------------------------

        private sealed class Lines
        {
            public ThoughtLineSet Car, Desk, RuiCar;
        }

        /// <summary>
        /// O texto do dia: o que o Tomas pensa, e o que o Rui diz.
        ///
        /// Nada disto e sublinhado. Nao ha musica, nao ha reaccao escrita e ninguem
        /// comenta a fala dele — **a fissura tem de passar despercebida a primeira
        /// vez**, senao o jogo ensina o jogador a desconfiar antes de ele ter razao
        /// para isso.
        /// </summary>
        private static Lines WriteLines()
        {
            var lines = new Lines
            {
                // **O detalhe que vai voltar tem de ser impossivel de saber de fora.**
                //
                // Isto era *"Third space from the corner, where I left it."* — onde o
                // carro estava estacionado. Nao funcionava, e a razao e simples: onde
                // um carro esta parado ve-o **qualquer pessoa que passe na rua**. Um
                // estranho a repetir esse detalhe explica-se sozinho, e um detalhe que
                // se explica sozinho nao deixa desconforto nenhum atras de si.
                //
                // A regra do projecto e "tem de **poder** explicar-se", e ha uma
                // diferenca entre poder e explicar-se por si.
                //
                // Uma porta que so abre por dentro nao se ve da rua. Sabe-a quem
                // entrou no carro, ou quem viu o Tomas dar a volta para abrir — ou
                // seja, alguem que esteve perto dele. E uma pessoa dessas ha uma so.
                Car = Set("THT_Day1_Car", 3.4f,
                    "It is down there. Passenger door still only opens from the inside.",
                    "Front discs are shot and the alternator is on its way out.",
                    "A shop quoted me two hundred just for the parts."),

                // O que o Rui diz. Duas frases, e nenhuma acusa nada.
                //
                // A primeira devolve-lhe **a porta** — o detalhe que o Tomas acabou de
                // pensar sozinho. E a regra de escrita do projecto: dramatico le-se
                // como truque de guiao, banal le-se como vigilancia.
                //
                // E continua a explicar-se: ele vive aqui, pode te-lo visto dar a
                // volta ao carro uma dezena de vezes. O jogador encolhe os ombros, e e
                // suposto encolher — a frase so passa a valer alguma coisa quando um
                // homem a quarenta minutos de casa a disser tambem.
                //
                // A segunda explica-se sozinha da mesma maneira: ouviu os discos daqui
                // de cima, o que e perfeitamente possivel. Uma explicacao que funciona
                // incomoda mais do que uma que nao funciona, porque deixa o jogador
                // sem nada de concreto de que se queixar.
                RuiCar = Set("THT_Day1_Rui_Car", 3.6f,
                    "You want to get that passenger door looked at. It will jam shut one day.",
                    "Front discs are going, by the sound of it. I would get that done before winter."),

                Desk = Set("THT_Day1_Desk", 3.2f,
                    "Ninety for both. That is not a price, that is a story.",
                    "Deadline is Monday. Colour pass on four minutes of somebody's wedding.")
            };

            AssetDatabase.SaveAssets();
            return lines;
        }

        private static ThoughtLineSet Set(string fileName, float hold, params string[] text)
        {
            string path = ThoughtFolder + fileName + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<ThoughtLineSet>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ThoughtLineSet>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.EditorPopulate(text, ThoughtLineSet.Order.SequentialLastRepeats, hold);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // ------------------------------------------------------------------

        private static ChapterDefinition WriteChapter()
        {
            var chapter = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);
            if (chapter == null)
            {
                chapter = ScriptableObject.CreateInstance<ChapterDefinition>();
                AssetDatabase.CreateAsset(chapter, ChapterPath);
            }

            var steps = new[]
            {
                // **Os objectivos dizem o que fazer, e nao o estado de espirito.**
                //
                // Estavam escritos como frases de humor — "Take a breather", "Settle
                // in" — e a segunda linha, que e a que devia dizer onde ir, estava
                // escrita como observacao: "The balcony is open". A jogar isso nao
                // e uma instrucao, e uma nota de atmosfera, e o resultado medido foi
                // um jogador a fumar tres cigarros a espera que o passo avancasse,
                // quando o que o fecha e ir a varanda olhar para a rua.
                //
                // O misterio deste jogo e *porque*, nunca *o que* — e a regra ja
                // estava escrita no `CarryableBox`. Estes passos estavam a fazer
                // misterio da coisa errada.
                Chores("OBJECTIVE: Get something to eat.",
                     "The kitchen is at the end of the hall",
                     "First morning in a room that is mine.",
                     "day1_kitchen"),

                // A rotina, e o unico passo deste dia que custa tempo de propósito.
                //
                // Sem ele o Dia 1 resolvia-se em quatro cliques e a fissura do carro
                // caia num dia que ainda nao tinha acontecido — o jogador nao teve
                // tempo nenhum de achar o apartamento normal, e uma coisa so pode
                // ficar estranha depois de ter sido normal. O `ChoreSet` pede tres
                // tarefas de cinco; ver `DailyPressureWiring`.
                // Sem linha secundaria escrita a mao: quem a preenche agora e o
                // `ChoreSet`, tarefa a tarefa, com a conta do que falta. A linha fixa
                // que aqui estava — "Eat, wash up, unpack, sit down" — nomeava quatro
                // coisas, pedia tres de cinco, e nunca se apagava a medida que se
                // fazia nenhuma delas.
                // **"Settle in." nao dizia o que o jogo queria.**
                //
                // O objectivo secundario ja mostrava a tarefa seguinte e a conta do
                // que falta, mas so **depois** de o jogador ter feito a primeira — e
                // ate la a linha principal era uma frase de atmosfera a fazer de
                // instrucao. Reportado a jogar: "pouco claras".
                //
                // Dizer o numero em cima nao tira a escolha, que e o que esta secao
                // protege: continuam a ser tres de quatro, e o jogador escolhe quais.
                // So deixa de ter de adivinhar que ha uma conta.
                Step("OBJECTIVE: Settle in. Do three things around the flat.",
                     "The boxes are gone. Now it is just a flat I have not tidied.",
                     "day1_chores"),

                // Nao aponta ao carro nem denuncia a cena, mas tambem nao deixa o
                // HUD vazio a parecer que o capitulo acabou ou encravou.
                Chores("OBJECTIVE: Take a breather.",
                     "Out on the balcony. Look down at the street",
                     "He was already up. Every door in this flat was already open.",
                     "day1_car_seen"),

                // O mesmo objectivo fica durante o silencio e a fala: mudar o HUD
                // aqui denunciava o Rui antes de ele abrir a boca. Sem isto a
                // reaccao do Tomas chegava antes da causa: o `day1_car_seen`
                // fecha-se no instante em que o jogador larga o olhar da rua, e o
                // Rui so abre a boca dois segundos depois.
                Step("OBJECTIVE: Take a breather.", string.Empty, "day1_car_remarked"),

                Chores("OBJECTIVE: Check your phone.",
                     "Someone messaged about parts",
                     "I never told him what I drive.",
                     "day1_deal"),

                Chores("OBJECTIVE: Get some work done.",
                     "Sit at the desk",
                     "Ninety for both. Nobody sells these for ninety.",
                     "day1_desk", "day_one_done")
            };

            chapter.EditorPopulate("Day 1 - Good deal", "prologue_done", steps,
                "TUESDAY", "09:20");
            EditorUtility.SetDirty(chapter);
            AssetDatabase.SaveAssets();
            return chapter;
        }

        /// <summary>
        /// Como <see cref="Step"/>, mas com a linha secundaria preenchida.
        ///
        /// A linha secundaria e onde se diz **onde ir e o que carregar**. O objectivo
        /// de cima da o tom; este da a instrucao. Quando esta vazio, o jogador tem o
        /// tom e mais nada — e foi assim que o passo da varanda ficou tres cigarros
        /// sem avancar.
        /// </summary>
        private static ChapterDefinition.Step Chores(string objective, string side,
            string thought, string eventId, params string[] raiseOnComplete)
        {
            var step = Step(objective, thought, eventId, raiseOnComplete);
            step.SideObjective = side;
            return step;
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

        /// <summary>
        /// O anuncio, e a escolha do §5.
        ///
        /// O `PHN_Seller` ja existia e comecava com *"Morning. Still want the
        /// alternator and the front discs?"* — uma frase que pressupoe uma conversa
        /// anterior que o jogo nunca tinha. **Esta e essa conversa.** O contacto
        /// ainda nao tem nome: e um anuncio, e so no dia da entrega e que passa a
        /// ser o Vitor.
        ///
        /// A escolha nao cancela nada (§5): muda o **tom** com que ele e abordado, e
        /// e o tom, nao a posicao, que alimenta a ameaca oculta.
        /// </summary>
        private static PhoneConversationDefinition WritePhoneThread()
        {
            var thread = AssetDatabase.LoadAssetAtPath<PhoneConversationDefinition>(PhonePath);
            if (thread == null)
            {
                thread = ScriptableObject.CreateInstance<PhoneConversationDefinition>();
                AssetDatabase.CreateAsset(thread, PhonePath);
            }

            var steps = new[]
            {
                new PhoneConversationDefinition.Step
                {
                    Line = "Alternator + front discs, same model as yours. 90 for both. "
                         + "Boxed, never fitted.",
                    WaitForEvent = "day1_car_seen",
                    DelaySeconds = 18f,
                    TypingSeconds = 3f,
                    ReplyDelaySeconds = 22f,
                    Choices = new[]
                    {
                        // **A pergunta que faltava, e faltava a olhos vistos.**
                        //
                        // A mensagem diz "same model as yours". Um desconhecido que
                        // sabe que carro ele conduz e exactamente a fissura em que a
                        // cena da varanda foi construida — o Rui a devolver-lhe a
                        // esquina, e o Tomas a pensar "I never told him what I
                        // drive". Chega no mesmo dia, faz a mesma coisa, e nao havia
                        // uma unica resposta que reparasse nisso: as duas opcoes
                        // eram regatear e comprar.
                        //
                        // A explicacao dele funciona, e e por isso que incomoda. A
                        // regra de escrita do projecto e essa: dramatico le-se como
                        // truque de guiao, banal le-se como vigilancia. "Vi-o
                        // estacionado" nao se pode contestar, e quer dizer que ele
                        // esteve na rua a olhar para o carro.
                        new DialogueChoice
                        {
                            Text = "Who is this? How do you know what I drive?",
                            Reply = "Saw it parked. Same as the one I broke for parts. "
                                  + "No offence meant.",
                            Tone = DialogueTone.Edgy
                        },
                        new DialogueChoice
                        {
                            Text = "Boxed and never fitted, for ninety. Where are they from?",
                            Reply = "Off a write-off. Insurance job. All above board, I have the paperwork somewhere.",
                            Tone = DialogueTone.Edgy
                        },
                        new DialogueChoice
                        {
                            Text = "I'll take both. How do I get them?",
                            Reply = "I drop them. Cash at the door, I do not come in.",
                            Tone = DialogueTone.Calm
                        }
                    }
                },

                // **Ele traz as pecas a casa, e nao o contrario.**
                //
                // Isto dizia *"I am at the unit past the water tower. Easier after
                // dark."* — mandava o jogador sair de casa, e a oficina deixou de
                // existir com o corte de 11-08. Uma conversa que marca um sitio a que
                // o jogo nunca vai le-se como um bug, e e pior do que isso: promete
                // uma parte do jogo que nao ha.
                //
                // A troca melhora o Dia 4. Antes, o vendedor era um homem que o Tomas
                // ia visitar; agora e um homem que **sabe onde ele mora** — e que o vai
                // dizer em voz alta ao telefone, do lado de fora daquela porta, dois
                // dias depois. A frase "I do not come in" fica a pesar quando ele
                // estiver no patamar.
                new PhoneConversationDefinition.Step
                {
                    Line = "Give me the block and the floor. I will not knock, I will "
                         + "leave it and text you.",
                    DelaySeconds = 30f,
                    TypingSeconds = 4f,
                    EndsThread = true
                }
            };

            thread.EditorPopulate("Unknown number", steps);
            EditorUtility.SetDirty(thread);
            AssetDatabase.SaveAssets();

            // Quando e que estes dois entram no telemovel.
            //
            // O anuncio chega **no dia em que ele olha para o carro** — antes disso
            // nao havia razao nenhuma para um numero desconhecido estar gravado, e
            // estava, desde o primeiro segundo do prologo.
            //
            // O Vitor e o mesmo homem com nome. So passa a ser um contacto depois de
            // o negocio estar feito, que e quando uma pessoa grava mesmo o numero de
            // alguem de um site de anuncios. Te-lo no telemovel na primeira noite
            // desfazia a unica coisa que o Dia 1 tem para dar.
            PhoneThreadEditing.SetUnlock(thread, "day1_car_seen");

            var seller = PhoneThreadEditing.Load("PHN_Seller");
            if (seller != null) PhoneThreadEditing.SetUnlock(seller, "day1_deal");
            else Debug.LogWarning("[Day1] PHN_Seller nao encontrado: o Vitor fica a aparecer " +
                                  "no telemovel desde o inicio do jogo.");

            AddThreadToSystems(thread);
            return thread;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// O dono do `prologue_done` e do arranque das 02:47.
        ///
        /// Ver <see cref="DayOneStage"/>: sem isto o prologo acabava e a casa ficava
        /// com as caixas na sala para sempre, porque o `Finish` nunca era chamado.
        /// </summary>
        private static void BuildStage(Transform parent, GameObject player,
            PhoneConversationDefinition market, GameObject content)
        {
            var go = new GameObject("DAY1_STAGE");
            go.transform.SetParent(parent, false);
            var stage = go.AddComponent<DayOneStage>();

            var lights = new List<Light>();
            foreach (var light in Object.FindObjectsOfType<Light>(true))
                if (light.type != LightType.Directional) lights.Add(light);

            var suppress = new List<MonoBehaviour>
            {
                player.GetComponent<Pungent.Player.PlayerMotor>(),
                player.GetComponent<PlayerInteractor>(),
                player.GetComponent<PrototypePhoneUI>()
            };
            suppress.RemoveAll(c => c == null);

            stage.EditorConfigure(
                Object.FindObjectOfType<PrologueStage>(true),
                Object.FindObjectOfType<PlayerDeskOpening>(true),
                GameObject.Find("NPC_Rui"),
                lights.ToArray(),
                suppress.ToArray(),
                player.transform.Find("ViewYaw"),
                player.GetComponentInChildren<Camera>(true)?.transform,
                market, content);

            EditorUtility.SetDirty(stage);
        }

        /// <summary>Chegar a cozinha. Volume largo: e um objectivo mundano, nao um teste.</summary>
        private static void BuildKitchenArrival(Transform parent)
        {
            var go = Make(parent, "EVT_Day1_Kitchen", new Vector3(-5.0f, 0f, 2.85f));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3.6f, 2.4f, 4.0f);
            box.center = new Vector3(0f, 1.2f, 0f);

            Configure(go, "day1_kitchen", ChapterEventRaiser.Trigger.EnterArea,
                string.Empty, string.Empty, null);
        }

        /// <summary>
        /// A varanda: o carro la em baixo, e o Rui atras dele.
        ///
        /// O par `FlavourInteractable` + raiser com `afterReading` e o molde da
        /// lanterna da oficina — quem fala e o objecto, e o acontecimento so se
        /// levanta quando ele acabou de dizer o que tinha a dizer. So depois disso e
        /// que o Rui abre a boca, e por isso e que a frase dele cai em cima de um
        /// jogador que acabou de contar ao ecra o que tem de errado no carro.
        /// </summary>
        private static void BuildBalcony(Transform parent, Lines lines)
        {
            // Varanda: x [0, 5], z a norte de 5.25. Encostado a guarda, virado para
            // a rua — e de la que se ve o lugar onde ele estacionou.
            var go = Make(parent, "EVT_Day1_Car", new Vector3(2.50f, 1.05f, 6.55f));
            // Trigger: um ponto de interaccao nao tem nada desenhado, e um colisor
            // solido sem nada desenhado e uma parede invisivel — trava o jogador, e
            // tapa o que estiver atras dele a mira.
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.60f, 0.60f, 0.50f);

            var flavour = go.AddComponent<FlavourInteractable>();
            var so = new SerializedObject(flavour);
            so.FindProperty("prompt").stringValue = "Look down at the street";
            so.FindProperty("thoughtLines").objectReferenceValue = lines.Car;
            so.ApplyModifiedPropertiesWithoutUndo();

            var raiser = go.AddComponent<ChapterEventRaiser>();
            raiser.EditorConfigure("day1_car_seen", ChapterEventRaiser.Trigger.Interact,
                string.Empty, string.Empty, "day1_kitchen", flavour);
            EditorUtility.SetDirty(raiser);
        }

        /// <summary>
        /// A fala do Rui sobre o carro. **A batida do capitulo.**
        ///
        /// Num objecto proprio e nao no `DayOneStage`: aquele encena o dia inteiro e
        /// ja tem tres trabalhos. Este tem um, e e o mesmo molde do `SellerBeats` da
        /// oficina — uma batida encenada com um dono so.
        /// </summary>
        private static void BuildRuiRemark(Transform parent, GameObject rui, Lines lines)
        {
            var go = new GameObject("EVT_Day1_RuiCar");
            go.transform.SetParent(parent, false);

            var remark = go.AddComponent<Pungent.NPC.RuiCarRemark>();
            remark.EditorConfigure(rui, lines.RuiCar);
            EditorUtility.SetDirty(remark);
        }

        /// <summary>A secretaria. Sentar-se a trabalhar e o que entrega o dia a noite.</summary>
        private static void BuildDesk(Transform parent, Lines lines)
        {
            // Secretaria do Tomas, no quarto dele.
            var go = Make(parent, "EVT_Day1_Desk", new Vector3(-4.45f, 0.95f, -4.35f));
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.20f, 0.70f, 0.80f);

            var flavour = go.AddComponent<FlavourInteractable>();
            var so = new SerializedObject(flavour);
            so.FindProperty("prompt").stringValue = "Sit down and work";
            so.FindProperty("thoughtLines").objectReferenceValue = lines.Desk;
            so.ApplyModifiedPropertiesWithoutUndo();

            var raiser = go.AddComponent<ChapterEventRaiser>();
            raiser.EditorConfigure("day1_desk", ChapterEventRaiser.Trigger.Interact,
                string.Empty, string.Empty, "day1_deal", flavour);
            EditorUtility.SetDirty(raiser);
        }

        // ------------------------------------------------------------------

        private static void AddChapterToSystems(ChapterDefinition chapter)
        {
            AddToSystemsArray(root =>
            {
                var d = root.GetComponentInChildren<ChapterDirector>(true);
                return d == null ? null : new SerializedObject(d).FindProperty("chapters");
            }, chapter, "capitulo");
        }

        private static void AddThreadToSystems(PhoneConversationDefinition thread)
        {
            AddToSystemsArray(root =>
            {
                var s = root.GetComponentInChildren<PhoneMessageService>(true);
                return s == null ? null : new SerializedObject(s).FindProperty("conversations");
            }, thread, "fio");
        }

        /// <summary>
        /// Acrescenta um asset a uma lista dentro do `GAME_SYSTEMS`.
        ///
        /// No prefab e nao na cena: os sistemas atravessam a troca de cena, e um
        /// capitulo acrescentado a instancia do apartamento desaparecia assim que o
        /// jogo passasse pela garagem.
        /// </summary>
        private static void AddToSystemsArray(System.Func<GameObject, SerializedProperty> find,
            Object asset, string label)
        {
            var root = PrefabUtility.LoadPrefabContents(SystemsPrefab);
            if (root == null)
            {
                Debug.LogWarning("[Day1] Sem " + SystemsPrefab + ": o " + label + " fica por ligar.");
                return;
            }

            var array = find(root);
            if (array == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                Debug.LogWarning("[Day1] Lista do " + label + " nao encontrada no GAME_SYSTEMS.");
                return;
            }

            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == asset)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                    return;   // re-executavel
                }

            // O Dia 1 entra **antes** dos outros. O `ChapterDirector` percorre a
            // lista por ordem e fica no primeiro que esteja pronto; posto no fim,
            // um capitulo mais antigo que tambem estivesse pronto ganhava-lhe.
            array.InsertArrayElementAtIndex(0);
            array.GetArrayElementAtIndex(0).objectReferenceValue = asset;
            array.serializedObject.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, SystemsPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
        }

        private static GameObject Make(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            return go;
        }

        private static void Configure(GameObject go, string eventId,
            ChapterEventRaiser.Trigger trigger, string prompt, string thought, string requires)
        {
            var raiser = go.AddComponent<ChapterEventRaiser>();
            raiser.EditorConfigure(eventId, trigger, prompt, thought, requires);
            EditorUtility.SetDirty(raiser);
        }
    }
}
