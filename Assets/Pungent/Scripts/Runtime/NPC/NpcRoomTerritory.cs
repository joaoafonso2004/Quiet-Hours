using Pungent.Dialogue;
using Pungent.Narrative;
using UnityEngine;

namespace Pungent.NPC
{
    /// <summary>
    /// Marca um espaco como territorio de um NPC. Quando o jogador entra, o NPC
    /// acorre, encara-o e manda-o sair; so retoma o circuito quando o jogador sair.
    ///
    /// A presenca e verificada por sondagem das bounds do BoxCollider em vez de
    /// eventos de trigger: o jogador pode ser teleportado por checkpoints e o
    /// CharacterController nem sempre gera OnTriggerExit de forma fiavel.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class NpcRoomTerritory : MonoBehaviour
    {
        [SerializeField] private PrototypeNpcRoutine owner;
        [SerializeField] private Transform player;
        [SerializeField] private WorldDialogueController dialogue;
        [SerializeField] private NpcMumbleVoice voice;

        [Header("Falas")]
        [SerializeField] private string speaker = "Rui";
        [Tooltip("Ditas por ordem; a ultima repete-se se o jogador insistir.")]
        [SerializeField, TextArea]
        private string[] warnings =
        {
            "Hey. Out. This is my room.",
            "I said out. Now.",
            "Do not make me ask again."
        };
        [SerializeField, Min(0.8f)] private float lineSeconds = 2.6f;
        [Tooltip("Intervalo minimo entre avisos enquanto o jogador nao sai.")]
        [SerializeField, Min(1f)] private float repeatCooldown = 5f;
        [Tooltip("Distancia a que o NPC comeca a falar depois de chegar ao intruso.")]
        [SerializeField, Min(0.5f)] private float speakDistance = 2.6f;

        [Header("Verificacao")]
        [Tooltip("Altura, acima da base do jogador, usada para testar a presenca.")]
        [SerializeField, Min(0f)] private float sampleHeight = 0.9f;

        [Header("Stinger & Camera Lock")]
        [SerializeField] private AudioSource stingerSource;
        [SerializeField] private AudioClip stingerClip;
        [SerializeField] private float lockDuration = 1.2f;

        [Header("Trancar em vez de expulsar")]
        [Tooltip("Quantas ENTRADAS antes de ele desistir de pedir e simplesmente "
               + "trancar a porta.\n\n0 desliga isto.\n\nÉ a versão do dia um: ele "
               + "não te toca, resolve o problema à maneira dele, e o que fica é a "
               + "informação de que ele tranca portas — que é a coisa que mais "
               + "tarde vais desejar saber fazer.")]
        [SerializeField, Min(0)] private int locksAfterEntries = 2;

        [SerializeField, TextArea] private string lockingLine = "That's it. I'm locking the door.";

        [Tooltip("Pensamento do Tomás depois de a fechadura estalar.")]
        [SerializeField, TextArea] private string lockedThought = "What is he hiding?";

        [Header("Expulsao")]
        [Tooltip("Quantos avisos ele da antes de deixar de pedir. Passado esse "
               + "numero, tira o jogador do quarto ele proprio.")]
        [SerializeField, Min(1)] private int warningsBeforeEviction = 4;
        [Tooltip("Para onde o jogador acorda: fora da porta, virado para ela.")]
        [SerializeField] private Transform evictionSpot;
        [SerializeField] private Vector3 evictionFallbackPosition = new Vector3(-1.95f, 0.05f, -0.20f);
        [SerializeField] private float evictionFallbackYaw = 180f;
        [Tooltip("Quanto sobe a ameaca oculta por ter sido preciso chegar aqui.")]
        [SerializeField, Min(0)] private int evictionThreat = 2;
        [SerializeField, TextArea] private string evictionLine = "I warned you.";
        [Tooltip("Distancia a que ele fecha para bater.\n\n"
               + "Nao confundir com o `evictStopDistance` do PrototypeNpcRoutine "
               + "(1.8), que e a distancia de **encarar**. Enquanto so avisa, ele "
               + "para a dois metros e ate recua se ficar mais perto; a partir do "
               + "momento em que decide expulsar, essa regra e substituida por esta.")]
        [SerializeField, Min(0.4f)] private float strikeDistance = 0.95f;

        [Tooltip("Distancia a que a sequencia considera que ele chegou. Uma folga "
               + "pequena por cima da de bater.")]
        [SerializeField, Min(0.5f)] private float grabDistance = 1.15f;
        [Tooltip("Rede de seguranca: se ele nao chegar (NavMesh presa, porta), fecha na mesma.")]
        [SerializeField, Min(1f)] private float approachTimeout = 6f;
        [SerializeField, TextArea]
        private string evictionThought = "I did not even see him cross the room.";

        [Header("Pontape")]
        [Tooltip("Estado do Animator tocado quando ele chega ao pe do jogador. "
               + "Ver RuiAnimatorSetup.Actions.")]
        [SerializeField] private string kickState = "RoomKick";
        [Tooltip("Rede de seguranca se o Animator nao souber dizer a duracao do estado.")]
        [SerializeField, Min(0.2f)] private float kickFallbackSeconds = 2.2f;
        [Tooltip("Quanto tempo o ecra demora a fechar a preto.\n\n"
               + "**Tem de ser o mesmo numero que o `Blink` usa**, porque e com ele "
               + "que se calcula o instante de comecar a fechar. Dois numeros para a "
               + "mesma coisa era o preto a chegar cedo ou tarde sem ninguem "
               + "perceber qual dos dois mexer.")]
        [SerializeField, Range(0.02f, 0.4f)] private float fadeToBlackSeconds = 0.06f;
        [Tooltip("O que o jogador diz no instante em que o ecra vai a preto.")]
        [SerializeField] private string kickText = "Ouch";
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private ScreenFade fade;
        [Tooltip("Porta do quarto, para ficar fechada depois de ele te por fora.")]
        [SerializeField] private Pungent.Interaction.DoorDragInteractable roomDoor;

        private BoxCollider volume;
        private bool intruderInside;
        private int warningIndex;
        private float nextLineTime;
        private Coroutine lockCoroutine;
        private bool evicting;

        /// <summary>A camara ja o agarrou uma vez nesta invasao. Ver `TrySpeak`.</summary>
        private bool cameraLocked;
        private int entriesSoFar;
        private bool roomLocked;
        private bool lockPending;

        /// <summary>
        /// Consulta usada por encenacoes que suspendem a rotina normal do NPC mas
        /// continuam a querer a resposta territorial.
        /// </summary>
        public bool ContainsPlayer(Transform target)
        {
            if (target == null) return false;
            if (volume == null) volume = GetComponent<BoxCollider>();
            if (volume == null) return false;
            return volume.bounds.Contains(target.position + Vector3.up * sampleHeight);
        }

        private void Awake()
        {
            volume = GetComponent<BoxCollider>();
            volume.isTrigger = true;

            if (player == null)
            {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }

            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
            if (owner != null && voice == null) voice = owner.GetComponentInChildren<NpcMumbleVoice>(true);

            if (player != null)
            {
                if (thoughts == null) thoughts = player.GetComponentInParent<PlayerThoughtDirector>();
                if (fade == null)
                {
                    // Vive no jogador: o canvas tem de acompanhar a camara.
                    Transform root = player.root != null ? player.root : player;
                    fade = root.GetComponent<ScreenFade>();
                    if (fade == null) fade = root.gameObject.AddComponent<ScreenFade>();
                }
            }
        }

        private void Update()
        {
            if (owner == null || player == null || volume == null || evicting) return;

            bool nowInside = ContainsPlayer(player);

            if (nowInside != intruderInside)
            {
                intruderInside = nowInside;
                owner.SetIntruderPresent(nowInside, player);

                // Insistir em estar onde nao se deve conta tanto como responder mal
                // (§6.2). Levantado como acontecimento e nao somado a mao para o
                // registo passar pelo mesmo sitio por onde tudo o resto passa.
                if (nowInside)
                {
                    Pungent.Narrative.NarrativeBlackboard.Instance?.OnEvent("rui_room_entered");
                    entriesSoFar++;
                }

                if (!nowInside)
                    nextLineTime = 0f; // o proximo aviso e imediato se voltar a entrar
            }

            // Voltar a entrar depois de ter sido mandado sair decide a porta para o
            // resto do dia — mas so decide. Ele vem na mesma, avisa na mesma, e a
            // fechadura so estala quando o quarto estiver vazio. Ver `LockRoomWhenEmpty`.
            if (nowInside && locksAfterEntries > 0 && entriesSoFar >= locksAfterEntries)
                lockPending = true;

            if (!intruderInside) return;

            OpenDoorIfShutOut();

            float distance = Vector3.Distance(owner.transform.position, player.position);
            if (distance <= speakDistance)
                TrySpeak();
        }

        /// <summary>
        /// Se o intruso se fechou la dentro, ele abre a porta.
        ///
        /// Desde que as portas passaram a tapar o vao na NavMesh, uma porta fechada
        /// deixou de ser atravessavel — o que esta certo, mas deixava-o parado do
        /// outro lado sem caminho, e a expulsao nunca chegava a acontecer. Abrir a
        /// porta e o que uma pessoa faria, e vale mais como momento do que ve-lo
        /// aparecer ja dentro do quarto.
        /// </summary>
        private void OpenDoorIfShutOut()
        {
            if (roomDoor == null || roomDoor.IsOpen) return;

            // So se ele estiver do lado de fora: se ja esta dentro, a porta fechada
            // atras dele e exactamente o que se quer.
            Vector3 ownerSample = owner.transform.position + Vector3.up * sampleHeight;
            if (volume.bounds.Contains(ownerSample)) return;

            roomDoor.ForceOpen();
        }

        private void TrySpeak()
        {
            if (dialogue == null || Time.unscaledTime < nextLineTime) return;
            if (warnings == null || warnings.Length == 0) return;
            if (dialogue.IsBusy) return;

            // Ele nao pede indefinidamente. Passados os avisos, resolve o assunto.
            if (warningIndex >= warningsBeforeEviction) { BeginEviction(); return; }

            nextLineTime = Time.unscaledTime + repeatCooldown;

            // Na visita em que a porta fica decidida, ele diz outra coisa. A frase
            // anuncia a fechadura; a fechadura so acontece quando o quarto esvaziar.
            string line = lockPending && !string.IsNullOrWhiteSpace(lockingLine)
                ? lockingLine
                : warnings[Mathf.Min(warningIndex, warnings.Length - 1)];

            // **A camara so o agarra quando ele estiver mesmo a vista.**
            //
            // Isto disparava so por distancia. Como a distancia atravessa paredes e
            // o quarto dele e pequeno, o primeiro aviso apanhava-o muitas vezes do
            // outro lado da parede: a camara puxava a cabeca do jogador contra
            // alcatifa e tijolo, com o sting a tocar em cima, a apontar para um
            // homem que ainda nao tinha aparecido. O momento e ve-lo **no vao** — e
            // se o jogador nao o pode ver, nao ha nada para lhe mostrar.
            //
            // O aviso em si continua a sair na mesma. Ouvir a voz dele antes de o
            // ver e o que se quer; o que nao se quer e a camara a virar-se para uma
            // parede. Nao esta preso ao primeiro aviso mas ao primeiro em que ele
            // seja visivel — senao, quem o ouvisse pela parede perdia o momento
            // para sempre, e passar a ver o Rui a chegar ao vao nao fazia nada.
            if (!cameraLocked && CanPlayerSeeOwner())
            {
                cameraLocked = true;
                TriggerCameraLock();
            }

            warningIndex++;
            dialogue.ShowReaction(speaker, line, voice, lineSeconds, null);

            if (lockPending && !roomLocked)
            {
                roomLocked = true;
                StartCoroutine(LockRoomWhenEmpty());
            }
        }

        /// <summary>
        /// A consequencia de insistir.
        ///
        /// Nao ha combate nem game over: ele atravessa o quarto, o ecra fecha, e o
        /// jogador reaparece no corredor com a porta fechada. O que fica nao e uma
        /// punicao mecanica — e teres perdido o controlo do teu proprio corpo
        /// durante um segundo, e ele saber que pode fazer isso. A ameaca sobe e nao
        /// volta a descer.
        /// </summary>
        private void BeginEviction()
        {
            if (evicting) return;
            evicting = true;
            StartCoroutine(EvictionSequence());
        }

        /// <summary>
        /// Toca o pontape e devolve o controlo **no instante exacto de comecar a
        /// fechar o ecra** — nem antes, nem depois.
        ///
        /// ---
        ///
        /// **O que estava mal, e eram duas coisas em fila.**
        ///
        /// O corte caia aos 55% do clip e, no mesmo sitio, o Rui era mandado de
        /// volta para `Locomotion`. So que o `Blink` do chamador **ainda nao tinha
        /// comecado**: entre o `CrossFade` e o primeiro frame preto passavam os 0,06
        /// s do fecho mais o frame em que a corotina devolve. Nesses frames via-se o
        /// murro a ser interrompido a meio e o corpo a saltar para a pose de andar —
        /// que a jogar se le como o Rui a congelar a meio da animacao. O comentario
        /// ao lado do `CrossFade` dizia "isto acontece atras do ecra preto"; nao
        /// acontecia, e por isso e que ninguem foi procurar mais longe.
        ///
        /// Agora sao duas responsabilidades separadas: isto espera, e quem chama
        /// fecha o ecra e so **depois** de estar preto e que devolve o Rui ao
        /// circuito.
        ///
        /// ---
        ///
        /// **O instante.** O preto tem de estar fechado um frame antes de o clip
        /// acabar — nunca se chega a ver a ultima pose, que num clip sem loop fica
        /// congelada. Portanto comeca-se a fechar a `fadeToBlackSeconds + um frame`
        /// do fim, e a conta e feita com o `deltaTime` do proprio frame para
        /// acompanhar seja qual for a taxa a que o jogo esta a correr.
        ///
        /// A duracao vem do Animator e nao de um numero escrito a mao: trocar o clip
        /// por outro mais curto ou mais longo nao deve obrigar a reafinar nada. So
        /// se o Animator nao souber responder e que se usa a rede de seguranca.
        /// </summary>
        private System.Collections.IEnumerator PlayKick(Pungent.Player.CameraPhysics camera)
        {
            Animator animator = owner != null ? owner.Animator : null;
            if (animator == null || animator.runtimeAnimatorController == null ||
                string.IsNullOrWhiteSpace(kickState))
                yield break;

            int kickHash = Animator.StringToHash(kickState);
            animator.CrossFadeInFixedTime(kickHash, 0.08f);

            // Um frame nao chega necessariamente: durante o crossfade o current
            // ainda pode ser Locomotion. Esperar pelo hash certo impede que a
            // duracao do pontape seja lida do loop de caminhada.
            float enterDeadline = Time.time + 0.3f;
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            while (Time.time < enterDeadline)
            {
                info = animator.IsInTransition(0)
                    ? animator.GetNextAnimatorStateInfo(0)
                    : animator.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash == kickHash) break;
                yield return null;
            }

            float length = info.shortNameHash == kickHash ? info.length : kickFallbackSeconds;
            if (length <= 0.05f || float.IsInfinity(length)) length = kickFallbackSeconds;

            // Espera enquanto ainda houver clip suficiente para fechar o ecra **e**
            // sobrar um frame. Quando deixar de haver, e agora.
            float elapsed = 0f;
            while (elapsed + fadeToBlackSeconds + Time.deltaTime < length)
            {
                if (camera != null && owner != null)
                    camera.ForceLookAt(GetOwnerLookPoint());
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// Devolve o Rui ao circuito. **So se chama com o ecra ja preto.**
        ///
        /// O `RoomKick` nao tem loop e nada o substituia: quando a expulsao acabava,
        /// o Rui retomava o circuito preso na ultima pose do clip e
        /// atravessava a casa a andar em posicao de murro.
        /// </summary>
        private void ReleaseKickPose()
        {
            Animator animator = owner != null ? owner.Animator : null;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.CrossFadeInFixedTime("Locomotion", 0.15f);
        }

        private System.Collections.IEnumerator EvictionSequence()
        {
            var input = player.GetComponentInParent<Pungent.Player.PlayerInputReader>();
            var camera = player.GetComponentInChildren<Pungent.Player.CameraPhysics>();
            var body = player.GetComponentInParent<CharacterController>();

            input?.SetMoveSuppressed(true);
            input?.SetLookSuppressed(true);

            dialogue?.Cancel();
            if (!string.IsNullOrWhiteSpace(evictionLine))
                dialogue?.ShowReaction(speaker, evictionLine, voice, 1.1f, null);

            if (stingerSource != null && stingerClip != null)
                stingerSource.PlayOneShot(stingerClip, 1f);

            // Daqui para a frente ele deixa de encarar e passa a fechar. Sem isto o
            // `FaceIntruder` mantinha-o a 1,8 m e afastava-o se ficasse a menos de
            // 1,2 — o murro acontecia a dois metros de distancia, no ar.
            owner.BeginStrikeApproach(strikeDistance);

            // O jogador fica preso a olhar para ele enquanto ele atravessa o quarto.
            // A parte que assusta e a aproximacao — ve-lo aproximar-se sem poder
            // recuar —, nao o corte. Por isso o ecra so fecha quando ele chega
            // mesmo ao pe, e nao ao fim de um tempo fixo.
            float elapsed = 0f;
            while (elapsed < approachTimeout)
            {
                if (camera != null && owner.isActiveAndEnabled)
                    camera.ForceLookAt(GetOwnerLookPoint());

                float gap = Vector3.Distance(
                    new Vector3(owner.transform.position.x, 0f, owner.transform.position.z),
                    new Vector3(player.position.x, 0f, player.position.z));
                if (owner.HasReachedIntruder() || gap <= grabDistance) break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            // A fala fica no ecra ate o jogador carregar, e daqui para a frente ele
            // nao vai carregar em nada: sai antes do corte.
            dialogue?.Cancel();
            owner.RaiseThreat(evictionThreat);

            // O pontape. A camara continua presa nele: a coisa acontece-te de frente
            // e nao ha para onde olhar.
            yield return PlayKick(camera);

            bool moved = false;
            // Corte seco, nao um fade: o preto tem de chegar com o impacto. Um
            // desvanecer de meio segundo transformava um pontape numa despedida.
            if (!string.IsNullOrWhiteSpace(kickText))
                thoughts?.Think("rui_kick", kickText, 6, false, 2.0f);

            yield return fade.Blink(fadeToBlackSeconds, 0.85f, 0.8f, () =>
            {
                Vector3 spot = evictionSpot != null ? evictionSpot.position : evictionFallbackPosition;
                float yaw = evictionSpot != null ? evictionSpot.eulerAngles.y : evictionFallbackYaw;

                Transform root = player.root != null ? player.root : player;
                if (body != null) body.enabled = false;
                root.position = spot;
                root.rotation = Quaternion.Euler(0f, yaw, 0f);
                if (body != null) body.enabled = true;

                // Aqui dentro **e** que o ecra esta preto. O Rui sai da pose do
                // murro no mesmo instante em que o jogador e posto no corredor, e
                // nenhuma das duas coisas se ve.
                ReleaseKickPose();

                moved = true;
            });

            // Volta a distancia de encarar. Deixar a de bater aberta punha-o a andar
            // colado ao jogador pela casa toda daqui em diante, o que e outro
            // comportamento e nao foi o que ninguem pediu.
            owner.EndStrikeApproach();

            input?.SetMoveSuppressed(false);
            input?.SetLookSuppressed(false);

            if (moved && thoughts != null && !string.IsNullOrWhiteSpace(evictionThought))
                thoughts.Think("rui_eviction", evictionThought, 5, false, 3.4f);

            // A porta so fecha depois de ele sair. Fechar durante o escuro parecia
            // melhor — ele poe-te fora e bate-te com a porta na cara — mas ele ainda
            // estava dentro do quarto e a seguir voltava para a cozinha: como as
            // portas nao sao NavMeshObstacle, o caminho ignora-as e via-se o Rui a
            // atravessar a porta fechada.
            if (roomDoor != null) StartCoroutine(CloseDoorWhenOwnerLeaves());

            // Recomeca a contar: voltar a entrar volta a dar avisos, mas ele ja
            // esta mais alto na escala de ameaca.
            warningIndex = 0;
            nextLineTime = Time.unscaledTime + repeatCooldown;
            evicting = false;

            // Voltar a entrar volta a dar direito ao momento. Sem isto, quem fosse
            // expulso uma vez nunca mais via a camara agarra-lo — e a segunda vez e
            // que devia ser pior, nao mais fraca.
            cameraLocked = false;
        }

        /// <summary>
        /// Ha linha de vista limpa entre os olhos do jogador e a cabeca dele.
        ///
        /// Nao testa angulo de proposito: o jogador pode estar de costas quando ele
        /// aparece ao vao, e e precisamente esse o momento que a camara existe para
        /// nao deixar perder. O que se exige e que **nao haja parede pelo meio** —
        /// virar a cabeca de alguem a forca para um tijolo nao assusta, confunde.
        ///
        /// `QueryTriggerInteraction.Ignore` pela mesma razao que o `RuiHunt`: a casa
        /// esta cheia de volumes de acontecimento e de esconderijos, todos triggers,
        /// e qualquer um deles contava como parede.
        /// </summary>
        private bool CanPlayerSeeOwner()
        {
            if (player == null || owner == null || !owner.isActiveAndEnabled) return false;

            // Os olhos do jogador, e nao os pes: a linha rasteira batia em moveis
            // que a cabeca ve por cima.
            var eyes = player.GetComponentInChildren<Camera>();
            Vector3 from = eyes != null ? eyes.transform.position : player.position + Vector3.up * 1.6f;

            // A raiz do HumanoidHeadLook e a raiz do modelo, ao nivel dos pes; nao
            // e o osso da cabeca. Apontar o raio para esse Transform fazia-o sair
            // dos olhos para baixo e bater primeiro no CharacterController do
            // proprio jogador. Amostramos o corpo do Rui e ignoramos ambos os
            // corpos, mas continuamos a rejeitar paredes e mobilia pelo meio.
            Transform observerRoot = player.root != null ? player.root : player;
            return PlayerSight.HasLineOfSight(from, owner.transform, 1.85f, ~0,
                observerRoot);
        }

        /// <summary>Ponto visual do Rui, nao a raiz do modelo ao nivel dos pes.</summary>
        private Vector3 GetOwnerLookPoint()
        {
            if (owner == null) return transform.position + Vector3.up * 1.65f;

            Animator animator = owner.Animator;
            if (animator != null && animator.isHuman)
            {
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null) return head.position;
            }

            return owner.transform.position + Vector3.up * 1.65f;
        }

        /// <summary>
        /// Ele desiste de pedir e tranca a porta pelo resto do dia.
        ///
        /// **É a versao do dia um da mesma decisao.** A expulsao a forca — atravessar
        /// o quarto, pegar no jogador, po-lo no corredor — e a mascara a cair, e o
        /// jogo guarda isso para mais tarde. Trancar resolve o mesmo problema sem
        /// lhe tocar, e e pior de outra maneira: nao ha discussao nenhuma, o assunto
        /// acabou porque ele decidiu que acabou.
        ///
        /// E deixa ficar a informacao que interessa. O jogador aprende que aquela
        /// porta tranca e que o Rui tem a chave — que e exactamente o par de factos
        /// que o vai assombrar quando for a **dele** que estiver trancada.
        /// </summary>
        /// <summary>
        /// Volta a pôr o quarto no estado de início de dia.
        ///
        /// **Nada disto se limpa sozinho.** O reboot corre tudo numa cena só, sem
        /// carregamentos entre dias, por isso `entriesSoFar`, o trancar e o próprio
        /// componente desligado sobrevivem de um dia para o outro. Quem construir o
        /// dia seguinte tem de dizer aqui o que quer, e é de propósito que tem de o
        /// dizer: o dia três manda o jogador entrar naquele quarto, e uma porta
        /// trancada no dia um que ninguém se lembrasse de abrir tornava o objectivo
        /// impossível sem um único erro na consola.
        /// </summary>
        /// <param name="armed">Se o Rui volta a vigiar o quarto neste dia.</param>
        /// <param name="unlockDoor">Se a porta volta a abrir.</param>
        public void ResetForNewDay(bool armed, bool unlockDoor)
        {
            entriesSoFar = 0;
            roomLocked = false;
            lockPending = false;
            warningIndex = 0;
            cameraLocked = false;
            nextLineTime = 0f;
            intruderInside = false;
            evicting = false;

            if (unlockDoor && roomDoor != null) roomDoor.SetLocked(false);

            enabled = armed;
        }

        private System.Collections.IEnumerator LockRoomWhenEmpty()
        {
            // **Espera pelos dois.** Fechar a porta com alguem no vao prende quem la
            // esta entre a folha e o aro: o dono ficou preso antes sequer de entrar.
            // A frase ja foi dita; a fechadura e o que acontece depois de o quarto
            // ficar vazio, e essa espera e o momento — ele fica ali a olhar para ti
            // ate saires.
            float waited = 0f;
            const float giveUpAfter = 40f;

            while (waited < giveUpAfter)
            {
                if (owner == null) yield break;

                bool playerOut = !ContainsPlayer(player);
                bool ownerOut = !volume.bounds.Contains(
                    owner.transform.position + Vector3.up * sampleHeight);

                if (playerOut && ownerOut) break;

                waited += Time.deltaTime;
                yield return null;
            }

            // Uma pausa curta para acabarem de atravessar o vao antes de a folha se
            // mexer, senao a porta fecha-se por cima do ombro de alguem.
            yield return new WaitForSeconds(0.6f);

            if (roomDoor != null)
            {
                roomDoor.SetOpen(false, true);
                roomDoor.SetLocked(true);
            }

            if (thoughts != null && !string.IsNullOrWhiteSpace(lockedThought))
                thoughts.Think("rui_room_locked", lockedThought, 3, true, 3.2f);

            Pungent.Narrative.NarrativeBlackboard.Instance?.OnEvent("rui_room_locked");

            // A partir daqui nao ha nada a vigiar: a porta esta trancada.
            enabled = false;
        }

        private void TriggerCameraLock()
        {
            if (player == null) return;
            var input = player.GetComponentInParent<Pungent.Player.PlayerInputReader>();
            var cam = player.GetComponentInChildren<Pungent.Player.CameraPhysics>();
            
            if (input != null && cam != null)
            {
                if (lockCoroutine != null) StopCoroutine(lockCoroutine);
                lockCoroutine = StartCoroutine(LockSequence(input, cam));
            }
        }

        private System.Collections.IEnumerator LockSequence(Pungent.Player.PlayerInputReader input, Pungent.Player.CameraPhysics cam)
        {
            input.SetMoveSuppressed(true);
            input.SetLookSuppressed(true);
            
            if (stingerSource != null && stingerClip != null)
            {
                stingerSource.PlayOneShot(stingerClip, 1f);
            }
            
            float elapsed = 0f;
            while (elapsed < lockDuration)
            {
                if (owner.isActiveAndEnabled)
                {
                    cam.ForceLookAt(GetOwnerLookPoint());
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            input.SetMoveSuppressed(false);
            input.SetLookSuppressed(false);
        }

        /// <summary>
        /// Espera que o dono saia do proprio quarto e so entao fecha a porta.
        /// </summary>
        private System.Collections.IEnumerator CloseDoorWhenOwnerLeaves()
        {
            float waited = 0f;
            const float giveUpAfter = 15f;

            while (waited < giveUpAfter)
            {
                if (owner == null) yield break;

                Vector3 sample = owner.transform.position + Vector3.up * sampleHeight;
                if (!volume.bounds.Contains(sample)) break;

                waited += Time.deltaTime;
                yield return null;
            }

            // Uma pausa curta para ele acabar de atravessar o vao antes de a folha
            // se mexer, senao a porta fecha-se por cima do ombro dele.
            yield return new WaitForSeconds(0.6f);
            roomDoor.ForceClosed();
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider box = volume != null ? volume : GetComponent<BoxCollider>();
            if (box == null) return;

            Gizmos.color = new Color(0.9f, 0.25f, 0.2f, 0.22f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.9f, 0.25f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
