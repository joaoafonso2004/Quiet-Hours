using System.Collections.Generic;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.Narrative;
using UnityEngine;
using UnityEngine.AI;

namespace Pungent.NPC
{
    /// <summary>
    /// O Rui na noite em que deixa de fingir.
    ///
    /// Nao substitui o <see cref="PrototypeNpcRoutine"/> — vive ao lado dele. A
    /// rotina e a casa dos dias em que ele cozinha, se senta no sofa e olha de
    /// relance; isto e a casa do Dia 5, e as duas nunca correm ao mesmo tempo (o
    /// <see cref="ClimaxStage"/> desliga a outra). Fundi-las dava uma classe com
    /// dois comportamentos que nao partilham nada a nao ser o corpo.
    ///
    /// A maquina de estados e o subconjunto da seccao 9.2 que este capitulo precisa:
    ///
    ///   Hidden -> AtTheDoor -> Searching -> Pursuing -> Lost -> Searching
    ///
    /// **Ele fala calmamente atraves da porta** (seccao 5). E a coisa mais
    /// importante que este componente faz, e a razao de haver um estado so para
    /// isso: um perseguidor que grita e um monstro, e um monstro nao assusta
    /// ninguem num jogo sobre um colega de casa. O que assusta e o tom de sempre a
    /// dizer coisas que so se sabem de dentro do quarto.
    ///
    /// **Nao ha como venca-lo.** Ser apanhado nao e um fim: e um salto curto para
    /// tras, com ele mais perto e mais rapido da proxima vez (o "checkpoint curto"
    /// da seccao 5). O unico fim deste capitulo esta na porta 3B.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuiHunt : MonoBehaviour
    {
        public enum State
        {
            /// <summary>A espera na lavandaria. O jogador ainda nao sabe que ele esta em casa.</summary>
            Hidden,
            /// <summary>Do outro lado de uma porta, a falar.</summary>
            AtTheDoor,
            /// <summary>A percorrer a casa divisao a divisao.</summary>
            Searching,
            /// <summary>Ve-o. Vai direito a ele.</summary>
            Pursuing,
            /// <summary>Perdeu-o. Vai ao ultimo sitio onde o viu.</summary>
            Lost,
            /// <summary>Apanhou-o. Estado de passagem: o corte a preto ja comecou.</summary>
            Caught
        }

        [Header("Quando comeca")]
        [Tooltip("Acontecimento que o poe a andar. E levantado quando o jogador tira "
               + "as chaves de cima da secretaria dele.")]
        [SerializeField] private string wakeEvent = "rui_awake";

        [Tooltip("Levantado a cada captura. Nenhum passo de capitulo espera por ele "
               + "— existe para o registo, que e o que os finais da seccao 6.4 vao "
               + "precisar de ler quando existirem.")]
        [SerializeField] private string caughtEvent = "caught";

        [Tooltip("Quando isto acontecer, ele para. O jogador saiu.")]
        [SerializeField] private string endEvent = "escaped";

        [Header("Movimento")]
        [SerializeField, Min(0.2f)] private float walkSpeed = 1.15f;
        [SerializeField, Min(0.2f)] private float searchSpeed = 1.6f;
        [SerializeField, Min(0.2f)] private float chaseSpeed = 3.1f;
        [Tooltip("Quanto mais depressa ele anda por cada vez que ja apanhou o "
               + "jogador. E o preco de falhar, e o unico que ha.")]
        [SerializeField, Min(0f)] private float speedPerCatch = 0.35f;
        [SerializeField, Min(30f)] private float turnSpeed = 220f;

        [Header("Percepcao")]
        [SerializeField, Min(1f)] private float sightRange = 11f;
        [SerializeField, Range(20f, 180f)] private float sightAngle = 78f;
        [Tooltip("Distancia a que ouve passos rapidos. Nao ve atraves de paredes, "
               + "mas ouve — e sao os passos do jogador que o trazem.")]
        [SerializeField, Min(1f)] private float hearingRange = 9f;
        [Tooltip("Velocidade a partir da qual o jogador faz barulho a andar.")]
        [SerializeField, Min(0.5f)] private float loudSpeed = 2.2f;
        [Tooltip("Quanto tempo continua a ir atras da ultima posicao depois de o "
               + "perder de vista. Memoria curta de proposito (seccao 9.6): um "
               + "perseguidor que sabe sempre onde estas nao se despista.")]
        [SerializeField, Min(0.5f)] private float memorySeconds = 4.5f;
        [SerializeField, Min(0.3f)] private float catchDistance = 1.15f;

        [Header("A porta")]
        [Tooltip("Onde ele se poe para falar: o corredor, do lado de fora da porta "
               + "do quarto dele.\n\n"
               + "Um ponto a mao e nao geometria a adivinhar de que lado da folha "
               + "fica o corredor. O `keys_found` so pode ser levantado de dentro do "
               + "quarto dele — e onde estao as chaves — por isso sabe-se sempre onde "
               + "o jogador esta neste instante, e um calculo esperto so podia "
               + "acertar menos vezes do que um numero escrito.")]
        [SerializeField] private Vector3 doorwaySpot = new Vector3(-2.45f, 0f, 0.05f);

        [Header("Procura")]
        [Tooltip("As divisoes, por onde ele passa. O primeiro destino nunca e "
               + "escolhido daqui: e sempre o ultimo sitio onde o viu.")]
        [SerializeField] private Transform[] searchPoints = new Transform[0];
        [Tooltip("Tempo parado em cada divisao antes de passar a seguinte.")]
        [SerializeField, Min(0f)] private float lookAroundSeconds = 2.2f;
        [Tooltip("A que distancia de um esconderijo e que da com quem la esta.")]
        [SerializeField, Min(0.5f)] private float hidingCheckDistance = 1.6f;

        [Header("Portas")]
        [Tooltip("As portas da casa. Uma porta fechada tapa a NavMesh; sem isto ele "
               + "ficava do outro lado sem caminho nenhum e a caca acabava ali.")]
        [SerializeField] private DoorDragInteractable[] doors = new DoorDragInteractable[0];
        [SerializeField, Min(0.5f)] private float doorReach = 1.5f;

        [Header("Voz")]
        [Tooltip("O que ele diz atraves da porta. Calmo, e sobre coisas banais.")]
        [SerializeField] private ThoughtLineSet doorLines;
        [Tooltip("O que diz enquanto procura, de divisao em divisao.")]
        [SerializeField] private ThoughtLineSet searchLines;
        [Tooltip("O que diz quando te ve.")]
        [SerializeField] private ThoughtLineSet sightLines;
        [SerializeField] private string speakerName = "Rui";
        [SerializeField, Min(1f)] private float lineHoldSeconds = 3.2f;
        [SerializeField] private NpcMumbleVoice voice;

        [Header("Passos")]
        [SerializeField] private AudioSource footsteps;
        [SerializeField] private AudioClip[] footstepClips = new AudioClip[0];

        [Header("Ser apanhado")]
        [Tooltip("Toca no instante da captura, por cima do corte a preto.\n\n"
               + "**Plano e nao espacial.** Tudo o resto que ele faz — passos, gavetas, "
               + "voz — vem de um sitio da casa e e assim que o jogador o localiza. "
               + "Este nao: no momento em que a mao cai no ombro dele, deixa de haver "
               + "sitio nenhum, e um som que ainda viesse da direita seria informacao "
               + "sobre uma perseguicao que ja acabou.")]
        [SerializeField] private AudioClip caughtClip;
        [SerializeField] private AudioSource caughtSource;
        [SerializeField, Range(0f, 1f)] private float caughtVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.34f;

        [Header("Captura")]
        [Tooltip("Onde ele te deixa. O quarto do Tomas: acordas em casa, com a porta "
               + "que ja nao fecha aberta.")]
        [SerializeField] private Vector3 checkpointPosition = new Vector3(-5.20f, 0.05f, -2.90f);
        [SerializeField] private float checkpointYaw = 90f;
        [Tooltip("Para onde ele se retira depois de te largar. Longe, para haver "
               + "outra vez uma casa entre voces os dois.")]
        [SerializeField] private Vector3 retreatPosition = new Vector3(-4.90f, 0f, 3.70f);

        [Header("Ligacoes")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Animator animator;
        [SerializeField] private HumanoidHeadLook headLook;
        [SerializeField] private Transform player;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private ChapterDirector director;
        [SerializeField] private WorldDialogueController dialogue;
        [SerializeField] private ScreenFade fade;
        [SerializeField] private string speedParameter = "Speed";

        private State state = State.Hidden;
        /// <summary>Quando entrou no estado actual. Publico via <see cref="TimeInState"/>.</summary>
        private float stateEntered;
        private Vector3 lastKnown;
        private float lastSeenAt = -999f;
        private int searchIndex;
        private float arrivedAt = -1f;

        /// <summary>
        /// A primeira busca da noite ainda nao aconteceu. Ver
        /// <see cref="FirstSearchAnchor"/> — a memoria do Dia 3 so conta uma vez, e
        /// a seguir a caca e a de sempre.
        /// </summary>
        private bool firstSearch = true;

        /// <summary>Esconderijos da cena. Procurados uma vez: nao nascem a meio da noite.</summary>
        private Pungent.Interaction.HidingSpot[] allHidingSpots;

        /// <summary>Estados que o controller nao tem. Avisados uma vez cada, ver <see cref="PlayAction"/>.</summary>
        private readonly System.Collections.Generic.HashSet<string> missingStates =
            new System.Collections.Generic.HashSet<string>();

        /// <summary>Quando o ultimo destino foi pedido. Ver <see cref="ArrivedAtDestination"/>.</summary>
        private float destinationSetAt = -1f;

        /// <summary>Quanto tempo o `remainingDistance` fica a valer o do destino anterior.</summary>
        private const float destinationSettleSeconds = 0.3f;
        private float nextStepAt;
        private float nextLineAt;
        private int doorCursor, searchCursor, sightCursor;
        private int doorLinesSaid;
        private int speedHash;
        private float smoothedSpeed;
        private Vector3 previousPosition;
        private Vector3 previousPlayerPosition;
        private bool stopped;

        /// <summary>Quantas vezes ja te apanhou. O preco de falhar acumula.</summary>
        public int Catches { get; private set; }

        public State Current => state;

        /// <summary>Ha quanto tempo esta no estado actual. Util para inspeccionar.</summary>
        public float TimeInState => Time.time - stateEntered;

        private void Awake()
        {
            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (headLook == null) headLook = GetComponentInChildren<HumanoidHeadLook>(true);
            if (voice == null) voice = GetComponentInChildren<NpcMumbleVoice>(true);
            if (director == null) director = FindObjectOfType<ChapterDirector>();
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
            if (fade == null) fade = FindObjectOfType<ScreenFade>();

            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor != null) player = motor.transform;
            }
            if (playerCamera == null && player != null)
                playerCamera = player.GetComponentInChildren<Camera>(true);

            speedHash = Animator.StringToHash(speedParameter);
            previousPosition = transform.position;
            if (player != null) previousPlayerPosition = player.position;

            // Parado ate `rui_awake`. Um agente ligado no `Awake` ja empurra o corpo
            // para a NavMesh, e ele saia do sitio onde a encenacao o pos.
            if (agent != null) agent.enabled = false;
        }

        private void Update()
        {
            UpdateLocomotionSpeed();

            if (stopped) return;

            if (director != null && !string.IsNullOrWhiteSpace(endEvent)
                && director.HasSeen(endEvent))
            {
                Stop();
                return;
            }

            if (state == State.Hidden)
            {
                if (director != null && director.HasSeen(wakeEvent)) Enter(State.AtTheDoor);
                return;
            }

            if (state == State.Caught) return;

            UpdatePerception();
            OpenNearbyDoor();

            switch (state)
            {
                case State.AtTheDoor: UpdateAtTheDoor(); break;
                case State.Searching: UpdateSearching(); break;
                case State.Pursuing: UpdatePursuing(); break;
                case State.Lost: UpdateLost(); break;
            }

            UpdateFootsteps();
            if (headLook != null)
                headLook.SetLookWeight(state == State.Pursuing || state == State.AtTheDoor ? 1f : 0f);
        }

        // ------------------------------------------------------------------
        // Estados
        // ------------------------------------------------------------------

        private void Enter(State next)
        {
            state = next;
            stateEntered = Time.time;
            arrivedAt = -1f;

            switch (next)
            {
                case State.AtTheDoor:
                    // Ele nao aparece: vem ate a porta do quarto onde tu estas e
                    // fica la fora. Ver o NPC a atravessar a casa a andar era ver o
                    // truque; ouvi-lo do outro lado da folha nao tem explicacao.
                    EnableAgent(walkSpeed);
                    SetDestination(doorwaySpot);
                    nextLineAt = Time.time + 1.6f;
                    break;

                // Os tres estados em que ele anda voltam a locomocao.
                //
                // Sem isto, sair do vao da porta deixava-o a atravessar a casa com a
                // pose de quem esta encostado a uma folha, e sair de uma pausa de
                // busca deixava-o a andar de cocoras. O `CrossFade` nao volta
                // sozinho: fica no estado onde foi posto ate alguem o mandar sair.
                case State.Searching:
                    EnableAgent(searchSpeed + Catches * speedPerCatch);
                    PlayAction("Locomotion", 0.2f);
                    searchIndex = NearestSearchPoint(FirstSearchAnchor());
                    GoToSearchPoint();
                    break;

                case State.Pursuing:
                    EnableAgent(chaseSpeed + Catches * speedPerCatch);
                    PlayAction("Locomotion", 0.15f);
                    Say(sightLines, ref sightCursor);
                    break;

                case State.Lost:
                    EnableAgent(searchSpeed + Catches * speedPerCatch);
                    PlayAction("Locomotion", 0.2f);
                    SetDestination(lastKnown);
                    break;
            }
        }

        /// <summary>
        /// Fala tres ou quatro linhas do outro lado da porta e vai-se embora para o
        /// outro extremo da casa.
        ///
        /// Ir-se embora e obrigatorio e nao uma cortesia: ele esta parado no unico
        /// vao do quarto onde o jogador esta, e a seccao 9.6 e clara — nunca
        /// bloquear a unica saida sem alternativa. Fica o tempo das falas, e depois
        /// da o corredor.
        /// </summary>
        private void UpdateAtTheDoor()
        {
            FaceTowards(player != null ? player.position : transform.position, turnSpeed * 0.5f);

            if (ArrivedAtDestination() && !agent.isStopped)
            {
                agent.isStopped = true;

                // Chegou ao vao e vai ficar la a falar. Encostado a porta, e nao
                // parado no meio do corredor com a pose de quem espera o autocarro:
                // a cena inteira depende de o jogador acreditar que ha uma pessoa
                // do outro lado da folha.
                PlayAction("ListenAtDoor", 0.25f);
            }

            if (Time.time < nextLineAt) return;

            // Contado a parte e nao pelo cursor do asset: em `SequentialLastRepeats`
            // o cursor para na ultima linha e nunca chega ao fim da lista, por isso
            // compara-lo com o total dava um Rui a falar pela porta para sempre.
            if (doorLines != null && doorLinesSaid < doorLines.Count)
            {
                if (!Say(doorLines, ref doorCursor)) return;   // ha outra coisa a falar; espera
                doorLinesSaid++;
                nextLineAt = Time.time + lineHoldSeconds + 1.4f;
                return;
            }

            lastKnown = player != null ? player.position : transform.position;
            Enter(State.Searching);
        }

        private void UpdateSearching()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

            if (CheckHidingSpot()) return;

            if (!ArrivedAtDestination()) { arrivedAt = -1f; EnsureMoving(); return; }

            // Chegou a divisao. Olha em volta antes de passar a seguinte — e nesse
            // tempo parado que o jogador consegue atravessar por tras dele.
            //
            // Cronometrado a partir da chegada e nao da entrada no estado: contado
            // desde a entrada, a caminhada ate a primeira divisao ja gastava a pausa
            // toda e ele varria a casa inteira sem nunca parar.
            agent.isStopped = true;
            if (arrivedAt < 0f)
            {
                arrivedAt = Time.time;

                // Chegou agora. E o unico momento em que o jogador o pode
                // atravessar por tras, e por isso tem de se **ver** que ele esta a
                // olhar para outro lado. Ate aqui ficava em pe, parado, com a mesma
                // pose com que cozinha: procurar uma pessoa pela casa e estar a
                // espera do bule liam-se exactamente da mesma maneira.
                //
                // De cocoras se houver um esconderijo ali ao pe: a tensao de estar
                // debaixo da cama depende de ele parecer que verifica.
                PlayAction(HidingSpotNear(transform.position) ? "CrouchLookUnder" : "SearchScan", 0.2f);
            }
            if (Time.time - arrivedAt < lookAroundSeconds) return;

            arrivedAt = -1f;
            agent.isStopped = false;
            PlayAction("Locomotion", 0.2f);
            searchIndex = (searchIndex + 1) % Mathf.Max(1, searchPoints.Length);
            GoToSearchPoint();
            if (Random.value < 0.5f) Say(searchLines, ref searchCursor);
        }

        private void UpdatePursuing()
        {
            if (player == null) return;

            SetDestination(player.position);
            FaceTowards(player.position, turnSpeed);

            if (Vector3.Distance(Flat(transform.position), Flat(player.position)) <= catchDistance)
            {
                Catch();
                return;
            }

            if (Time.time - lastSeenAt >= memorySeconds) Enter(State.Lost);
        }

        private void UpdateLost()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;

            if (CheckHidingSpot()) return;

            if (!ArrivedAtDestination()) { arrivedAt = -1f; EnsureMoving(); return; }

            // Chegou ao ultimo sitio onde o viu e nao esta la ninguem. Fica um
            // instante — e o unico momento em que ele parece nao saber o que fazer.
            if (arrivedAt < 0f) arrivedAt = Time.time;
            if (Time.time - arrivedAt < 1.4f) return;

            arrivedAt = -1f;
            Enter(State.Searching);
        }

        // ------------------------------------------------------------------
        // Percepcao
        // ------------------------------------------------------------------

        private void UpdatePerception()
        {
            if (player == null) return;

            if (CanSeePlayer())
            {
                lastKnown = player.position;
                lastSeenAt = Time.time;
                if (state != State.Pursuing) Enter(State.Pursuing);
                return;
            }

            // Ouvir. Nao da posicao exacta — da a divisao, que e o que a seccao 9.5
            // pede quando diz para nao dar conhecimento perfeito ao NPC.
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                float playerSpeed = Vector3.Distance(Flat(player.position), Flat(previousPlayerPosition)) / dt;
                previousPlayerPosition = player.position;

                if (playerSpeed >= loudSpeed &&
                    Vector3.Distance(transform.position, player.position) <= hearingRange &&
                    state != State.AtTheDoor)
                {
                    lastKnown = player.position + Random.insideUnitSphere * 1.5f;
                    lastKnown.y = player.position.y;
                    if (state == State.Searching) Enter(State.Lost);
                }
            }
        }

        /// <summary>
        /// Linha de vista. Quem esta escondido nao e visto, por muito perto que
        /// esteja — encontrar quem se escondeu e trabalho da procura e nao dos
        /// olhos, senao um esconderijo nao valia nada.
        /// </summary>
        private bool CanSeePlayer()
        {
            if (player == null || HidingSpot.PlayerIsHidden) return false;

            Vector3 eyes = transform.position + Vector3.up * 1.6f;
            Vector3 target = playerCamera != null ? playerCamera.transform.position
                                                  : player.position + Vector3.up * 1.6f;
            Vector3 toPlayer = target - eyes;

            if (toPlayer.magnitude > sightRange) return false;
            if (Vector3.Angle(transform.forward, toPlayer) > sightAngle * 0.5f) return false;

            // `QueryTriggerInteraction.Ignore`: os volumes de acontecimento e os
            // esconderijos sao triggers espalhados pela casa e tapavam-lhe a vista.
            if (Physics.Raycast(eyes, toPlayer.normalized, out RaycastHit hit,
                    toPlayer.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.GetComponentInParent<CharacterController>() == null) return false;
            }

            return true;
        }

        /// <summary>
        /// Da com quem se escondeu, se passar mesmo por cima do esconderijo.
        ///
        /// So durante a procura. Enquanto persegue nao pode: o jogador que se
        /// enfia num roupeiro com ele em cima seria apanhado na mesma, e nesse caso
        /// esconder-se nao era uma decisao, era uma armadilha.
        /// </summary>
        /// <returns>Verdadeiro se deu com ele. Quem chama tem de sair do Update: a
        /// partir daqui o corte a preto ja comecou e o resto do estado deixou de
        /// fazer sentido.</returns>
        private bool CheckHidingSpot()
        {
            var spot = HidingSpot.Occupied;
            if (spot == null) return false;
            if (Vector3.Distance(Flat(transform.position), Flat(spot.SearchPoint)) > hidingCheckDistance)
                return false;

            spot.Leave();
            Catch();
            return true;
        }

        // ------------------------------------------------------------------
        // Captura
        // ------------------------------------------------------------------

        private void Catch()
        {
            if (state == State.Caught) return;

            state = State.Caught;
            stateEntered = Time.time;
            Catches++;
            director = ChapterDirector.Resolve(director);
            director?.Notify(caughtEvent);

            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            dialogue?.Cancel();

            // **Antes do corte, e nao depois.** O ecra fecha em pouco mais de meio
            // segundo; um som que so entrasse com o preto ja chegava a um jogador que
            // percebeu o que aconteceu. Isto tem de cair no mesmo instante da mao no
            // ombro — e a seguir sobra-lhe cauda de sobra para atravessar os quatro
            // segundos de escuro e ainda estar a acabar quando ele acorda no quarto.
            //
            // `PlayOneShot` e nao `Play`: uma segunda captura por cima da primeira
            // sobrepoe em vez de cortar, e duas capturas seguidas sao exactamente o
            // momento em que cortar um som soaria a jogo partido.
            if (caughtSource != null && caughtClip != null)
                caughtSource.PlayOneShot(caughtClip, caughtVolume);

            if (fade != null) fade.Blink(0.55f, 2.2f, 2.0f, Restart);
            else Restart();
        }

        /// <summary>
        /// O salto curto para tras. Nao ha ecra de derrota, nao ha menu e nao se
        /// perde nada do que ja se descobriu: acorda-se no quarto, e ele esta outra
        /// vez algures na casa. O que muda e que da proxima vez anda mais depressa.
        /// </summary>
        private void Restart()
        {
            if (player != null)
            {
                var body = player.GetComponent<CharacterController>();
                if (body != null) body.enabled = false;
                player.position = checkpointPosition;
                player.rotation = Quaternion.Euler(0f, checkpointYaw, 0f);
                if (body != null) body.enabled = true;
                previousPlayerPosition = player.position;
            }

            Warp(retreatPosition);
            lastKnown = retreatPosition;
            lastSeenAt = -999f;
            // Nao volta a falar pela porta: aquela fala e a apresentacao, e uma
            // apresentacao nao se repete. Daqui em diante ele so procura.
            Enter(State.Searching);
        }

        // ------------------------------------------------------------------
        // Corpo
        // ------------------------------------------------------------------

        private void Stop()
        {
            stopped = true;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            if (footsteps != null) footsteps.Stop();
        }

        /// <summary>
        /// Chegou ao destino?
        ///
        /// **Esta pergunta estava a ser feita com um numero a martelo, e a resposta
        /// era sempre nao.** O `NavMeshAgent` para sozinho quando fica a
        /// `stoppingDistance` do destino, e neste projecto essa distancia vem da
        /// cena e vale `0.85`. Os tres sitios que perguntavam se ele tinha chegado
        /// exigiam `0.4` ou `0.5`. O agente parava a `0.8`, que e mais do que ambos:
        /// **nunca chegava a lado nenhum.**
        ///
        /// O efeito era o capitulo inteiro. Visto a correr: o Rui saia da
        /// lavandaria, ia ate ao primeiro ponto de busca, parava a oitenta
        /// centimetros dele e ficava ali **para sempre** — `Searching`, agente
        /// ligado, na malha, velocidade zero, `remainingDistance` cravado em 0,8.
        /// Nunca avancava para a divisao seguinte, nunca voltava a procurar, e o
        /// jogador podia sair pela porta 3B sem pressa nenhuma. Nao havia erro na
        /// consola nem nada partido a vista: so uma caca que nao acontece.
        ///
        /// Comparado com a `stoppingDistance` real do agente e nao com uma
        /// constante, porque a constante e exactamente o que ja falhou. Quem mexer
        /// no valor na cena — para ele parar mais longe de uma porta, por exemplo —
        /// nao volta a partir isto sem dar por ela.
        /// </summary>
        private bool ArrivedAtDestination(float slack = 0.15f)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
            if (agent.pathPending) return false;

            // Um destino acabado de pedir ainda nao tem caminho calculado, e o
            // `remainingDistance` desses primeiros frames e o do destino **anterior**
            // — que e zero, porque ele acabou de la chegar. Sem esta espera, o passo
            // seguinte da busca era dado por chegado no instante em que era pedido.
            //
            // E era isso que causava a segunda paragem: dado por chegado, punha
            // `isStopped = true`; no frame seguinte o caminho ficava pronto, a
            // distancia saltava para dois metros, e a partir dai a condicao de
            // chegada nunca mais era verdadeira — **e o unico sitio que voltava a
            // por o agente a andar estava do lado de la dessa condicao.** Ficava
            // parado a olhar para uma divisao a dois metros de distancia, para
            // sempre. Medido: `isStopped=True`, `resta=2,08`, sessenta segundos sem
            // se mexer um centimetro.
            if (Time.time - destinationSetAt < destinationSettleSeconds) return false;

            return agent.remainingDistance <= Mathf.Max(agent.stoppingDistance, 0.1f) + slack;
        }

        /// <summary>
        /// Garante que ele esta a andar.
        ///
        /// A rede de seguranca do problema descrito acima: seja qual for o caminho
        /// que deixe o agente parado a meio de uma travessia, o proximo frame da
        /// busca volta a po-lo a andar. Barato e idempotente — `isStopped` e um
        /// campo, nao um pedido de caminho.
        /// </summary>
        private void EnsureMoving()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh && agent.isStopped)
                agent.isStopped = false;
        }

        private bool EnableAgent(float speed)
        {
            if (agent == null) return false;

            agent.enabled = true;
            if (!agent.isOnNavMesh)
            {
                // Fora da malha: acontece se a encenacao o poe num sitio que a
                // NavMesh nao cobre. Vale mais puxa-lo para a malha do que deixar a
                // caca nao acontecer.
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                    agent.Warp(hit.position);
                if (!agent.isOnNavMesh) { agent.enabled = false; return false; }
            }

            agent.speed = speed;
            agent.angularSpeed = turnSpeed;
            agent.isStopped = false;
            return true;
        }

        private void Warp(Vector3 position)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.Warp(position); return; }
            transform.position = position;
        }

        private void SetDestination(Vector3 position)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                position = hit.position;
            agent.isStopped = false;
            // Ver `ArrivedAtDestination`: no frame em que um destino e pedido, o
            // `remainingDistance` ainda e o do destino anterior — normalmente zero,
            // porque ele acabou de chegar la.
            destinationSetAt = Time.time;
            agent.SetDestination(position);
        }

        /// <summary>
        /// Abre a porta que tem a frente. Uma porta fechada tapa a NavMesh (ver
        /// `DoorDragInteractable`), por isso sem isto ele parava do lado de fora de
        /// metade da casa — e o ruido de uma porta a abrir do outro lado do
        /// corredor e metade da navegacao por som que a seccao 5 pede.
        /// </summary>
        /// <summary>
        /// Toca um estado do controller, se ele existir.
        ///
        /// `CrossFadeInFixedTime` com um nome que nao esta no controller nao da erro
        /// nem excepcao: nao faz absolutamente nada. Como os cinco clips desta caca
        /// so entram depois de alguem correr `Setup Rui Animator`, isso significa que
        /// a diferenca entre "as animacoes estao ligadas" e "nao estao" e silenciosa
        /// — e este projecto ja perdeu tempo de mais com coisas que falham caladas.
        /// Um aviso uma vez chega para nao voltar a acontecer.
        /// </summary>
        private void PlayAction(string state, float blend)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (string.IsNullOrWhiteSpace(state)) return;

            if (!animator.HasState(0, Animator.StringToHash(state)))
            {
                if (missingStates.Add(state))
                    Debug.LogWarning($"[RuiHunt] O controller nao tem o estado '{state}'. " +
                                     "Correr 'Pungent/Blockout/Setup Rui Animator' depois de " +
                                     "por o clip em Assets/ThirdParty/Mixamo/Animations/.");
                return;
            }

            animator.CrossFadeInFixedTime(state, blend);
        }

        /// <summary>
        /// Ha um esconderijo a beira deste sitio?
        ///
        /// Nao serve para o apanhar — disso trata o `CheckHidingSpot`, e o resultado
        /// nao depende de o jogador estar la dentro. Serve so para ele se baixar a
        /// olhar, esteja o esconderijo ocupado ou vazio. Se ele so se baixasse
        /// quando o jogador la esta, o gesto passava a ser um aviso de que foi
        /// encontrado.
        /// </summary>
        private bool HidingSpotNear(Vector3 position)
        {
            if (allHidingSpots == null)
                allHidingSpots = FindObjectsOfType<Pungent.Interaction.HidingSpot>(true);

            foreach (var spot in allHidingSpots)
            {
                if (spot == null) continue;
                if (Vector3.Distance(Flat(position), Flat(spot.SearchPoint)) <= hidingCheckDistance * 1.6f)
                    return true;
            }
            return false;
        }

        private void OpenNearbyDoor()
        {
            // Enquanto fala pela porta, nao. A fala inteira depende de haver uma
            // folha fechada entre os dois: e ela que abafa a voz e que faz o jogador
            // ficar a ouvir sem saber se ele vai entrar. Abri-la ao chegar acabava
            // com a cena antes de a primeira frase sair.
            if (state == State.AtTheDoor) return;

            foreach (var door in doors)
            {
                if (door == null || door.IsOpen || door.IsLocked) continue;
                if (Vector3.Distance(Flat(transform.position), Flat(door.transform.position)) > doorReach) continue;

                // Empurrar ou puxar, decidido pelo lado em que ele esta.
                //
                // A folha roda sempre para o mesmo lado do mundo — o `openAngle` e
                // um valor so, igual nas cinco portas interiores. Mas os nove pontos
                // de busca estao nos quartos e o corredor e a espinha, por isso ele
                // atravessa cada vao **nos dois sentidos** ao longo de um circuito:
                // do corredor para o quarto e do quarto para o corredor. Uma
                // animacao so estava sempre certa em metade das passagens.
                //
                // O produto escalar entre a direccao para onde a folha abre e a
                // direccao em que ele vai da a resposta: a abrir para longe dele,
                // empurra; a abrir para cima dele, puxa.
                Vector3 swing = door.transform.right;
                Vector3 heading = Flat(door.transform.position - transform.position).normalized;
                bool pushing = Vector3.Dot(Flat(swing).normalized, heading) >= 0f;

                PlayAction(pushing ? "OpenDoorPush" : "OpenDoorPull", 0.15f);
                door.ForceOpen();
                return;
            }
        }

        private void UpdateFootsteps()
        {
            if (footsteps == null || footstepClips.Length == 0) return;
            if (smoothedSpeed < 0.35f) return;
            if (Time.time < nextStepAt) return;

            var clip = footstepClips[Random.Range(0, footstepClips.Length)];
            if (clip == null) return;

            footsteps.pitch = Random.Range(0.94f, 1.05f);
            footsteps.PlayOneShot(clip, footstepVolume);
            nextStepAt = Time.time + Mathf.Lerp(0.62f, 0.34f, Mathf.InverseLerp(0.5f, 3.2f, smoothedSpeed));
        }

        /// <summary>
        /// Partilha a fonte e os clips ja ligados na cena com a rotina normal.
        ///
        /// O Rui tem dois donos de movimento em momentos diferentes, mas o som dos
        /// pes e o mesmo. Manter uma segunda lista serializada na rotina deixa-a
        /// inevitavelmente vazia quando o wiring do climax e refeito; esta passagem
        /// conserva uma unica configuracao sem permitir que ambos toquem ao mesmo
        /// tempo (a rotina verifica o estado desta caca antes de tocar).
        /// </summary>
        internal void ShareFootstepAudio(ref AudioSource source, ref AudioClip[] clips)
        {
            if (source == null) source = footsteps;
            if ((clips == null || clips.Length == 0) && footstepClips != null)
                clips = footstepClips;
        }

        /// <summary>
        /// Alimenta o blend tree pelo deslocamento medido e nao pela velocidade do
        /// agente: assim funciona igual quando quem o move e o agente e quando e
        /// uma encenacao. E o mesmo que o `PrototypeNpcRoutine` ja fazia.
        /// </summary>
        private void UpdateLocomotionSpeed()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float instant = Vector3.Distance(Flat(transform.position), Flat(previousPosition)) / dt;
            previousPosition = transform.position;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, instant, 1f - Mathf.Exp(-8f * dt));

            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetFloat(speedHash, smoothedSpeed);
        }

        private void FaceTowards(Vector3 position, float speed)
        {
            Vector3 direction = Flat(position) - Flat(transform.position);
            if (direction.sqrMagnitude < 0.0004f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction.normalized, Vector3.up), speed * Time.deltaTime);
        }

        /// <summary>
        /// Fala. **Sem bloquear as maos do jogador**: uma caixa de dialogo que tira
        /// o controlo a meio de uma perseguicao e uma sentenca de morte, e a fala
        /// dele e ambiente e nao conversa.
        /// </summary>
        private bool Say(ThoughtLineSet lines, ref int cursor)
        {
            if (lines == null || lines.Count == 0 || dialogue == null) return false;
            if (dialogue.IsBusy) return false;

            dialogue.ShowReaction(speakerName, lines.Pick(ref cursor), voice,
                lineHoldSeconds, null, autoClose: true, blocksInteraction: false);
            return true;
        }

        /// <summary>
        /// De onde arranca a busca — e a **unica** coisa que a manha do Dia 3 muda
        /// nesta noite.
        ///
        /// ---
        ///
        /// Normalmente e o ultimo sitio onde ele te viu, e continua a ser em todas
        /// as buscas menos uma. Na primeira, se o <see cref="HidingMemory"/> se
        /// lembrar de um esconderijo e esse esconderijo existir nesta cena, a busca
        /// comeca **por ai**.
        ///
        /// A manha do Dia 3 tem uma pergunta a que o jogador respondeu sem reparar
        /// que estava a responder: quando ele entrou em casa e nao aconteceu nada,
        /// meteste-te nalgum sitio? Isto e a unica vez em que o jogo mostra que
        /// reparou. Ele nao diz nada, nao muda de tom, nao anda mais depressa — vai
        /// so, primeiro, ao sitio errado para toda a gente menos para ti.
        ///
        /// ---
        ///
        /// **Cirurgico de proposito.** Muda um `Vector3` a entrada de um estado, uma
        /// vez por noite. Nao mexe em velocidades, em distancias de paragem, em
        /// `remainingDistance`, nem na ordem do circuito — a busca continua a ser o
        /// mesmo anel pelas nove divisoes, so que entrado por outra porta. Esta caca
        /// ja esteve inerte duas vezes sem dar erro nenhum, e nao e uma nota de cor
        /// que vale uma terceira.
        ///
        /// **Cala-se em tres casos, e em todos eles o comportamento e o de sempre:**
        /// nao ha memoria, o jogador nao se escondeu, ou o nome lembrado nao existe
        /// nesta cena.
        ///
        /// **O sitio mais provavel e o que menos se ve.** O esconderijo que a manha
        /// do Dia 3 costuma registar e o roupeiro, porque a cena acontece com o
        /// jogador dentro do quarto do Rui — e o ponto de busca mais perto do
        /// roupeiro e o desse mesmo quarto, que e onde o jogador esta quando a caca
        /// comeca. Nesse caso isto nao muda nada de visivel, e esta certo: quem se
        /// escondeu longe do sitio onde vai buscar as chaves e que ve a diferenca.
        /// </summary>
        private Vector3 FirstSearchAnchor()
        {
            if (!firstSearch) return lastKnown;
            firstSearch = false;

            var memory = HidingMemory.Resolve();
            if (memory == null || !memory.Remembers) return lastKnown;

            var spot = memory.ResolveSpot();
            return spot != null ? spot.SearchPoint : lastKnown;
        }

        private int NearestSearchPoint(Vector3 near)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < searchPoints.Length; i++)
            {
                if (searchPoints[i] == null) continue;
                float d = Vector3.Distance(Flat(searchPoints[i].position), Flat(near));
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = i;
            }
            return best;
        }

        private void GoToSearchPoint()
        {
            if (searchPoints.Length == 0) return;
            var point = searchPoints[Mathf.Clamp(searchIndex, 0, searchPoints.Length - 1)];
            if (point != null) SetDestination(point.position);
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(Transform[] points, DoorDragInteractable[] houseDoors,
            ThoughtLineSet atDoor, ThoughtLineSet whileSearching, ThoughtLineSet onSight,
            AudioSource stepSource, AudioClip[] steps)
        {
            searchPoints = points;
            doors = houseDoors;
            doorLines = atDoor;
            searchLines = whileSearching;
            sightLines = onSight;
            footsteps = stepSource;
            footstepClips = steps;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.3f, 0.25f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, catchDistance);
            Gizmos.color = new Color(0.9f, 0.6f, 0.2f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, hearingRange);

            if (!Application.isPlaying) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(lastKnown, Vector3.one * 0.35f);
        }
    }
}
