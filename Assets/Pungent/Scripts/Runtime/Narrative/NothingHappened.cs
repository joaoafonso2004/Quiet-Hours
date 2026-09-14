using System.Collections;
using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O Rui chega a casa na manha do Dia 3, com o Tomas dentro do quarto dele.
    /// Vai a casa de banho e volta a sair. **Nao acontece nada.**
    ///
    /// ---
    ///
    /// **O que isto e.** O Dia 3 inteiro assenta na ausencia dele: o
    /// <see cref="DayThreeStage"/> tira-o de casa, deixa a porta do quarto
    /// escancarada e nao aponta objectivo nenhum para la. O jogador entra por
    /// vontade propria, e e nesse instante — dentro do quarto de outra pessoa, sem
    /// desculpa nenhuma — que a chave roda na porta da rua.
    ///
    /// A partir dai a casa cala-se, ouvem-se passos a atravessar o corredor, o
    /// puxador da porta atras da qual ele esta roda... e ele vai a casa de banho.
    /// Passados uns segundos sai, atravessa o corredor outra vez e vai-se embora.
    ///
    /// Nao ha fala, nao ha reaccao, nao ha consequencia. **Nada aconteceu.** O que
    /// fica e o que o jogador fez com os vinte segundos em que achou que ia
    /// acontecer — e isso fica guardado (ver <see cref="HidAt"/>).
    ///
    /// ---
    ///
    /// **Porque e que isto nao e um susto falhado.** A seccao 3.4 do plano proibe o
    /// jogo de viver de jumpscares, e este beat e o argumento contrario: a tensao
    /// toda e construida por coisas banais — uma porta, passos, um puxador — e
    /// resolvida pela explicacao mais banal que existe. Um homem entrou em casa
    /// dele para ir a casa de banho. Nao ha nada de que queixar-se, e e por nao
    /// haver que o jogador fica a pensar nisso.
    ///
    /// **A regra de escrita do projecto ao contrario:** aqui nem sequer ha detalhe
    /// que se repita depois. Ha so a memoria de se ter escondido de nada.
    ///
    /// ---
    ///
    /// **Os esconderijos sao emprestados, e nao criados.**
    ///
    /// Os quatro `HidingSpot` da casa pertencem ao Dia 5 e vivem dentro do
    /// `CLIMAX_CONTENT`, que esta desligado nos outros capitulos de proposito — um
    /// prompt "Hide" debaixo da cama durante o prologo oferece uma mecanica que
    /// ainda nao existe. Este beat pede-os emprestados exactamente como o
    /// <see cref="NPC.DoorConversation"/> pede o Rui emprestado: liga o grupo com
    /// os irmaos calados, usa-os o tempo da cena, e repoe tudo como estava.
    ///
    /// Construir um segundo conjunto de esconderijos era criar um segundo dono para
    /// a mesma coisa, que e a armadilha que este projecto ja pagou com os passos do
    /// Rui.
    ///
    /// **E e aqui que o jogo ensina a mecanica.** A primeira vez que a casa oferece
    /// "Get in the wardrobe" e a unica vez em que esconder-se nao serve para nada —
    /// o que faz do Dia 5, quando servir, uma coisa que o jogador ja sabe fazer e
    /// ja sabe o que custa.
    ///
    /// ---
    ///
    /// **O Rui e emprestado sem NavMesh.** Anda por pontos escritos a mao, com o
    /// agente desligado, pelo padrao do `DoorConversation`. Nao e preguica: as
    /// portas tapam a NavMesh quando estao fechadas e este percurso atravessa duas
    /// delas: com o agente a mandar, a caminhada dependia do estado em que o
    /// jogador tivesse deixado a casa. O que ele faz aqui e encenacao, e encenacao
    /// nao pode falhar por causa de uma porta.
    ///
    /// Volta ao sitio exacto onde estava — que no Dia 3 e o canto fora do mundo
    /// onde o `DayThreeStage` o pos. Sem isto, a casa deixava de estar vazia a meio
    /// da manha e o dia inteiro perdia o pe.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NothingHappened : MonoBehaviour
    {
        private enum Phase { Idle, Waiting, Running, Done }

        [Header("Quando")]
        [Tooltip("Arma a cena. `day_two` e o acontecimento que abre o Dia 3 e poe a "
               + "casa vazia — ver o `DayThreeStage`.")]
        [SerializeField] private string armOnEvent = "day_two";

        [Tooltip("Fecha a janela. `day3_ready` e o instante em que o Rui volta a "
               + "casa; depois disso ele estar la dentro deixa de ser noticia.")]
        [SerializeField] private string silencedByEvent = "day3_ready";

        [Tooltip("Tempo minimo entre a casa ficar vazia e ele poder voltar.\n\n"
               + "Nao e enfeite. Um jogador que va direito ao quarto dele ao "
               + "acordar levava com a chave na porta trinta segundos depois de o "
               + "Rui ter saido, e o que se le nisso e uma armadilha montada pelo "
               + "jogo. A manha tem de ter tido tempo de parecer uma manha.")]
        [SerializeField, Min(0f)] private float armDelaySeconds = 45f;

        [Tooltip("Passado isto sem o jogador entrar no quarto dele, a cena perde-se. "
               + "Perder-se e melhor do que esperar para sempre — e a mesma decisao "
               + "do `DoorConversation`.")]
        [SerializeField, Min(10f)] private float patienceSeconds = 420f;

        [Header("Onde tem de estar o jogador")]
        [Tooltip("Centro do chao do quarto do Rui, em metros do mundo. Escrito pela "
               + "ferramenta a partir das bounds do `Floor_Bedroom_Rui` — nao e um "
               + "numero adivinhado.")]
        [SerializeField] private Vector3 roomCentre = new Vector3(-1.75f, 0f, -3.03f);

        [Tooltip("Meia-medida da divisao em x e z. O y e ignorado.")]
        [SerializeField] private Vector3 roomHalfExtents = new Vector3(1.65f, 3f, 2.23f);

        [Tooltip("Opcional. Se estiver ligado, a cena so corre enquanto ele disser "
               + "que a casa esta mesmo vazia.")]
        [SerializeField] private DayThreeStage stage;

        [Header("Quem")]
        [SerializeField] private GameObject rui;
        [SerializeField] private Animator animator;
        [SerializeField] private string speedParameter = "Speed";

        [Tooltip("Tudo o que conduz o Rui e nao pode correr enquanto a encenacao "
               + "manda no corpo dele. Guardados como estao e repostos no fim: no "
               + "Dia 3 quase todos ja vem desligados pelo `DayThreeStage`, e "
               + "religa-los era devolver a casa um habitante que o dia nao tem.")]
        [SerializeField] private MonoBehaviour[] suppressed = new MonoBehaviour[0];

        [Tooltip("O NavMeshAgent, e o que mais precise de ficar quieto sem ser "
               + "MonoBehaviour.")]
        [SerializeField] private Behaviour[] alsoDisabled = new Behaviour[0];

        [Header("O percurso")]
        [Tooltip("Da entrada ate ao vao do quarto dele. O primeiro ponto e o "
               + "patamar, do lado de fora da porta 3B; o ultimo e onde ele para a "
               + "experimentar o puxador.")]
        [SerializeField] private Vector3[] arrival =
        {
            new Vector3( 6.90f, 0f, -1.90f),
            new Vector3( 5.30f, 0f, -1.90f),
            new Vector3( 3.20f, 0f, -0.60f),
            new Vector3( 1.00f, 0f, -0.05f),
            new Vector3(-0.90f, 0f, -0.05f),
            new Vector3(-2.05f, 0f, -0.30f)
        };

        [Tooltip("Do vao do quarto dele ate dentro da casa de banho.")]
        [SerializeField] private Vector3[] toBathroom =
        {
            new Vector3(-0.90f, 0f, -0.05f),
            new Vector3( 0.90f, 0f, -0.35f),
            new Vector3( 1.15f, 0f, -1.70f)
        };

        [Tooltip("Da casa de banho ate ao patamar, outra vez.")]
        [SerializeField] private Vector3[] departure =
        {
            new Vector3( 0.90f, 0f, -0.35f),
            new Vector3( 3.20f, 0f, -0.60f),
            new Vector3( 5.30f, 0f, -1.90f),
            new Vector3( 6.90f, 0f, -1.90f)
        };

        [Tooltip("Passo de uma pessoa que nao tem pressa nenhuma, que e metade do "
               + "que faz isto ler como banal.")]
        [SerializeField, Min(0.2f)] private float walkSpeed = 1.05f;

        [SerializeField, Min(30f)] private float turnSpeed = 260f;

        [Header("As portas")]
        [Tooltip("A da rua. Vem trancada; ele destranca, entra, fecha, e volta a "
               + "ficar como estava.")]
        [SerializeField] private DoorDragInteractable frontDoor;

        [Tooltip("**A porta 3B e uma porta estatica, e por isso e muda.**\n\n"
               + "Medido a correr: o `staticDoor` dela esta ligado, e nesse caso o "
               + "`ForceOpen` sai *antes* de tocar os clips e o `SetOpen` sai logo a "
               + "entrada. Ou seja, ela abre e fecha na logica do jogo e **nao faz "
               + "barulho nenhum** — e esta cena inteira comeca com o barulho dela.\n\n"
               + "A fonte e os clips sao os da propria porta, emprestados pela "
               + "ferramenta. Nao e um segundo dono: numa porta estatica nenhum dos "
               + "dois caminhos do `DoorDragInteractable` chega a toca-los, portanto "
               + "nao ha ninguem de quem se roubar o som.")]
        [SerializeField] private AudioSource frontDoorSource;
        [SerializeField] private AudioClip[] frontDoorOpening = new AudioClip[0];
        [SerializeField] private AudioClip[] frontDoorClosing = new AudioClip[0];

        [Tooltip("A chave a entrar, antes de a porta abrir. E o primeiro som da cena "
               + "e o unico que nao pode faltar: e ele que diz que ha alguem, e nao "
               + "que ha alguma coisa.")]
        [SerializeField] private AudioClip frontDoorUnlock;
        [SerializeField, Range(0f, 1f)] private float frontDoorVolume = 0.85f;

        [Tooltip("Entre a chave rodar e a porta abrir.")]
        [SerializeField, Min(0f)] private float unlockToOpenSeconds = 0.9f;

        [Tooltip("A do quarto dele — a porta atras da qual o jogador esta.")]
        [SerializeField] private DoorDragInteractable bedroomDoor;

        [SerializeField] private DoorDragInteractable bathroomDoor;

        [Header("Tempos")]
        [Tooltip("Desde os esconderijos aparecerem ate a chave entrar na porta. Sao "
               + "os segundos em que ainda nao aconteceu nada.\n\n"
               + "**Oito e nao dois, e o numero tem uma razao.** O `DEADAIR_RuiRoom` "
               + "cala a casa quando o jogador se aproxima da porta do quarto dele, "
               + "e leva cinco segundos e meio a devolve-la. A chave nao pode rodar "
               + "dentro desse buraco: o silencio dele acabaria a meio dos passos "
               + "e ouvir-se-ia a casa a voltar por cima de uma coisa que estava a "
               + "acontecer. Ver tambem `roomSilence`.")]
        [SerializeField, Min(0f)] private float leadSeconds = 8f;

        [Tooltip("Quanto tempo ele fica parado a porta do quarto **antes** de mexer "
               + "no puxador.")]
        [SerializeField, Min(0f)] private float beforeHandleSeconds = 1.4f;

        [Tooltip("E quanto tempo fica parado **depois**, sem entrar. E o buraco "
               + "inteiro da cena: passado este tempo ele simplesmente vai-se "
               + "embora. Curto de mais e o jogador nao chega a acreditar que a "
               + "porta ia abrir.")]
        [SerializeField, Min(0f)] private float afterHandleSeconds = 2.6f;

        [Tooltip("Quanto tempo passa la dentro. Nao ha nada para ouvir e e essa a "
               + "piada.")]
        [SerializeField, Min(1f)] private float bathroomSeconds = 18f;

        [Tooltip("Se o jogador ainda estiver escondido quando a cena acaba, espera "
               + "isto por ele sair sozinho antes de o tirar de la. Ver a nota do "
               + "`ReturnHidingSpots`.")]
        [SerializeField, Min(1f)] private float hidingGraceSeconds = 60f;

        [Header("Som")]
        [Tooltip("A fonte dos passos dele. Vazio = o filho `Rui_Footsteps`, que ja "
               + "existe e ja tem o passa-baixo das paredes — que aqui e a peca toda: "
               + "o jogador esta fechado num quarto e so pode saber onde ele esta "
               + "pelo som.")]
        [SerializeField] private AudioSource footsteps;
        [SerializeField] private AudioClip[] footstepClips = new AudioClip[0];
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.34f;

        [Tooltip("O puxador a rodar. **Opcional, e por enquanto vazio de proposito.**\n\n"
               + "A folha a estremecer contra o aro (`Rattle`) ja diz o que ha a "
               + "dizer, e um som errado aqui diria de mais: uma chave le-se como "
               + "ele a tentar entrar a serio. Se algum dia houver um "
               + "`door_handle.mp3`, entra aqui.")]
        [SerializeField] private AudioSource handleSource;
        [SerializeField] private AudioClip handleClip;
        [SerializeField, Range(0f, 1f)] private float handleVolume = 0.7f;

        [Header("A mudez da casa")]
        [Tooltip("**A mudez que ja existe, e que esta cena nao pode atropelar.**\n\n"
               + "O `DEADAIR_RuiRoom` esta armado na mesma janela que este beat "
               + "(`day_two` -> `day3_ready`) e dispara por proximidade a porta do "
               + "quarto dele — ou seja, dispara **sempre** um instante antes desta "
               + "cena, porque entrar no quarto e a condicao das duas.\n\n"
               + "Dois `DeadAir` sobrepostos nao sao dois silencios: o segundo grava "
               + "os volumes ja baixados pelo primeiro e repoe a casa a zero **para "
               + "sempre**, sem erro nenhum. Por isso a cena nao comeca antes de "
               + "aquele ter disparado, e o `leadSeconds` cobre o resto.")]
        [SerializeField] private Pungent.Audio.DeadAir roomSilence;

        [Tooltip("Mudez propria, no instante em que a porta da rua abre. **Vazio de "
               + "proposito.**\n\n"
               + "A divisao ja gasta a sua no `roomSilence` uns segundos antes, e "
               + "dois buracos de quatro segundos no mesmo minuto deixam de se ler "
               + "como a casa a prender a respiracao e passam a ler-se como um erro "
               + "de mistura. O silencio ja aconteceu — e nao aconteceu nada nele, "
               + "que e exactamente o assunto desta cena.\n\n"
               + "Fica o campo para quem quiser experimentar o contrario. Preenche-lo "
               + "e seguro **so** por causa da guarda acima.")]
        [SerializeField] private Pungent.Audio.DeadAir deadAir;

        [Header("Os esconderijos emprestados")]
        [Tooltip("O grupo onde eles vivem (`CLIMAX_CONTENT`). Ligado com os irmaos "
               + "calados durante a cena e reposto exactamente como estava. Ver a "
               + "nota da classe.")]
        [SerializeField] private GameObject hidingGroup;

        [Tooltip("Os esconderijos que ficam disponiveis. Tudo o resto dentro do "
               + "grupo fica desligado — o movel das chaves e a saida do Dia 5 nao "
               + "tem nada que fazer numa manha de quinta-feira.")]
        [SerializeField] private HidingSpot[] spots = new HidingSpot[0];

        [Header("Depois")]
        [Tooltip("Quase sempre vazio, e neste beat mais do que em qualquer outro: "
               + "uma linha a dizer o que aconteceu seria o jogo a admitir que "
               + "aconteceu alguma coisa.")]
        [SerializeField, TextArea] private string thought;

        [Tooltip("Vazio por omissao. Nenhum passo de capitulo espera por isto e nao "
               + "deve esperar — uma manha que so acontece a quem entrar no quarto "
               + "dele nao pode trancar o dia a quem nao entrar.")]
        [SerializeField] private string raisesEvent;

        [SerializeField] private ChapterDirector director;
        [SerializeField] private PlayerThoughtDirector thoughts;

        private Phase phase = Phase.Idle;
        private Transform player;
        private Coroutine running;
        private float armedAt;
        private int speedHash;

        // O Rui como estava antes de ser emprestado.
        private Vector3 ruiWasAt;
        private Quaternion ruiWasFacing;
        private bool[] suppressedWas;
        private bool[] alsoDisabledWas;
        private bool borrowedRui;

        // Os esconderijos como estavam antes de serem emprestados.
        private bool groupWas;
        private bool[] siblingsWas;
        private bool borrowedSpots;

        private float nextStepAt;

        /// <summary>
        /// O nome do esconderijo onde o jogador se meteu durante a cena, ou vazio.
        ///
        /// **E o unico produto deste beat.** Nada mais muda: nenhuma variavel do
        /// blackboard, nenhum acontecimento, nenhuma fala. O que fica e isto, e o
        /// que le isto e a noite de Dia 5 — o `RuiHunt` comeca a procurar pelo
        /// sitio onde tu ja te escondeste uma vez.
        ///
        /// Guardado por **nome** e nao por referencia: a cena e recarregada entre o
        /// Dia 3 e o climax e nenhuma referencia sobrevive a isso.
        /// </summary>
        public string HidAt { get; private set; } = string.Empty;

        /// <summary>Escondeu-se em algum momento da cena.</summary>
        public bool PlayerHid => !string.IsNullOrEmpty(HidAt);

        /// <summary>Ja correu (ou ja se perdeu). Util para inspeccionar.</summary>
        public bool IsDone => phase == Phase.Done;

        /// <summary>Esta a correr agora.</summary>
        public bool IsRunning => phase == Phase.Running;

        private void Awake()
        {
            if (rui == null) rui = GameObject.Find("NPC_Rui");
            if (animator == null && rui != null) animator = rui.GetComponentInChildren<Animator>(true);
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            if (stage == null) stage = FindObjectOfType<DayThreeStage>();

            if (footsteps == null && rui != null)
            {
                var found = rui.transform.Find("Rui_Footsteps");
                if (found != null) footsteps = found.GetComponent<AudioSource>();
            }

            // Os clips sao os que ja estao ligados na cena, pedidos ao dono deles.
            // Uma segunda lista serializada neste componente ficava vazia — ou pior,
            // desactualizada — na primeira vez que alguem refizesse o wiring do
            // climax, e passos em falta numa cena que **e** feita de passos nao dao
            // erro nenhum.
            if ((footstepClips == null || footstepClips.Length == 0) && rui != null)
            {
                var hunt = rui.GetComponent<Pungent.NPC.RuiHunt>();
                if (hunt != null) hunt.ShareFootstepAudio(ref footsteps, ref footstepClips);
            }

            speedHash = Animator.StringToHash(speedParameter);
        }

        private void Update()
        {
            switch (phase)
            {
                case Phase.Idle:
                    if (Armed())
                    {
                        phase = Phase.Waiting;
                        armedAt = Time.time;
                    }
                    break;

                case Phase.Waiting:
                    if (!Armed()) { phase = Phase.Done; break; }
                    if (Time.time - armedAt > patienceSeconds) { phase = Phase.Done; break; }
                    if (Time.time - armedAt < armDelaySeconds) break;

                    // A mudez da divisao tem de ter comecado antes desta cena. Ver a
                    // nota do campo: as duas partilham a condicao de entrada e
                    // sobrepo-las apaga a casa para o resto do jogo.
                    if (roomSilence != null && !roomSilence.HasFired) break;

                    if (PlayerIsInHisRoom()) running = StartCoroutine(Run());
                    break;
            }
        }

        /// <summary>
        /// A janela: o Dia 3 comecou, o Rui ainda nao voltou, e a casa esta mesmo
        /// vazia.
        ///
        /// A terceira condicao e redundante com as duas primeiras e existe na
        /// mesma: se algum dia a ordem dos acontecimentos mudar, e melhor esta cena
        /// deixar de acontecer do que acontecer com o Rui em duplicado — um na
        /// cozinha e outro a bater a porta da rua.
        /// </summary>
        private bool Armed()
        {
            director = ChapterDirector.Resolve(director);
            if (director == null) return false;

            if (!string.IsNullOrWhiteSpace(armOnEvent) && !director.HasSeen(armOnEvent)) return false;
            if (!string.IsNullOrWhiteSpace(silencedByEvent) && director.HasSeen(silencedByEvent)) return false;
            if (stage != null && !stage.HouseIsEmpty) return false;

            return true;
        }

        /// <summary>
        /// Dentro do quarto dele, e nao algures na casa.
        ///
        /// Caixa e nao raio: o quarto tem 3,3 por 4,5 metros e um raio que o
        /// cobrisse todo entrava pelo corredor dentro, onde estar nao quer dizer
        /// nada. A caixa sai das bounds do chao — ver a nota do campo.
        /// </summary>
        private bool PlayerIsInHisRoom()
        {
            if (!ResolvePlayer()) return false;

            Vector3 d = player.position - roomCentre;
            return Mathf.Abs(d.x) <= roomHalfExtents.x
                && Mathf.Abs(d.z) <= roomHalfExtents.z;
        }

        private bool ResolvePlayer()
        {
            if (player != null) return true;
            var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
            if (motor == null) return false;
            player = motor.transform;
            return true;
        }

        // ------------------------------------------------------------------
        // A cena
        // ------------------------------------------------------------------

        private IEnumerator Run()
        {
            phase = Phase.Running;

            BorrowHidingSpots();
            BorrowRui();

            // Os esconderijos aparecem antes de haver razao para eles. Sao dois
            // segundos em que a casa esta na mesma e ha um prompt novo numa porta de
            // roupeiro — quem estiver a olhar para la ve-o nascer, e e melhor assim
            // do que ve-lo nascer no mesmo instante em que a chave roda: isso lia-se
            // como o jogo a dizer o que fazer.
            yield return Wait(leadSeconds);

            // ---- a porta da rua ----
            //
            // A chave primeiro, e a porta quase um segundo depois. Sao dois sons e
            // nao um: o que assusta nao e a porta a abrir — e o intervalo entre
            // saber que ha uma chave naquela fechadura e a porta ceder.
            bool frontWasLocked = frontDoor != null && frontDoor.IsLocked;

            PlayFrontDoor(frontDoorUnlock);
            yield return Wait(unlockToOpenSeconds);
            OpenFrontDoor();

            // A casa cala-se agora, e nao com os passos ja a andar: o silencio tem
            // de cair em cima da porta para que a primeira coisa que se ouca nele
            // sejam os pes dele.
            deadAir?.Fire();

            // Fecha atras dele assim que ele esta ca dentro — no ponto a seguir ao
            // patamar, e nao no patamar: fechada com ele ainda la fora, o jogador
            // ouvia a porta bater e mais ninguem entrava.
            yield return WalkAlong(arrival, i => { if (i == 1) CloseFrontDoor(frontWasLocked); });

            // ---- o puxador ----
            //
            // So ha puxador se a porta estiver fechada, e ela pode nao estar: o
            // `DayThreeStage` deixa-a escancarada e ha jogadores que nao a fecham
            // atras de si. Nesse caso ele passa ao lado do vao sem parar, que e a
            // mesma cena com menos volume — e a unica que nao se estraga. Um homem
            // que para a olhar para dentro do proprio quarto, ve la uma pessoa e nao
            // diz nada deixa de ser banal e passa a ser guiao.
            if (bedroomDoor != null && !bedroomDoor.IsOpen)
            {
                // **Virar-se leva tempo, e o `Face` so roda um frame.**
                //
                // Apanhado a correr: com uma chamada so ele chegava ao vao a andar
                // para oeste e ficava parado de lado, virado para o corredor, a
                // mexer no puxador de uma porta que tinha ao ombro. O `Face` e um
                // passo de rotacao por frame — dava-lhe quatro graus e mais nada.
                yield return TurnTo(DoorFacingPoint(bedroomDoor));
                yield return Wait(beforeHandleSeconds);

                if (handleSource != null && handleClip != null)
                    handleSource.PlayOneShot(handleClip, handleVolume);

                bedroomDoor.Rattle();
                yield return Wait(0.5f);
                bedroomDoor.Rattle();

                yield return Wait(afterHandleSeconds);

                // Vira-se antes de arrancar, e nao a andar: um homem que se afasta
                // de uma porta a deslizar de lado por meio metro e a unica coisa
                // desta cena que nao se explica por ele estar distraido.
                if (toBathroom != null && toBathroom.Length > 0)
                    yield return TurnTo(toBathroom[0]);
            }

            // ---- a casa de banho ----
            //
            // A porta abre-se quando ele chega ao vao — o penultimo ponto — e nao
            // depois de ele ja la estar dentro: uma porta fechada tapa a NavMesh e
            // tapa a vista, e ve-lo atravessa-la para depois a abrir era o unico
            // instante desta cena em que o truque se via.
            yield return WalkAlong(toBathroom, i =>
            {
                if (i == toBathroom.Length - 2 && bathroomDoor != null) bathroomDoor.ForceOpen();
            });

            if (bathroomDoor != null)
            {
                yield return Wait(0.4f);
                bathroomDoor.SetOpen(false, true);
            }

            yield return Wait(bathroomSeconds);

            if (bathroomDoor != null)
            {
                bathroomDoor.SetOpen(true, true);
                yield return Wait(0.5f);
            }

            // ---- e vai-se embora ----
            //
            // Sai e deixa a porta da casa de banho aberta atras dele, que e como as
            // pessoas deixam a porta da casa de banho. A da rua abre-se no ponto de
            // dentro e fecha-se no patamar, com ele ja do lado de la.
            yield return WalkAlong(departure, i =>
            {
                if (i == departure.Length - 2) OpenFrontDoor();
                if (i == departure.Length - 1) CloseFrontDoor(frontWasLocked);
            });

            ReturnRui();
            yield return ReturnHidingSpots();

            // O unico produto da cena sai daqui para fora dela.
            //
            // Escrito **depois** do `ReturnHidingSpots` e nao antes: e ele que espera
            // que o jogador saia do esconderijo, e quem entra num segundo sitio no
            // fim da cena escolheu esse. Escrito antes, ficava sempre o primeiro.
            if (PlayerHid) HidingMemory.Resolve()?.Remember(HidAt);

            if (!string.IsNullOrWhiteSpace(thought))
                thoughts?.Think($"nothing_{GetInstanceID()}", thought, 3, true, 3.6f);

            if (!string.IsNullOrWhiteSpace(raisesEvent))
            {
                director = ChapterDirector.Resolve(director);
                director?.Notify(raisesEvent);
            }

            phase = Phase.Done;
            running = null;
        }

        /// <summary>
        /// Espera, sem deixar de reparar onde o jogador se meteu.
        ///
        /// Toda a espera desta cena passa por aqui de proposito: o jogador pode
        /// entrar no roupeiro em qualquer instante entre a chave na porta e ele sair
        /// de casa, e um `WaitForSeconds` a seco perdia metade desses instantes.
        /// </summary>
        private IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                NoteHiding();
                yield return null;
            }
        }

        /// <summary>
        /// Onde o jogador se escondeu, se se escondeu.
        ///
        /// Fica com o **ultimo** sitio e nao com o primeiro: quem sai do roupeiro a
        /// meio e se enfia debaixo da cama tomou a segunda decisao com mais
        /// informacao do que a primeira, e e a segunda que diz alguma coisa sobre
        /// ele.
        /// </summary>
        private void NoteHiding()
        {
            var occupied = HidingSpot.Occupied;
            if (occupied == null) return;
            HidAt = occupied.gameObject.name;
        }

        /// <summary>
        /// Leva-o de ponto em ponto, a pe.
        ///
        /// A rotacao segue o rumo e nao o destino final: numa esquina de corredor a
        /// diferenca entre as duas e um homem que atravessa a casa de lado.
        ///
        /// O <paramref name="onArrive"/> corre depois de cada ponto e e por onde as
        /// portas se abrem e se fecham. E assim e nao com booleanos porque cada porta
        /// tem o **seu** ponto do percurso: fechar a da rua antes de ele entrar ou
        /// abrir a da casa de banho depois de ele ja la estar sao os dois unicos
        /// sitios onde esta cena se ve a acontecer em vez de acontecer.
        /// </summary>
        private IEnumerator WalkAlong(Vector3[] points, System.Action<int> onArrive = null)
        {
            if (rui == null || points == null) yield break;

            for (int i = 0; i < points.Length; i++)
            {
                Vector3 target = points[i];

                while (true)
                {
                    NoteHiding();

                    Vector3 here = rui.transform.position;
                    Vector3 flat = new Vector3(target.x - here.x, 0f, target.z - here.z);
                    float distance = flat.magnitude;
                    if (distance <= 0.05f) break;

                    float step = walkSpeed * Time.deltaTime;
                    if (step >= distance) step = distance;

                    rui.transform.position = here + flat.normalized * step;
                    Face(target);
                    Step();
                    SetLocomotion(walkSpeed);

                    yield return null;
                }

                onArrive?.Invoke(i);
            }

            SetLocomotion(0f);
        }

        // ------------------------------------------------------------------
        // A porta da rua
        // ------------------------------------------------------------------

        /// <summary>
        /// Abre a 3B, e **fa-la ouvir-se**.
        ///
        /// O `ForceOpen` sozinho nao chega: ver a nota do campo `frontDoorSource`. A
        /// porta e estatica, e numa porta estatica o `DoorDragInteractable` sai antes
        /// de tocar o clip. Aqui abre-se o trinco na logica do jogo e toca-se o som a
        /// mao, com a fonte e os clips que sao dela.
        /// </summary>
        private void OpenFrontDoor()
        {
            if (frontDoor != null)
            {
                frontDoor.SetLocked(false);
                frontDoor.ForceOpen();
            }
            PlayFrontDoor(Pick(frontDoorOpening));
        }

        /// <summary>
        /// Fecha-a e devolve-lhe o trinco como estava.
        ///
        /// Trancada outra vez de propriedade: quem der pela porta da rua destrancada
        /// a meio da manha tem uma informacao que esta cena nao lhe quer dar.
        /// </summary>
        private void CloseFrontDoor(bool relock)
        {
            PlayFrontDoor(Pick(frontDoorClosing));
            if (frontDoor == null) return;

            // `ForceClosed` e nao `SetOpen(false)`: o segundo sai a entrada numa
            // porta estatica e deixava-a aberta na logica do jogo para o resto do
            // capitulo — com o vao da NavMesh destapado, que e como um NPC atravessa
            // uma porta que o jogador ve fechada.
            frontDoor.ForceClosed();
            frontDoor.SetLocked(relock);
        }

        private void PlayFrontDoor(AudioClip clip)
        {
            if (frontDoorSource == null || clip == null) return;
            frontDoorSource.PlayOneShot(clip, frontDoorVolume);
        }

        private static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;
            return clips[Random.Range(0, clips.Length)];
        }

        /// <summary>
        /// Vira-se para la, e espera de facto que ele la chegue.
        ///
        /// Com um limite de tempo por cima: uma rotacao que por alguma razao nunca
        /// converge trancava a cena a meio, com o Rui parado no corredor e o dia
        /// sem maneira de continuar. Tres graus chegam — a diferenca entre isso e
        /// zero nao se ve, e esperar por zero e esperar por um `RotateTowards` que
        /// pode passar ao lado do alvo num frame comprido.
        /// </summary>
        private IEnumerator TurnTo(Vector3 target, float maxSeconds = 2f)
        {
            if (rui == null) yield break;

            float until = Time.time + maxSeconds;
            while (Time.time < until)
            {
                NoteHiding();

                Vector3 direction = target - rui.transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.0004f) yield break;
                if (Vector3.Angle(rui.transform.forward, direction.normalized) <= 3f) yield break;

                Face(target);
                yield return null;
            }
        }

        /// <summary>
        /// Para onde se olha quando se olha para uma porta.
        ///
        /// **Nao e o transform dela.** Neste projecto o transform de uma porta esta
        /// na dobradica e a folha estende-se dai para o lado — virar-se para o
        /// transform da porta do quarto do Rui e virar-se para o canto da ombreira,
        /// que fica quarenta e cinco graus fora. A malha e que diz onde a folha
        /// esta.
        /// </summary>
        private static Vector3 DoorFacingPoint(DoorDragInteractable door)
        {
            var mesh = door.GetComponentInChildren<Renderer>(true);
            return mesh != null ? mesh.bounds.center : door.transform.position;
        }

        private void Face(Vector3 target)
        {
            if (rui == null) return;
            Vector3 direction = target - rui.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0004f) return;

            rui.transform.rotation = Quaternion.RotateTowards(rui.transform.rotation,
                Quaternion.LookRotation(direction.normalized, Vector3.up),
                turnSpeed * Time.deltaTime);
        }

        /// <summary>
        /// O `Speed` do blend tree, a mao.
        ///
        /// Quem o alimenta nos dias normais e o `PrototypeNpcRoutine`, e ele esta
        /// desligado durante a cena. Sem isto o Rui atravessava o corredor na pose
        /// parada — e o jogador esta a ouvir passos, por isso a unica coisa que
        /// falharia era exactamente a que ele veria se abrisse a porta.
        /// </summary>
        private void SetLocomotion(float speed)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.SetFloat(speedHash, speed);
        }

        private void Step()
        {
            if (footsteps == null || footstepClips == null || footstepClips.Length == 0) return;
            if (Time.time < nextStepAt) return;

            var clip = footstepClips[Random.Range(0, footstepClips.Length)];
            if (clip == null) return;

            footsteps.pitch = Random.Range(0.94f, 1.05f);
            footsteps.PlayOneShot(clip, footstepVolume);
            nextStepAt = Time.time + Mathf.Lerp(0.62f, 0.34f, Mathf.InverseLerp(0.5f, 3.2f, walkSpeed));
        }

        // ------------------------------------------------------------------
        // Emprestimos
        // ------------------------------------------------------------------

        private void BorrowRui()
        {
            if (rui == null) return;

            ruiWasAt = rui.transform.position;
            ruiWasFacing = rui.transform.rotation;

            suppressedWas = new bool[suppressed.Length];
            for (int i = 0; i < suppressed.Length; i++)
            {
                if (suppressed[i] == null) continue;
                suppressedWas[i] = suppressed[i].enabled;
                suppressed[i].enabled = false;
            }

            alsoDisabledWas = new bool[alsoDisabled.Length];
            for (int i = 0; i < alsoDisabled.Length; i++)
            {
                if (alsoDisabled[i] == null) continue;
                alsoDisabledWas[i] = alsoDisabled[i].enabled;
                alsoDisabled[i].enabled = false;
            }

            borrowedRui = true;

            if (arrival != null && arrival.Length > 0)
            {
                rui.transform.position = arrival[0];
                rui.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            }
        }

        /// <summary>
        /// Devolve o Rui ao sitio exacto e ao estado exacto em que estava.
        ///
        /// **Reposto e nao religado.** No Dia 3 os componentes dele ja vinham
        /// desligados pelo `DayThreeStage` e o sitio dele e um canto fora do mundo;
        /// religar por omissao devolvia a casa um habitante a meio de um dia que
        /// existe por ele nao estar la.
        /// </summary>
        private void ReturnRui()
        {
            if (!borrowedRui) return;
            borrowedRui = false;

            if (rui != null)
            {
                rui.transform.position = ruiWasAt;
                rui.transform.rotation = ruiWasFacing;
            }

            SetLocomotion(0f);

            if (suppressedWas != null)
                for (int i = 0; i < suppressed.Length && i < suppressedWas.Length; i++)
                    if (suppressed[i] != null) suppressed[i].enabled = suppressedWas[i];

            if (alsoDisabledWas != null)
                for (int i = 0; i < alsoDisabled.Length && i < alsoDisabledWas.Length; i++)
                    if (alsoDisabled[i] != null) alsoDisabled[i].enabled = alsoDisabledWas[i];
        }

        /// <summary>
        /// Liga os esconderijos com os irmaos calados.
        ///
        /// O grupo inteiro esta desligado, e ligar o grupo acende tudo o que ha
        /// dentro dele — o movel das chaves do Dia 5, a fechadura da porta 3B, a
        /// secretaria dele, a saida. Nenhum deles tem que fazer numa manha de
        /// quinta-feira, e o `PlayerInteractor` escolhe o alvo pelo colisor que
        /// apanha: bastava um deles vivo para roubar o clique ao roupeiro.
        ///
        /// Por isso guarda-se o estado de **cada** filho, calam-se os que nao sao
        /// esconderijos, e no fim repoe-se tudo um por um.
        /// </summary>
        private void BorrowHidingSpots()
        {
            // **Isto falha em silencio e leva metade da cadeia atras, por isso
            // queixa-se.**
            //
            // As referencias sao para objectos que vivem dentro do `CLIMAX_DAY5`, e
            // o `Wire Climax` destroi e reconstroi essa raiz inteira. Quem correr
            // essa ferramenta depois desta fica com o grupo e os quatro
            // esconderijos a nulo — e entao nao ha prompt nenhum para o jogador,
            // ninguem se pode esconder, o `HidingMemory` nunca e escrito, e a
            // primeira busca do Dia 5 volta ao comportamento de sempre. Quatro
            // coisas a nao acontecer, nenhuma delas com erro.
            //
            // A ordem e: `Wire Climax` primeiro, `Wire Nothing Happened (Day 3)`
            // depois.
            if (hidingGroup == null || spots == null || spots.Length == 0)
            {
                Debug.LogWarning("[NadaAconteceu] Sem esconderijos ligados — a cena corre e nao " +
                                 "ha nada para fazer nela, e o Dia 5 nao herda memoria nenhuma. " +
                                 "Corre 'Pungent/Narrativa/Wire Nothing Happened (Day 3)' " +
                                 "(depois do 'Wire Climax', que reconstroi o grupo onde eles vivem).");
                return;
            }

            var group = hidingGroup.transform;
            groupWas = hidingGroup.activeSelf;
            siblingsWas = new bool[group.childCount];

            for (int i = 0; i < group.childCount; i++)
            {
                var child = group.GetChild(i).gameObject;
                siblingsWas[i] = child.activeSelf;
                if (!IsBorrowedSpot(child)) child.SetActive(false);
                else child.SetActive(true);
            }

            hidingGroup.SetActive(true);
            borrowedSpots = true;
        }

        private bool IsBorrowedSpot(GameObject child)
        {
            foreach (var spot in spots)
                if (spot != null && spot.gameObject == child) return true;
            return false;
        }

        /// <summary>
        /// Repoe o grupo, e **nao com o jogador la dentro**.
        ///
        /// Desligar um `HidingSpot` ocupado limpa o estatico `Occupied` mas nao
        /// devolve o jogador ao mundo: os componentes dele ficam suprimidos, a
        /// camara fica de cocoras, e nao ha nada na cena que volte a liga-los. E um
        /// bloqueio total sem erro nenhum na consola.
        ///
        /// Por isso espera-se que ele saia sozinho — sair e um clique e o prompt
        /// esta la — e so ao fim da paciencia e que se o tira de la a mao.
        /// </summary>
        private IEnumerator ReturnHidingSpots()
        {
            if (!borrowedSpots) yield break;

            float until = Time.time + hidingGraceSeconds;
            while (Time.time < until && IsBorrowedSpot(HidingSpot.Occupied))
            {
                NoteHiding();
                yield return null;
            }

            var stuck = HidingSpot.Occupied;
            if (IsBorrowedSpot(stuck)) stuck.Leave();

            RestoreHidingSpots();
        }

        private bool IsBorrowedSpot(HidingSpot spot)
        {
            return spot != null && IsBorrowedSpot(spot.gameObject);
        }

        private void RestoreHidingSpots()
        {
            if (!borrowedSpots) return;
            borrowedSpots = false;

            if (hidingGroup == null) return;

            var group = hidingGroup.transform;
            if (siblingsWas != null)
                for (int i = 0; i < group.childCount && i < siblingsWas.Length; i++)
                    group.GetChild(i).gameObject.SetActive(siblingsWas[i]);

            hidingGroup.SetActive(groupWas);
        }

        /// <summary>
        /// A rede. Trocar de cena a meio da cena deixava o Rui no corredor, o grupo
        /// do climax meio aceso e o jogador dentro de um roupeiro que ja nao existe.
        /// </summary>
        private void OnDisable()
        {
            if (running != null) StopCoroutine(running);
            running = null;

            var stuck = HidingSpot.Occupied;
            if (IsBorrowedSpot(stuck)) stuck.Leave();

            ReturnRui();
            RestoreHidingSpots();
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(GameObject ruiObject, Animator ruiAnimator,
            MonoBehaviour[] toSuppress, Behaviour[] toDisable,
            Vector3 centre, Vector3 halfExtents, DayThreeStage dayThree,
            DoorDragInteractable front, DoorDragInteractable bedroom, DoorDragInteractable bathroom,
            AudioSource stepSource, AudioClip[] steps, AudioSource handle,
            Pungent.Audio.DeadAir roomDeadAir, GameObject group, HidingSpot[] toBorrow,
            AudioSource frontSource, AudioClip[] frontOpening, AudioClip[] frontClosing,
            AudioClip unlock)
        {
            rui = ruiObject;
            animator = ruiAnimator;
            suppressed = toSuppress;
            alsoDisabled = toDisable;
            roomCentre = centre;
            roomHalfExtents = halfExtents;
            stage = dayThree;
            frontDoor = front;
            bedroomDoor = bedroom;
            bathroomDoor = bathroom;
            footsteps = stepSource;
            footstepClips = steps;
            handleSource = handle;
            roomSilence = roomDeadAir;
            hidingGroup = group;
            spots = toBorrow;
            frontDoorSource = frontSource;
            frontDoorOpening = frontOpening;
            frontDoorClosing = frontClosing;
            frontDoorUnlock = unlock;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            // A divisao onde o jogador tem de estar.
            Gizmos.color = new Color(0.35f, 0.7f, 1f, 0.8f);
            Gizmos.DrawWireCube(roomCentre + Vector3.up * 0.9f,
                new Vector3(roomHalfExtents.x * 2f, 1.8f, roomHalfExtents.z * 2f));

            DrawRoute(arrival, new Color(0.95f, 0.65f, 0.2f, 0.9f));
            DrawRoute(toBathroom, new Color(0.6f, 0.6f, 0.65f, 0.9f));
            DrawRoute(departure, new Color(0.35f, 0.35f, 0.4f, 0.9f));
        }

        private static void DrawRoute(Vector3[] points, Color colour)
        {
            if (points == null || points.Length == 0) return;
            Gizmos.color = colour;
            for (int i = 0; i < points.Length; i++)
            {
                Gizmos.DrawWireSphere(points[i] + Vector3.up * 0.1f, 0.12f);
                if (i > 0) Gizmos.DrawLine(points[i - 1] + Vector3.up * 0.1f,
                                           points[i] + Vector3.up * 0.1f);
            }
        }
    }
}
