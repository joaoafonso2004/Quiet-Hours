using Pungent.Atmosphere;
using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O dia seguinte. Pega no ecra preto que o <see cref="PlayerSleep"/> deixou e
    /// devolve o jogador ao quarto de manha.
    ///
    /// Tudo o que muda o mundo acontece atras das palpebras fechadas: o ceu, a luz,
    /// e o Rui a desaparecer de casa. O jogador nao ve nada a mudar — abre os olhos
    /// e o apartamento e outro. E dessa diferenca que sai a confusao.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayTwoDirector : MonoBehaviour
    {
        [Header("Jogador")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Transform tiltTransform;
        [SerializeField] private MonoBehaviour[] suppressed;
        [SerializeField] private GameObject heldPhone;

        [Header("Ligacoes")]
        [SerializeField] private PlayerSleep sleep;
        [SerializeField] private ScreenEyelid eyelid;
        [SerializeField] private DayCycleController dayCycle;
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private PrototypeHUD hud;
        [SerializeField] private PhoneMessageService messages;

        [Header("A casa de manha")]
        [Tooltip("O Rui sai de casa. Desactivado, nao destruido: ele volta.")]
        [SerializeField] private GameObject rui;
        [Tooltip("Luzes que ficam apagadas de dia.")]
        [SerializeField] private Light[] lightsOffAtDawn;

        [Header("Acordar")]
        [Tooltip("Quanto tempo fica preto antes de os olhos abrirem.")]
        [SerializeField, Min(0f)] private float blackSeconds = 2.2f;
        [Tooltip("Abrir os olhos custa mais do que fecha-los.")]
        [SerializeField, Min(0.3f)] private float eyesOpenSeconds = 3.2f;
        [SerializeField, Min(0.3f)] private float sitUpSeconds = 2.6f;

        [Tooltip("Onde ele fica de pe depois de se levantar da cama.")]
        [SerializeField] private Vector3 standingPosition = new Vector3(-5.20f, 0.05f, -3.20f);
        [SerializeField] private float standingYaw = 0f;
        [SerializeField, Min(0.1f)] private float lyingHeight = 0.62f;

        [Tooltip("Altura do olhar de pe. So e usada quando a rede do `Begin` dispara "
               + "— ou seja, quando quem correu antes deixou o pivo em baixo.")]
        [SerializeField, Min(0.5f)] private float standingEyeHeight = 1.65f;
        [SerializeField] private Vector3 lyingEuler = new Vector3(-62f, 0f, 0f);

        [Header("Evento")]
        [Tooltip("Evento de historia disparado ao acordar. E o que solta as mensagens do Pai.")]
        [SerializeField] private string dayTwoEvent = "day_two";

        [Tooltip("So acorda depois deste acontecimento. Sem ele, este director pegava "
               + "no **primeiro** `DaySlept` — que e o do prologo — e o jogo saltava a "
               + "noite das 02:47 inteira para vir dar a manha seguinte. Nao dava erro "
               + "nenhum: dava um dia a menos.\n\n"
               + "Com o `day_one_done` aqui, a primeira noite entrega ao Dia 1, o Dia 1 "
               + "entrega a noite do router, e e a segunda vez que ele se deita que "
               + "traz esta manha.")]
        [SerializeField] private string requiresEvent = "day_one_done";

        [SerializeField] private ChapterDirector director;

        [Header("Procurar o Rui")]
        [Tooltip("Centro do quarto do Rui. Chegar la e o que fecha o objectivo da manha.")]
        [SerializeField] private Vector3 ruiRoomCentre = new Vector3(-1.75f, 0f, -2.95f);
        [SerializeField, Min(0.5f)] private float ruiRoomRadius = 2.2f;

        private enum Phase { Waiting, Black, Opening, SittingUp, Searching, Done }

        private Phase phase = Phase.Waiting;
        private float phaseTime;
        private Vector3 standingLocalPosition;
        private Quaternion standingLocalRotation;

        /// <summary>Onde o corpo estava quando ele comecou a levantar-se.</summary>
        private Vector3 wokeAt;
        private Quaternion wokeFacing;

        private CharacterController body;
        private bool bodyWas;

        public bool HasWoken => phase == Phase.Searching || phase == Phase.Done;

        private void Awake()
        {
            if (sleep == null) sleep = FindObjectOfType<PlayerSleep>(true);
            if (eyelid == null) eyelid = FindObjectOfType<ScreenEyelid>();
            if (dayCycle == null) dayCycle = FindObjectOfType<DayCycleController>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            if (hud == null) hud = FindObjectOfType<PrototypeHUD>();
            if (messages == null) messages = FindObjectOfType<PhoneMessageService>();

            if (sleep != null) sleep.DaySlept += BeginDayTwo;
        }

        private void OnDestroy()
        {
            if (sleep != null) sleep.DaySlept -= BeginDayTwo;
        }

        /// <summary>
        /// Chamado com o ecra ja preto. Tudo o que muda o mundo acontece aqui:
        /// quando os olhos abrirem, ja esta feito.
        /// </summary>
        private void BeginDayTwo()
        {
            if (phase != Phase.Waiting) return;

            // A noite errada. Ver a nota do `requiresEvent`.
            if (!string.IsNullOrWhiteSpace(requiresEvent))
            {
                if (director == null) director = FindObjectOfType<ChapterDirector>();
                if (director == null || !director.HasSeen(requiresEvent)) return;
            }

            if (cameraPivot != null)
            {
                standingLocalPosition = cameraPivot.localPosition;
                standingLocalRotation = tiltTransform != null
                    ? tiltTransform.localRotation
                    : cameraPivot.localRotation;

                // **A rede: nao guardar uma pose de deitado como sendo a de pe.**
                //
                // Isto guarda a altura actual do olhar para poder voltar a ela no
                // fim. So que quem acabou de correr foi o `PlayerSleep`, que baixa o
                // pivo para se deitar — e durante muito tempo nao o repunha. Este
                // metodo guardava entao 0,62 como sendo "de pe", e o Dia 3 inteiro
                // corria com o Tomas a olhar da altura de uma crianca. Nada na
                // consola dizia porque.
                //
                // O `PlayerSleep` ja devolve o pivo, portanto isto nao devia voltar a
                // acontecer. Fica na mesma, porque o defeito e mudo e o sintoma
                // aparece a tres passos de distancia: se a altura guardada for
                // parecida com a de deitado, e porque alguem nao devolveu o que
                // pediu, e vale mais uma linha na consola do que um dia inteiro
                // torto.
                if (standingLocalPosition.y <= lyingHeight * 1.4f)
                {
                    Debug.LogWarning("[Dia3] A altura do olhar estava em " +
                        standingLocalPosition.y.ToString("F2") + " quando este dia comecou — " +
                        "isso e uma pose de deitado, nao de pe. Alguem nao repos o pivo da " +
                        "camara. Reposto a " + standingEyeHeight.ToString("F2") + ".", this);
                    standingLocalPosition.y = standingEyeHeight;
                    standingLocalRotation = Quaternion.identity;
                }

                // Fica deitado: e desta pose que ele vai acordar.
                Vector3 lying = standingLocalPosition;
                lying.y = lyingHeight;
                cameraPivot.localPosition = lying;
                if (tiltTransform != null)
                    tiltTransform.localRotation = Quaternion.Euler(lyingEuler);
            }

            // **Noite, e nao manha.**
            //
            // Isto chamava `ApplyMorning()` aqui — no instante em que a noite das
            // **02:47** comeca. O cartao dizia 02:47 e a janela mostrava dia; e o
            // `ApplyNight()` nao era chamado por ninguem no jogo inteiro, so pelo
            // climax do Dia 5.
            //
            // Ninguem deu por isso porque nao ha erro possivel aqui: um ceu e um
            // material, e qualquer material serve. So a jogar e que se ve.
            //
            // A manha passa para o <see cref="Finish"/>, que e onde ele se levanta
            // mesmo e onde o `day_two` abre o Dia 3.
            dayCycle?.ApplyNight();

            // O Rui sai. Nao ha bilhete, nao ha mensagem, nao ha explicacao — e essa
            // a questao: de manha ele simplesmente nao esta.
            if (rui != null) rui.SetActive(false);

            if (eyelid != null) eyelid.Amount = 1f;
            if (heldPhone != null) heldPhone.SetActive(false);
            SetSuppressed(true);

            hud?.SetObjective(string.Empty);
            hud?.SetSideObjective(string.Empty);

            // **O `day_two` levanta-se aqui, com o ecra ainda preto.**
            //
            // Estava no `Finish`, depois de o Tomas ja estar de pe — e o cartao
            // `WEDNESDAY 08:14` aparecia por cima do quarto, com o jogador a olhar
            // para ele. Um cartao de capitulo e uma elipse: pertence ao preto, entre
            // uma cena e a seguinte, e nao por cima da que ja comecou.
            //
            // O mesmo marco alimenta dois consumidores — o capitulo "Day 3 - Limits"
            // e o fio do Pai — e por isso sobe **uma vez, num sitio so**. Levantado em
            // dois momentos diferentes era a mesma armadilha de sempre com outra
            // roupa.
            director = ChapterDirector.Resolve(director);
            director?.Notify(dayTwoEvent);
            messages?.RaiseEvent(dayTwoEvent);

            phase = Phase.Black;
            phaseTime = 0f;
        }

        private void Update()
        {
            if (phase == Phase.Waiting || phase == Phase.Done) return;

            // A procura ja e jogo a serio: o jogador anda, olha e usa o telemovel.
            if (phase == Phase.Searching) { UpdateSearching(); return; }

            SetSuppressed(true);
            phaseTime += Time.deltaTime;

            switch (phase)
            {
                case Phase.Black: UpdateBlack(); break;
                case Phase.Opening: UpdateOpening(); break;
                case Phase.SittingUp: UpdateSittingUp(); break;
            }
        }

        private void LateUpdate()
        {
            if (phase == Phase.Black || phase == Phase.Opening || phase == Phase.SittingUp)
                SetSuppressed(true);
        }

        /// <summary>
        /// O objectivo da manha fecha-se por chegar ao quarto do Rui e nao encontrar
        /// nada. Nao ha nada para carregar nem para apanhar: a informacao e a ausencia.
        /// </summary>
        private void UpdateSearching()
        {
            if (player == null) { phase = Phase.Done; return; }

            Vector3 flat = new Vector3(player.position.x, 0f, player.position.z);
            Vector3 room = new Vector3(ruiRoomCentre.x, 0f, ruiRoomCentre.z);
            if (Vector3.Distance(flat, room) > ruiRoomRadius) return;

            phase = Phase.Done;
            hud?.SetObjective(string.Empty);
            thoughts?.Think("day2_empty", "His bed has not been touched. He never went to sleep.",
                5, true, 3.8f);
        }

        private void UpdateBlack()
        {
            if (phaseTime < blackSeconds) return;
            phase = Phase.Opening;
            phaseTime = 0f;
        }

        /// <summary>
        /// Os olhos abrem devagar e com hesitacao: duas tentativas antes de ficarem
        /// abertos. Fechar foi cansaco; abrir e custar a sair de la.
        /// </summary>
        private void UpdateOpening()
        {
            float t = Mathf.Clamp01(phaseTime / eyesOpenSeconds);
            float hesitation = Mathf.Sin(t * Mathf.PI * 2.5f) * 0.22f * (1f - t);
            if (eyelid != null) eyelid.Amount = Mathf.Clamp01(1f - t + Mathf.Max(0f, hesitation));

            if (t < 1f) return;

            if (eyelid != null) eyelid.Amount = 0f;
            phase = Phase.SittingUp;
            phaseTime = 0f;

            // Onde ele estava deitado, guardado no instante em que se comeca a
            // levantar. Ver `UpdateSittingUp`: e daqui que o movimento parte, para
            // poder chegar ao fim em vez de se aproximar dele.
            if (player != null)
            {
                wokeAt = player.position;
                wokeFacing = player.rotation;

                // Mesma razao que no deitar: com o `CharacterController` ligado, as
                // escritas no transform sao corrigidas pela fisica e ele levanta-se
                // da cama travado nela. Desligado durante o levantar, religado no
                // `Finish`.
                body = player.GetComponent<CharacterController>();
                if (body != null) { bodyWas = body.enabled; body.enabled = false; }
            }

            thoughts?.Think("day2_wake", "Morning. I did not think I would actually sleep.",
                5, true, 3.2f);
        }

        private void UpdateSittingUp()
        {
            float t = Mathf.Clamp01(phaseTime / sitUpSeconds);
            float eased = t * t * (3f - 2f * t);

            if (cameraPivot != null)
            {
                Vector3 lying = standingLocalPosition;
                lying.y = lyingHeight;
                cameraPivot.localPosition = Vector3.Lerp(lying, standingLocalPosition, eased);
            }

            if (tiltTransform != null)
                tiltTransform.localRotation = Quaternion.Slerp(
                    Quaternion.Euler(lyingEuler), standingLocalRotation, eased);

            // **Do sitio onde ele dormiu ate ao lado da cama, e chega la.**
            //
            // O mesmo defeito que o deitar tinha: interpolar a partir da posicao
            // actual todos os frames depende da cadencia de frames e **nunca chega
            // ao destino**, so se aproxima. Ele acabava de pe algures ao lado do
            // sitio certo, com uma curva diferente em cada maquina — e e o primeiro
            // movimento do dia, o que faz dele o pior sitio do jogo para se ter uma
            // curva a sorte.
            if (player != null)
            {
                player.position = Vector3.Lerp(wokeAt, standingPosition, eased);
                player.rotation = Quaternion.Slerp(wokeFacing,
                    Quaternion.Euler(0f, standingYaw, 0f), eased);
            }

            if (t < 1f) return;

            Finish();
        }

        private void Finish()
        {
            phase = Phase.Searching;

            // O amanhecer acontece aqui, e nao no arranque da noite. As luzes que se
            // apagam "ao amanhecer" apagavam-se as 02:47, que e precisamente quando o
            // jogador precisa delas.
            dayCycle?.ApplyMorning();

            if (lightsOffAtDawn != null)
                foreach (var light in lightsOffAtDawn)
                    if (light != null) light.enabled = false;

            if (cameraPivot != null) cameraPivot.localPosition = standingLocalPosition;
            if (tiltTransform != null) tiltTransform.localRotation = standingLocalRotation;
            if (player != null)
            {
                player.position = standingPosition;
                player.rotation = Quaternion.Euler(0f, standingYaw, 0f);

                // O mesmo que a abertura a secretaria: rodar o corpo nao roda o
                // olhar. Sem isto ele levanta-se da cama virado para onde estava a
                // olhar quando adormeceu — que e o tecto.
                player.GetComponent<Pungent.Player.CameraPhysics>()?.AlignToBody();
            }

            // Depois da ultima escrita no transform, e nao antes: religa-lo primeiro
            // fazia a fisica corrigir o sitio final, que e o defeito de que este dia
            // acabou de sair.
            if (body != null) body.enabled = bodyWas;

            if (heldPhone != null) heldPhone.SetActive(true);
            SetSuppressed(false);

            // O `day_two` ja subiu no `Begin`, com o ecra preto — ver a nota la.
            //
            // **E o objectivo deixou de ser escrito aqui.** Este metodo punha
            // "OBJECTIVE: See if Rui is up." por cima do que o capitulo tinha acabado
            // de escrever — o primeiro passo do `CH_Day3_Limits` diz "Check the
            // apartment.". Duas coisas a escrever no mesmo sitio, e ganhava a ultima:
            // o jogador lia um objectivo e o jogo esperava por outro.
            //
            // O capitulo e o dono do objectivo. Este director trata do corpo.
            thoughts?.Think("day2_quiet", "It is quiet. He is usually banging around by now.",
                4, true, 3.4f);
        }

        private void SetSuppressed(bool value)
        {
            if (suppressed == null) return;
            foreach (var component in suppressed)
                if (component != null) component.enabled = !value;
        }
    }
}
