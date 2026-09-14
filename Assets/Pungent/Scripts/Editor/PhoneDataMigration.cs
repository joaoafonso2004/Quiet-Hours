using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.NPC;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Cria os fios do telemovel e liga o `PhoneMessageService` a cena.
    ///
    /// O fio do Rui e migrado a partir dos cinco campos antigos do
    /// `PrototypePhoneUI`, lidos da cena e nao de defaults. O fio do Pai e um
    /// primeiro jacto para ser reescrito: o asset existe para o texto poder ser
    /// editado sem tocar em codigo nem sujar a cena.
    ///
    /// Re-executavel: nao recria assets que ja existam.
    /// </summary>
    public static class PhoneDataMigration
    {
        private const string Folder = "Assets/Pungent/Dialogue/Phone";
        private const string RuiPath = Folder + "/PHN_Rui_Router.asset";
        private const string DadPath = Folder + "/PHN_Dad.asset";
        private const string SellerPath = Folder + "/PHN_Seller.asset";
        private const string MarketPath = Folder + "/PHN_Market.asset";

        [MenuItem("Pungent/Dialogue/Build Phone Threads", false, 11)]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/Pungent/Dialogue", "Phone");

            var phone = Object.FindObjectOfType<PrototypePhoneUI>(true);
            if (phone == null) { Debug.LogError("[PhoneMigration] PrototypePhoneUI nao encontrado."); return; }

            var rui = BuildRuiThread(phone);
            var dad = BuildDadThread();
            var seller = BuildSellerThread();
            PhoneThreadEditing.SetUnlock(rui, "prologue_talked");
            PhoneThreadEditing.SetUnlock(seller, "day1_deal");

            // O servico vivia no mesmo GameObject do telemovel. Deixou de viver: as
            // mensagens atravessam cenas com o resto dos sistemas e mudaram-se para
            // o `GAME_SYSTEMS` quando o `PlayerRoot` se partiu em dois.
            //
            // Procurar em toda a cena e nao so no telemovel. Enquanto isto foi
            // `phone.GetComponent`, cada corrida desta ferramenta criava um servico
            // novo em cima do jogador — dois servicos a correr os mesmos fios, com o
            // telemovel a ouvir o errado e as conversas novas a irem parar ao que
            // morre na troca de cena.
            var service = Object.FindObjectOfType<PhoneMessageService>(true);
            if (service == null)
            {
                Debug.LogWarning("[PhoneMigration] Sem `PhoneMessageService` na cena. " +
                                 "Criado no telemovel; devia estar no GAME_SYSTEMS.");
                service = Undo.AddComponent<PhoneMessageService>(phone.gameObject);
            }

            var serviceSo = new SerializedObject(service);
            var list = serviceSo.FindProperty("conversations");
            PreserveChapterThreads(list, rui, dad, seller);
            serviceSo.FindProperty("ruiRoutine").objectReferenceValue =
                Object.FindObjectOfType<PrototypeNpcRoutine>(true);
            serviceSo.ApplyModifiedProperties();

            var phoneSo = new SerializedObject(phone);
            phoneSo.FindProperty("messages").objectReferenceValue = service;
            phoneSo.ApplyModifiedProperties();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[PhoneMigration] Fios ligados: {rui.ContactName} ({rui.StepCount} passos), " +
                      $"{dad.ContactName} ({dad.StepCount} passos), " +
                      $"{seller.ContactName} ({seller.StepCount} passos).");
        }

        /// <summary>
        /// Garante os tres fios base sem apagar os que um capitulo acrescentou.
        ///
        /// A versao antiga fazia `arraySize = 3`. Correr esta ferramenta depois de
        /// `Wire Day 1` retirava silenciosamente o `PHN_Market`: o prefab continuava
        /// correcto, mas a instancia da cena ganhava um override sem o anuncio, e o
        /// Dia 1 ficava preso a espera de uma conversa que nao podia arrancar.
        /// </summary>
        private static void PreserveChapterThreads(SerializedProperty list,
            params PhoneConversationDefinition[] required)
        {
            var definitions = new List<PhoneConversationDefinition>();
            foreach (var thread in required)
                if (thread != null && !definitions.Contains(thread)) definitions.Add(thread);

            for (int i = 0; i < list.arraySize; i++)
            {
                var thread = list.GetArrayElementAtIndex(i).objectReferenceValue
                    as PhoneConversationDefinition;
                if (thread != null && !definitions.Contains(thread)) definitions.Add(thread);
            }

            // O anuncio e criado pelo Dia 1 e pode nao estar ainda na lista desta
            // instancia, precisamente por causa do override antigo. Se o asset ja
            // existe, recupera-o mesmo assim.
            var market = AssetDatabase.LoadAssetAtPath<PhoneConversationDefinition>(MarketPath);
            if (market != null && !definitions.Contains(market)) definitions.Add(market);

            list.arraySize = definitions.Count;
            for (int i = 0; i < definitions.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
        }

        /// <summary>
        /// A conversa que ja existia, tirada dos cinco campos do componente.
        /// O texto vem da cena para nao se perder nenhuma edicao feita no inspector.
        /// </summary>
        private static PhoneConversationDefinition BuildRuiThread(PrototypePhoneUI phone)
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhoneConversationDefinition>(RuiPath);
            if (existing != null) return existing;

            var so = new SerializedObject(phone);
            string Read(string field) => so.FindProperty(field).stringValue;

            var opening = new PhoneConversationDefinition.Step
            {
                Line = Read("incomingMessage"),
                DelaySeconds = so.FindProperty("initialMessageDelay").floatValue,
                ReplyDelaySeconds = so.FindProperty("replyDelay").floatValue,
                Choices = new[]
                {
                    new DialogueChoice
                    {
                        Text = Read("neutralResponse"),
                        Reply = Read("ruiFollowUp"),
                        Tone = DialogueTone.Calm
                    },
                    new DialogueChoice
                    {
                        Text = Read("defiantResponse"),
                        Reply = Read("ruiFollowUpDefiant"),
                        Tone = DialogueTone.Edgy
                    }
                }
            };

            var asset = ScriptableObject.CreateInstance<PhoneConversationDefinition>();
            asset.EditorPopulate(Read("contactName"), new[] { opening });
            AssetDatabase.CreateAsset(asset, RuiPath);
            return asset;
        }

        /// <summary>
        /// Primeiro jacto do fio do Pai. Arco: abre no carro, deriva para o pessoal.
        ///
        /// O detalhe que o Rui vai repetir e a **aula de terca de manha**, escolhido
        /// por ser banal. O Rui ja diz "Get some sleep. You have got that early class."
        /// no fim da conversa guionada da cozinha — se o jogador souber disso pelo
        /// Pai, aquela linha deixa de ser simpatia e passa a ser um homem a citar
        /// o horario dele.
        /// </summary>
        private static PhoneConversationDefinition BuildDadThread()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhoneConversationDefinition>(DadPath);
            if (existing != null) { GateDadOpening(existing); return existing; }

            var steps = new List<PhoneConversationDefinition.Step>
            {
                // --- o carro: assunto seguro, ancorado no texto de abertura ---
                new PhoneConversationDefinition.Step
                {
                    Line = "Still awake? Saw you were online.",
                    DelaySeconds = 22f,
                    ReplyDelaySeconds = 40f,
                    Choices = new[]
                    {
                        new DialogueChoice { Text = "Internet went down. Sorting it.",
                            Reply = "At this hour. Alright.", Tone = DialogueTone.Calm },
                        new DialogueChoice { Text = "Why are you awake?",
                            Reply = "I am old. You do not get to ask me that yet.", Tone = DialogueTone.Edgy }
                    }
                },
                new PhoneConversationDefinition.Step
                {
                    Line = "Did you ever sort out that noise in the car?",
                    DelaySeconds = 35f,
                    ReplyDelaySeconds = 55f,
                    Choices = new[]
                    {
                        new DialogueChoice { Text = "Waiting on parts. They are expensive.",
                            Reply = "Do not buy the cheap ones. I mean it. You always buy the cheap ones.",
                            Tone = DialogueTone.Honest },
                        new DialogueChoice { Text = "It is fine. Stop worrying about it.",
                            Reply = "I will stop worrying when it stops making the noise.",
                            Tone = DialogueTone.Edgy }
                    }
                },

                // --- deriva para o pessoal ---
                new PhoneConversationDefinition.Step
                {
                    Line = "Your mother froze some soup for you. It has your name taped on it.",
                    WaitForEvent = "router_restarted",
                    DelaySeconds = 90f,
                    ReplyDelaySeconds = 70f,
                    Choices = new[]
                    {
                        new DialogueChoice { Text = "I will come by Sunday.",
                            Reply = "She will act surprised. Let her.", Tone = DialogueTone.Calm },
                        new DialogueChoice { Text = "I have food here.",
                            Reply = "That was not the question.", Tone = DialogueTone.Evasive }
                    }
                },
                new PhoneConversationDefinition.Step
                {
                    Line = "Are you eating? Properly, not that thing you call eating.",
                    WaitForEvent = "talked_to_rui",
                    DelaySeconds = 60f,
                    ReplyDelaySeconds = 65f,
                    Choices = new[]
                    {
                        new DialogueChoice { Text = "I am eating.",
                            Reply = "Alright.", Tone = DialogueTone.Evasive },
                        new DialogueChoice { Text = "Not really. Been busy.",
                            Reply = "Busy. You are twenty and you are busy.", Tone = DialogueTone.Honest }
                    }
                },

                // --- o detalhe banal que o Rui vai repetir ---
                new PhoneConversationDefinition.Step
                {
                    Line = "Go to bed. You have that early one on Tuesday and you paid for it yourself.",
                    WaitForEvent = "talked_to_rui",
                    DelaySeconds = 150f,
                    ReplyDelaySeconds = 80f,
                    Choices = new[]
                    {
                        new DialogueChoice { Text = "I know. Goodnight.",
                            Reply = "Goodnight. Lock the door.", Tone = DialogueTone.Calm },
                        new DialogueChoice { Text = "You remember my timetable better than I do.",
                            Reply = "Someone has to.", Tone = DialogueTone.Honest }
                    }
                },

                // A pergunta sobre o Rui nao fica aqui: vive no fim do fio, ja de
                // manha, quando o jogador deu a volta a casa e nao encontrou
                // ninguem. E acrescentada pelo DayTwoWiring.
                //
                // Prender um passo a um evento que ninguem dispara bloqueia todos
                // os seguintes, porque o fio e linear. Ficou aqui registado porque
                // foi exactamente o que aconteceu na primeira versao.
            };

            var asset = ScriptableObject.CreateInstance<PhoneConversationDefinition>();
            asset.EditorPopulate("Dad", steps.ToArray());
            AssetDatabase.CreateAsset(asset, DadPath);
            GateDadOpening(asset);
            return asset;
        }

        /// <summary>
        /// O Pai ja estava gravado, mas a conversa dele nao pertence ao primeiro
        /// minuto do prologo: a primeira frase diz que viu o Tomas online e a
        /// resposta fala da internet ter caido. Isso so e verdade na noite das
        /// 02:47, depois de o Dia 1 fechar.
        ///
        /// Corrige tambem assets que ja existam. A ferramenta de migracao e
        /// re-executavel, portanto devolver cedo deixava para sempre a versao antiga
        /// a falar durante a mudanca para o apartamento.
        /// </summary>
        private static void GateDadOpening(PhoneConversationDefinition thread)
        {
            var steps = PhoneThreadEditing.ReadSteps(thread);
            if (steps.Count == 0 || steps[0] == null) return;
            if (steps[0].WaitForEvent == "day_one_done") return;

            steps[0].WaitForEvent = "day_one_done";
            PhoneThreadEditing.Write(thread, thread.ContactName, steps);
        }

        /// <summary>
        /// O vendedor das pecas.
        ///
        /// Este fio existe porque sem ele o Dia 4 nao se percebe: o jogador acorda,
        /// o objectivo diz "conduz ate ao parque industrial" e nao ha em lado nenhum
        /// um motivo, uma morada ou uma pessoa. O Dia 1 — "Bom negocio", onde o
        /// negocio nascia — ainda nao existe, e o capitulo seguinte estava a ser
        /// jogado a contar com uma cena que ninguem viu.
        ///
        /// Alem do motivo, o fio paga duas dividas que ja estavam no jogo:
        ///
        /// - o pensamento do Dia 4 diz "ele disse para vir sozinho, que e uma coisa
        ///   que as pessoas dizem" — **ninguem tinha dito isso**. Agora disse;
        /// - o estranho, na estrada, sabe que o Tomas veio "do parque industrial, o
        ///   que fica depois do deposito de agua". Essa frase passa a ter sido lida
        ///   pelo jogador, num SMS, horas antes. E a regra de escrita do projecto a
        ///   funcionar: o detalhe que se repete e banal, e por isso e que arrepia.
        ///
        /// O nome do contacto e "Vitor (parts)" de proposito. E como uma pessoa
        /// grava mesmo um desconhecido de um site de anuncios, e diz quem ele e e
        /// para que serve sem uma unica linha de exposicao.
        /// </summary>
        /// <summary>
        /// Reescreve o fio do vendedor por cima do que la estiver. **Explicito de
        /// proposito**, como o `Reescrever Dia 4`.
        ///
        /// O <see cref="BuildSellerThread"/> nunca toca num fio que ja exista — e bem,
        /// porque estes textos sao reescritos a mao. O preco e a divergencia: o corte
        /// de 11-08 tirou a oficina do jogo e o fio continuou a marcar hora la, dois
        /// dias depois de o sitio deixar de existir.
        ///
        /// Imprime o que substituiu antes de substituir.
        /// </summary>
        [MenuItem("Pungent/Dialogue/Reescrever fio do vendedor (corte da garagem)", false, 12)]
        private static void RewriteSellerThread()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhoneConversationDefinition>(SellerPath);
            if (existing == null)
            {
                Debug.LogError("[Telemovel] `PHN_Seller` nao existe. Corre 'Build Phone Threads' primeiro.");
                return;
            }

            var before = new List<string>();
            var so = new SerializedObject(existing);
            var steps = so.FindProperty("steps");
            for (int i = 0; i < steps.arraySize; i++)
                before.Add("  [" + i + "] " + steps.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("Line").stringValue);

            AssetDatabase.DeleteAsset(SellerPath);
            var rebuilt = BuildSellerThread();

            Debug.Log("[Telemovel] Fio do vendedor reescrito: deixa de marcar encontros fora de casa.\n\n"
                    + "O que estava la antes (para nao se perder):\n" + string.Join("\n", before)
                    + "\n\nAgora: " + (rebuilt != null ? rebuilt.StepCount : 0) + " passos.");
        }

        private static PhoneConversationDefinition BuildSellerThread()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PhoneConversationDefinition>(SellerPath);
            if (existing != null) return existing;

            var steps = new List<PhoneConversationDefinition.Step>
            {
                // Manha do Dia 3. Quem e, o que vende, e que o negocio ja estava
                // combinado antes de o jogador acordar.
                new PhoneConversationDefinition.Step
                {
                    Line = "Morning. Still want the alternator and the front discs?",
                    WaitForEvent = "day_two",
                    DelaySeconds = 26f,
                    ReplyDelaySeconds = 30f,
                    Choices = new[]
                    {
                        new DialogueChoice { Text = "Yes. Today still works?",
                            Reply = "Today works. Cash, like we said.", Tone = DialogueTone.Calm },
                        new DialogueChoice { Text = "Remind me what you want for them.",
                            Reply = "Ninety for both. That is half what the shop wanted, so do not push it.",
                            Tone = DialogueTone.Honest }
                    }
                },

                // **A entrega, e nao o encontro.**
                //
                // Isto dizia *"Unit 7, the industrial park past the water tower. Ten
                // o'clock. Come alone"* — era a direccao do Dia 4 quando o Dia 4 era
                // uma oficina. Com o corte de 11-08 o jogo passou a acontecer todo
                // dentro do apartamento, e uma conversa a marcar hora num sitio que
                // nao existe promete uma parte do jogo que nao ha.
                //
                // O que se ganha: ele deixa de ser um homem que o Tomas visita e passa
                // a ser um homem que **vem ate a porta dele**. A segunda escolha e a
                // peca que interessa — o Tomas nunca lhe deu a morada, e a resposta
                // dele nao explica nada. E o mesmo tipo de desconforto que o Rui ja da
                // com a porta do passageiro: banal, verificavel, e sem explicacao boa.
                new PhoneConversationDefinition.Step
                {
                    Line = "I am up your way this evening. I will leave them inside the "
                         + "street door, third floor landing.",
                    DelaySeconds = 30f,
                    ReplyDelaySeconds = 26f,
                    Choices = new[]
                    {
                        new DialogueChoice { Text = "Fine. I will be in.",
                            Reply = "You do not need to be. Cash under the mat is fine by me.",
                            Tone = DialogueTone.Calm },
                        new DialogueChoice { Text = "I never told you which floor.",
                            Reply = "Did you not? Someone did.", Tone = DialogueTone.Edgy }
                    }
                },

                // O "porque agora" do capitulo seguinte. Dizia *"I am there from ten
                // until about one"* — outra hora marcada noutro sitio. Passa a ser o
                // aviso de que as pecas ja estao a porta, que e o que abre o Dia 4.
                new PhoneConversationDefinition.Step
                {
                    Line = "Left it on your landing. Do not leave it out there all night, "
                         + "that building is not as locked as you think.",
                    WaitForEvent = "day3_ready",
                    DelaySeconds = 18f,
                    EndsThread = true
                }
            };

            var asset = ScriptableObject.CreateInstance<PhoneConversationDefinition>();
            asset.EditorPopulate("Vitor (parts)", steps.ToArray());
            AssetDatabase.CreateAsset(asset, SellerPath);
            return asset;
        }
    }
}
