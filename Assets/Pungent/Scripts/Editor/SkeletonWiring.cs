using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Interaction;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Poe de pe o esqueleto do jogo do Dia 3 ao epilogo.
    ///
    /// Tudo fino de proposito: objectivos, pensamentos e gatilhos, sem arte e sem
    /// espacos novos. Serve para se poder jogar do inicio ao fim e descobrir que o
    /// Dia 4 e longo de mais ou que o climax nao funciona — descobertas que custam
    /// dez vezes mais depois de estar tudo vestido.
    ///
    /// Os capitulos ficam em assets: mudar a ordem de um passo e arrastar linhas.
    /// Re-executavel; nao recria capitulos que ja existam.
    /// </summary>
    public static class SkeletonWiring
    {
        private const string Folder = "Assets/Pungent/Narrative/Chapters";

        [MenuItem("Pungent/Blockout/Wire Story Skeleton", false, 36)]
        public static void Wire()
        {
            EnsureFolder();

            var chapters = new List<ChapterDefinition>
            {
                DayThree(),
                DayFour(),
                // **O `CH_Epilogue` generico saiu da cadeia.**
                //
                // Arrancava em `epilogue` — e o `EndingSelector` tambem decide em
                // `epilogue`. O director so corre um capitulo de cada vez, por isso
                // este agarrava-o primeiro e **o final escolhido nunca chegava a
                // correr**. Tres finais escritos, com epilogos, e nenhum deles se via.
                //
                // Era um marcador de posicao de quando os finais ainda nao existiam, e
                // ficou. Agora o `epilogue` tem um dono so: o `EndingSelector`, que
                // levanta o `ending_out`, o `ending_taken` ou o `ending_father`.
                Chapter("CH_Epilogue_Out"),
                // NightRoad() saiu da cadeia com o corte de 11-08. O metodo e o
                // `.asset` ficam; a chamada e que nao. Quem o quiser de volta poe
                // esta linha outra vez e devolve o `raise: climax` ao Dia 4.
                Climax(),
            };
            chapters.RemoveAll(c => c == null);

            var player = GameObject.Find("PlayerRoot");
            if (player == null) { Debug.LogError("[Skeleton] PlayerRoot nao encontrado."); return; }

            // **Esta ferramenta escrevia num director que o jogo nao usa.**
            //
            // Ha dois `ChapterDirector` na cena: um no `PlayerRoot`, posto aqui, e o do
            // `GAME_SYSTEMS`, onde todas as outras ferramentas registam. Em runtime
            // quem manda e o segundo — o `ChapterDirector.Resolve` procura primeiro
            // dentro do `GameSystemsRoot`, e so cai no `FindObjectOfType` se nao
            // houver. O registo feito aqui nunca chegou a valer nada, e ninguem deu
            // por isso porque os capitulos apareciam na mesma: eram as outras
            // ferramentas a mete-los na lista certa.
            //
            // Descoberto a 11-08, a procurar um prologo que parecia ter desaparecido e
            // que afinal estava intacto na lista do lado.
            //
            // **O duplicado nao se apaga aqui.** Tirar um componente de que alguem
            // possa ter uma referencia serializada e outra maneira de partir coisas em
            // silencio; fica registado no handoff para se resolver de proposito.
            var systems = Object.FindObjectOfType<GameSystemsRoot>(true);
            var director = systems != null ? systems.GetComponentInChildren<ChapterDirector>(true) : null;

            if (director == null)
            {
                director = player.GetComponent<ChapterDirector>();
                if (director == null) director = Undo.AddComponent<ChapterDirector>(player);
                Debug.LogWarning("[Skeleton] Sem ChapterDirector no GAME_SYSTEMS; usei o do "
                               + "PlayerRoot. Em runtime pode nao ser este a mandar.");
            }

            var so = new SerializedObject(director);
            var list = so.FindProperty("chapters");

            // **Isto escrevia por cima da lista inteira**, e a lista nao e so desta
            // ferramenta: o Prologo, o Dia 1 e o Dia 2 sao registados pelas ferramentas
            // deles. Bastava correr esta a seguir a qualquer uma para o jogo ficar sem
            // inicio — e nao dava erro nenhum, porque um director sem capitulo que
            // arranque sozinho nao se queixa, apenas nao comeca nada.
            //
            // Aconteceu a 11-08, durante o corte da garagem: quatro capitulos ficaram
            // na lista e tres desapareceram. Passa a acrescentar o que falta e a nao
            // tocar no que ja la esta, como as outras ferramentas de capitulo sempre
            // fizeram.
            // **Capitulos reformados: saem da lista do director, ficam no disco.**
            //
            // A `Build` nunca apaga um `.asset`, e ainda bem. Mas deixa-los na lista
            // viva e outra coisa: o `CH_Night_Road` espera por um acontecimento que
            // ninguem levanta desde o corte, e os tres epilogos antigos idem. Inertes
            // hoje, e uma armadilha para quem venha ler a lista daqui a seis meses.
            int removed = 0;
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                var at = list.GetArrayElementAtIndex(i).objectReferenceValue;
                if (at != null && System.Array.IndexOf(Retired, at.name) < 0) continue;
                if (at != null) Debug.Log("[Skeleton] `" + at.name + "` retirado da lista do director.");
                list.DeleteArrayElementAtIndex(i);
                removed++;
            }

            int added = 0;
            foreach (var chapter in chapters)
            {
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue != chapter) continue;
                    present = true;
                    break;
                }
                if (present) continue;

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = chapter;
                added++;
            }

            so.FindProperty("hud").objectReferenceValue = player.GetComponent<PrototypeHUD>();
            so.FindProperty("thoughts").objectReferenceValue = player.GetComponent<PlayerThoughtDirector>();
            so.FindProperty("player").objectReferenceValue = player.transform;
            so.FindProperty("messages").objectReferenceValue = player.GetComponent<PhoneMessageService>();
            so.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            var log = new List<string>();
            foreach (var c in chapters)
                log.Add($"  {c.Title,-22} {c.StepCount} passos" +
                        (string.IsNullOrEmpty(c.StartsOnEvent) ? "  [arranca sozinho]"
                                                               : $"  [espera '{c.StartsOnEvent}']"));
            Debug.Log($"[Skeleton] Esqueleto ligado. {added} capitulo(s) acrescentado(s), "
                    + $"{list.arraySize} na lista do director.\n" + string.Join("\n", log));
        }

        // ------------------------------------------------------------------
        // Os capitulos. O texto e provisorio e esta aqui para ser reescrito no
        // Inspector: o que importa nesta fase e a forma, nao as palavras.
        // ------------------------------------------------------------------

        /// <summary>
        /// Dia 3 — "Limites". Continua a manha que o DayTwoDirector deixa a meio.
        /// A casa mostra sinais de que alguem la esteve.
        /// </summary>
        private static ChapterDefinition DayThree() => Build("CH_Day3_Limits", "Day 3 - Limits",
            startsOn: "day_two", date: "WEDNESDAY", time: "08:14",
            steps: new[]
            {
                // "Porque agora": ele acorda e o Rui nao esta em casa, o que nunca
                // acontece. O objectivo diz para verificar; o pensamento diz porque.
                Step(objective: "OBJECTIVE: Check the apartment.",
                     thought: "Rui is never out this early. His bed has not been touched.",
                     ends: ChapterDefinition.Completion.ReachArea,
                     place: new Vector3(-1.75f, 0f, -2.95f), radius: 2.2f,
                     raise: new[] { "day3_searched" }),

                // A manha sozinho em casa, e o unico passo deste dia que custa tempo.
                //
                // E aqui que o Dia 3 acontece de verdade: o Rui nao esta, o jogador
                // tem a casa toda e uma razao domestica para atravessar cada divisao,
                // e e nesses segundos parado — a olhar, sem poder andar — que a
                // `HouseChange` da poltrona e a do banco da varanda tem espaco para
                // acontecer. Sem este passo o jogador ia direito as chaves e saia de
                // casa antes de a casa ter tempo de estar errada.
                //
                // O `ChoreSet` pede duas tarefas de quatro; ver `DailyPressureWiring`.
                Step(objective: "OBJECTIVE: Do two things while the flat is empty.",
                     side: "Choose from food, laundry, washing up, the table or the spilled wine",
                     thought: "The flat is mine until one o'clock. That has not happened yet.",
                     ends: ChapterDefinition.Completion.Event, eventId: "day3_chores"),

                // O nome e a hora, em vez de "o vendedor" e "esta manha". O jogador
                // acabou de ler os dois no telemovel — repeti-los aqui e o que faz
                // o objectivo parecer dele e nao do jogo.
                Step(objective: "OBJECTIVE: Finish the morning's work.",
                     side: "Use the laptop at your desk",
                     thought: "One quiet hour. Use it while the flat is still empty.",
                     ends: ChapterDefinition.Completion.Event, eventId: "day3_ready",
                     raise: new[] { "seller_confirms" }),

                Step(objective: "OBJECTIVE: Go to your room and lock the door.",
                     side: "Rui has come back",
                     thought: "He came in without calling out.",
                     ends: ChapterDefinition.Completion.Event, eventId: "day3_door_talk",
                     raise: new[] { "day3_done", "day_four" }),
            });

        /// <summary>
        /// Dia 4 — "A caixa". **Deixou de ser a garagem.**
        ///
        /// ---
        ///
        /// **O que mudou e porque.** A garagem e a estrada sairam (ver
        /// `COMO_TRABALHAR.md` §2.4 e §2.5). O que se cortou foi o *sitio*, e nao o
        /// que la acontecia: a prova e a meia conversa continuam a existir, dentro
        /// de casa, onde doem mais.
        ///
        /// - A lanterna com fita azul deixa de aparecer num monte de pecas a dez
        ///   quilometros e sai da **caixa, desempacotada na cozinha**, com o Rui no
        ///   quarto ao lado e a caixa de ferramentas dela debaixo da cama, a seis
        ///   metros. Mesma informacao, e reaproveita a mecanica de desempacotar que
        ///   o prologo ja ensinou.
        /// - A meia conversa do vendedor passa para **a varanda**: ele esta na rua,
        ///   ao telefone, e para ouvir e preciso debruçar. Perto de mais e ele
        ///   levanta os olhos. E o mesmo parapeito onde, no Dia 1, o Tomas ja tinha
        ///   pensado na porta do passageiro.
        ///
        /// **O `evidence_found` mantem o nome de proposito.** E nele que a chamada
        /// esta armada; mudar-lhe o id partia a ligacao sem dar erro nenhum.
        ///
        /// ---
        ///
        /// **Este capitulo levanta o `climax` e passou a ser ele a abrir o Dia 5.**
        /// Era o `road_resolved` que o fazia, e ele morreu com a estrada. Confirmado
        /// a mao que nao havia segundo dono: o `ClimaxStage.climaxEvent` so faz
        /// `HasFired(...)` — espera, nao levanta.
        ///
        /// ---
        ///
        /// **Falta aqui um passo, e falta de proposito.** O beat de se esconder do
        /// Rui com a lanterna na mao — o degrau do meio entre o Dia 3, onde
        /// esconder-se nao serve de nada, e o Dia 5, onde serve para tudo — ainda
        /// nao tem quem levante o acontecimento dele. Escrever o passo antes do dono
        /// era prender o capitulo a um evento orfao, que e a armadilha que este
        /// projecto ja pagou seis vezes. Entra quando a ligacao existir.
        /// </summary>
        private static ChapterDefinition DayFour() => Build("CH_Day4_Garage", "Day 4 - The box",
            startsOn: "day_four", date: "THURSDAY", time: "22:41",
            steps: DayFourSteps());

        /// <summary>
        /// Os passos do Dia 4, num sitio so. Sao usados por dois donos — o
        /// <see cref="DayFour"/>, que cria o capitulo se ele nao existir, e o
        /// <see cref="RewriteDayFour"/>, que o reescreve quando alguem mandar. Ter
        /// duas copias disto era garantir que divergiam.
        /// </summary>
        private static ChapterDefinition.Step[] DayFourSteps() => new[]
            {
                Step(objective: "OBJECTIVE: Get the box inside.",
                     thought: "Ninety for both. Nobody sells a pair of these for ninety.",
                     ends: ChapterDefinition.Completion.Event, eventId: "parts_carried"),

                // O passo que faz o capitulo existir. O objectivo diz "desempacotar",
                // que e o que ele foi ali fazer; o que ele encontra e outra coisa, e
                // nao esta escrito em lado nenhum no ecra.
                Step(objective: "OBJECTIVE: Unpack it. Kitchen counter.",
                     thought: "His door has been shut since I got back.",
                     ends: ChapterDefinition.Completion.Event, eventId: "evidence_found"),

                // Sem objectivo, de proposito: nada no ecra diz que se pode ouvir. Quem
                // estiver distraido ouve um murmurio na rua e perde o dia inteiro.
                Step(objective: string.Empty,
                     thought: "Someone is on the phone down in the street.",
                     ends: ChapterDefinition.Completion.Event, eventId: "heard_call",
                     raise: new[] { "climax" }),
            };

        /// <summary>
        /// Noite — "A estrada de regresso". **Fora da cadeia desde o corte de 11-08.**
        ///
        /// Continua aqui, e o `.asset` continua no repositorio, porque desligar nao e
        /// apagar: se a versao do apartamento nao resultar, o material esta pronto a
        /// voltar. O que deixou de existir e a chamada a este metodo na lista de
        /// capitulos — e com ela o `road_resolved`, que era quem abria o Dia 5.
        /// </summary>
        private static ChapterDefinition NightRoad() => Build("CH_Night_Road", "Night - The road back",
            startsOn: "night_road", date: "THURSDAY", time: "22:41",
            steps: new[]
            {
                Step(objective: "OBJECTIVE: Drive home.",
                     thought: "There has been the same pair of lights behind me for six miles.",
                     ends: ChapterDefinition.Completion.Event, eventId: "road_followed"),

                Step(objective: string.Empty,
                     thought: "The car stopped. Of course it did.",
                     ends: ChapterDefinition.Completion.Event, eventId: "car_stopped"),

                Step(objective: "OBJECTIVE: Decide what to do.",
                     thought: "Someone is walking up the road towards me.",
                     ends: ChapterDefinition.Completion.Event, eventId: "road_resolved",
                     raise: new[] { "climax" }),
            });

        /// <summary>Dia 5 / Climax — "Porta 3B".</summary>
        private static ChapterDefinition Climax() => Build("CH_Day5_Door3B", "Day 5 - Door 3B",
            startsOn: "climax", date: "FRIDAY", time: "01:56",
            steps: new[]
            {
                Step(objective: "OBJECTIVE: Get back inside.",
                     thought: "The front door of the building was already open.",
                     ends: ChapterDefinition.Completion.Event, eventId: "back_inside"),

                Step(objective: "OBJECTIVE: Find out what he has.",
                     thought: "His door has never been locked before.",
                     ends: ChapterDefinition.Completion.Event, eventId: "rui_room_opened"),

                Step(objective: string.Empty,
                     thought: "He knew about the class. He knew about the car. He knew the road.",
                     ends: ChapterDefinition.Completion.Timer, seconds: 5f,
                     raise: new[] { "confrontation" }),

                Step(objective: "OBJECTIVE: Face him.",
                     ends: ChapterDefinition.Completion.Event, eventId: "confrontation_resolved",
                     raise: new[] { "epilogue" }),
            });

        private static ChapterDefinition Epilogue() => Build("CH_Epilogue", "Epilogue",
            startsOn: "epilogue", date: "THREE DAYS LATER", time: "",
            steps: new[]
            {
                Step(objective: string.Empty,
                     thought: "Nobody asked me anything for three days.",
                     ends: ChapterDefinition.Completion.Timer, seconds: 6f),

                Step(objective: string.Empty,
                     thought: "I still park in the same spot every night.",
                     ends: ChapterDefinition.Completion.Timer, seconds: 6f,
                     raise: new[] { "game_end" }),
            });

        // ------------------------------------------------------------------

        private static ChapterDefinition.Step Step(
            string objective = "", string side = "", string thought = "",
            ChapterDefinition.Completion ends = ChapterDefinition.Completion.Event,
            float seconds = 5f, Vector3 place = default, float radius = 2f,
            string eventId = "", string[] raise = null)
        {
            return new ChapterDefinition.Step
            {
                Objective = objective,
                SideObjective = side,
                Thought = thought,
                Ends = ends,
                Seconds = seconds,
                Place = place,
                Radius = radius,
                EventId = eventId,
                RaiseOnComplete = raise ?? System.Array.Empty<string>(),
            };
        }

        /// <summary>
        /// Reescreve o Dia 4 a partir do <see cref="DayFour"/>, por cima do que la
        /// estiver. **Explicito de proposito.**
        ///
        /// O <see cref="Build"/> nunca toca num capitulo que ja exista, e isso e uma
        /// decisao certa: o texto dos capitulos e escrito a mao no Inspector depois de
        /// gerado, e uma ferramenta que o reescrevesse a cada execucao apagava o
        /// trabalho de alguem sem avisar. O preco dessa decisao e a divergencia — o
        /// `.asset` do Dia 4 tinha cinco passos e o codigo descrevia quatro, e ninguem
        /// deu por isso durante semanas.
        ///
        /// O corte da garagem obriga a uma reescrita **estrutural**, nao de texto. Fica
        /// aqui, numa entrada separada que so corre quando alguem a manda correr, e que
        /// **imprime o que substituiu** antes de substituir: assim o que estava escrito
        /// nao se perde sem deixar rasto.
        /// </summary>
        [MenuItem("Pungent/Blockout/Reescrever Dia 4 (corte da garagem)", false, 37)]
        private static void RewriteDayFour()
        {
            string path = $"{Folder}/CH_Day4_Garage.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(path);
            if (existing == null)
            {
                Debug.LogError("[Skeleton] CH_Day4_Garage nao existe. Corre o 'Wire Story Skeleton' primeiro.");
                return;
            }

            var before = new List<string> { $"  titulo: {existing.Title}   arranca em '{existing.StartsOnEvent}'" };
            for (int i = 0; i < existing.StepCount; i++)
            {
                var step = existing.StepAt(i);
                if (step == null) continue;
                before.Add($"  [{i}] \"{step.Objective}\"  ->  '{step.EventId}'"
                         + (step.RaiseOnComplete != null && step.RaiseOnComplete.Length > 0
                            ? "  levanta: " + string.Join(", ", step.RaiseOnComplete) : string.Empty));
            }

            var replacement = DayFourSteps();
            existing.EditorPopulate("Day 4 - The box", "day_four", replacement, "THURSDAY", "22:41");
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();

            Debug.Log("[Skeleton] Dia 4 reescrito: a garagem saiu, a caixa entrou.\n\n"
                    + "O que estava la antes (para nao se perder):\n" + string.Join("\n", before)
                    + $"\n\nAgora: {replacement.Length} passos, acaba em 'heard_call' e levanta 'climax'.");
        }

        /// <summary>
        /// Capitulos que sairam da linha e nao voltam a entrar na lista do director.
        /// Os `.asset` ficam no disco: desligar nao e apagar.
        /// </summary>
        private static readonly string[] Retired =
        {
            "CH_Night_Road",       // saiu com a estrada
            "CH_Epilogue",         // marcador de posicao; disputava o `epilogue` com o EndingSelector
            "CH_Epilogue_Proof",   // fundido no CH_Epilogue_Out
            "CH_Epilogue_NoProof", // idem
            "CH_Epilogue_Trust",   // o final da falsa confianca saiu com a estrada
        };

        /// <summary>
        /// Um capitulo que outra ferramenta escreveu, so para o registar aqui. Devolve
        /// nulo em silencio se ainda nao existir — quem o cria e o `BlackboardWiring`,
        /// e a ordem entre as duas ferramentas nao esta garantida.
        /// </summary>
        private static ChapterDefinition Chapter(string fileName) =>
            AssetDatabase.LoadAssetAtPath<ChapterDefinition>($"{Folder}/{fileName}.asset");

        private static ChapterDefinition Build(string fileName, string title, string startsOn,
            ChapterDefinition.Step[] steps, string date = "", string time = "")
        {
            string path = $"{Folder}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<ChapterDefinition>();
            asset.EditorPopulate(title, startsOn, steps, date, time);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Pungent/Narrative"))
                AssetDatabase.CreateFolder("Assets/Pungent", "Narrative");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Pungent/Narrative", "Chapters");
        }
    }
}
