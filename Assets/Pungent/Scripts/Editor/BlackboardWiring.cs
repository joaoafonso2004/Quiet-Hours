using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.Narrative;
using V = Pungent.Narrative.NarrativeBlackboard.Variable;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Liga as variaveis ocultas (§6.2) e os quatro finais (§6.4).
    ///
    /// Nao existia nada disto. O unico estado acumulado era o `ThreatLevel` do Rui,
    /// que vive nele e morre com a cena — util para o comportamento dele, inutil
    /// para um final. Sem um sitio onde guardar se a prova foi enviada ou se a ajuda
    /// da estrada foi aceite, os quatro desfechos do §6.4 eram impossiveis de
    /// escrever, e as escolhas do jogo nao acumulavam para lado nenhum.
    ///
    /// Tudo isto vive no `GAME_SYSTEMS`, com o resto do que atravessa cenas. Um
    /// registo de escolhas que morresse ao entrar na garagem nao registava nada.
    ///
    /// Escreve tambem os quatro epilogos e o `ChapterDefinition` do Dia 2 — este
    /// ultimo por uma razao pequena e real: a noite das 02:47 corria pelo
    /// `DayTwoDirector`, fora do sistema de capitulos, e por isso era a unica que
    /// nao tinha cartao com a hora. E a hora e que faz o trabalho.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class BlackboardWiring
    {
        private const string SystemsPrefab = "Assets/Pungent/Prefabs/GAME_SYSTEMS.prefab";
        private const string ChapterFolder = "Assets/Pungent/Narrative/Chapters/";

        [MenuItem("Pungent/Blockout/Wire Hidden Variables + Endings", false, 19)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireBlackboard")) return;

            var chapters = new List<ChapterDefinition>
            {
                WriteDayTwo(),
                // **Tres finais, e nao cinco** (decisao de 11-08, com o corte da
                // garagem e da estrada).
                //
                // O `ending_proof` e o `ending_no_proof` eram o mesmo homem a descer as
                // mesmas escadas, e num jogo que acontece todo dentro de uma casa a
                // diferenca entre eles cabe num plano. Fundiram-se aqui. Estas tres
                // linhas tem de funcionar **com prova e sem ela** — a diferenca vive no
                // `ending_out_proof`, que o `EndingSelector` levanta ao lado e que um
                // `EpilogueTableau` pode usar para trocar a imagem.
                //
                // O `ending_trust` saiu com a estrada: dependia do `TrustStranger`, e
                // quem o alimentava era o `StrangerEncounter`, que vivia no
                // `Night_Road`. A **regra do blackboard fica** — desligar nao e apagar,
                // e se a estrada voltar o final volta com ela.
                //
                // `CH_Epilogue_Proof`, `CH_Epilogue_NoProof` e `CH_Epilogue_Trust`
                // continuam no disco e deixam de ser escritos aqui.
                WriteEnding("CH_Epilogue_Out", "Epilogue - The statement", "ending_out",
                    "THREE DAYS LATER",
                    "I gave a statement in a room with a plant in it.",
                    "They asked me twice whether I was sure the torch was mine. I said the tape was mine.",
                    "I still check the street before I come up. I lock the passenger door now."),

                WriteEnding("CH_Epilogue_Taken", "Epilogue - The lease", "ending_taken",
                    "SOME TIME LATER",
                    "He told them I went home to my father's. He had my keys, so nobody asked twice.",
                    "The lease is in his name now. It always was.",
                    "The street door downstairs still does not lock."),
            };

            var root = PrefabUtility.LoadPrefabContents(SystemsPrefab);
            if (root == null)
            {
                Debug.LogError("[Blackboard] Sem " + SystemsPrefab + ".");
                return;
            }

            BuildBlackboard(root);
            BuildSelector(root);
            AddChapters(root, chapters);

            PrefabUtility.SaveAsPrefabAsset(root, SystemsPrefab);
            PrefabUtility.UnloadPrefabContents(root);

            string evidence = AppendEvidenceMessage();

            AssetDatabase.SaveAssets();

            // Os numeros sao interpolados e nao escritos a mao: esta linha ja disse
            // "quatro finais" durante uma execucao em que escrevia dois, porque o
            // texto ficou para tras quando a lista mudou. Uma mensagem de consola que
            // reafirma um valor a mao e uma copia a espera de mentir.
            int endings = chapters.Count - 1;   // menos o Dia 2, que nao e um final
            Debug.Log($"[Blackboard] Variaveis do §6.2 ligadas, {endings} epilogo(s) escrito(s) "
                    + "aqui (mais o do Pai, que vive no `FatherEndingWiring`), e o Dia 2 passou "
                    + $"a ter capitulo.\n  {evidence}");
        }

        /// <summary>
        /// O gesto que faltava: mandar a fotografia ao Pai.
        ///
        /// Nao havia no jogo maneira nenhuma de tirar a prova de casa. O
        /// `EvidenceShared` ficava a zero em todas as jogadas possiveis, e o melhor
        /// dos quatro finais do §6.4 — *"sobrevive com prova"* — estava escrito,
        /// tinha epilogo, e nao podia acontecer.
        ///
        /// **Escrito aqui e nao no `GarageWiring`.** A mensagem e do Dia 4 e a
        /// tentacao era po-la com o resto da oficina, mas a ferramenta da oficina so
        /// corre com a cena da garagem aberta — e isto nao mexe em cena nenhuma,
        /// mexe num asset. Pior: a ordem passava a depender de qual das duas alguem
        /// corresse primeiro. Aqui esta ao lado da linha da tabela que a le, que e o
        /// unico sitio onde as duas metades se explicam uma a outra.
        ///
        /// **Uma escolha so, no fim do fio do Pai, e sem ecra novo.** A tentacao era
        /// uma mecanica de camara; o que falta ao capitulo e uma decisao, e o
        /// telemovel ja e o sitio onde o Tomas decide coisas. O pai pergunta uma
        /// banalidade — o domingo, a mae, o costume da noite toda — e e a resposta
        /// que carrega tudo.
        ///
        /// A replica de quem envia e o que torna a escolha legivel: o pai reconhece a
        /// lanterna sem que ninguem lha aponte, e devolve a pergunta a que o Tomas
        /// nao sabe responder. Quem enviou sabe exactamente o que fez, que e o que a
        /// §6.4 exige — *"nao criar finais baseados numa unica escolha obscura"*.
        ///
        /// **A ordem do fio e verificada e nao suposta.** O fio e linear: um passo
        /// preso ao `evidence_found` posto antes das mensagens de manha do Dia 3
        /// prendia-as ate ao Dia 4. Se as do `day_two` ainda la nao estiverem, esta
        /// ferramenta nao escreve nada e diz porque.
        /// </summary>
        private static string AppendEvidenceMessage()
        {
            var dad = PhoneThreadEditing.Load("PHN_Dad");
            if (dad == null) return "fio do Pai nao encontrado: correr 'Build Phone Threads'.";

            var steps = PhoneThreadEditing.ReadSteps(dad);

            // **A guarda pergunta pela escolha e nao pelo passo, e a diferenca custou
            // o melhor final do jogo.**
            //
            // Ate aqui bastava existir um passo preso ao `evidence_found` para esta
            // ferramenta se dar por satisfeita. O asset tinha esse passo com a linha
            // certa e **zero escolhas** — meio escrito, por uma passagem antiga ou por
            // alguem lhe ter passado por cima. A guarda via a espera, dizia "ja estava"
            // e saia. O passo defeituoso era pegajoso: correr a ferramenta outra vez
            // nunca o podia reparar.
            //
            // E o `evidence_shared` sai **daquela** escolha e de mais nenhuma no jogo
            // inteiro, e e a unica condicao do `ending_proof`. Resultado: o melhor
            // desfecho era inalcancavel e nada na consola dizia isso.
            //
            // Agora pergunta-se pelo que interessa — ha alguma escolha que levante o
            // acontecimento? — e um passo incompleto e **substituido** em vez de
            // duplicado.
            int existing = -1;
            bool hasMorning = false;
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null) continue;
                if (step.WaitForEvent == "evidence_found") existing = i;
                if (step.WaitForEvent == "day_two") hasMorning = true;
            }

            if (existing >= 0 && RaisesEvidenceShared(steps[existing]))
                return "a fotografia ja estava no fio do Pai.";

            if (!hasMorning)
                return "SALTADO: correr 'Wire Day Two' primeiro. As mensagens de manha do Pai "
                     + "ainda nao estao no fio, e por-lhe a fotografia agora deixava-as presas "
                     + "atras de um acontecimento do Dia 4.";

            var evidenceStep = BuildEvidenceStep();

            if (existing >= 0)
            {
                steps[existing] = evidenceStep;
                PhoneThreadEditing.Write(dad, dad.ContactName, steps);
                return "REPARADO: o passo da fotografia estava sem escolhas e o "
                     + "`evidence_shared` nao tinha quem o levantasse — o `ending_proof` "
                     + "era inalcancavel. Reescrito.";
            }

            // O fio acabava aqui. Deixa de acabar: ele volta a escrever a noite.
            if (steps.Count > 0 && steps[steps.Count - 1] != null)
                steps[steps.Count - 1].EndsThread = false;

            steps.Add(evidenceStep);

            PhoneThreadEditing.Write(dad, dad.ContactName, steps);
            return "a fotografia entrou no fio do Pai: quem a enviar levanta `evidence_shared`.";
        }

        /// <summary>
        /// Ha aqui uma escolha que levante mesmo o `evidence_shared`?
        ///
        /// E esta a pergunta que a guarda tem de fazer. "Existe um passo" nao chega:
        /// um passo sem escolhas espera por um acontecimento, mostra uma linha, e nao
        /// deixa o jogador fazer nada — que e indistinguivel de nao existir, excepto
        /// para a guarda.
        /// </summary>
        private static bool RaisesEvidenceShared(PhoneConversationDefinition.Step step)
        {
            if (step?.Choices == null) return false;
            foreach (var choice in step.Choices)
                if (choice != null && choice.RaisesEvent == "evidence_shared") return true;
            return false;
        }

        private static PhoneConversationDefinition.Step BuildEvidenceStep()
        {
            return new PhoneConversationDefinition.Step
            {
                // Banal, e a abrir a porta sem lhe tocar. Ele nao pergunta pela
                // oficina: pergunta pelo domingo, como perguntou a noite toda.
                Line = "Did you get them in the end? Your mother is still asking about Sunday.",
                WaitForEvent = "evidence_found",

                // Cai a caminho de casa e nao no patio. Os 75 s de silencio sao de
                // proposito: com o telemovel a apitar dentro da oficina, a escolha
                // era feita com o vendedor a olhar, e o peso dela vem de ser tomada
                // sozinho, ja com as pecas na mala.
                DelaySeconds = 75f,
                TypingSeconds = 5f,
                ReplyDelaySeconds = 50f,
                EndsThread = true,
                Choices = new[]
                {
                    new DialogueChoice
                    {
                        Text = "Got them. I took a picture of something there, sending it now. "
                             + "Tell me if anything in it looks familiar.",

                        // Ele reconhece a lanterna sozinho, e a pergunta fica sem
                        // resposta. Nada de dramatico: um pai a olhar para uma
                        // fotografia e a reparar numa coisa que conhece.
                        Reply = "That is your torch. The tape on the grip is your tape. "
                              + "Why is it in a yard on the other side of town?",
                        Tone = DialogueTone.Honest,

                        // A unica linha do jogo inteiro que alimenta o `EvidenceShared`.
                        RaisesEvent = "evidence_shared"
                    },
                    new DialogueChoice
                    {
                        Text = "Got them. Nothing else to say about it.",
                        Reply = "Alright. Drive back before it gets any later.",
                        Tone = DialogueTone.Evasive
                    }
                }
            };
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// A tabela: que acontecimento mexe em que variavel.
        ///
        /// **So entram acontecimentos que ja existem no jogo.** A tentacao aqui e
        /// escrever a tabela inteira do §6.2 e ficar com metade das linhas a apontar
        /// para coisas que ninguem levanta — que e exactamente a armadilha que este
        /// projecto ja pagou seis vezes, so que desta vez calada: a variavel ficava a
        /// zero para sempre e o final correspondente nunca acontecia.
        ///
        /// Duas estiveram por alimentar durante muito tempo, e com elas metade dos
        /// finais escritos era decorativa. **Ja nao estao**, e como cada uma passou a
        /// ter dono importa mais do que a linha que a liga:
        ///
        /// - `EvidenceShared` — nao havia no jogo gesto nenhum de tirar a prova de
        ///   casa. Ha agora um passo no fim do fio do Pai, preso ao `evidence_found`,
        ///   com duas respostas: mandar a fotografia ou nao. Quem a manda levanta o
        ///   `evidence_shared` pelo campo novo do `DialogueChoice`, porque o servico
        ///   de mensagens **consome** acontecimentos e ate aqui nunca levantou
        ///   nenhum. Escrito pelo `GarageWiring`.
        /// - `TrustStranger` — o `road_resolved` fecha o encontro da estrada, mas e
        ///   levantado tanto por quem aceita ajuda como por quem foge, e alimentar a
        ///   variavel com ele era mentir. O `StrangerEncounter` passou a decidir
        ///   entre `stranger_helped` e `stranger_refused` pelo corpo do jogador —
        ///   ficar ali ou entrar no carro — e continua sem botao nenhum.
        ///
        /// O `stranger_refused` nao tem linha nesta tabela, e nao e esquecimento:
        /// fugir de um estranho as duas da manha nao e uma decisao que deva acumular
        /// para lado nenhum. Existe para ter dono e para o dia em que alguma coisa o
        /// queira ler.
        ///
        /// Vale mais estar dito aqui do que descoberto por acaso daqui a tres meses.
        /// </summary>
        private static NarrativeBlackboard.Rule[] BuildRules()
        {
            var rules = new List<NarrativeBlackboard.Rule>();

            void R(string id, V target, int amount = 1, bool once = true) =>
                rules.Add(new NarrativeBlackboard.Rule
                {
                    EventId = id, Target = target, Amount = amount, Once = once
                });

            // A prova. A lanterna dele no monte das pecas, com a fita azul no punho.
            R("evidence_found", V.EvidenceFound);

            // E a prova fora de casa. Nao e o mesmo que encontra-la: o Tomas passa o
            // Dia 4 a decidir se conta a alguem, e esta e a linha que faz a decisao
            // dele valer alguma coisa no fim.
            R("evidence_shared", V.EvidenceShared);

            // Ficou na berma a ver um desconhecido carregar-lhe as pecas, e disse-lhe
            // onde mora. Quem entra no carro levanta `stranger_refused`, que nao tem
            // linha nenhuma — ver a nota da tabela.
            R("stranger_helped", V.TrustStranger);

            // Cada captura do climax conta, e nao so a primeira: o `caught` e
            // levantado a cada uma, e por isso e a unica linha que nao e `once`.
            R("caught", V.RuiAggression, 1, once: false);

            // A partir do momento em que ele fala pela porta ja nao ha duvida
            // nenhuma de parte a parte.
            R("rui_awake", V.RuiSuspicion, 2);

            // Entrar no quarto dele. O `NpcRoomTerritory` ja o mandava sair; agora
            // tambem conta.
            R("rui_room_entered", V.RuiSuspicion, 1, once: false);

            // A casa preparada. Nao ha acontecimento chamado "tranquei a porta", mas
            // ha um facto equivalente e com dono: a partir do Dia 2 o `PlayerSleep`
            // **nao deixa** dormir com a porta destrancada. Acordar no dia seguinte
            // e, portanto, prova de que ela ficou trancada.
            //
            // **Nesse "nao deixa" esta o defeito, e fica dito.** Se nao ha maneira de
            // dormir sem trancar, entao acordar nao distingue jogador nenhum: estas
            // duas linhas sobem sempre, em todas as jogadas. O `EndingSelector` lia
            // isto e exigia `DoorsSecured <= 0` para o final de falsa confianca —
            // uma condicao que nao podia ser verdadeira, e um final que nao podia
            // acontecer. Deixou de ler; as linhas ficam porque o facto e verdadeiro
            // e porque a variavel volta a servir no dia em que houver mesmo uma
            // maneira de ir dormir com a casa aberta.
            R("day_two", V.DoorsSecured);
            R("day3_ready", V.DoorsSecured);

            // O telemovel. Variavel narrativa simples (§6.2), nao um recurso.
            R("night_road", V.PhoneBattery, -1);
            R("climax", V.PhoneBattery, -1);

            return rules.ToArray();
        }

        private static void BuildBlackboard(GameObject root)
        {
            var host = Child(root, "NARRATIVE_STATE");
            var board = host.GetComponent<NarrativeBlackboard>();
            if (board == null) board = host.AddComponent<NarrativeBlackboard>();

            board.EditorConfigure(BuildRules());
            EditorUtility.SetDirty(board);
        }

        private static void BuildSelector(GameObject root)
        {
            var host = Child(root, "ENDING");
            var selector = host.GetComponent<EndingSelector>();
            if (selector == null) selector = host.AddComponent<EndingSelector>();

            selector.EditorConfigure("ending_out", "ending_out_proof", "ending_taken");
            EditorUtility.SetDirty(selector);
        }

        private static GameObject Child(GameObject root, string name)
        {
            var found = root.transform.Find(name);
            if (found != null) return found.gameObject;

            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// A noite das 02:47 passa a ter capitulo.
        ///
        /// Continua a ser o `DayTwoDirector` e o `OpeningQuestDirector` a fazer o
        /// trabalho — reescreve-los agora era gastar tempo a nao andar para a frente,
        /// que e o corolario 1 do §0.1. O que este asset traz e o que faltava mesmo:
        /// o cartao com a hora. Num jogo em que o apartamento e sempre o mesmo, o
        /// cartao e a unica coisa que diz ao jogador que passou uma noite — e "02:47"
        /// a terceira vez ja nao precisa de explicacao.
        /// </summary>
        private static ChapterDefinition WriteDayTwo()
        {
            return WriteChapter("CH_Day2_0247", "Day 2 - 02:47", "day_one_done",
                "TUESDAY", "02:47", new[]
                {
                    // **Um passo so, e sem objectivo escrito.** A tentacao era
                    // escrever "reiniciar o router" e "trancar a porta" como passos,
                    // mas ninguem levanta esses acontecimentos: quem conduz esta
                    // noite e o `OpeningQuestDirector`, com objectivos proprios. Dois
                    // passos presos a eventos sem dono deixavam o capitulo parado
                    // para sempre — e nao dava erro nenhum.
                    //
                    // O que este capitulo traz e o cartao. Fecha-se quando a manha
                    // chegar, e a manha tem dono: o `DayTwoDirector`.
                    new ChapterDefinition.Step
                    {
                        Objective = string.Empty,
                        Thought = "The upload stopped. Of course it did.",
                        Ends = ChapterDefinition.Completion.Event,
                        EventId = "day_two"
                    }
                });
        }

        private static ChapterDefinition WriteEnding(string file, string title,
            string startsOn, string card, params string[] thoughts)
        {
            var steps = new ChapterDefinition.Step[thoughts.Length];
            for (int i = 0; i < thoughts.Length; i++)
                steps[i] = new ChapterDefinition.Step
                {
                    Objective = string.Empty,
                    Thought = thoughts[i],
                    Ends = ChapterDefinition.Completion.Timer,
                    Seconds = 6.5f,
                    // A ultima fecha o jogo. A imagem quotidiana com desconforto que
                    // o §5 pede ja esta na propria linha: ninguem grita nada.
                    RaiseOnComplete = i == thoughts.Length - 1
                        ? new[] { "game_end" } : new string[0]
                };

            return WriteChapter(file, title, startsOn, card, string.Empty, steps);
        }

        private static ChapterDefinition WriteChapter(string file, string title,
            string startsOn, string cardDate, string cardTime, ChapterDefinition.Step[] steps)
        {
            string path = ChapterFolder + file + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ChapterDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.EditorPopulate(title, startsOn, steps, cardDate, cardTime);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// Mete os capitulos novos na lista, sem duplicar.
        ///
        /// No fim e nao no principio: o `ChapterDirector` fica no primeiro que
        /// estiver pronto, e nenhum destes arranca sem o seu acontecimento. O
        /// `CH_Epilogue` antigo fica onde esta — arranca com `epilogue`, que os
        /// quatro novos nao usam, e por isso continua a servir de rede se o selector
        /// nao decidir nada.
        /// </summary>
        private static void AddChapters(GameObject root, List<ChapterDefinition> chapters)
        {
            var director = root.GetComponentInChildren<ChapterDirector>(true);
            if (director == null)
            {
                Debug.LogWarning("[Blackboard] Sem ChapterDirector no GAME_SYSTEMS.");
                return;
            }

            var so = new SerializedObject(director);
            var array = so.FindProperty("chapters");

            foreach (var chapter in chapters)
            {
                if (chapter == null) continue;

                bool already = false;
                for (int i = 0; i < array.arraySize; i++)
                    if (array.GetArrayElementAtIndex(i).objectReferenceValue == chapter)
                        already = true;
                if (already) continue;

                array.arraySize++;
                array.GetArrayElementAtIndex(array.arraySize - 1).objectReferenceValue = chapter;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
