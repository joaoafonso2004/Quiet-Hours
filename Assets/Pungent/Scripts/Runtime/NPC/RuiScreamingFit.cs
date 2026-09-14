using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Pungent.Interaction;

namespace Pungent.NPC
{
    /// <summary>
    /// O Rui fecha-se no quarto, grita dez segundos, cala-se dez, e sai como se
    /// nada fosse.
    ///
    /// ---
    ///
    /// **A parte que faz isto funcionar e o silencio, e nao o grito.**
    ///
    /// Dez segundos a gritar sao um acontecimento: o jogador ouve, percebe o que e,
    /// e sabe o que se passa. Sozinho, isso e so ruido alto — assusta uma vez e
    /// explica-se a si proprio. O que nao se explica sao os **dez segundos
    /// seguintes**, com a porta ainda fechada e nada do outro lado. E ai que o
    /// jogador tem de decidir se vai bater a porta, e e ai que percebe que nao sabe
    /// o que faria se ela abrisse.
    ///
    /// Por isso o silencio e tao comprido como o grito, e nao um remate curto.
    ///
    /// ---
    ///
    /// **E por isso e que ele sai normal.** Sair a chorar, ou desalinhado, ou a
    /// pedir desculpa, fecha a pergunta: passa a haver uma explicacao — ele estava
    /// mal — e o jogador arruma aquilo. Sair como quem sai de uma sesta deixa a
    /// pergunta aberta, que e onde este jogo vive. Ver <see cref="RuiDenial"/>: se o
    /// jogador lhe perguntar, ele nao sabe do que e que o outro esta a falar.
    ///
    /// ---
    ///
    /// **A porta e trancada de proposito.** Nao para impedir o jogador de entrar —
    /// para lhe dar uma coisa a fazer com as maos enquanto aquilo dura. Uma porta
    /// que abre transforma isto numa cena com o Rui la dentro; uma porta que nao
    /// abre deixa-o do lado de fora com o barulho, que e o lugar do jogador nesta
    /// historia.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuiScreamingFit : MonoBehaviour
    {
        [Header("Quem")]
        [SerializeField] private Transform npc;
        [SerializeField] private Transform player;
        [SerializeField] private Animator animator;
        [SerializeField] private NavMeshAgent agent;

        [Tooltip("A rotina domestica, calada enquanto isto dura. Dois sistemas a "
               + "escrever destinos dao um Rui a tremer entre dois sitios.")]
        [SerializeField] private MonoBehaviour routineToSuspend;


        [Header("O quarto")]
        [Tooltip("Sitio dentro do quarto dele onde fica enquanto grita.")]
        [SerializeField] private Transform insideRoom;

        [Tooltip("Sitio no corredor para onde ele sai. Vazio = volta a onde estava.")]
        [SerializeField] private Transform outsideRoom;

        [SerializeField] private DoorDragInteractable door;

        [Header("O som")]
        [Tooltip("O grito. **Por preencher** — poe aqui o ficheiro.\n\nSem clip, o "
               + "episodio corre na mesma: ele fecha-se, fica la o tempo todo e "
               + "sai. So nao se ouve nada, e o aviso na consola diz porque.")]
        [SerializeField] private AudioClip screamClip;
        [SerializeField] private AudioSource screamSource;

        [Tooltip("Alto de proposito. Isto atravessa uma porta fechada e a casa "
               + "inteira tem de o ouvir.")]
        [SerializeField, Range(0f, 1f)] private float screamVolume = 0.85f;

        [Tooltip("Ate onde se ouve. A casa tem treze metros na diagonal; isto chega "
               + "a todo o lado e ainda perde forca com a distancia.")]
        [SerializeField, Min(5f)] private float screamRange = 22f;

        [Tooltip("Corte do passa-baixo com a porta fechada. E o que faz a diferenca "
               + "entre um grito **atras de uma porta** e um grito colado a camara.")]
        [SerializeField, Min(200f)] private float mufflingCutoff = 900f;

        [Header("Ritmo")]
        [SerializeField, Min(1f)] private float screamSeconds = 10f;

        [Tooltip("O silencio a seguir. Tao comprido como o grito, e nao um remate: "
               + "e nele que o jogador decide se vai bater a porta.")]
        [SerializeField, Min(1f)] private float silenceSeconds = 10f;

        [Tooltip("Espera minima e maxima entre episodios. Raro: isto e um "
               + "acontecimento, e um acontecimento que se repete e um mecanismo.")]
        [SerializeField] private Vector2 gapSeconds = new Vector2(420f, 900f);

        [Tooltip("Nao comeca com o jogador a menos disto do quarto. Ele nao se "
               + "tranca no quarto com o jogador a olhar para a porta — e ninguem "
               + "quer o episodio a comecar com a cara dele a trinta centimetros.")]
        [SerializeField, Min(0f)] private float playerClearance = 3.5f;

        /// <summary>Esta a decorrer agora.</summary>
        public bool IsScreaming { get; private set; }

        /// <summary>Quando acabou o ultimo episodio. Negativo = nunca houve.</summary>
        public float EndedAt { get; private set; } = -1f;

        /// <summary>
        /// Ha um episodio por confrontar. Ver <see cref="RuiPeeking.HasFreshSighting"/>:
        /// a janela existe para a pergunta ser sobre aquilo que acabou de acontecer.
        /// </summary>
        public bool HasFreshEpisode =>
            EndedAt >= 0f && !confronted && Time.time - EndedAt <= confrontWindow;

        public void MarkConfronted() => confronted = true;

        [SerializeField, Min(10f)] private float confrontWindow = 240f;

        private bool confronted;
        private float nextAttemptAt;
        private Coroutine running;
        private AudioLowPassFilter muffler;
        private bool routineWasEnabled;
        private bool warnedNoClip;

        private void Awake()
        {
            if (npc == null) npc = transform;
            if (animator == null) animator = npc.GetComponentInChildren<Animator>();
            if (agent == null) agent = npc.GetComponent<NavMeshAgent>();
            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor != null) player = motor.transform;
            }

            EnsureSource();
            nextAttemptAt = Time.time + Random.Range(gapSeconds.x, gapSeconds.y);
        }

        /// <summary>
        /// A fonte vive **no quarto** e nao no Rui.
        ///
        /// Presa a ele, seguia-o para fora do quarto quando o episodio acaba, e o
        /// fim do grito andava pelo corredor. Aqui fica onde a cena e.
        /// </summary>
        private void EnsureSource()
        {
            if (screamSource == null)
            {
                Transform where = insideRoom != null ? insideRoom : npc;
                var holder = new GameObject("RUI_Scream");
                holder.transform.SetParent(where, false);
                screamSource = holder.AddComponent<AudioSource>();
            }

            screamSource.playOnAwake = false;
            screamSource.loop = true;
            screamSource.spatialBlend = 1f;
            screamSource.rolloffMode = AudioRolloffMode.Linear;
            screamSource.minDistance = 1.5f;
            screamSource.maxDistance = screamRange;
            screamSource.volume = screamVolume;
            screamSource.dopplerLevel = 0f;

            muffler = screamSource.GetComponent<AudioLowPassFilter>();
            if (muffler == null) muffler = screamSource.gameObject.AddComponent<AudioLowPassFilter>();
            muffler.cutoffFrequency = mufflingCutoff;
        }

        private void Update()
        {
            if (IsScreaming || Time.time < nextAttemptAt) return;
            if (!ConditionsAllow()) { nextAttemptAt = Time.time + 15f; return; }
            running = StartCoroutine(Fit());
        }

        private bool ConditionsAllow()
        {
            if (npc == null || insideRoom == null) return false;
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;

            var routine = routineToSuspend as PrototypeNpcRoutine;
            if (routine != null && routine.InConversation) return false;

            // Nao se tranca no quarto com o jogador em cima da porta.
            if (player != null &&
                Vector3.Distance(player.position, insideRoom.position) < playerClearance)
                return false;

            // **O agente e o corpo tem de estar no mesmo sitio.**
            //
            // Depois de uma encenacao que teleporta o Rui — o `RuiOnTheFloor` poe-no
            // no chao da cozinha e desliga-lhe o agente — o agente pode voltar a ser
            // ligado a achar que esta onde estava antes. `isOnNavMesh` continua a
            // dizer que sim, porque a posicao **interna** dele e valida; o que nao
            // bate certo e o corpo.
            //
            // A partir dai este episodio corre a partir do sitio errado, e foi assim
            // que o grito aconteceu na cozinha em vez de dentro do quarto. Adiar e
            // melhor do que correr torto — e a linha de aviso e o rasto que faltou da
            // primeira vez.
            float drift = Vector3.Distance(agent.nextPosition, npc.position);
            if (drift > 1f)
            {
                Debug.LogWarning($"[Grito] Adiado: o agente esta a {drift:F2} m do corpo. "
                               + "Alguem teleportou o Rui e nao repos o agente.");
                return false;
            }

            return true;
        }

        private IEnumerator Fit()
        {
            IsScreaming = true;
            Suspend(true);

            Vector3 wasAt = npc.position;
            try
            {
                // **A porta abre-se primeiro, e nao por delicadeza.**
                //
                // Uma porta fechada corta a NavMesh — cada uma tem um
                // `NavMeshObstacle` a escavar o vao — e por isso, com a dele
                // fechada, **nao ha caminho nenhum para dentro do quarto do Rui**.
                // Medido: `CalculatePath` da cozinha para o meio do quarto devolve
                // caminho incompleto. Mandar o agente para la sem isto dava-lhe um
                // destino inalcancavel e ele ficava parado no corredor a olhar para
                // a folha, com o episodio inteiro a correr por tras.
                if (door != null && outsideRoom != null)
                    yield return WalkTo(outsideRoom.position);

                if (door != null)
                {
                    door.SetLocked(false);
                    door.ForceOpen();
                    // O `carving` do obstaculo nao desaparece no mesmo frame; sem
                    // esperar, o caminho ainda e calculado contra o vao tapado.
                    yield return WaitForPathTo(insideRoom.position, 2f);
                }

                yield return WalkTo(insideRoom.position);

                // A porta fecha-se e tranca-se. `ForceClosed` antes de trancar: uma
                // folha meio aberta e trancada deixava um vao por onde se via o
                // quarto durante os vinte segundos.
                if (door != null)
                {
                    door.ForceClosed();
                    door.SetLocked(true);
                }

                FaceAwayFromDoor();

                if (screamClip != null)
                {
                    screamSource.clip = screamClip;
                    if (muffler != null) muffler.cutoffFrequency = mufflingCutoff;
                    screamSource.volume = screamVolume;
                    screamSource.Play();
                }
                else if (!warnedNoClip)
                {
                    warnedNoClip = true;
                    Debug.LogWarning("[RuiScreamingFit] Sem clip de grito. O episodio " +
                                     "corre todo — ele fecha-se, fica la e sai — mas em " +
                                     "silencio. Poe o ficheiro no campo `screamClip`.", this);
                }

                yield return new WaitForSeconds(screamSeconds);

                if (screamSource != null && screamSource.isPlaying) screamSource.Stop();

                // O silencio. Nada acontece aqui de proposito: e o unico momento em
                // que o jogo esta a espera do jogador e nao ao contrario.
                yield return new WaitForSeconds(silenceSeconds);

                if (door != null)
                {
                    door.SetLocked(false);
                    door.ForceOpen();
                    yield return WaitForPathTo(
                        outsideRoom != null ? outsideRoom.position : wasAt, 2f);
                }

                EndedAt = Time.time;
                confronted = false;

                Vector3 exit = outsideRoom != null ? outsideRoom.position : wasAt;
                yield return WalkTo(exit);
            }
            finally
            {
                if (screamSource != null && screamSource.isPlaying) screamSource.Stop();
                if (door != null) door.SetLocked(false);
                Restore();
                IsScreaming = false;
                running = null;
                nextAttemptAt = Time.time + Random.Range(gapSeconds.x, gapSeconds.y);
            }
        }

        /// <summary>
        /// Virado para dentro do quarto, de costas para a porta.
        ///
        /// Se o jogador conseguir espreitar por uma fresta — e mais tarde ha uma
        /// cena em que consegue — o que tem de ver e as costas dele. Uma cara e uma
        /// resposta; umas costas continuam a ser uma pergunta.
        /// </summary>
        private void FaceAwayFromDoor()
        {
            if (door == null) return;
            Vector3 away = npc.position - door.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f)
                npc.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        /// <summary>
        /// Espera ate haver caminho ate ali, ou desiste ao fim de `timeout`.
        ///
        /// Uma porta acabada de abrir nao devolve o vao a NavMesh no mesmo frame: o
        /// `NavMeshObstacle` que a representa escava, e a escavacao e recalculada
        /// quando o sistema entender. Perguntar antes disso da caminho incompleto e
        /// manda o Rui para um destino que ainda nao existe.
        /// </summary>
        private IEnumerator WaitForPathTo(Vector3 destination, float timeout)
        {
            var probe = new NavMeshPath();
            float until = Time.time + timeout;
            while (Time.time < until)
            {
                if (NavMesh.CalculatePath(npc.position, destination, NavMesh.AllAreas, probe) &&
                    probe.status == NavMeshPathStatus.PathComplete)
                    yield break;
                yield return null;
            }
        }

        /// <summary>Pela NavMesh, como tudo o que mexe este corpo.</summary>
        private IEnumerator WalkTo(Vector3 destination)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) yield break;

            if (animator != null) animator.SetFloat("Speed", agent.speed);
            agent.isStopped = false;
            if (!agent.SetDestination(destination)) yield break;

            float giveUpAt = Time.time + 25f;
            while (Time.time < giveUpAt)
            {
                if (!agent.pathPending &&
                    agent.remainingDistance <= Mathf.Max(0.15f, agent.stoppingDistance))
                    break;
                yield return null;
            }

            agent.isStopped = true;
            agent.ResetPath();
            if (animator != null) animator.SetFloat("Speed", 0f);
        }

        private void Suspend(bool on)
        {
            if (!on) { Restore(); return; }
            routineWasEnabled = routineToSuspend != null && routineToSuspend.enabled;
            if (routineToSuspend != null) routineToSuspend.enabled = false;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        private void Restore()
        {
            var routine = routineToSuspend as PrototypeNpcRoutine;
            if (routine != null && routineWasEnabled) { routine.ResumeRoutine(); return; }
            if (routineToSuspend != null) routineToSuspend.enabled = routineWasEnabled;
        }

        private void OnDisable()
        {
            if (running != null) StopCoroutine(running);
            if (screamSource != null && screamSource.isPlaying) screamSource.Stop();
            if (door != null) door.SetLocked(false);
            Restore();
            IsScreaming = false;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(Transform who, Transform target, MonoBehaviour routine,
            Transform inside, Transform outside, DoorDragInteractable roomDoor)
        {
            npc = who;
            player = target;
            routineToSuspend = routine;
            insideRoom = inside;
            outsideRoom = outside;
            door = roomDoor;
        }
#endif
    }
}
