using System;
using Pungent.NPC;
using Pungent.Player;
using System.Collections.Generic;
using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>
    /// Telemóvel diegético: o modelo físico levanta-se e baixa-se na mão e as
    /// mensagens são desenhadas num ecrã em world-space, não em OnGUI por cima do
    /// jogo (Tarefa 4B do plano mestre).
    ///
    /// O nome da classe e os campos serializados foram mantidos de propósito: a
    /// cena, o `WorldDialogueController` e o `PhoneLightController` já apontam para
    /// aqui, e renomear partiria essas ligações.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypePhoneUI : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PrototypeNpcRoutine ruiRoutine;
        [SerializeField] private AudioSource notificationSource;
        [SerializeField] private AudioClip notificationClip;

        [Header("Conversa")]
        [Tooltip("Fonte das mensagens. Ligado = os campos de texto abaixo deixam de ser usados.")]
        [SerializeField] private PhoneMessageService messages;
        [SerializeField] private string contactName = "Rui";
        [SerializeField, TextArea] private string incomingMessage =
            "Did your internet go down too? The router is at the end of the hall.";
        [SerializeField, TextArea] private string neutralResponse = "I'll restart it.";
        [SerializeField, TextArea] private string defiantResponse = "Why are you awake at this hour?";
        [SerializeField, TextArea] private string ruiFollowUp = "I was working too. Don't take long.";
        [SerializeField, TextArea] private string ruiFollowUpDefiant =
            "Does it matter? Just go and restart it.";
        [SerializeField, Min(0f)] private float initialMessageDelay = 1.4f;
        [SerializeField, Min(0f)] private float replyDelay = 1.8f;
        [SerializeField, Range(0f, 1f)] private float notificationVolume = 0.42f;
        [Tooltip("Hora mostrada antes de haver capitulo a correr. Depois disso quem "
               + "manda no relogio e o ChapterDirector.")]
        [SerializeField] private string clockLabel = "02:47";
        [Tooltip("Opcional: resolvido sozinho em runtime. E dele que vem a hora.")]
        [SerializeField] private Pungent.Narrative.ChapterDirector chapters;

        [Header("Telemóvel físico")]
        [SerializeField] private Transform phoneTransform;
        [Tooltip("Pose com o telemóvel guardado, fora do enquadramento.")]
        [SerializeField] private Vector3 loweredPosition = new Vector3(0.24f, -0.42f, 0.40f);
        [SerializeField] private Vector3 loweredEuler = new Vector3(58f, -22f, 14f);
        [Tooltip("Pose de leitura.")]
        [SerializeField] private Vector3 raisedPosition = new Vector3(0.045f, -0.10f, 0.20f);
        [SerializeField] private Vector3 raisedEuler = new Vector3(6f, -7f, 2f);
        [SerializeField, Min(1f)] private float raiseSpeed = 9f;

        [Header("Aviso de mensagem")]
        [Tooltip("Quanto tempo o ecrã acende quando chega mensagem com o telemóvel baixado.")]
        [SerializeField, Min(0f)] private float notificationGlowSeconds = 2.5f;

        private PhoneScreenUI screen;
        private Camera playerCamera;
        private bool phoneOpen;
        private readonly Vector3[] phoneScreenCorners = new Vector3[4];
        private bool messageReceived;
        private bool unread;
        private bool playerReplied;
        private bool defiantChoice;
        private bool waitingForRui;
        private bool ruiReplied;
        private bool dialogueSuppressed;
        private float messageReceiveTime;
        private float ruiReplyTime;
        private float glowUntil;
        private float openAmount;
        private int selectedReply = -1;
        private bool threadDirty = true;

        public bool IsOpen => phoneOpen;

        /// <summary>
        /// O telemovel fechou-se com o Escape **neste** frame.
        ///
        /// Serve o `PauseMenu`, e existe por causa da ordem de execucao: se este
        /// componente correr primeiro, o telemovel ja esta fechado quando o menu
        /// le o `IsOpen`, e o Escape passava para ele na mesma — fechar o
        /// telemovel e pausar o jogo com o mesmo toque, que e o defeito que isto
        /// veio resolver. Perguntar pelo frame nao depende de quem corre antes.
        /// </summary>
        public bool ClosedByEscapeThisFrame => closedByEscapeFrame == Time.frameCount;

        /// <summary>
        /// O telemovel de **qualquer** exemplar esta aberto.
        ///
        /// ---
        ///
        /// **Existe porque perguntar a um exemplar concreto nao chega.** A cena tem
        /// mais do que um `PrototypePhoneUI` — um no `PLAYER` e outro no
        /// `PlayerRoot` — e o `PauseMenu` guardava o primeiro que o
        /// `FindObjectOfType` lhe desse. Se o aberto fosse o outro, o `IsOpen` que
        /// ele lia era falso para sempre e o Escape abria a pausa por cima das
        /// mensagens, que e exactamente o defeito que esta guarda veio impedir.
        ///
        /// O apartamento ainda e recarregado para o climax, e ai o jogador e um
        /// objecto novo: uma referencia guardada aponta para um componente destruido.
        /// Compara como nula e volta a procurar, mas ha um frame pelo meio — e o
        /// frame de uma tecla e tudo o que este problema precisa.
        ///
        /// Estatico resolve os dois de uma vez, e segue o que o `HidingSpot.Occupied`
        /// ja faz neste projecto: quem precisa de saber se ha **algum** aberto
        /// pergunta ao tipo e nao a um objecto.
        /// </summary>
        public static bool AnyOpen { get; private set; }

        /// <summary>Algum telemovel fechou-se com o Escape **neste** frame.</summary>
        public static bool AnyClosedByEscapeThisFrame => anyClosedFrame == Time.frameCount;

        private static int anyClosedFrame = -1;
        private static PrototypePhoneUI openHolder;

        /// <summary>
        /// Limpa o estado estatico antes de a primeira cena carregar.
        ///
        /// **Sem isto, o Editor herda a sessao anterior.** Com "Enter Play Mode
        /// without Domain Reload" ligado, um estatico deixado a `true` por uma
        /// sessao em que o jogo foi parado com o telemovel aberto sobrevive para a
        /// seguinte — e o menu de pausa nunca mais abria, porque a guarda achava que
        /// havia mensagens abertas que ja nao existem. Falha so no Editor, so as
        /// vezes, e sem nada na consola: a pior especie.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyOpen = false;
            openHolder = null;
            anyClosedFrame = -1;
        }

        private int closedByEscapeFrame = -1;
        public bool HasUnreadMessage => UsingService ? messages.AnyUnread : unread;
        public bool PlayerReplied => playerReplied;

        /// <summary>
        /// Verdadeiro quando ha um PhoneMessageService com fios. Enquanto for falso,
        /// o telemovel corre pelos cinco campos antigos, para a cena nao ficar muda
        /// entre esta alteracao e a migracao.
        ///
        /// Conta os fios **todos** e nao so os desbloqueados: se a pergunta fosse
        /// sobre a lista visivel, um telemovel que ainda nao conhece ninguem caia no
        /// caminho antigo e mostrava a conversa do router escrita a mao no meio do
        /// prologo.
        /// </summary>
        private bool UsingService => messages != null && messages.Threads.Count > 0;

        /// <summary>
        /// Conversa aberta. Nulo = o jogador esta na lista de contactos.
        ///
        /// Antes o ecra mostrava sempre o fio de actividade mais recente, e uma
        /// mensagem do Pai roubava o ecra a conversa do Rui a meio de a ler. Quem
        /// escolhe o que esta no ecra passa a ser o jogador.
        /// </summary>
        private PhoneMessageService.Thread openThread;

        private readonly List<PhoneScreenUI.InboxRow> inboxRows = new List<PhoneScreenUI.InboxRow>();

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (ruiRoutine == null) ruiRoutine = FindObjectOfType<PrototypeNpcRoutine>();

            if (phoneTransform == null)
            {
                GameObject held = GameObject.Find("HeldPhone");
                if (held != null) phoneTransform = held.transform;
            }

            if (notificationSource == null && phoneTransform != null)
                notificationSource = phoneTransform.GetComponent<AudioSource>();

            playerCamera = GetComponentInChildren<Camera>(true);
            BuildScreen();

            if (messages == null) messages = FindObjectOfType<PhoneMessageService>();
            if (messages != null)
            {
                messages.MessageArrived += OnMessageArrived;
                messages.ThreadChanged += OnThreadChanged;
            }
        }

        private void OnDestroy()
        {
            if (messages == null) return;
            messages.MessageArrived -= OnMessageArrived;
            messages.ThreadChanged -= OnThreadChanged;
        }

        /// <summary>Chegou mensagem: som e brilho se o telemovel estiver baixado, redesenho sempre.</summary>
        private void OnMessageArrived(PhoneMessageService.Thread thread)
        {
            // Chegou com o aparelho levantado: o jogador esta a ler a mensagem, logo
            // ela nao fica por ler. Sem isto, fechar o telemovel deixava o aviso de
            // "New message" aceso por uma coisa que ele acabou de ver.
            // So conta como lida se o jogador estiver mesmo nessa conversa. Chegar
            // uma do Pai enquanto ele le o Rui nao a marca como vista, e a lista
            // continua a assinala-la.
            if (phoneOpen && openThread == thread) messages.MarkRead(thread);
            Notify();
        }

        private void OnThreadChanged(PhoneMessageService.Thread thread) => threadDirty = true;

        private void Start()
        {
            messageReceiveTime = Time.time + initialMessageDelay;
            openAmount = 0f;
            ApplyPhonePose(0f);
        }

        private void BuildScreen()
        {
            if (phoneTransform == null) return;

            screen = phoneTransform.GetComponent<PhoneScreenUI>();
            if (screen == null) screen = phoneTransform.gameObject.AddComponent<PhoneScreenUI>();

            // O ecrã cobre a face frontal do aparelho, ligeiramente à frente para
            // não haver z-fighting com a malha. O pivô do modelo está na base, por
            // isso a posição vem do centro medido e não de zero.
            Bounds bounds = MeasurePhoneLocal();
            screen.Build(phoneTransform, bounds.size.x * 0.90f);
            if (screen.Root != null)
            {
                // O telemóvel está à frente da câmara, logo o que se vê é a sua face
                // -Z, e é aí que o ecrã fica. Sem rotação: um Canvas do Unity é
                // olhado pelo lado -Z do seu próprio transform — virá-lo 180°
                // espelhava o texto todo.
                screen.Root.localPosition = new Vector3(
                    bounds.center.x, bounds.center.y, bounds.center.z - bounds.extents.z - 0.0015f);
                screen.Root.localRotation = Quaternion.identity;
            }

            screen.SetHeader(contactName, ClockLabel);
        }

        /// <summary>
        /// Bounds do modelo em espaço local do telemóvel, cantos incluídos. Usar
        /// `renderer.bounds` daria uma caixa alinhada ao mundo e, com o aparelho
        /// inclinado na mão, as medidas saíam infladas.
        /// </summary>
        private Bounds MeasurePhoneLocal()
        {
            var fallback = new Bounds(new Vector3(0f, 0.073f, 0f), new Vector3(0.0745f, 0.1463f, 0.0123f));
            if (phoneTransform == null) return fallback;

            bool first = true;
            Bounds result = default;

            foreach (var filter in phoneTransform.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                if (filter.transform == phoneTransform) continue;   // caixa placeholder
                if (filter.name == "PhoneScreen") continue;         // brilho da lanterna

                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 toLocal = phoneTransform.worldToLocalMatrix * filter.transform.localToWorldMatrix;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 offset = Vector3.Scale(mesh.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                    Vector3 point = toLocal.MultiplyPoint3x4(mesh.center + offset);

                    if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                    else result.Encapsulate(point);
                }
            }

            return first ? fallback : result;
        }

        private void Update()
        {
            // O servico trata da chegada e do ritmo das mensagens; aqui so resta o
            // aparelho. Os dois blocos temporizados que existiam neste Update eram
            // a agenda de uma unica conversa codificada a mao.
            if (!UsingService)
            {
                if (!messageReceived && Time.time >= messageReceiveTime)
                {
                    messageReceived = true;
                    unread = true;
                    threadDirty = true;
                    Notify();
                }

                if (waitingForRui && Time.time >= ruiReplyTime)
                {
                    waitingForRui = false;
                    ruiReplied = true;
                    unread = !phoneOpen;
                    threadDirty = true;
                    Notify();
                }
            }

            if (!dialogueSuppressed && input != null && input.PhonePressed)
                SetPhoneOpen(!phoneOpen);

            // **O Escape desfaz uma camada de cada vez.**
            //
            // Uma tecla que fecha coisas fecha uma de cada vez, da mais recente para
            // a mais antiga, e no telemovel ha duas camadas antes do jogo:
            //
            //   dentro de uma conversa  ->  volta a lista de conversas
            //   na lista                ->  fecha o telemovel
            //   telemovel fechado       ->  pausa (e o `PauseMenu` que trata)
            //
            // A primeira faltava. O Escape fechava o telemovel inteiro a partir de
            // dentro de uma conversa, e quem so queria voltar a lista tinha de o
            // reabrir — ou descobrir que havia um Backspace, que ninguem descobre.
            //
            // O Backspace continua a fazer o mesmo que a primeira camada. Nao lhe
            // tiro o lugar: quem ja o tem nos dedos nao perde nada, e ha teclados em
            // que o Escape esta longe.
            //
            // Os marcadores de "fechou-se agora" so se levantam quando o telemovel
            // **fecha mesmo**. Levanta-los ao recuar de uma conversa dizia ao
            // `PauseMenu` para ignorar um Escape que ele nunca ia ver.
            if (phoneOpen && UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (openThread != null)
                {
                    openThread = null;
                    selectedReply = -1;
                    threadDirty = true;
                }
                else
                {
                    selectedReply = -1;
                    threadDirty = true;
                    SetPhoneOpen(false);
                    closedByEscapeFrame = Time.frameCount;
                    anyClosedFrame = Time.frameCount;
                }
            }

            UpdateUnreadNotice();
            UpdatePhonePose();
            UpdatePhoneVhsClarity();
            if (phoneOpen) UpdateInteraction();
            RefreshScreen();

            // O relogio anda enquanto o ecra estiver a vista, e nao so quando o
            // cabecalho e reescrito: quem fica a ler as mensagens ve o minuto mudar.
            if (openAmount > 0.01f) screen?.SetClock(ClockLabel);
        }

        /// <summary>
        /// A hora que o telemovel mostra.
        ///
        /// Vinha de um campo serializado com `02:47` escrito a mao — a hora do
        /// cartao do `CH_Day2_0247`, parada para sempre em todos os dias do jogo.
        /// Quem sabe as horas e o <see cref="ChapterDirector"/>, que abre cada
        /// capitulo com a hora do cartao dele. O campo fica como esta e passa a ser
        /// so o que se ve antes de haver capitulo nenhum a correr.
        /// </summary>
        private string ClockLabel
        {
            get
            {
                chapters = Pungent.Narrative.ChapterDirector.Resolve(chapters);
                return chapters != null ? chapters.ClockLabel : clockLabel;
            }
        }

        private void UpdateInteraction()
        {
            if (input == null) return;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool canPoint = screen != null && mouse != null && playerCamera != null;
            Vector2 pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;

            if (!UsingService)
            {
                if (!messageReceived || playerReplied) return;

                int legacyHover = canPoint ? screen.GetReplyUnderPointer(pointer, playerCamera) : -1;
                if (legacyHover != selectedReply)
                {
                    selectedReply = legacyHover;
                    screen?.SetHovered(legacyHover);
                }

                if (input.ChoiceOnePressed) ResolveResponse(false);
                else if (input.ChoiceTwoPressed) ResolveResponse(true);
                else if (legacyHover >= 0 && click) ResolveResponse(legacyHover == 1);
                return;
            }

            // --- lista de contactos ---
            if (openThread == null)
            {
                int row = canPoint ? screen.GetRowUnderPointer(pointer, playerCamera) : -1;
                screen?.SetRowHovered(row);

                int chosenRow = input.NumberPressed > 0 ? input.NumberPressed - 1 : -1;
                if (chosenRow < 0 && row >= 0 && click) chosenRow = row;

                // `Visible` e nao `Threads`: a lista desenhada e a lista indexada tem
                // de ser a mesma. Com contactos por desbloquear pelo meio, indexar a
                // outra abria a conversa errada — e sem erro nenhum.
                if (chosenRow >= 0 && chosenRow < messages.Visible.Count)
                    OpenThread(messages.Visible[chosenRow]);
                return;
            }

            // --- dentro de uma conversa ---
            bool overBack = canPoint && screen.IsBackUnderPointer(pointer, playerCamera);
            screen?.SetBackHovered(overBack);

            // So o Backspace. O Escape ja fechou o telemovel inteiro la em cima,
            // antes disto correr — deixa-lo aqui era uma segunda regra para a
            // mesma tecla, que nunca chega a ser lida e contradiz a de cima.
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            bool backKey = keyboard != null && keyboard.backspaceKey.wasPressedThisFrame;

            if ((overBack && click) || backKey)
            {
                openThread = null;
                selectedReply = -1;
                threadDirty = true;
                return;
            }

            if (openThread.State != PhoneMessageService.ThreadState.AwaitingPlayer) return;

            int hovered = canPoint ? screen.GetReplyUnderPointer(pointer, playerCamera) : -1;
            if (hovered != selectedReply)
            {
                selectedReply = hovered;
                screen?.SetHovered(hovered);
            }

            int picked = -1;
            if (input.ChoiceOnePressed) picked = 0;
            else if (input.ChoiceTwoPressed) picked = 1;
            else if (hovered >= 0 && click) picked = hovered;
            if (picked < 0) return;

            messages.Choose(openThread, picked);
            selectedReply = -1;
            threadDirty = true;
        }

        private void OpenThread(PhoneMessageService.Thread thread)
        {
            openThread = thread;
            messages.MarkRead(thread);
            selectedReply = -1;
            threadDirty = true;
        }

        private void UpdatePhonePose()
        {
            float target = phoneOpen ? 1f : 0f;
            openAmount = Mathf.MoveTowards(openAmount, target, Time.deltaTime * raiseSpeed);
            ApplyPhonePose(openAmount);
        }

        private void ApplyPhonePose(float amount)
        {
            if (phoneTransform == null) return;

            // Curva suave nas duas pontas: o gesto tem peso, como o resto da câmara.
            float t = amount * amount * (3f - 2f * amount);
            phoneTransform.localPosition = Vector3.Lerp(loweredPosition, raisedPosition, t);
            phoneTransform.localRotation = Quaternion.Slerp(
                Quaternion.Euler(loweredEuler), Quaternion.Euler(raisedEuler), t);

            // O ecrã só liga quando vale a pena: aberto, ou a avisar de mensagem.
            bool lit = amount > 0.05f || Time.time < glowUntil;
            if (screen != null && screen.Root != null && screen.Root.gameObject.activeSelf != lit)
                screen.Root.gameObject.SetActive(lit);
        }

        /// <summary>
        /// Liga e desliga o aviso de mensagem por abrir no HUD.
        ///
        /// ---
        ///
        /// **O telemovel deste jogo nao se ouve.** As mensagens chegam com dezenas
        /// de segundos de atraso, muitas vezes a meio de uma tarefa, e nao ha nada
        /// no ecra a dizer que chegaram. Quem nao tem o habito de abrir o telemovel
        /// de vez em quando a ver perde fios inteiros — e um deles e o do Pai, que
        /// decide um dos cinco finais.
        ///
        /// **Some com o telemovel aberto.** Nao ha razao para um aviso a dizer para
        /// abrir uma coisa que ja esta aberta, e o `MarkRead` do fio aberto trata do
        /// resto sozinho.
        ///
        /// **Cala-se durante o dialogo**, pela mesma razao por que o telemovel se
        /// cala: uma linha nova a piscar por cima de alguem a falar e o jogo a pedir
        /// que se olhe para outro lado no unico momento em que nao se deve.
        ///
        /// Resolvido na leitura e nao guardado no `Awake`: o HUD vive no jogador, e
        /// o jogador e outro depois de uma troca de cena.
        /// </summary>
        private void UpdateUnreadNotice()
        {
            if (hud == null) hud = FindObjectOfType<PrototypeHUD>();
            if (hud == null || messages == null) return;

            hud.SetNotice(!phoneOpen && !dialogueSuppressed && messages.AnyUnread);
        }

        private PrototypeHUD hud;

        private void RefreshScreen()
        {
            if (screen == null || !threadDirty) return;
            threadDirty = false;

            if (UsingService) { RefreshFromService(); return; }

            screen.ClearThread();
            if (!messageReceived)
            {
                screen.SetTyping("No new messages.");
                screen.SetReplies(null, null);
                return;
            }

            screen.AddBubble(incomingMessage, false);

            if (!playerReplied)
            {
                screen.SetTyping(string.Empty);
                screen.SetReplies(neutralResponse, defiantResponse);
                return;
            }

            screen.AddBubble(defiantChoice ? defiantResponse : neutralResponse, true);
            screen.SetReplies(null, null);

            if (waitingForRui)
            {
                screen.SetTyping($"{contactName} is typing...");
            }
            else if (ruiReplied)
            {
                screen.SetTyping(string.Empty);
                screen.AddBubble(defiantChoice ? ruiFollowUpDefiant : ruiFollowUp, false);
            }
        }

        /// <summary>
        /// Desenha o fio de actividade mais recente. Ao contrario do caminho antigo,
        /// isto nao sabe nada sobre o conteudo: percorre o historico que o servico
        /// entregou, seja ele do Rui, do Pai ou de quem vier a seguir.
        /// </summary>
        private void RefreshFromService()
        {
            if (openThread == null) { RefreshInbox(); return; }
            RefreshThread(openThread);
        }

        /// <summary>Lista de contactos: quem falou, o que disse por ultimo, e se falta ler.</summary>
        private void RefreshInbox()
        {
            inboxRows.Clear();
            foreach (var t in messages.Visible)
            {
                // Quem esta a escrever mostra-o tambem na lista: saber que vem
                // alguma coisa e metade do efeito, e ir a conversa para descobrir
                // e uma decisao do jogador.
                string preview = t.State == PhoneMessageService.ThreadState.Typing
                    ? "typing…"
                    : t.Entries.Count > 0
                        ? t.Entries[t.Entries.Count - 1].Text
                        : "No messages yet";

                inboxRows.Add(new PhoneScreenUI.InboxRow
                {
                    Contact = t.ContactName,
                    Preview = preview,
                    Unread = t.Unread
                });
            }

            screen.ClearThread();
            screen.SetHeader("Messages", ClockLabel);
            screen.ShowInbox(inboxRows);
        }

        private void RefreshThread(PhoneMessageService.Thread thread)
        {
            screen.ShowThread(thread.ContactName);
            screen.SetHeader(thread.ContactName, ClockLabel);
            screen.ClearThread();

            if (thread.Entries.Count == 0)
            {
                screen.SetTyping("No messages yet.");
                screen.SetReplies(null, null);
                return;
            }

            for (int i = 0; i < thread.Entries.Count; i++)
                screen.AddBubble(thread.Entries[i].Text, thread.Entries[i].Mine);

            switch (thread.State)
            {
                case PhoneMessageService.ThreadState.AwaitingPlayer:
                    var step = thread.Definition.StepAt(thread.PendingChoiceStep);
                    screen.SetTyping(string.Empty);
                    if (step != null && step.HasChoices)
                        screen.SetReplies(step.Choices[0].Text, step.Choices[1].Text);
                    else
                        screen.SetReplies(null, null);
                    break;

                case PhoneMessageService.ThreadState.Typing:
                    screen.SetTyping($"{thread.ContactName} is typing...");
                    screen.SetReplies(null, null);
                    break;

                default:
                    screen.SetTyping(string.Empty);
                    screen.SetReplies(null, null);
                    break;
            }
        }

        private void OnDisable() => SetPhoneOpen(false);

        public void SetDialogueSuppressed(bool suppressed)
        {
            dialogueSuppressed = suppressed;
            // The phone is no longer forced to close, per user request.
        }

        private void ResolveResponse(bool defiant)
        {
            playerReplied = true;
            defiantChoice = defiant;
            waitingForRui = true;
            ruiReplyTime = Time.time + replyDelay;
            threadDirty = true;
            selectedReply = -1;

            if (defiant && ruiRoutine != null)
                ruiRoutine.ApplyDialogueChoice(true);
        }

        private void SetPhoneOpen(bool value)
        {
            phoneOpen = value;

            // O estatico segue o exemplar que mexeu. Fechar so limpa a bandeira se
            // for **este** que a tinha levantado: dois exemplares na cena e um deles
            // a fechar-se no `OnDisable` do cartao de capitulo apagavam o estado do
            // outro, e o menu de pausa voltava a abrir por cima das mensagens.
            if (value) { AnyOpen = true; openHolder = this; }
            else if (openHolder == this) { AnyOpen = false; openHolder = null; }
            if (!phoneOpen) Pungent.Atmosphere.VhsSettings.PhoneClarity = 0f;
            if (phoneOpen)
            {
                unread = false;
                // Abre sempre na lista, como um telemovel a serio.
                if (UsingService) openThread = null;
                threadDirty = true;
            }

            input?.SetLookSuppressed(phoneOpen);
        }

        /// <summary>
        /// Projeta os cantos do canvas diegetico para o passe VHS. O filtro nunca e
        /// desligado: a mascara apenas reduz bleed e ruido dentro do ecra, com uma
        /// transicao suave nas margens.
        /// </summary>
        private void UpdatePhoneVhsClarity()
        {
            if (!phoneOpen || playerCamera == null || screen == null || screen.Root == null ||
                !screen.Root.gameObject.activeInHierarchy)
            {
                Pungent.Atmosphere.VhsSettings.PhoneClarity = 0f;
                return;
            }

            screen.Root.GetWorldCorners(phoneScreenCorners);

            float minX = 1f;
            float minY = 1f;
            float maxX = 0f;
            float maxY = 0f;
            for (int i = 0; i < phoneScreenCorners.Length; i++)
            {
                Vector3 viewport = playerCamera.WorldToViewportPoint(phoneScreenCorners[i]);
                if (viewport.z <= 0f)
                {
                    Pungent.Atmosphere.VhsSettings.PhoneClarity = 0f;
                    return;
                }

                minX = Mathf.Min(minX, viewport.x);
                minY = Mathf.Min(minY, viewport.y);
                maxX = Mathf.Max(maxX, viewport.x);
                maxY = Mathf.Max(maxY, viewport.y);
            }

            const float featherPadding = 0.008f;
            Pungent.Atmosphere.VhsSettings.PhoneClarityRect = new Vector4(
                Mathf.Clamp01(minX - featherPadding), Mathf.Clamp01(minY - featherPadding),
                Mathf.Clamp01(maxX + featherPadding), Mathf.Clamp01(maxY + featherPadding));
            Pungent.Atmosphere.VhsSettings.PhoneClarity = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.2f, 0.75f, openAmount));
        }

        /// <summary>
        /// Aviso de mensagem nova. O som e o brilho existem para chamar a atencao de
        /// quem nao esta a olhar: com o telemovel ja levantado o jogador tem a
        /// mensagem a frente dos olhos, e tocar o alerta na mesma soa a erro — alem
        /// de gastar num momento banal um som que so devia assustar quando apanha
        /// o jogador desprevenido.
        /// </summary>
        private void Notify()
        {
            threadDirty = true;
            if (phoneOpen) return;

            glowUntil = Time.time + notificationGlowSeconds;

            if (notificationSource == null || notificationClip == null) return;

            // A fonte pode estar desligada, e o `PlayOneShot` nesse estado nao falha
            // em silencio: atira "Can not play a disabled audio source" para a
            // consola. Acontece nas trocas de cena e por tras do cartao de capitulo,
            // que suspende este componente — o telemovel continua a receber
            // mensagens enquanto o ecra preto esta a frente, e o alerta chega a uma
            // fonte que ja nao esta viva.
            //
            // Nao ha nada a salvar aqui: se a fonte esta desligada, o aviso nao devia
            // mesmo tocar. So a mensagem de erro e que nao ajudava ninguem.
            if (!notificationSource.isActiveAndEnabled) return;

            notificationSource.pitch = UnityEngine.Random.Range(0.98f, 1.02f);
            notificationSource.PlayOneShot(notificationClip, notificationVolume);
        }

        /// <summary>Aviso mínimo no ecrã do jogo; o resto da informação está no aparelho.</summary>
        private void OnGUI()
        {
            if (dialogueSuppressed || phoneOpen) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = HasUnreadMessage ? new Color(0.95f, 0.87f, 0.62f) : new Color(0.82f, 0.85f, 0.88f, 0.75f) }
            };

            string hint = HasUnreadMessage ? "New message  •  [TAB]" : "[TAB] Phone";
            var rect = new Rect(Screen.width - 330f, Screen.height - 52f, 300f, 28f);

            var shadow = new GUIStyle(style);
            shadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), hint, shadow);
            GUI.Label(rect, hint, style);
        }
    }
}
