using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Da forma aos dias: **tarefas que ocupam tempo, e coisas que mudam enquanto
    /// elas decorrem.**
    ///
    /// ---
    ///
    /// **O diagnostico.**
    ///
    /// O jogo tinha os sete capitulos ligados de ponta a ponta e mesmo assim nao
    /// assustava ninguem, e a razao nao era a escrita. Era que cada dia se
    /// resolvia em quatro ou cinco cliques: chegar a cozinha, olhar para a rua,
    /// abrir o telemovel, sentar-se a secretaria. Um jogador que saiba o caminho
    /// acaba o Dia 1 em noventa segundos.
    ///
    /// Num dia de noventa segundos **nao ha onde por tensao**. Nao e um defeito de
    /// ritmo que se corrija a escrever melhores frases: nao existe intervalo em que
    /// alguma coisa possa acontecer, nao ha momento em que o jogador esteja ocupado
    /// e de costas, e nao ha tempo suficiente entre dois acontecimentos para ele
    /// comecar a duvidar do primeiro. A casa nunca tem oportunidade de ser
    /// estranha.
    ///
    /// A seccao 5 pede *"rotina, perturbacao e uma pequena consequencia"* em cada
    /// dia. Havia perturbacao e havia consequencia. **A rotina nunca foi feita**, e
    /// e ela que segura as outras duas.
    ///
    /// ---
    ///
    /// **As duas metades, que sao uma so.**
    ///
    /// 1. **Tarefas** (<see cref="HomeTaskInteractable"/>, que ja existia com uma
    ///    unica tarefa em todo o jogo: fumar na varanda). Comer, lavar a loica,
    ///    arrumar a roupa, por uma maquina. Cada uma prende o jogador no sitio
    ///    durante dez a vinte segundos — **a andar nao, a olhar sim**, que e a
    ///    parte que interessa.
    ///
    /// 2. **Mudancas** (<see cref="HouseChange"/>). Uma cadeira rodada, um banco
    ///    puxado da guarda. Acontecem quando ninguem esta a ver, nunca sao
    ///    comentadas por ninguem, e todas tem uma explicacao inocente possivel.
    ///
    /// Uma sem a outra nao vale nada. Tarefas sozinhas sao afazeres; mudancas
    /// sozinhas nunca chegam a acontecer, porque o jogador nunca esta parado tempo
    /// suficiente longe do sitio certo. Juntas sao a mecanica inteira do genero:
    /// **estiveste ocupado durante vinte segundos e a sala nao esta como a
    /// deixaste.**
    ///
    /// ---
    ///
    /// **Nenhuma tarefa e obrigatoria por si.** O <see cref="ChoreSet"/> pede tres
    /// de cinco no Dia 1 e duas de quatro no Dia 3. Obrigar todas transformava a
    /// casa numa lista; pedir algumas deixa o jogador escolher **quais**, e gasta o
    /// tempo na mesma.
    ///
    /// ---
    ///
    /// **Ordem de execucao:** depois de `Dress Apartment V2` e de `Wire Home
    /// Tasks`. Esta ferramenta toma conta de moveis que a primeira tinha deixado
    /// so com `FlavourInteractable`, e remove esse componente onde a tarefa passa a
    /// falar — um movel que se usa nao devia tambem oferecer "Look".
    ///
    /// Re-executavel.
    /// </summary>
    internal static class DailyPressureWiring
    {
        private const string Root = "DAY_PRESSURE";
        private const string ThoughtFolder = "Assets/Pungent/Dialogue/Thoughts/";

        // As janelas de cada dia, escritas uma vez.
        //
        // **O `day_two` nao abre o Dia 2.** E levantado pelo `DayTwoDirector.Finish`,
        // que corre quando o Tomas acorda na manha do Dia 3. Quem abre a noite das
        // 02:47 e o `day_one_done`. A primeira versao desta ferramenta fechava o
        // Dia 1 em `day_two` e oferecia "Put your clothes away" as tres da manha,
        // no corredor as escuras, a caminho do router.
        //
        // | `prologue_done` | `day_one_done` | `day_two` | `day3_ready` |
        // |---|---|---|---|
        // | comeca o Dia 1 | comeca a noite | comeca o Dia 3 | ele sai de casa |
        private const string Day1Opens = "prologue_done";
        private const string Day1Closes = "day_one_done";
        private const string NightOpens = "day_one_done";
        private const string NightCloses = "day_two";
        private const string Day3Opens = "day_two";
        private const string Day3Closes = "day3_ready";

        /// <summary>Uma tarefa: onde vive, o que diz, quanto tempo ocupa, e o que
        /// fica na mao enquanto dura.</summary>
        private struct Chore
        {
            public string Id, Prop, Prompt, Repeat, Opens, Closes;
            public float Seconds;
            public int Times;
            public string[] Beats;

            /// <summary>Objecto na mao. `null` = maos vazias, e isso e uma escolha:
            /// sentar-se no sofa e recomecar um upload nao trazem nada consigo.</summary>
            public HeldTaskProp.Kind? Held;
        }

        /// <summary>Uma coisa que muda quando ninguem esta a ver.</summary>
        private struct Change
        {
            public string Name, Prop, Opens, Closes;
            public HouseChange.Kind Kind;
            public Vector3 Amount;
        }

        // ==================================================================
        // DIA 1 — instalar-se. Nada aqui e sinistro: e a primeira manha dele
        // numa casa que ainda nao e dele, e as falas sao de quem esta a
        // descobrir onde ficam as coisas.
        // ==================================================================
        private static readonly Chore[] DayOne =
        {
            new Chore
            {
                // **"Wash up" queria dizer duas coisas.** Em ingles tanto e lavar a
                // loica como lavar-te a ti, e o jogador estava em frente a um
                // lava-loica sem saber qual. O comentario do proprio `ChoreSet` ja lhe
                // chamava "Wash the dishes" — o codigo e o ecra nao concordavam.
                //
                // Diz agora o objecto que vai para a mao (`Held = Mug`), que e a unica
                // maneira de o prompt nao poder ser lido de duas maneiras.
                Id = "d1_wash", Prop = "Kit_Sink", Held = HeldTaskProp.Kind.Mug,
                Prompt = "Wash the mug", Repeat = "",
                Seconds = 11f, Times = 1,
                Opens = Day1Opens, Closes = Day1Closes,
                Beats = new[]
                {
                    "Water takes a while to run hot here.",
                    "Leave it on the rack. He will only move it."
                }
            },
            // **A tarefa de sentar saiu, a pedido do dono do projecto.**
            //
            // Vinte segundos de barra de progresso a correr **sem o jogador estar
            // sentado** — o corpo continuava de pe no meio da sala enquanto o ecra
            // dizia que ele descansava. Uma barra que mede uma coisa que nao esta a
            // acontecer e pior do que nao haver tarefa nenhuma: ensina o jogador a
            // desconfiar do que o ecra lhe diz, e este jogo precisa dele a confiar no
            // ecra para poder duvidar da casa.
            //
            // Fazer isto bem exigia uma pose sentada e uma camara a descer ate a
            // altura do sofa. Isso e um clip de animacao, e clips sao trabalho do dono
            // do projecto. Se algum dia existir, a tarefa volta — as tres falas ficam
            // aqui para quem a reescrever:
            //
            //   "First time I have sat down in this room without a box in my hands."
            //   "The traffic downstairs sounds like rain if you stop listening to it."
            //   "I should be working. Five more minutes."
        };

        // ==================================================================
        // A NOITE DAS 02:47 — duas, e **nenhuma obrigatoria**.
        //
        // O caminho critico desta noite pertence ao `OpeningQuestDirector`:
        // levantar-se, atravessar o corredor, reiniciar o router, falar com o Rui,
        // voltar para a cama. Pendurar um `ChoreSet` nesse encadeamento era por um
        // segundo dono a mandar no mesmo capitulo, e este projecto ja pagou seis
        // vezes por passos com dois donos.
        //
        // Nao precisam de ser obrigatorias para fazer o trabalho. O que a noite nao
        // tinha era **um unico momento em que o jogador estivesse parado** — e e
        // disso que a `HouseChange` do quarto dele precisa. Um copo de agua na
        // cozinha as tres da manha e a coisa mais banal do mundo, e sao doze
        // segundos de costas para o corredor.
        // ==================================================================
        private static readonly Chore[] NightOne =
        {
            new Chore
            {
                Id = "d2_water", Prop = "Kit_Sink", Held = HeldTaskProp.Kind.Glass,
                Prompt = "Get a glass of water", Repeat = "",
                Seconds = 12f, Times = 1,
                Opens = NightOpens, Closes = NightCloses,
                Beats = new[]
                {
                    "Cold tap runs warm for a while. Everything here takes a while.",
                    "I can hear the fridge from the hall. That is how quiet it is."
                }
            },
            new Chore
            {
                Id = "d2_upload", Prop = "Laptop_Screen",
                Prompt = "Start the upload again", Repeat = "",
                Seconds = 15f, Times = 1,
                Opens = NightOpens, Closes = NightCloses,
                Beats = new[]
                {
                    "From forty per cent. It does not resume, it starts again.",
                    "Six hours if the line holds. It will not hold.",
                    "Bed. There is nothing else I can do tonight."
                }
            }
        };

        // ==================================================================
        // DIA 3 — a manha em que o Rui nao esta. As mesmas tarefas domesticas,
        // e nenhuma frase acusa ninguem de nada.
        //
        // A regra de escrita do projecto governa aqui mais do que em qualquer
        // outro sitio: **dramatico le-se como truque de guiao, banal le-se como
        // vigilancia.** Ele nao diz que tem medo. Diz que a maquina ja tinha
        // roupa dentro, e deixa o jogador fazer a conta sozinho.
        // ==================================================================
        private static readonly Chore[] DayThree =
        {
            new Chore
            {
                Id = "d3_laundry", Prop = "WashingMachine", Held = HeldTaskProp.Kind.Cloth,
                Prompt = "Put a wash on", Repeat = "",
                Seconds = 16f, Times = 1,
                Opens = Day3Opens, Closes = Day3Closes,
                Beats = new[]
                {
                    "There is already a load in here. Not mine.",
                    "I will do it after. Or I will not, and nobody will say anything.",
                    "Forty minutes. I will be gone before it finishes."
                }
            },
            new Chore
            {
                // Mesma ambiguidade que a do Dia 1: "Wash up" tanto e a loica como
                // tu. Diz o objecto que vai para a mao.
                Id = "d3_wash", Prop = "Kit_Sink", Held = HeldTaskProp.Kind.Mug,
                Prompt = "Wash the mug", Repeat = "",
                Seconds = 11f, Times = 1,
                Opens = Day3Opens, Closes = Day3Closes,
                Beats = new[]
                {
                    "One mug. Always exactly one mug.",
                    "It is still warm."
                }
            },
            // Na mesa da cozinha e nao na porta da rua.
            //
            // "Check the lock" era melhor frase e um erro de montagem: o ponto
            // quente e um filho do movel e ganha o clique dentro da sua janela, e o
            // movel, neste caso, seria a `Door_Front_3B` — que tem o
            // `DoorDragInteractable`. Durante toda a manha do Dia 3 a porta da rua
            // deixaria de oferecer "Open" e passaria a oferecer so isto. A fechadura
            // ja tem quem fale dela no Dia 5, e a porta tem trabalho a fazer.
            new Chore
            {
                Id = "d3_table", Prop = "Dining_Table", Held = HeldTaskProp.Kind.Plate,
                Prompt = "Clear the dining table", Repeat = "",
                Seconds = 12f, Times = 1,
                Opens = Day3Opens, Closes = Day3Closes,
                Beats = new[]
                {
                    "Two plates. I ate alone last night.",
                    "Both chairs pulled out, though."
                }
            },

            // **Limpar o vinho e a garrafa partida**, a pedido do dono do projecto.
            //
            // A poca e os vidros ficam no chao da cozinha desde as 02:47, e ate agora
            // nao havia nada a fazer com eles — o jogador encontrava o Rui caido, ele
            // levantava-se, e a casa ficava com aquilo ali a manha toda como se nada
            // fosse.
            //
            // **Poder limpar muda o que aquilo significa.** Deixa de ser cenario e
            // passa a ser uma coisa que ele teve de arrumar por outra pessoa, de
            // joelhos no chao da cozinha, na manha em que essa pessoa nao esta em casa
            // e nao atende mensagens. Nenhuma das falas o diz.
            //
            // E a unica das quatro que nao e rotina: as outras sao a vida dele, esta e
            // a do Rui.
            new Chore
            {
                Id = "d3_clean", Prop = "MESS_V2", Held = HeldTaskProp.Kind.Cloth,
                Prompt = "Clean up the wine", Repeat = "",
                Seconds = 15f, Times = 1,
                Opens = Day3Opens, Closes = Day3Closes,
                Beats = new[]
                {
                    "The glass went further than I thought.",
                    "It has been here long enough to go tacky.",
                    "He did not clean this up. He just went to bed."
                }
            }
        };

        // ==================================================================
        // AS MUDANCAS
        //
        // Tres, e nenhuma mais. Uma quarta transformava isto numa mecanica e o
        // jogador comecava a olhar para tras de proposito, a espera dela — que
        // e o oposto exacto do efeito.
        //
        // Todas em moveis parados. Portas nao: a `Door_Bedroom_Rui` tem
        // `Rigidbody` e `HingeJoint`, e escrever no transform dela era brigar
        // com a fisica e ganhar um movel a tremer.
        // ==================================================================
        private static readonly Change[] Changes =
        {
            // A poltrona da sala, rodada para o corredor. Alguem sentado nela ve
            // quem passa. Ninguem diz isso, e uma poltrona roda-se com uma perna.
            new Change
            {
                Name = "CHG_CoffeeTable", Prop = "Living_CoffeeTable",
                Kind = HouseChange.Kind.Move, Amount = new Vector3(0.18f, 0f, -0.10f),
                Opens = NightOpens, Closes = NightCloses
            },

            // A luz do quarto **dele**, acesa, com ele encostado ao fogao na
            // cozinha a dois metros do jogador.
            //
            // E a unica das quatro que se ve sem se olhar para nada: a porta dele
            // "nunca esta bem fechada", e a risca de luz por baixo aparece no
            // caminho de volta do router. E exactamente o que a seccao 5 pede para
            // esta noite — *"no regresso, um objeto ou porta mudou discretamente"*.
            //
            // Continua a caber na regra da explicacao inocente: ele foi la buscar
            // uma coisa enquanto o Tomas estava na cozinha. So que o Tomas esteve
            // sempre a olhar para ele.
            new Change
            {
                Name = "CHG_RuiRoomLight", Prop = "Bedroom_Rui_Light",
                Kind = HouseChange.Kind.Light, Amount = Vector3.zero,
                Opens = NightOpens, Closes = NightCloses
            },

            // O banco da varanda, puxado da guarda. E o unico sitio da casa onde
            // o Tomas para por vontade propria, e agora esta virado ao contrario.
            new Change
            {
                Name = "CHG_BalconyPlant", Prop = "Balcony_Plant_A",
                Kind = HouseChange.Kind.Move, Amount = new Vector3(0f, 0f, -0.24f),
                Opens = Day3Opens, Closes = Day3Closes
            },

            // **A caneca da mesa de apoio, que deixa de la estar.**
            //
            // E a unica das cinco em que o objecto **desaparece** em vez de se mexer,
            // e por isso e a unica que o jogador pode ir perguntar. O `RuiDenial` ja
            // esta de pe no `NPC_Rui` a espera exactamente disto: ele nao mente, nao
            // se justifica, **nao reconhece o acontecimento**. A partir dai ha duas
            // versoes e nenhuma testemunha.
            //
            // Continua a caber na regra da explicacao inocente, e neste caso e a mais
            // inocente de todas: alguem levou uma caneca para a cozinha. O
            // `Living_CoffeeTable` ja diz *"Two mugs. I only ever use one."* — o
            // jogador ja reparou nas duas antes de uma sumir.
            //
            // Fica no Dia 3, depois do banco da varanda: sao os dois do mesmo dia, mas
            // este e o unico que se pode testar em voz alta.
            new Change
            {
                Name = "CHG_CoffeeTableMug", Prop = "Mug White",
                Kind = HouseChange.Kind.Vanish, Amount = Vector3.zero,
                Opens = Day3Opens, Closes = Day3Closes
            },

            // A mais forte das tres, e por isso e a ultima: a cadeira **dele**, na
            // secretaria **dele**, no quarto que ele tranca. O Dia 3 ja pos a
            // gaveta entreaberta e a janela aberta; isto acontece depois, com o
            // jogador ja dentro do quarto e a olhar para outro lado.
            new Change
            {
                Name = "CHG_DeskChair", Prop = "Chair_Desk_Tomas",
                Kind = HouseChange.Kind.Rotate, Amount = new Vector3(0f, -38f, 0f),
                Opens = "day3_searched", Closes = "day3_ready"
            }
        };

        [MenuItem("Pungent/Blockout/Wire Daily Pressure (chores + changes)", false, 13)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireDailyPressure")) return;

            var scene = EditorSceneManager.GetActiveScene();
            var player = GameObject.Find("PlayerRoot");
            if (player == null)
            {
                Debug.LogError("[Pressure] Sem PlayerRoot nesta cena.");
                return;
            }

            var tasks = Object.FindObjectOfType<HomeTaskDirector>();
            if (tasks == null) tasks = player.AddComponent<HomeTaskDirector>();
            var thoughts = player.GetComponent<PlayerThoughtDirector>();

            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);

            // **Os pontos quentes nao vivem debaixo do `Root`.**
            //
            // Cada tarefa e filha do movel a que pertence — tem de ser, para herdar
            // a posicao dele e para o `PlayerInteractor` a encontrar ao subir pelos
            // pais a partir do colisor. Apagar so o `DAY_PRESSURE` deixava-os todos
            // para tras, e a segunda passagem desta ferramenta duplicava as dez
            // tarefas: vinte `HomeTaskInteractable` na cena, dez com objecto na mao
            // e dez de maos vazias, e o clique ia parar a qualquer um deles.
            //
            // Apanhado a correr, e exactamente o tipo de coisa que uma ferramenta
            // "re-executavel" tem de garantir e nao prometer.
            int removed = 0;
            foreach (var task in Object.FindObjectsOfType<HomeTaskInteractable>(true))
            {
                if (!task.gameObject.name.StartsWith("TASK_")) continue;
                Object.DestroyImmediate(task.gameObject);
                removed++;
            }
            foreach (var step in Object.FindObjectsOfType<StagedHomeTaskStep>(true))
                Object.DestroyImmediate(step.gameObject);
            if (removed > 0) Debug.Log($"[Pressure] {removed} tarefas antigas removidas.");

            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Wire daily pressure");

            int chores = 0;
            chores += BuildChores(DayOne, tasks, thoughts);
            chores += BuildChores(NightOne, tasks, thoughts);
            chores += BuildChores(DayThree, tasks, thoughts);
            chores += BuildStagedTasks(root.transform, tasks, thoughts);

            // A varanda ja tinha a sua tarefa, montada pelo `HomeTaskWiring` e sem
            // porta nenhuma: era oferecida na noite do prologo, em que ele ainda
            // tem as caixas na sala. Fica com a mesma janela do Dia 1.
            GateExistingSmoke();

            BuildTaskTints();

            int changes = BuildChanges(root.transform);

            BuildChoreSet(root.transform, "DAY1_CHORES",
                // O `d1_sit` saiu; ficam quatro e continuam a pedir-se tres. Se caisse
                // para tres de tres, o "Settle in" deixava de ser uma escolha e passava
                // a ser uma lista de verificacao.
                new[] { "d1_eat", "d1_wash", "d1_laundry", "smoke" }, 3,
                "day1_chores", Day1Opens,
                "That is the flat sorted. I should get some work done.");

            BuildChoreSet(root.transform, "DAY3_CHORES",
                // Cinco, e continuam a pedir-se duas. O `d3_clean` entrou a 11-08.
                new[] { "d3_laundry", "d3_eat", "d3_wash", "d3_table", "d3_clean" }, 2,
                "day3_chores", Day3Opens,
                "One more thing and I can get back to work.");

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Pressure] {chores} tarefas e {changes} mudancas ligadas. " +
                      "Dia 1 pede 3 de 4 (`day1_chores`); Dia 3 pede 2 de 5 (`day3_chores`).");
        }

        // ------------------------------------------------------------------

        private static int BuildStagedTasks(Transform root, HomeTaskDirector tasks,
            PlayerThoughtDirector thoughts)
        {
            var worktop = FindAnywhere("Kit_Worktop");
            var fridge = FindAnywhere("Kit_Fridge");
            var dresser = FindAnywhere("Dresser_Tomas");
            if (worktop == null || fridge == null || dresser == null)
            {
                Debug.LogError("[Pressure] Faltam bancada, frigorifico ou comoda para as tarefas por fases.");
                return 0;
            }

            var plate = HeldProp(HeldTaskProp.Kind.Plate);
            var cloth = HeldProp(HeldTaskProp.Kind.Cloth);

            BuildMeal(root, "d1_eat", Day1Opens, Day1Closes, 16f,
                new[]
                {
                    "Bread, and whatever of mine made it out of the box.",
                    "There is one clean plate and I am fairly sure it is his.",
                    "I will buy my own everything at the weekend."
                }, worktop, fridge, plate, tasks, thoughts);

            BuildMeal(root, "d3_eat", Day3Opens, Day3Closes, 14f,
                new[]
                {
                    "Standing up, over the sink, like an animal.",
                    "His half of the fridge has not moved in three days."
                }, worktop, fridge, plate, tasks, thoughts);

            // **A roupa deixou de aparecer na cama.**
            //
            // Ate 11-08 esta pilha nascia em cima do colchao no inicio do Dia 1, vinda
            // do nada, e a tarefa de a arrumar estava partida — reportado a jogar. E
            // era estranha por outra razao: o prologo inteiro e sobre trazer as tuas
            // coisas para dentro, e depois havia uma pilha que nunca entrou por porta
            // nenhuma.
            //
            // Agora a roupa entra na quarta caixa do prologo e vai para a comoda. Esta
            // tarefa passa a ser o passo seguinte da mesma coisa: **tirar da comoda e
            // por na maquina**. Nasce onde o jogador a deixou.
            var clothesRoot = new GameObject("CLOTHES_TO_WASH");
            clothesRoot.transform.SetParent(root, false);

            Vector3 clothesFrom = new Vector3(-3.75f, 0.81f, -3.80f);   // tampo da comoda
            var dresserMesh = GameObject.Find("Dresser_Tomas_Mesh");
            if (dresserMesh != null)
            {
                var r = dresserMesh.GetComponentInChildren<Renderer>(true);
                if (r != null) clothesFrom = new Vector3(r.bounds.center.x, r.bounds.max.y, r.bounds.center.z);
            }
            clothesRoot.transform.position = clothesFrom;

            var clothPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Pungent/Meshes/HeldProps/cloth.prefab");
            GameObject clothesVisual = null;
            if (clothPrefab != null)
            {
                clothesVisual = (GameObject)PrefabUtility.InstantiatePrefab(clothPrefab);
                clothesVisual.name = "ClothesPile";
                clothesVisual.transform.SetParent(clothesRoot.transform, false);

                // **O pivot da pilha nao esta na base dela.** Medido: com o objecto a
                // 0,78, a malha comeca a 0,73 — cinco centimetros de diferenca. Poisar
                // o *pivot* na superficie deixa a pilha meia enterrada no colchao, que
                // e o mesmo erro ao contrario.
                //
                // A conta e feita depois de a malha existir porque so ela sabe onde
                // esta a propria base.
                var clothRenderer = clothesVisual.GetComponentInChildren<Renderer>(true);
                if (clothRenderer != null)
                {
                    float lift = clothesRoot.transform.position.y - clothRenderer.bounds.min.y;
                    clothesRoot.transform.position += Vector3.up * lift;
                }
            }

            var clothesTaskObject = new GameObject("STAGED_d1_laundry");
            clothesTaskObject.transform.SetParent(root, false);
            var clothesTask = clothesTaskObject.AddComponent<StagedHomeTask>();

            // **Os prompts dizem o objecto e o destino.** O antigo dizia "Pick up the
            // clothes from the bed" para uma pilha que nao devia estar na cama, e a
            // tarefa nem sequer se fechava. Reportado a jogar como "pouco claras e mal
            // feitas".
            clothesTask.EditorConfigure(StagedHomeTask.Mode.CarryToDestination, "d1_laundry",
                // "Load the machine" e nao "Put a wash on": essa e a tarefa do Dia 3
                // (`d3_laundry`), e dois prompts iguais em dias diferentes fazem o
                // jogador pensar que ja fez esta.
                "Take the clothes from the drawers", "Load the machine", 0f,
                Day1Opens, Day1Closes, cloth, clothesVisual,
                Lines("THT_Task_d1_laundry", 3.1f, new[]
                {
                    "Everything I own smells of the old place.",
                    "The machine is in the bathroom. Of course it is.",
                    "There. That is the last of the boxes, in a manner of speaking."
                }), tasks, thoughts);

            BuildFreeStep(clothesRoot.transform, "TASKSTEP_d1_laundry_pickup", clothesTask,
                StagedHomeTask.Step.First, new Vector3(0.72f, 0.46f, 0.72f));

            // **O destino e a maquina de lavar**, e nao a comoda de onde ela saiu.
            // Passar a roupa de um sitio para o mesmo sitio nao e uma tarefa.
            var machine = GameObject.Find("WashingMachine");
            if (machine != null)
                BuildFurnitureStep(machine, "TASKSTEP_d1_laundry_machine", clothesTask,
                    StagedHomeTask.Step.Second);
            else
                BuildFurnitureStep(dresser, "TASKSTEP_d1_laundry_machine", clothesTask,
                    StagedHomeTask.Step.Second);

            return 3;
        }

        private static void BuildMeal(Transform root, string id, string opens, string closes,
            float duration, string[] beats, GameObject worktop, GameObject fridge,
            HeldTaskProp plate, HomeTaskDirector tasks, PlayerThoughtDirector thoughts)
        {
            var go = new GameObject("STAGED_" + id);
            go.transform.SetParent(root, false);
            var task = go.AddComponent<StagedHomeTask>();
            task.EditorConfigure(StagedHomeTask.Mode.WalkingMeal, id,
                "Take a clean plate", "Get food from the fridge", duration,
                opens, closes, plate, null, Lines("THT_Task_" + id, 3.1f, beats),
                tasks, thoughts);

            BuildFurnitureStep(worktop, "TASKSTEP_" + id + "_plate", task,
                StagedHomeTask.Step.First);
            BuildFurnitureStep(fridge, "TASKSTEP_" + id + "_fridge", task,
                StagedHomeTask.Step.Second);
        }

        private static void BuildFurnitureStep(GameObject prop, string name,
            StagedHomeTask task, StagedHomeTask.Step step)
        {
            var go = new GameObject(name);
            go.transform.SetParent(prop.transform, false);
            go.layer = prop.layer;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            FitTo(box, prop, 1.08f);
            ReachableFrom(box, prop);
            go.AddComponent<StagedHomeTaskStep>().EditorConfigure(task, step);
        }

        /// <summary>
        /// Garante que o volume de um gesto se consegue apontar.
        ///
        /// ---
        ///
        /// **Um tampo de tres centimetros dava um alvo de tres centimetros.** O
        /// `FitTo` copia as bounds do movel, e o `Kit_Worktop` e uma tabua: o passo
        /// "Take a clean plate" ficava com um volume de 2,08 x **0,05** x 0,80,
        /// deitado a altura da bancada. Medido na cena. Acertar-lhe exigia apontar a
        /// uma fatia de cinco centimetros a um metro de altura, e o relatorio de
        /// teste diz o que isso e a jogar: *"nao ha indicacao de onde estao as
        /// coisas, esta tudo muito fraco"*.
        ///
        /// Ninguem poisa um prato **dentro** de um tampo: poisa-o **em cima**. O
        /// volume passa a ter uma altura minima de gesto e a assentar por cima da
        /// superficie, que e onde a mao vai. Moveis altos — o frigorifico, a maquina
        /// de lavar — nao mudam nada: ja sao mais altos do que o minimo.
        /// </summary>
        private static void ReachableFrom(BoxCollider box, GameObject prop)
        {
            const float minimumHeight = 0.45f;

            var scale = prop.transform.lossyScale;
            float worldHeight = box.size.y * Mathf.Abs(scale.y);
            if (worldHeight >= minimumHeight) return;

            float was = box.size.y;
            box.size = new Vector3(box.size.x,
                minimumHeight / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                box.size.z);

            // **Cresce para cima a partir da base, e mexe-se no `center` e nao na
            // posicao no mundo.**
            //
            // A primeira versao escrevia `box.transform.position` e o volume ficava
            // exactamente onde estava — o `FitTo` ja tinha posto a posicao no centro
            // das bounds, e as duas escritas anulavam-se de maneiras que dependem da
            // escala do movel. O `center` do colisor e local e nao tem essa
            // ambiguidade: a caixa cresce a partir de onde estava e nao muda de
            // sitio.
            //
            // Cresce para cima porque e para cima que fica a mao de quem poisa um
            // prato num tampo.
            box.center += new Vector3(0f, (box.size.y - was) * 0.5f, 0f);
        }

        private static void BuildFreeStep(Transform parent, string name,
            StagedHomeTask task, StagedHomeTask.Step step, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            go.AddComponent<StagedHomeTaskStep>().EditorConfigure(task, step);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Monta as tarefas de um dia.
        ///
        /// Cada tarefa vive num filho do movel e nao no movel: o
        /// `HomeTaskInteractable` e `DisallowMultipleComponent`, e a bancada da
        /// cozinha tem uma tarefa no Dia 1 e outra no Dia 3.
        /// </summary>
        private static int BuildChores(Chore[] set, HomeTaskDirector tasks,
            PlayerThoughtDirector thoughts)
        {
            int built = 0;

            foreach (var chore in set)
            {
                var prop = FindAnywhere(chore.Prop);
                if (prop == null)
                {
                    Debug.LogWarning($"[Pressure] '{chore.Prop}' nao existe na cena: " +
                                     $"a tarefa '{chore.Id}' fica por montar.");
                    continue;
                }

                // **Nada e removido do movel.**
                //
                // A primeira versao disto apagava o `FlavourInteractable` de cada
                // movel que ganhava uma tarefa, com o argumento de que um movel que
                // se usa nao devia tambem oferecer "Look". Estava errado por duas
                // razoes, e a segunda partia o jogo:
                //
                // - O `InvasionSigns` guarda uma referencia para o
                //   `FlavourInteractable` da comoda e troca-lhe as linhas no Dia 3,
                //   quando a gaveta aparece entreaberta. Sem ele, a referencia fica
                //   nula, o `ApplyDrawer` salta a troca **sem dar erro nenhum**, e a
                //   comoda continua a dizer que esta tudo no sitio com a gaveta
                //   aberta a frente do jogador. Um sinal de invasao perdido.
                // - E era desnecessario. A disputa ja se resolve sozinha: o
                //   `PlayerInteractor` escolhe o primeiro componente com **prompt
                //   nao vazio**, e o ponto quente e filho do movel. Dentro da janela
                //   da tarefa ganha a tarefa; fora dela o prompt vem vazio e o movel
                //   responde como sempre respondeu.
                var go = new GameObject($"TASK_{chore.Id}");
                go.transform.SetParent(prop.transform, false);

                // `new GameObject` nasce sempre na layer 0, e nao na do pai. Se o
                // movel estiver numa layer propria, a `interactionMask` do
                // `PlayerInteractor` pode nao a apanhar e a tarefa fica invisivel a
                // mira sem dar erro nenhum.
                go.layer = prop.layer;

                // Ponto quente ligeiramente maior do que o movel, e **trigger**.
                //
                // As duas coisas juntas resolvem a disputa pelo clique sem codigo
                // novo: maior faz com que o cast lhe bata primeiro, e trigger faz
                // com que o `ResolveNearestHit` o atravesse quando a tarefa esta
                // fora do seu dia e o prompt vem vazio. Nos outros dias o movel
                // responde como sempre respondeu.
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                FitTo(box, prop, 1.06f);

                var task = go.AddComponent<HomeTaskInteractable>();
                var so = new SerializedObject(task);
                so.FindProperty("id").stringValue = chore.Id;
                so.FindProperty("prompt").stringValue = chore.Prompt;
                so.FindProperty("repeatPrompt").stringValue = chore.Repeat ?? string.Empty;
                so.FindProperty("maximumTimes").intValue = Mathf.Max(1, chore.Times);
                so.FindProperty("seconds").floatValue = chore.Seconds;
                so.FindProperty("exhaustedThought").stringValue = string.Empty;
                so.FindProperty("beatLines").objectReferenceValue =
                    Lines($"THT_Task_{chore.Id}", 3.1f, chore.Beats);
                so.FindProperty("requiresEvent").stringValue = chore.Opens;
                so.FindProperty("silencedByEvent").stringValue = chore.Closes;
                so.FindProperty("thoughts").objectReferenceValue = thoughts;
                so.FindProperty("tasks").objectReferenceValue = tasks;
                if (chore.Held.HasValue)
                    so.FindProperty("progressVisual").objectReferenceValue = HeldProp(chore.Held.Value);
                if (chore.Held == HeldTaskProp.Kind.Mug)
                    so.FindProperty("worldProp").objectReferenceValue = BuildWorldMug(go, prop, chore.Id);
                so.ApplyModifiedPropertiesWithoutUndo();

                built++;
            }

            return built;
        }

        private static GameObject BuildWorldMug(GameObject taskRoot, GameObject sink, string id)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Pungent/Meshes/HeldProps/mug.prefab");
            if (prefab == null) return null;

            var mug = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            mug.name = "WORLDPROP_" + id + "_Mug";
            mug.transform.SetParent(taskRoot.transform, true);

            var renderers = new System.Collections.Generic.List<Renderer>();
            foreach (var renderer in sink.GetComponentsInChildren<Renderer>(true))
                if (!renderer.transform.name.StartsWith("WORLDPROP_")) renderers.Add(renderer);
            Vector3 position = sink.transform.position + new Vector3(0.18f, 0.92f, 0f);
            if (renderers.Count > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Count; i++) bounds.Encapsulate(renderers[i].bounds);
                position = new Vector3(bounds.center.x + bounds.extents.x * 0.55f,
                    bounds.max.y + 0.045f, bounds.center.z);
            }

            mug.transform.position = position;
            mug.transform.rotation = Quaternion.Euler(0f, 18f, 0f);
            foreach (var collider in mug.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            return mug;
        }

        /// <summary>
        /// O objecto que fica na mao, um por tipo, debaixo da `Main Camera`.
        ///
        /// **Um por tipo e nao um por tarefa.** Sete tarefas partilham quatro
        /// objectos: o copo, a caneca, o prato e a roupa. Um `HeldTaskProp` por
        /// tarefa dava dez rigs de primitivas pendurados na camara, todos invisiveis
        /// menos um — e nada distinguia a caneca do `d1_wash` da do `d3_wash`.
        ///
        /// Na camara e nao no `PlayerRoot`, como o `HeldCigarette` ja estava: a pose
        /// destes objectos e dada em espaco de camara, e pendurados no corpo do
        /// jogador ficavam para tras sempre que ele olhasse para cima.
        ///
        /// Re-executavel: reutiliza o que ja la esteja com o mesmo nome.
        /// </summary>
        private static HeldTaskProp HeldProp(HeldTaskProp.Kind kind)
        {
            var camera = Object.FindObjectOfType<Camera>();
            if (camera == null)
            {
                Debug.LogWarning("[Pressure] Sem camara na cena: as tarefas ficam sem " +
                                 "nada nas maos.");
                return null;
            }

            string name = "HELD_" + kind;
            var existing = camera.transform.Find(name);
            if (existing != null)
            {
                var found = existing.GetComponent<HeldTaskProp>();
                if (found != null)
                {
                    // **O modelo e reprocurado, e nao so a primeira vez.**
                    //
                    // O `LEIA-ME.txt` da pasta dos modelos promete que largar la um
                    // ficheiro e correr esta ferramenta chega, sem mexer na cena.
                    // Isso era mentira em qualquer cena que ja tivesse sido ligada
                    // uma vez: este `return` acontecia antes do
                    // `AttachModelIfPresent`, e os quatro objectos ja existem desde
                    // que as tarefas foram montadas.
                    //
                    // E a ordem de trabalho normal que expunha o buraco — as
                    // primitivas existem para o jogo correr **enquanto** os modelos
                    // nao existem, portanto eles chegam sempre depois. A ferramenta
                    // so sabia adopta-los no unico caso em que ainda nao eram
                    // precisos.
                    //
                    // A **pose nao** e reposta de proposito: ela foi medida contra o
                    // tamanho real do que esta na mao, e um modelo novo tem outro
                    // tamanho. Repor aqui os numeros do enquadramento antigo era
                    // desfazer, a cada re-execucao, o unico ajuste que tem de ser
                    // feito com o objecto ja la dentro.
                    AttachModelIfPresent(found, kind);
                    EditorUtility.SetDirty(found);
                    return found;
                }
                Object.DestroyImmediate(existing.gameObject);
            }

            var go = new GameObject(name);
            go.transform.SetParent(camera.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var prop = go.AddComponent<HeldTaskProp>();
            AttachModelIfPresent(prop, kind);

            // Poses por tipo. Todas abaixo do centro do ecra e ao lado da mao
            // dominante: o que esta em causa e o canto do olho, e nao tapar o
            // apartamento — que e a unica coisa que interessa durante estes
            // segundos.
            //
            // ---
            //
            // **Estavam todas fora do ecra, e a regra de cima e que as la pos.**
            //
            // "Nao tapar o apartamento" foi cumprido baixando o objecto ate ele
            // deixar de aparecer. Medido em Play, com a camara a 60 graus de campo
            // vertical, quanto e que se via mesmo de cada um a meio da tarefa:
            //
            //   caneca  28% da altura        prato  21%
            //   pano     4% da altura        copo   76%
            //
            // O pano com 4% e o caso que explica o resto: com o centro do objecto a
            // 33 graus abaixo do olhar e meio campo a valer 30, o **centro dele ja
            // estava por baixo da margem de baixo do ecra**. O que se via era a
            // aresta de cima de uma pilha de roupa. Catorze segundos preso no sitio
            // a olhar para uma bancada, que era exactamente o que estes objectos
            // existem para evitar.
            //
            // A correccao nao e subir e pronto: subir o pano ate se ver punha-o a
            // ocupar 62% da altura do ecra, e ai tapava mesmo o apartamento. Sao
            // duas coisas separadas e resolvem-se com dois numeros diferentes:
            //
            // - **a distancia manda no tamanho.** Afastar da camara ate cada um
            //   ocupar cerca de 40% da altura — presente ao canto do olho, longe de
            //   dominar. E porque o vector todo e escalado, o objecto nao muda de
            //   canto: so fica mais pequeno;
            // - **a altura manda no enquadramento.** Subir ate a base assentar
            //   mesmo por baixo da margem, para ele ficar pousado no canto de baixo
            //   em vez de cortado a meio.
            //
            // Resultado medido, com o gesto inteiro percorrido e nao so uma pose:
            // os quatro ficam entre 32% e 41% da altura do ecra e entre 81% e 86%
            // dentro dele. O que sai fora e a base, que e o que deve sair.
            switch (kind)
            {
                // Estes numeros valem para os modelos que estao em
                // `Meshes/HeldProps/`, e nao em geral: **um modelo novo com outro
                // volume precisa de os voltar a resolver.** O copo tem 11,5 cm, a
                // caneca 9,5, o prato 21,5 de diametro e a pilha de roupa 30.
                case HeldTaskProp.Kind.Glass:
                    prop.EditorConfigure(kind, new Vector3(0.201f, -0.212f, 0.464f),
                        new Vector3(0f, -10f, 5f));
                    break;

                // Mais ao centro do que os outros: as maos estao dentro do
                // lava-loica, a frente do corpo.
                case HeldTaskProp.Kind.Mug:
                    prop.EditorConfigure(kind, new Vector3(0.076f, -0.134f, 0.365f),
                        new Vector3(12f, -6f, 0f));
                    break;

                case HeldTaskProp.Kind.Plate:
                    prop.EditorConfigure(kind, new Vector3(0.085f, -0.133f, 0.381f),
                        new Vector3(4f, -8f, 0f));
                    break;

                // A pilha de roupa e a maior das quatro — 30 cm de lado — e por
                // isso e a que fica mais longe da camara.
                case HeldTaskProp.Kind.Cloth:
                    prop.EditorConfigure(kind, new Vector3(0.162f, -0.226f, 0.710f),
                        new Vector3(6f, -14f, 0f));
                    break;
            }

            EditorUtility.SetDirty(prop);
            return prop;
        }

        /// <summary>
        /// Pasta onde os modelos dos objectos de mao sao procurados.
        ///
        /// Mesma ideia da pasta das animacoes do Rui: **quem faz o asset larga o
        /// ficheiro com o nome certo e nao mexe em mais nada.** Sem isto era preciso
        /// abrir a cena, encontrar quatro filhos da camara e arrastar cada modelo
        /// para o campo certo — quatro oportunidades de o por no sitio errado, e
        /// zero avisos se ficasse.
        /// </summary>
        private const string HeldModelFolder = "Assets/Pungent/Meshes/HeldProps/";

        /// <summary>
        /// Procura o modelo desta forma e liga-o, se existir.
        ///
        /// Nome do ficheiro = nome do tipo, em minusculas: `glass`, `mug`, `plate`,
        /// `cloth`. Aceita `.fbx`, `.obj` ou `.prefab`.
        ///
        /// **Nao existindo, nao e erro.** As primitivas continuam a servir e o jogo
        /// corre na mesma; e por isso que isto vive numa passagem separada e nao
        /// numa referencia obrigatoria.
        /// </summary>
        private static void AttachModelIfPresent(HeldTaskProp prop, HeldTaskProp.Kind kind)
        {
            string stem = HeldModelFolder + kind.ToString().ToLowerInvariant();

            GameObject model = null;
            foreach (var extension in new[] { ".prefab", ".fbx", ".obj" })
            {
                model = AssetDatabase.LoadAssetAtPath<GameObject>(stem + extension);
                if (model != null) break;
            }

            if (model == null) return;

            var so = new SerializedObject(prop);
            so.FindProperty("authoredModel").objectReferenceValue = model;
            so.ApplyModifiedPropertiesWithoutUndo();

            // O aviso do conteudo em falta e dado em jogo pelo proprio componente,
            // que e quem sabe se o modelo traz o filho certo. Aqui so se diz que
            // deixou de haver primitivas.
            Debug.Log($"[Pressure] '{model.name}' ligado ao HELD_{kind}; deixa de usar primitivas.");
        }

        /// <summary>
        /// A loica a ficar limpa enquanto se lava.
        ///
        /// ---
        ///
        /// **A barra diz que esta a andar; isto diz que esta a acontecer.** Sao duas
        /// perguntas diferentes e as duas eram precisas. A queixa era que uma tarefa
        /// domestica prende o jogador quinze segundos e ele nao sabe o que se esta a
        /// passar — a barra resolve metade disso, e a metade que fica e a pior: um
        /// gesto sem consequencia visivel continua a ler-se como um temporizador.
        ///
        /// O lava-loica clareia ao longo dos onze segundos e fica assim. Nao volta
        /// atras: a loica lavada nao se suja outra vez, e no Dia 3 o gesto repete-se
        /// noutra tarefa e noutra caneca.
        ///
        /// Uma cor so, sem particulas e sem espuma. O que se quer nao e simular loica
        /// — e o canto do olho registar que aquilo mudou enquanto ele estava ali.
        /// </summary>
        private static void BuildTaskTints()
        {
            var sink = FindAnywhere("Kit_Sink");
            if (sink == null)
            {
                Debug.LogWarning("[Pressure] Sem `Kit_Sink`: a loica lava-se sem mudar de aspecto.");
                return;
            }

            var tint = sink.GetComponent<TaskProgressTint>();
            if (tint == null) tint = sink.AddComponent<TaskProgressTint>();

            // Os renderers do movel, sem os pontos quentes das tarefas — esses nao tem
            // malha nenhuma e entrariam na lista para nada.
            var renderers = new System.Collections.Generic.List<Renderer>();
            foreach (var r in sink.GetComponentsInChildren<Renderer>(true))
                if (!r.gameObject.name.StartsWith("TASK_")) renderers.Add(r);

            // Alfa zero na cor de partida: comeca no que o material ja tiver, seja ele
            // qual for. Assim isto continua certo depois de alguem trocar a textura do
            // lava-loica, que e coisa que acontece.
            tint.EditorConfigure(renderers.ToArray(), new Color(0.96f, 0.97f, 0.95f),
                "_BaseColor", keeps: true);
            EditorUtility.SetDirty(tint);

            int linked = 0;
            foreach (var task in Object.FindObjectsOfType<HomeTaskInteractable>(true))
            {
                if (task.gameObject.name != "TASK_d1_wash" &&
                    task.gameObject.name != "TASK_d3_wash") continue;

                var so = new SerializedObject(task);
                var extras = so.FindProperty("extraVisuals");
                extras.arraySize = 1;
                extras.GetArrayElementAtIndex(0).objectReferenceValue = tint;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(task);
                linked++;
            }

            Debug.Log("[Pressure] Loica: " + renderers.Count + " renderers do lava-loica " +
                      "clareiam durante " + linked + " tarefa(s) de lavar.");
        }

        private static int BuildChanges(Transform parent)
        {
            var group = new GameObject("HOUSE_CHANGES");
            group.transform.SetParent(parent, false);

            int built = 0;
            foreach (var change in Changes)
            {
                var prop = FindAnywhere(change.Prop);
                if (prop == null)
                {
                    Debug.LogWarning($"[Pressure] '{change.Prop}' nao existe na cena: " +
                                     $"a mudanca '{change.Name}' fica por montar.");
                    continue;
                }

                var go = new GameObject(change.Name);
                go.transform.SetParent(group.transform, false);

                var houseChange = go.AddComponent<HouseChange>();
                houseChange.EditorConfigure(prop.transform, change.Kind, change.Amount,
                    change.Opens, change.Closes);

                // **A `lamp` faltava, e sem ela o tipo `Light` nao faz nada.**
                //
                // O `EditorConfigure` liga o alvo, o tipo e a quantidade — e uma
                // mudanca do tipo `Light` nao mexe no *transform* do alvo: acende ou
                // apaga um `Light`, que vive noutro campo. Ficava a nulo, e a luz do
                // quarto do Rui — a **unica** das cinco que se ve sem se olhar para
                // nada, a risca por baixo da porta no caminho de volta do router —
                // simplesmente nao acendia. Sem erro nenhum.
                //
                // Procura-se no proprio prop: a luz de um quarto e filha do quarto.
                if (change.Kind == HouseChange.Kind.Light)
                {
                    var lamp = prop.GetComponent<Light>() ?? prop.GetComponentInChildren<Light>(true);
                    if (lamp != null)
                    {
                        var so = new SerializedObject(houseChange);
                        so.FindProperty("lamp").objectReferenceValue = lamp;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    else
                    {
                        Debug.LogWarning($"[Pressure] '{change.Name}' e do tipo Light mas nao ha "
                                       + $"nenhum componente Light em '{change.Prop}'. A mudanca "
                                       + "fica muda.");
                    }
                }

                EditorUtility.SetDirty(houseChange);
                built++;
            }

            return built;
        }

        private static void BuildChoreSet(Transform parent, string name, string[] ids,
            int required, string raises, string requires, string nearly)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var set = go.AddComponent<ChoreSet>();
            set.EditorConfigure(ids, required, raises, requires, nearly);
            EditorUtility.SetDirty(set);
        }

        /// <summary>
        /// A tarefa da varanda ja existia e nao tinha janela nenhuma.
        ///
        /// Sem isto o jogo oferecia "Smoke a cigarette" na noite da mudanca, com as
        /// caixas ainda por abrir, e outra vez no Dia 5 — se o `HOME_TASKS` nao
        /// tivesse sido desligado pelo `ClimaxStage`, que e uma rede e nao uma
        /// porta.
        /// </summary>
        private static void GateExistingSmoke()
        {
            var smoke = FindAnywhere("Task_Smoke");
            if (smoke == null)
            {
                Debug.LogWarning("[Pressure] 'Task_Smoke' nao encontrado: correr " +
                                 "'Tools/Pungent/Wire Home Tasks' primeiro. O cigarro " +
                                 "conta para o Dia 1 e fica sem porta.");
                return;
            }

            var task = smoke.GetComponent<HomeTaskInteractable>();
            if (task == null) return;

            var so = new SerializedObject(task);
            so.FindProperty("requiresEvent").stringValue = Day1Opens;
            so.FindProperty("silencedByEvent").stringValue = Day1Closes;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Ajusta a caixa as bounds do movel.
        ///
        /// `BoxCollider.size` e `center` sao em espaco local; as `bounds` de um
        /// `Renderer` sao em metros do mundo. Sem dividir pela escala, um movel
        /// escalado ganhava um ponto quente do tamanho errado — armadilha ja paga
        /// neste projecto.
        /// </summary>
        private static void FitTo(BoxCollider box, GameObject prop, float swell)
        {
            var renderers = prop.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                box.size = Vector3.one * 0.8f;
                return;
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            var scale = prop.transform.lossyScale;
            Vector3 Safe(Vector3 v) => new Vector3(
                v.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                v.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                v.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));

            box.size = Safe(bounds.size * swell);
            box.transform.position = bounds.center;
            box.center = Vector3.zero;
        }

        private static ThoughtLineSet Lines(string fileName, float hold, string[] text)
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
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>
        /// A altura da superficie do colchao debaixo deste ponto.
        ///
        /// ---
        ///
        /// **Nao serve o colisor, e nao serve um numero escrito a mao.**
        ///
        /// A roupa por arrumar estava a `y = 0.78`, escolhido a olho. O colisor da
        /// `Bed_Tomas` acaba a 0,743 — parece bater certo, e nao bate: aquele colisor
        /// e uma caixa unica ajustada a malha inteira, **cabeceira incluida**. A
        /// superficie a serio, medida nos vertices da malha debaixo daquele ponto,
        /// esta a **0,437**. A pilha de roupa flutuava vinte e nove centimetros acima
        /// da cama.
        ///
        /// Um raio para baixo tambem nao resolvia: bate no mesmo colisor e devolve os
        /// mesmos 0,743. A unica coisa que sabe onde esta o colchao e a malha.
        ///
        /// E a licao que este ficheiro ja tinha aprendido uma vez, com as ferramentas
        /// que se pousavam a dois metros da comoda: **os destinos saem da malha dos
        /// moveis**, nunca de constantes.
        /// </summary>
        private static Vector3 MattressTop(Vector3 at, float fallbackY)
        {
            var bed = FindAnywhere("Bed_Tomas");
            var filter = bed != null ? bed.GetComponentInChildren<MeshFilter>(true) : null;

            if (filter == null || filter.sharedMesh == null)
            {
                Debug.LogWarning("[Pressure] Sem malha na `Bed_Tomas`: a roupa fica na " +
                                 "altura por omissao e pode ficar a flutuar.");
                return new Vector3(at.x, fallbackY, at.z);
            }

            Transform t = filter.transform;
            float top = float.MinValue;
            var vertices = filter.sharedMesh.vertices;

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = t.TransformPoint(vertices[i]);
                if (Mathf.Abs(world.x - at.x) > 0.28f) continue;
                if (Mathf.Abs(world.z - at.z) > 0.28f) continue;
                if (world.y > top) top = world.y;
            }

            if (top <= float.MinValue + 1f) return new Vector3(at.x, fallbackY, at.z);

            // Um centimetro de folga: assente, e nao meia pilha dentro do colchao.
            return new Vector3(at.x, top + 0.01f, at.z);
        }

        private static GameObject FindAnywhere(string name)
        {
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
