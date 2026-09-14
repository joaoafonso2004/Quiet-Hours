using Pungent.Interaction;
using Pungent.NPC;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// A noite em que o Tomas se mudou.
    ///
    /// O apartamento e o mesmo, mas o estado nao: ha caixas por abrir, as luzes
    /// estao acesas, o Rui esta acordado e a noite das 02:47 ainda nao aconteceu.
    /// Este componente poe a cena nesse estado ao arrancar e, no fim, entrega o
    /// jogo a abertura a secretaria.
    ///
    /// A funcao do prologo, segundo a seccao 5, e ser tutorial sem parecer tutorial
    /// — e e tambem onde o jogador **entrega** a informacao que o vai assustar
    /// depois. Se ele disser ao Rui a que horas sai de manha, a fala da terca-feira
    /// no Dia 2 deixa de ser um susto e passa a ser uma coisa que ele proprio disse.
    /// Se ficar calado, o Rui fica a saber na mesma — e isso e pior.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    [DisallowMultipleComponent]
    public sealed class PrologueStage : MonoBehaviour
    {
        [Header("Adiar o resto do jogo")]
        [SerializeField] private PlayerDeskOpening deskOpening;
        [SerializeField] private IntroTextSequence intro;
        [SerializeField] private OpeningQuestDirector openingQuest;

        [Header("A casa nesta noite")]
        [Tooltip("As caixas por abrir. Desligadas no resto do jogo.")]
        [SerializeField] private GameObject boxes;
        [Tooltip("Acesas: ele acabou de chegar e ainda nao ha rotina nenhuma.")]
        [SerializeField] private Light[] lightsOn;
        [Tooltip("Onde o Tomas comeca: **a entrada**, acabado de chegar, com as tres "
               + "caixas aos pes e o Rui a frente. Comecava ja no quarto, o que "
               + "saltava a unica coisa que o prologo tem para dar — chegar.")]
        [SerializeField] private Vector3 startPosition = new Vector3(5.10f, 0.05f, -2.20f);

        [Tooltip("Virado para dentro de casa: o corredor fica a oeste.")]
        [SerializeField] private float startYaw = 270f;

        [Header("Portas, chaves e router")]
        [SerializeField] private PrologueKeyPickup keyPickup;
        [SerializeField] private DoorDragInteractable balconyDoor;
        [SerializeField] private RouterInteractable router;

        [Header("O Rui")]
        [SerializeField] private GameObject rui;

        [Tooltip("Guiao desta noite: a chave e a pergunta das manhas.")]
        [SerializeField] private Pungent.Dialogue.DialogueSequenceDefinition prologueScript;
        [Tooltip("Guiao das 02:47, reposto no fim do prologo.")]
        [SerializeField] private Pungent.Dialogue.DialogueSequenceDefinition nightScript;
        [Tooltip("A porta do quarto do Tomas, onde ele fica tempo a mais.")]
        [SerializeField] private Vector3 ruiDoorwaySpot = new Vector3(-4.60f, 0f, -1.05f);
        [SerializeField] private float ruiYaw = 180f;

        [Header("Onde ele esta, e quando")]
        [Tooltip("A entrada. E aqui que ele recebe o Tomas e da a chave.")]
        [SerializeField] private Vector3 ruiHallSpot = new Vector3(3.20f, 0f, -1.60f);
        [SerializeField] private float ruiHallYaw = 90f;

        [Tooltip("O corredor, encostado a parede. Ele muda-se para aqui quando o "
               + "Tomas acaba de levar a ultima caixa.")]
        [SerializeField] private Vector3 ruiCorridorSpot = new Vector3(-1.60f, 0f, -0.05f);
        [SerializeField] private float ruiCorridorYaw = 180f;

        [Tooltip("Acontecimento que o passa da entrada para o corredor, depois das caixas.")]
        [SerializeField] private string toCorridorEvent = "prologue_unpacked";

        [Tooltip("Acontecimento que o passa do corredor para o vao, depois da secretaria.")]
        [SerializeField] private string toDoorwayEvent = "prologue_rui_arrives";

        [SerializeField] private ChapterDirector director;
        [SerializeField] private NpcRoomTerritory ruiRoomTerritory;

        private bool finished;
        private bool atCorridor;
        private bool atDoorway;
        private Pungent.NPC.HumanoidHeadLook headLook;
        private PrototypeNpcRoutine ruiRoutine;
        private UnityEngine.AI.NavMeshAgent ruiAgent;
        private bool routineReleased;
        private Transform playerTransform;
        private bool guardingRoom;

        [Tooltip("Quanto a cabeca dele acompanha o jogador enquanto esta parado. "
               + "Um nao chega a ser fixar — e mais ou menos o que uma pessoa faz "
               + "quando repara em ti e nao desvia o olhar.")]
        [SerializeField, Range(0f, 1f)] private float watchWeight = 0.9f;

        public bool IsRunning => !finished;

        private void Awake()
        {
            // Nada do jogo normal arranca. O prologo e que decide quando.
            if (deskOpening != null) deskOpening.enabled = true;
            if (openingQuest != null) openingQuest.enabled = false;
        }

        private void Start()
        {
            // Estes campos nao sao serializados. Com Enter Play Mode sem scene/domain
            // reload, ficam vivos entre testes se nao forem repostos explicitamente.
            finished = false;
            atCorridor = false;
            atDoorway = false;
            guardingRoom = false;

            if (boxes != null) boxes.SetActive(true);
            if (boxes != null)
            {
                foreach (var box in boxes.GetComponentsInChildren<CarryableBox>(true))
                    box.ResetForPrologue();
                foreach (var zone in boxes.GetComponentsInChildren<BoxDropZone>(true))
                    zone.ResetProgress();
                foreach (var contents in boxes.GetComponentsInChildren<BoxContentsTask>(true))
                    contents.ResetTask();
            }
            FindObjectOfType<DeskSetup>(true)?.ResetForPrologue();

            FindObjectOfType<PrologueTracker>(true)?.ResetForPrologue();
            keyPickup?.BeginPrologue();
            balconyDoor?.ForceClosed();
            balconyDoor?.SetLocked(true);
            router?.SetState(false, true);

            foreach (var light in lightsOn)
                if (light != null) light.enabled = true;

            var player = FindObjectOfType<Pungent.Player.PlayerMotor>();
            if (player != null)
            {
                playerTransform = player.transform;
                var body = player.GetComponent<CharacterController>();
                if (body != null) body.enabled = false;
                player.transform.position = startPosition;
                player.transform.rotation = Quaternion.Euler(0f, startYaw, 0f);
                if (body != null) body.enabled = true;
            }

            // O Rui fica parado ao vao. Sem circuito: nesta noite ele nao tem
            // rotina nenhuma, so esta ali.
            if (rui != null)
            {
                ruiRoutine = rui.GetComponent<PrototypeNpcRoutine>();
                if (ruiRoutine != null) ruiRoutine.enabled = false;
                routineReleased = false;

                ruiAgent = rui.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (ruiAgent != null) ruiAgent.enabled = false;

                // Comeca na entrada, que e onde recebe o Tomas.
                rui.transform.position = ruiHallSpot;
                rui.transform.rotation = Quaternion.Euler(0f, ruiHallYaw, 0f);
                rui.SetActive(true);

                // Muda o guiao, nao o componente: entre as duas noites mudam as
                // falas e nao o comportamento.
                var convo = rui.GetComponent<RuiStoryConversation>();
                if (convo != null && prologueScript != null)
                    convo.SetConversation(prologueScript,
                        ignoreQuestGate: true, showAfterThought: false);

                // A cabeca dele segue o jogador enquanto ele esta parado.
                //
                // Quem punha peso no `HumanoidHeadLook` era o `PrototypeNpcRoutine`,
                // e nesta noite ele esta desligado — o Rui ficava a olhar em frente
                // como um manequim. Um homem parado a um canto que **acompanha o
                // jogador com a cabeca** e a coisa mais barata que este jogo tem para
                // dizer que alguem esta a observar, e nao custa uma animacao.
                headLook = rui.GetComponentInChildren<Pungent.NPC.HumanoidHeadLook>(true);
                if (headLook != null && player != null)
                {
                    var eyes = player.GetComponentInChildren<Camera>(true);
                    if (eyes != null) headLook.SetTarget(eyes.transform);
                }
            }

            // O prologo ensina o gesto de trancar antes do primeiro sono. A quest
            // antiga esta desligada, mas a porta e as luzes continuam a ser
            // condicoes fisicas reais da cama.
            var sleep = FindObjectOfType<PlayerSleep>(true);
            sleep?.SetGatesEnabled(true);

            // O texto de enquadramento pertence aqui e nao a noite das 02:47: no
            // Dia 2 ele explicava coisas que o jogador devia ter vivido.
            PlayIntro();
        }

        /// <summary>
        /// Arranca o texto de abertura, e queixa-se se nao conseguir.
        ///
        /// ---
        ///
        /// **Isto era um `intro?.Play()`.** O `playOnStart` do proprio componente
        /// esta desligado — quem manda no texto e o prologo — portanto uma
        /// referencia por ligar nao dava erro nenhum: o jogo comecava sem abertura
        /// e nada na consola dizia porque. Descobre-se a jogar, que e o pior sitio.
        ///
        /// Por isso duas redes. Se a referencia nao estiver ligada, procura-se o
        /// componente na cena, que e o mesmo principio do `ChapterDirector.Resolve`;
        /// e se mesmo assim nao houver, escreve-se. Um aviso na consola custa uma
        /// linha e poupa a busca.
        ///
        /// **E preciso estar activo.** O `Play()` monta a tela e poe `running` a
        /// true, mas o `Update` nunca corre num objecto desligado: o texto ficava
        /// no primeiro frame, parado, sem nunca aparecer nem sair do caminho. Isso
        /// e pior do que nao ter abertura nenhuma, e por isso liga-se aqui.
        /// </summary>
        private void PlayIntro()
        {
            if (intro == null) intro = FindObjectOfType<IntroTextSequence>(true);

            if (intro == null)
            {
                Debug.LogWarning("[Prologue] Sem `IntroTextSequence` na cena — o jogo " +
                                 "comeca sem texto de abertura.", this);
                return;
            }

            if (!intro.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[Prologue] `IntroTextSequence` estava desligado; " +
                                 "ligado para o texto poder correr.", intro);
                intro.gameObject.SetActive(true);
            }

            intro.Play();
        }

        /// <summary>
        /// Avanca o Rui pela casa: entrada, corredor, vao da porta do Tomas.
        ///
        /// **Ele nunca se ve mexer.** Cada mudanca acontece quando o Tomas esta de
        /// costas — depois de pousar a ultima caixa, e depois de montar a secretaria.
        /// E o mesmo principio dos sinais do Dia 3: ver a mudanca acontecer transforma-a
        /// num truque; encontra-la feita e o que assusta.
        ///
        /// Nao ha animacao de caminhada nem NavMesh nisto de proposito. Um NPC a
        /// atravessar o corredor a andar seria uma pessoa a mudar de sitio; um NPC
        /// que ja la esta quando te viras e outra coisa.
        /// </summary>
        private void Update()
        {
            if (finished || rui == null) return;

            // Reafirmado a cada frame: o `HumanoidHeadLook` desvanece o peso sozinho
            // quando ninguem lho pede, e quem lho pedia era a rotina, que esta noite
            // esta desligada.
            headLook?.SetLookWeight(watchWeight);

            director = ChapterDirector.Resolve(director);
            if (director == null) return;

            // Antes desta conversa ele esta a espera do novo inquilino. Depois
            // dela volta a viver a casa: a rotina nao deve ficar suspensa ate ao
            // dia seguinte. Era isso que deixava o Rui parado durante o prologo.
            if (!routineReleased && director.HasSeen("prologue_talked"))
            {
                routineReleased = true;
                ruiRoutine?.ResumeRoutine();
            }

            UpdateRoomGuard();

            // Enquanto acorre ao quarto dele, nenhum beat do prologo o pode
            // teleportar para outro marco da encenacao.
            if (guardingRoom) return;

            if (!atCorridor && director.HasSeen(toCorridorEvent))
            {
                atCorridor = true;
                Move(ruiCorridorSpot, ruiCorridorYaw);
            }
            else if (atCorridor && !atDoorway && director.HasSeen(toDoorwayEvent))
            {
                atDoorway = true;
                Move(ruiDoorwaySpot, ruiYaw);
            }
        }

        /// <summary>
        /// No prologo a rotina domestica esta suspensa, mas o quarto continua a ser
        /// dele. Se o Tomas entrar, liga-se apenas a resposta territorial; ao sair,
        /// o Rui fica onde chegou e a rotina volta a ficar parada.
        /// </summary>
        private void UpdateRoomGuard()
        {
            if (ruiRoomTerritory == null || playerTransform == null || ruiRoutine == null) return;

            bool inside = ruiRoomTerritory.ContainsPlayer(playerTransform);
            if (inside == guardingRoom) return;
            guardingRoom = inside;

            if (inside)
            {
                ruiRoutine.enabled = true;
                ruiRoutine.SetIntruderPresent(true, playerTransform);
                return;
            }

            ruiRoutine.SetIntruderPresent(false, playerTransform);
            if (routineReleased) return;

            ruiRoutine.enabled = false;
            if (ruiAgent != null && ruiAgent.enabled)
            {
                if (ruiAgent.isOnNavMesh)
                {
                    ruiAgent.isStopped = true;
                    ruiAgent.ResetPath();
                }
                ruiAgent.enabled = false;
            }
        }

        private void Move(Vector3 spot, float yaw)
        {
            rui.transform.position = spot;
            rui.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>
        /// Fim do prologo: a casa passa ao estado normal.
        ///
        /// **Nao arranca o capitulo seguinte.** Arrancava — chamava
        /// `deskOpening.Begin()` e ligava a quest das 02:47 — e isso escondia duas
        /// coisas. A primeira e que sao trabalhos diferentes: repor a casa e comecar
        /// outro capitulo nao tem razao nenhuma para viver na mesma chamada. A
        /// segunda so se viu quando o Dia 1 precisou de entrar no meio: entre a
        /// primeira noite e as 02:47 ha um dia inteiro (§5), e com o arranque preso
        /// aqui nao havia sitio onde o por.
        ///
        /// Quem chama isto e quem manda no dia seguinte — hoje o
        /// <see cref="DayOneStage"/>. Enquanto ninguem chamava, nada disto acontecia
        /// e as caixas ficavam na sala para sempre.
        /// </summary>
        public void Finish()
        {
            if (finished) return;
            finished = true;

            // As caixas ja se desligaram uma a uma quando ficaram vazias. O root
            // permanece activo porque contem os destinos; esconder tudo aqui era
            // fazer a casa perder outra vez aquilo que o jogador acabou de pousar.
            keyPickup?.EndPrologue();
            balconyDoor?.SetLocked(false);
            foreach (var light in lightsOn)
                if (light != null) light.enabled = false;

            if (rui != null)
            {
                if (guardingRoom && ruiRoutine != null)
                    ruiRoutine.SetIntruderPresent(false, playerTransform);
                guardingRoom = false;

                var routine = rui.GetComponent<PrototypeNpcRoutine>();
                if (routine != null) routine.ResumeRoutine();

                // Repoe o guiao das 02:47 e o contador de beats: sem isto o Rui
                // chegava a noite seguinte com a conversa do prologo ja terminada e
                // nao dizia nada.
                var convo = rui.GetComponent<RuiStoryConversation>();
                if (convo != null && nightScript != null)
                    convo.SetConversation(nightScript);
            }

            // A partir daqui dormir volta a exigir a porta trancada e a luz apagada.
            var sleep = FindObjectOfType<PlayerSleep>(true);
            if (sleep != null)
            {
                sleep.ResetForNextNight();
                sleep.SetGatesEnabled(true);
            }
        }

        /// <summary>
        /// A abertura a secretaria e a quest da noite das 02:47, para quem as tiver
        /// de arrancar. Estavam no <see cref="Finish"/> e sairam de la — ver a nota.
        /// </summary>
        public PlayerDeskOpening DeskOpening => deskOpening;
        public OpeningQuestDirector OpeningQuest => openingQuest;
    }
}
