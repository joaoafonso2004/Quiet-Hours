using System;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.NPC;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Encadeia os objetivos da abertura, para o jogador ter mais do que "liga o
    /// router". A sequência é: acordar, ler a mensagem, reiniciar o router, falar
    /// com o Rui na cozinha, voltar ao quarto.
    ///
    /// Cada passo avança por uma condição verificada aqui, e não por o objeto ter
    /// de saber da história — assim o router continua a ser um router.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OpeningQuestDirector : MonoBehaviour
    {
        public enum Step
        {
            WakeUp,
            ReadMessage,
            RestartRouter,
            TalkToRui,
            DrinkWater,
            ReturnToRoom,
            LockDoor,
            Sleep,
            Finished
        }

        [SerializeField] private PrototypeHUD hud;
        [SerializeField] private PrototypePhoneUI phone;
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private Transform player;
        [SerializeField] private PrototypeNpcRoutine rui;
        [SerializeField] private RouterInteractable router;

        [Header("Marcos")]
        [Tooltip("Centro do quarto do Tomás, para detetar o regresso.")]
        [SerializeField] private Vector3 bedroomCentre = new Vector3(-5.2f, 0f, -2.9f);
        [SerializeField, Min(0.5f)] private float bedroomRadius = 2.4f;
        [Tooltip("Distância a que se considera que o jogador chegou ao Rui.")]
        [SerializeField, Min(0.5f)] private float talkRadius = 3.0f;

        [Header("Fecho do dia")]
        [Tooltip("A porta do quarto do Tomas. Passa a pedir para trancar quando ele volta.")]
        [SerializeField] private Pungent.Interaction.DoorDragInteractable bedroomDoor;
        [Tooltip("Tarefa opcional oferecida depois do router.")]
        [SerializeField] private HomeTaskInteractable optionalTask;
        [SerializeField] private string optionalLabel = "Optional: smoke on the balcony";

        private Step step = Step.WakeUp;
        private bool routerDone;
        private bool conversationDone;
        private bool waterDrank;
        private bool slept;
        private bool sideObjectiveShown;

        public Step Current => step;

        /// <summary>
        /// Fios do telemóvel que reagem aos marcos da abertura. É por aqui que uma
        /// mensagem do Pai chega quando o jogo chega a um ponto, e não a um relógio.
        /// </summary>
        private PhoneMessageService messages;

        private void Awake()
        {
            messages = FindObjectOfType<PhoneMessageService>();
        }

        /// <summary>Chamado pelo director de história quando o router é reiniciado.</summary>
        public void NotifyRouterRestarted()
        {
            routerDone = true;
            messages?.RaiseEvent("router_restarted");
            if (step == Step.RestartRouter) Advance(Step.TalkToRui);
        }

        /// <summary>
        /// Chamado quando a conversa presencial com o Rui termina.
        ///
        /// ---
        ///
        /// **So conta quando esta noite esta mesmo a correr.**
        ///
        /// Este metodo e publico e chamado de fora — `RuiStoryConversation` chama-o
        /// no fim de **qualquer** conversa guionada com o Rui. E a do prologo, a da
        /// chave e das manhas, e uma dessas: dois dias de jogo antes desta noite,
        /// com este componente ainda desligado pelo `PrologueStage`.
        ///
        /// Nao chegava desligar o componente. Um metodo publico chamado a mao corre
        /// na mesma num componente desactivado — so o `Update` e que nao — e o
        /// `RaiseEvent` passava a valer no instante em que o Tomas recebe a chave.
        /// O `bypassQuestGate` da conversa tambem nao ajudava: esse so decide se o
        /// prompt "Talk to Rui" aparece.
        ///
        /// O que isto custava era o passo 09 do fio do Pai — "Go to bed. You have
        /// that early one on Tuesday" — deixar de esperar pela conversa das 02:47.
        /// Como o acontecimento ja estava dado, o passo armava-se e disparava logo
        /// a seguir ao da sopa: duas mensagens do pai em fila, e a segunda a perder
        /// o unico sitio onde fazia sentido, que era logo depois de ele falar com o
        /// Rui a meio da noite.
        ///
        /// **O `enabled` e o gate certo e nao prende nada.** O `DayOneStage.HandOver`
        /// liga este componente ao entrar na noite, e e o unico caminho para la; a
        /// partir dai qualquer conversa com o Rui conta. E o `RaiseEvent` ignora
        /// repetidos, portanto falar com ele mais do que uma vez nao faz mal.
        /// </summary>
        public void NotifyConversationFinished()
        {
            conversationDone = true;
            if (enabled) messages?.RaiseEvent("talked_to_rui");
            if (step == Step.TalkToRui) Advance(Step.DrinkWater);
        }

        public void NotifyWaterDrank()
        {
            waterDrank = true;
            if (step == Step.DrinkWater) Advance(Step.ReturnToRoom);
        }

        /// <summary>
        /// Fecha o passo da agua quando a **tarefa** acabar, e nao quando alguem
        /// clicar no lava-loica.
        ///
        /// ---
        ///
        /// Havia dois donos para o mesmo momento: o `SinkInteractable` fechava o
        /// passo num clique, e um `HomeTaskInteractable` de doze segundos com o id
        /// `d2_water` vivia no mesmo movel sem ninguem lhe pedir nada. Quem jogava
        /// carregava uma vez e o objectivo passava — *"a tarefa de beber agua nao
        /// tem nada"*, diz o relatorio de teste, e tinha, calada, a dois centimetros.
        ///
        /// O passo passa a esperar pela tarefa. O lava-loica sai da frente enquanto
        /// ela se estiver a oferecer (ver `SinkInteractable.Prompt`), portanto o
        /// clique cai onde deve.
        ///
        /// **Se a tarefa nao existir, isto nao faz nada** e o lava-loica volta a
        /// fechar o passo sozinho. Um passo curto e melhor do que um passo
        /// impossivel, e a nota fica na consola do outro lado.
        /// </summary>
        private void WatchTheWaterTask()
        {
            if (step != Step.DrinkWater || waterDrank) return;

            if (homeTasks == null) homeTasks = FindObjectOfType<HomeTaskDirector>();
            if (homeTasks == null) return;

            if (!string.IsNullOrWhiteSpace(waterTaskId) && homeTasks.IsCompleted(waterTaskId))
                NotifyWaterDrank();
        }

        [Tooltip("A tarefa de doze segundos que fecha o passo da agua. Vazio = fecha "
               + "ao clique, como fazia antes.")]
        [SerializeField] private string waterTaskId = "d2_water";

        [SerializeField] private HomeTaskDirector homeTasks;

        public void NotifyWokeUp()
        {
            if (step == Step.WakeUp) Advance(Step.ReadMessage);
        }

        private void Start()
        {
            // Este componente ja existe, desligado, durante o prologo. O
            // PlayerSleep dessa primeira noite ainda lhe enviava NotifySlept e
            // deixava `slept` armado; ao chegar ao passo Sleep das 02:47, o
            // objectivo concluia-se sozinho. A noite começa sempre limpa aqui.
            routerDone = false;
            conversationDone = false;
            waterDrank = false;
            slept = false;
            sideObjectiveShown = false;

            if (router == null) router = FindObjectOfType<RouterInteractable>();

            // E aqui, e nao no arranque da cena, que a ligacao cai. O componente
            // permanece desligado durante o prologo e o Dia 1, por isso Start so
            // corre quando esta noite comeca.
            router?.SetState(true, false);
            ApplyObjective();
        }

        private void Update()
        {
            WatchTheWaterTask();

            switch (step)
            {
                case Step.ReadMessage:
                    if (phone != null && phone.IsOpen) Advance(Step.RestartRouter);
                    break;

                case Step.RestartRouter:
                    if (routerDone) Advance(Step.TalkToRui);
                    break;

                case Step.TalkToRui:
                    NudgeTowardsRui();
                    if (conversationDone) Advance(Step.DrinkWater);
                    break;

                case Step.DrinkWater:
                    if (waterDrank) Advance(Step.ReturnToRoom);
                    break;

                case Step.ReturnToRoom:
                    if (player != null &&
                        Vector3.Distance(Flat(player.position), Flat(bedroomCentre)) <= bedroomRadius)
                        Advance(Step.LockDoor);
                    break;

                case Step.LockDoor:
                    if (bedroomDoor == null || bedroomDoor.IsLocked) Advance(Step.Sleep);
                    break;

                case Step.Sleep:
                    if (slept) Advance(Step.Finished);
                    break;
            }

            UpdateSideObjective();
        }

        /// <summary>
        /// O cigarro na varanda existia na cena desde sempre e nada apontava para
        /// la: quem nao tropecasse nele nunca sabia que podia fumar. Passa a haver
        /// uma linha por baixo do objectivo, mais apagada, a dizer que da.
        /// </summary>
        private void UpdateSideObjective()
        {
            if (hud == null) return;

            bool offer = routerDone
                      && step < Step.Sleep
                      && optionalTask != null
                      && !optionalTask.HasBeenDone;

            if (offer == sideObjectiveShown) return;
            sideObjectiveShown = offer;
            hud.SetSideObjective(offer ? optionalLabel : string.Empty);
        }

        /// <summary>Chamado pelo <see cref="PlayerSleep"/> quando o dia fecha.</summary>
        public void NotifySlept()
        {
            // Ignora o sono do prologo e qualquer chamada fora da noite que esta
            // quest dirige. O PlayerSleep e partilhado entre as duas noites.
            if (!isActiveAndEnabled || step != Step.Sleep) return;
            slept = true;
            Advance(Step.Finished);
        }

        private void NudgeTowardsRui()
        {
            if (rui == null || player == null || thoughts == null) return;
            float distance = Vector3.Distance(Flat(player.position), Flat(rui.transform.position));
            if (distance <= talkRadius)
                thoughts.Think("quest_rui_close", "He is still up. Of course he is.", 2, true, 2.8f);
        }

        private void Advance(Step next)
        {
            step = next;
            ApplyObjective();
        }

        private void ApplyObjective()
        {
            switch (step)
            {
                case Step.WakeUp:
                    hud?.SetObjective(string.Empty);
                    break;

                case Step.ReadMessage:
                    hud?.SetObjective("OBJECTIVE: Check your phone.  [TAB]");
                    thoughts?.Think("quest_phone", "Someone messaged me. At this hour.", 3, true, 3.0f);
                    break;

                case Step.RestartRouter:
                    hud?.SetObjective("OBJECTIVE: Restart the router at the end of the hall.");
                    thoughts?.Think("quest_router", "The router is by the front door. I can do this half asleep.",
                        2, true, 3.4f);
                    break;

                case Step.TalkToRui:
                    hud?.SetObjective("OBJECTIVE: Talk to Rui.");
                    thoughts?.Think("quest_talk", "I should say something to Rui about the router.", 2, true, 3.2f);
                    break;

                case Step.DrinkWater:
                    hud?.SetObjective("OBJECTIVE: Drink water from the sink.");
                    break;

                case Step.ReturnToRoom:
                    hud?.SetObjective("OBJECTIVE: Go back to your room.");
                    break;

                case Step.LockDoor:
                    hud?.SetObjective("OBJECTIVE: Lock the bedroom door.");
                    // A partir daqui o clique na porta tranca em vez de abrir.
                    bedroomDoor?.SetLockPromptArmed(true);
                    thoughts?.Think("quest_lock", "Lock it. Just in case.", 4, true, 3.2f);
                    break;

                case Step.Sleep:
                    hud?.SetObjective("OBJECTIVE: Turn off the light and sleep.");
                    break;

                case Step.Finished:
                    hud?.SetObjective(string.Empty);
                    hud?.SetSideObjective(string.Empty);
                    break;
            }
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
