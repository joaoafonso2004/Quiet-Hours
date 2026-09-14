using System;
using System.Collections.Generic;
using Pungent.Dialogue;
using Pungent.NPC;
using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>
    /// Guarda os fios de mensagens, entrega-os ao seu ritmo e avisa quem quiser saber.
    ///
    /// O `PrototypePhoneUI` deixa de conhecer o conteudo das conversas: passa a
    /// desenhar o que este servico ja decidiu. E isso que permite haver mais do que
    /// um contacto, e que uma mensagem do Pai caia a meio de o jogador estar a fazer
    /// outra coisa.
    ///
    /// Corresponde a `PhoneMessageService` da seccao 13.1.1 do plano.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhoneMessageService : MonoBehaviour, Pungent.Narrative.IRebindable
    {
        public enum ThreadState
        {
            /// <summary>A contar o atraso ate a proxima mensagem chegar.</summary>
            Waiting,
            /// <summary>A espera de um evento de historia para sequer comecar a contar.</summary>
            BlockedOnEvent,
            /// <summary>Ha respostas no ecra e o fio espera pelo jogador.</summary>
            AwaitingPlayer,
            /// <summary>O jogador respondeu; o contacto esta "a escrever".</summary>
            Typing,
            /// <summary>Nao ha mais passos.</summary>
            Done
        }

        public struct Entry
        {
            public string Text;
            /// <summary>Verdadeiro se foi o jogador que a enviou.</summary>
            public bool Mine;
        }

        public sealed class Thread
        {
            public PhoneConversationDefinition Definition;
            public readonly List<Entry> Entries = new List<Entry>();
            public int StepIndex;
            public ThreadState State;
            public float DeliverAt;
            public bool Unread;

            /// <summary>
            /// O contacto ja esta no telemovel. Enquanto for falso o fio nao existe
            /// para ninguem: nao aparece na lista, nao corre e nao apita.
            /// </summary>
            public bool Unlocked;

            /// <summary>Indice da escolha pendente no passo actual, quando ha uma.</summary>
            public int PendingChoiceStep = -1;

            /// <summary>
            /// Texto ja decidido mas ainda por entregar: so entra no historico quando
            /// o "a escrever" acabar, senao aparecia antes de ele existir.
            /// </summary>
            public string PendingText;

            /// <summary>
            /// Distingue as duas coisas que podem estar a ser escritas: a replica a
            /// uma escolha do jogador, ou a fala do passo seguinte do guiao. A
            /// primeira so avanca o fio; a segunda pode abrir escolhas ou termina-lo.
            /// </summary>
            public bool PendingIsReply;

            public string ContactName => Definition != null ? Definition.ContactName : "?";
            public PhoneConversationDefinition.Step Current => Definition?.StepAt(StepIndex);
        }

        [SerializeField] private PhoneConversationDefinition[] conversations =
            Array.Empty<PhoneConversationDefinition>();

        [Tooltip("Para as escolhas cortantes continuarem a alimentar a ameaca oculta.")]
        [SerializeField] private PrototypeNpcRoutine ruiRoutine;

        private readonly List<Thread> threads = new List<Thread>();
        private readonly List<Thread> visible = new List<Thread>();
        private readonly HashSet<string> firedEvents = new HashSet<string>();

        /// <summary>Disparado quando uma mensagem do contacto entra num fio.</summary>
        public event Action<Thread> MessageArrived;

        /// <summary>Disparado sempre que o conteudo visivel de um fio muda.</summary>
        public event Action<Thread> ThreadChanged;

        /// <summary>
        /// Todos os fios do jogo, incluindo os de contactos que o Tomas ainda nao
        /// conhece. Serve a quem pergunta pelo **estado** de um fio (o `DayOneStage`
        /// espera que o do anuncio acabe); nao serve para desenhar o telemovel.
        /// </summary>
        public IReadOnlyList<Thread> Threads => threads;

        /// <summary>
        /// Os contactos que estao mesmo no telemovel, pela ordem por que la entraram.
        ///
        /// E esta a lista que o ecra desenha **e indexa**. Desenhar uma e indexar
        /// outra abria o contacto errado, que e o unico modo de isto correr mal em
        /// silencio.
        /// </summary>
        public IReadOnlyList<Thread> Visible => visible;

        /// <summary>Disparado quando um contacto novo entra no telemovel.</summary>
        public event Action<Thread> ThreadUnlocked;

        /// <summary>
        /// O fio com actividade mais recente. E este que o telemovel mostra: com dois
        /// contactos e sem lista de conversas, abrir no fio onde alguma coisa acabou
        /// de acontecer e o unico comportamento que nunca esconde a mensagem nova.
        /// </summary>
        public Thread MostRecent { get; private set; }

        public bool AnyUnread
        {
            get
            {
                for (int i = 0; i < visible.Count; i++)
                    if (visible[i].Unread) return true;
                return false;
            }
        }

        /// <summary>
        /// O Rui e da cena, este servico ja nao e: passou a sobreviver a troca de
        /// cena com os outros sistemas. Sem voltar a procura-lo, responder seco ao
        /// colega de casa deixava de mexer na ameaca a partir da segunda cena — e
        /// como a chamada e `ruiRoutine?.`, nao dava erro nenhum.
        /// </summary>
        public void Rebind()
        {
            if (ruiRoutine == null) ruiRoutine = FindObjectOfType<PrototypeNpcRoutine>();
        }

        private void Awake()
        {
            Rebind();

            foreach (var definition in conversations)
            {
                if (definition == null) continue;
                var thread = new Thread { Definition = definition };
                threads.Add(thread);

                // Um contacto por conhecer nao arranca. Sem isto, um fio trancado
                // continuava a contar os seus atrasos as escondidas e chegava ao
                // telemovel ja a meio — o Vitor a dizer "ainda queres os discos?"
                // no instante em que o Tomas o grava.
                if (string.IsNullOrEmpty(definition.UnlockEvent)) Unlock(thread);
            }
        }

        /// <summary>
        /// Poe o contacto no telemovel e deixa o fio comecar a andar.
        ///
        /// So aqui e que um fio ganha vida: e o mesmo caminho para quem ja estava
        /// gravado (o Pai) e para quem entra a meio do jogo, para nao haver duas
        /// maneiras de um fio comecar.
        /// </summary>
        private void Unlock(Thread thread)
        {
            if (thread.Unlocked) return;
            thread.Unlocked = true;

            // **Nao entra na lista aqui.** Ver `ShowInInbox`: um contacto so aparece
            // depois de haver alguma coisa dentro dele.

            var definition = thread.Definition;
            if (definition.StartsDelivered)
            {
                // Historico anterior ao inicio do jogo: entra sem atraso e sem
                // notificacao, ate ao primeiro passo que espere pelo jogador.
                while (thread.State != ThreadState.AwaitingPlayer &&
                       thread.StepIndex < definition.StepCount)
                {
                    var step = definition.StepAt(thread.StepIndex);
                    if (!string.IsNullOrEmpty(step.WaitForEvent)) break;
                    Deliver(thread, step, silent: true);
                    if (thread.State == ThreadState.AwaitingPlayer) break;
                }
            }
            else
            {
                Arm(thread);
            }

            ThreadUnlocked?.Invoke(thread);
            ThreadChanged?.Invoke(thread);
        }

        private void Update()
        {
            for (int i = 0; i < threads.Count; i++)
            {
                var thread = threads[i];
                if (!thread.Unlocked) continue;
                if (thread.State != ThreadState.Waiting && thread.State != ThreadState.Typing)
                    continue;
                if (Time.time < thread.DeliverAt) continue;

                var step = thread.Current;
                if (step == null) { thread.State = ThreadState.Done; continue; }

                // **A mensagem que chegou tarde de mais.**
                //
                // Os atrasos aqui sao segundos de relogio; os dias do jogo nao sao.
                // O Pai dizia "Morning. Did you eat something that was not bread?"
                // com 120 s de atraso a contar do `day1_kitchen` — e um jogador que
                // se deitasse depressa recebia-a **as 02:47**, no meio da noite em
                // que a casa muda.
                //
                // Saltar e melhor do que atrasar: uma mensagem de manha entregue a
                // noite nao se salva mudando-lhe a hora, e o fio continua a andar.
                if (IsStale(step))
                {
                    Skip(thread);
                    continue;
                }

                if (thread.State == ThreadState.Waiting) BeginTyping(thread);
                else DeliverPending(thread);
            }
        }

        /// <summary>
        /// Marca um acontecimento da historia. Fios parados a espera dele comecam a
        /// contar o seu atraso a partir de agora.
        /// </summary>
        public void RaiseEvent(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (!firedEvents.Add(id)) return;

            // Primeiro os contactos que este acontecimento traz. O `Unlock` arma o
            // fio, e o `Arm` ja sabe consultar os acontecimentos que ja passaram —
            // um contacto que entre tarde nao fica preso a espera de coisas que
            // aconteceram antes de ele existir.
            for (int i = 0; i < threads.Count; i++)
            {
                var thread = threads[i];
                if (!thread.Unlocked && thread.Definition.UnlockEvent == id) Unlock(thread);
            }

            for (int i = 0; i < threads.Count; i++)
            {
                var thread = threads[i];
                if (thread.State != ThreadState.BlockedOnEvent) continue;

                var step = thread.Current;
                if (step != null && step.WaitForEvent == id)
                {
                    thread.State = ThreadState.Waiting;
                    thread.DeliverAt = Time.time + step.DelaySeconds;
                }
            }
        }

        /// <summary>O jogador escolheu uma resposta no fio dado.</summary>
        public void Choose(Thread thread, int index)
        {
            if (thread == null || thread.State != ThreadState.AwaitingPlayer) return;

            var step = thread.Definition.StepAt(thread.PendingChoiceStep);
            if (step == null || !step.HasChoices) return;

            DialogueChoice choice = step.Choices[Mathf.Clamp(index, 0, step.Choices.Length - 1)];

            // A resposta do jogador aparece como balao enviado antes da reaccao do
            // contacto — pedido explicito da seccao 13.1.1.
            thread.Entries.Add(new Entry { Text = choice.Text, Mine = true });
            MostRecent = thread;
            ThreadChanged?.Invoke(thread);

            // O tom, nao a posicao, e o que mexe na ameaca — e so no fio do Rui.
            // Isto corria em todos os fios, portanto responder seco ao pai tornava
            // o colega de casa perigoso.
            if (thread.Definition.AffectsRuiThreat && choice.Tone == DialogueTone.Edgy)
                ruiRoutine?.ApplyDialogueChoice(true);

            // O padrao de confronto do jogador (§6.2) conta em **todos** os fios, e
            // nao so no do Rui. Ser seco com o pai nao torna o colega de casa
            // perigoso — por isso a linha de cima e do fio dele — mas diz quem o
            // Tomas e quando esta encurralado, e e isso que os finais leem.
            Pungent.Narrative.NarrativeBlackboard.Instance?.OnChoice(choice.Tone);

            thread.PendingChoiceStep = -1;

            if (choice.HasReply)
            {
                // Primeiro o silencio: ninguem responde no instante em que recebe.
                // So depois e que o "a escrever" aparece, e so depois a replica.
                thread.PendingText = choice.Reply;
                thread.PendingIsReply = true;
                thread.State = ThreadState.Waiting;
                thread.DeliverAt = Time.time + step.ReplyDelaySeconds;
            }
            else
            {
                thread.State = ThreadState.Waiting;
                Advance(thread);
            }

            // E aqui que uma mensagem deixa de ser conversa e passa a ser um gesto.
            //
            // Este servico **consome** acontecimentos — um passo espera pelo
            // `evidence_found` para chegar — e nunca levantou nenhum. Enquanto assim
            // foi, nao havia maneira de o telemovel mexer na historia, e o
            // `EvidenceShared` ficou a zero em todas as jogadas possiveis: o final
            // "sobrevive com prova" estava escrito, tinha epilogo, e era
            // inalcancavel por construcao.
            //
            // **No fim e nao a meio.** O `Notify` volta a entrar aqui pelo
            // `RaiseEvent`, que mexe no estado dos fios; levantado antes destas
            // linhas, o fio ficava com o estado da escolha por escrever quando a
            // reentrada o fosse ler.
            //
            // O director e resolvido **na leitura** e nao guardado no `Awake`: este
            // componente atravessa cenas, e o exemplar apanhado na recarga do
            // apartamento para o climax e o que se vai desactivar.
            if (choice.RaisesAnything)
                Pungent.Narrative.ChapterDirector.Resolve()?.Notify(choice.RaisesEvent);
        }

        /// <summary>Chamado quando o jogador abre o fio.</summary>
        public void MarkRead(Thread thread)
        {
            if (thread == null || !thread.Unread) return;
            thread.Unread = false;
            ThreadChanged?.Invoke(thread);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// O silencio acabou. Mostra-se o "a escrever" e so depois a mensagem.
        ///
        /// Os dois tempos sao coisas diferentes e nao devem ser confundidos: o
        /// silencio e quanto a pessoa demora a pegar no telemovel, e pode ser
        /// enorme; o "a escrever" e quanto ela demora a escrever a frase, e sao
        /// segundos. Ter os dois no mesmo campo dava um contacto que ficava um
        /// minuto inteiro a escrever uma linha.
        /// </summary>
        private void BeginTyping(Thread thread)
        {
            var step = thread.Current;

            // Sem texto guardado, o que vem a seguir e a fala do passo actual.
            if (thread.PendingText == null)
            {
                if (step == null) { thread.State = ThreadState.Done; return; }
                if (string.IsNullOrWhiteSpace(step.Line)) { Deliver(thread, step, silent: false); return; }
                thread.PendingText = step.Line;
                thread.PendingIsReply = false;
            }

            float typing = step != null ? step.TypingSeconds : 0f;
            if (typing <= 0f) { DeliverPending(thread); return; }

            thread.State = ThreadState.Typing;
            thread.DeliverAt = Time.time + typing;
            ThreadChanged?.Invoke(thread);
        }

        /// <summary>Acabou o "a escrever": a mensagem entra no historico.</summary>
        private void DeliverPending(Thread thread)
        {
            var step = thread.Current;
            string text = thread.PendingText;
            bool isReply = thread.PendingIsReply;
            thread.PendingText = null;

            if (isReply)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    thread.Entries.Add(new Entry { Text = text, Mine = false });
                    ShowInInbox(thread);
                    thread.Unread = true;
                    MostRecent = thread;
                    MessageArrived?.Invoke(thread);
                    ThreadChanged?.Invoke(thread);
                }

                thread.State = ThreadState.Waiting;
                Advance(thread);
                return;
            }

            if (step == null) { thread.State = ThreadState.Done; return; }
            Deliver(thread, step, silent: false);
        }

        /// <summary>
        /// Poe o contacto na lista, se ainda la nao estiver.
        ///
        /// ---
        ///
        /// **Um contacto vazio no telemovel e sempre um bug.**
        ///
        /// Ate aqui a lista enchia-se no `Unlock`, e desbloquear nao e o mesmo que
        /// ter conversa: o `PHN_Seller` abre no `day1_deal` e a primeira mensagem
        /// dele espera pelo `day_two`. Entre os dois esta a noite das 02:47 inteira,
        /// e durante essa noite o telemovel mostrava um contacto chamado
        /// *"Vitor (parts)"* com **zero mensagens** — ao lado de um "Unknown number"
        /// a falar de pecas, que e a mesma pessoa antes de ter nome. Quem abria o
        /// telemovel via dois vendedores e nenhuma explicacao.
        ///
        /// A correccao e geral e nao de um fio: nenhum telemovel do mundo mostra um
        /// contacto com quem nunca se trocou uma palavra. Entra quando houver a
        /// primeira linha, seja ela do contacto ou uma resposta.
        /// </summary>
        private void ShowInInbox(Thread thread)
        {
            if (thread == null || visible.Contains(thread)) return;
            visible.Add(thread);
        }

        private void Deliver(Thread thread, PhoneConversationDefinition.Step step, bool silent)
        {
            if (!string.IsNullOrWhiteSpace(step.Line))
            {
                thread.Entries.Add(new Entry { Text = step.Line, Mine = false });
                ShowInInbox(thread);
                MostRecent = thread;
            }

            if (step.HasChoices)
            {
                thread.PendingChoiceStep = thread.StepIndex;
                thread.State = ThreadState.AwaitingPlayer;
            }
            else if (step.EndsThread)
            {
                thread.State = ThreadState.Done;
            }
            else
            {
                thread.State = ThreadState.Waiting;
                Advance(thread);
            }

            if (!silent)
            {
                thread.Unread = true;
                MessageArrived?.Invoke(thread);
            }
            ThreadChanged?.Invoke(thread);
        }

        /// <summary>Passa ao passo seguinte e arma-lhe a espera.</summary>
        private void Advance(Thread thread)
        {
            thread.StepIndex++;
            Arm(thread);
        }

        /// <summary>
        /// O dia desta mensagem ja acabou? Le-se dos acontecimentos ja levantados, que
        /// e o mesmo registo que o `WaitForEvent` usa para o contrario.
        /// </summary>
        private bool IsStale(PhoneConversationDefinition.Step step) =>
            step != null
            && !string.IsNullOrWhiteSpace(step.StaleAfterEvent)
            && firedEvents.Contains(step.StaleAfterEvent);

        /// <summary>
        /// Salta a mensagem sem a entregar, e **deixa rasto**. Uma mensagem que
        /// desaparece em silencio e indistinguivel de um fio partido, e este projecto
        /// ja perdeu tempo a procurar mensagens que nunca chegaram por outra razao.
        /// </summary>
        private void Skip(Thread thread)
        {
            var step = thread.Current;
            Debug.Log("[Telemovel] Saltada uma mensagem de " + thread.Definition.ContactName
                    + " — '" + step.StaleAfterEvent + "' ja aconteceu. Chegava fora do dia dela.");
            Advance(thread);
        }

        private void Arm(Thread thread)
        {
            var step = thread.Current;
            if (step == null) { thread.State = ThreadState.Done; return; }

            if (!string.IsNullOrEmpty(step.WaitForEvent) && !firedEvents.Contains(step.WaitForEvent))
            {
                thread.State = ThreadState.BlockedOnEvent;
                return;
            }

            thread.State = ThreadState.Waiting;
            thread.DeliverAt = Time.time + step.DelaySeconds;
        }
    }
}
