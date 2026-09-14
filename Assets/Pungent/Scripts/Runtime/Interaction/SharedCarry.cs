using System.Collections;
using System.Collections.Generic;
using Pungent.Narrative;
using UnityEngine;
using UnityEngine.AI;

namespace Pungent.Interaction
{
    /// <summary>
    /// A peca que nao se levanta sozinho, e o homem que pega na outra ponta.
    ///
    /// ---
    ///
    /// **O que isto e, e nao e uma tarefa.**
    ///
    /// E a unica mecanica do jogo em que **outra pessoa decide para onde o teu corpo
    /// vai**. Estas preso a um objecto pelas duas pontas: nao podes acelerar, nao
    /// podes parar, nao podes virar as costas, e nao podes pousar antes de chegar.
    /// Ele pode falar contigo durante quarenta segundos em que tu nao te podes
    /// afastar.
    ///
    /// Todo o resto do jogo e sobre alguem a ocupar o teu espaco. Isto e o unico
    /// momento em que ele ocupa o teu **passo**.
    ///
    /// ---
    ///
    /// **A ordem e o que faz isto funcionar, e nao a mecanica.**
    ///
    /// Acontece **depois** da chamada. Antes de saberes, sao dois homens a levantar
    /// uma coisa pesada. Depois de o teres ouvido dizer *"Blue one. Third from the
    /// corner, same as you said."*, e uma pessoa presa a ti — e o jogo nao comenta
    /// isso em lado nenhum.
    ///
    /// ---
    ///
    /// **Ele manda no ritmo, e nao no caminho.**
    ///
    /// A trajectoria e do jogador: anda para onde quiser, e o outro segue-o. O que
    /// nao pode e ir depressa. Passar da velocidade combinada faz a peca arrastar —
    /// um som de esforco e uma linha — e nao ha maneira de o apressar. Isso e a
    /// diferenca entre uma trela, que se sente como uma limitacao do jogo, e uma
    /// pessoa, que se sente como uma pessoa.
    ///
    /// **Ninguem aqui e um corpo fisico preso a outro.** Duas pessoas ligadas por
    /// fisica partem-se em qualquer vao de porta, e este projecto ja pagou por isso
    /// esta semana. O patio e aberto e nao ha vaos: ele segue com o `NavMeshAgent`, a
    /// peca vai a camara do jogador, e ninguem empurra ninguem.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SharedCarry : MonoBehaviour, IPlayerInteractable
    {
        private enum Phase { Alone, Waiting, Carrying, Done }

        [Header("Quem ajuda")]
        [SerializeField] private GameObject helper;

        [Tooltip("A que distancia a frente do jogador ele segura a outra ponta.")]
        [SerializeField, Min(0.6f)] private float helperGap = 1.35f;

        [Tooltip("Velocidade dele. E tambem a que o jogador nao pode passar.")]
        [SerializeField, Min(0.3f)] private float pace = 1.05f;

        [Header("Prompts")]
        [SerializeField] private string tooHeavyPrompt = "Try to lift it";
        [SerializeField] private string liftPrompt = "Lift";
        [SerializeField] private string putDownPrompt = "Put it down";

        [Tooltip("O que o Tomas pensa ao tentar sozinho.")]
        [SerializeField, TextArea] private string tooHeavyThought = "It is not moving.";

        [Header("Onde acaba")]
        [SerializeField] private BoxDropZone dropZone;
        [SerializeField] private string deliveredEvent = "parts_loaded";

        [Header("Como fica na mao")]
        [SerializeField] private Vector3 carryOffset = new Vector3(0f, -0.30f, 0.75f);
        [SerializeField, Range(0.2f, 1f)] private float carrySpeedFactor = 0.55f;

        [Header("O que ele diz")]
        [Tooltip("A chamada dele, quando ve o jogador a tentar sozinho.")]
        [SerializeField] private string waitLine = "Wait.";

        [Tooltip("Ditas pela ordem escrita, espacadas ao longo do percurso.\n\n"
               + "Banais **todas**. A regra do projecto manda aqui mais do que em "
               + "qualquer outro sitio: dramatico le-se como truque de guiao, banal "
               + "le-se como vigilancia. A que importa e a que ele diz sem pensar.")]
        [SerializeField, TextArea] private string[] lines =
        {
            "Mind the edge, it has a lip on it.",
            "You teach, right? Tuesdays.",
            "Ninety was a good price. You will see.",
        };

        [Tooltip("Dita quando o jogador anda depressa de mais.")]
        [SerializeField] private string tooFastLine = "Easy.";

        [Tooltip("Dita quando o jogador para a meio.")]
        [SerializeField] private string stalledLine = "You alright?";

        [SerializeField, Min(1f)] private float secondsBetweenLines = 7f;

        [Header("Ligacoes")]
        [SerializeField] private Pungent.Dialogue.WorldDialogueController dialogue;
        [SerializeField] private Pungent.Dialogue.NpcMumbleVoice voice;
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private ChapterDirector director;
        [SerializeField] private AudioSource strainSource;
        [SerializeField] private AudioClip strainClip;

        private Phase phase = Phase.Alone;
        private Transform carrier;
        private Transform player;
        private Pungent.Player.PlayerMotor motor;
        private NavMeshAgent agent;
        private Animator helperAnimator;

        private Vector3 restPosition;
        private Quaternion restRotation;
        private Transform restParent;

        private int spoken;
        private float nextLineAt;
        private float lastNagAt;
        private float stillSince = -1f;

        public string Prompt
        {
            get
            {
                switch (phase)
                {
                    case Phase.Alone: return tooHeavyPrompt;
                    case Phase.Waiting: return liftPrompt;
                    case Phase.Carrying: return InPlace ? putDownPrompt : string.Empty;
                    default: return string.Empty;
                }
            }
        }

        public bool HoldToInteract => false;

        /// <summary>
        /// So se pousa no sitio.
        ///
        /// Sem isto, o jogador largava a peca a meio do patio e ficava com um homem
        /// parado ao lado dela para sempre. O prompt vazio fora da zona ja diz "aqui
        /// nao" sem precisar de uma linha a explica-lo — e a mesma regra que o resto
        /// do jogo usa para dizer que uma coisa nao da agora.
        /// </summary>
        private bool InPlace => dropZone == null || dropZone.Contains(transform.position);

        private void Awake()
        {
            restParent = transform.parent;
            restPosition = transform.position;
            restRotation = transform.rotation;

            if (dialogue == null) dialogue = FindObjectOfType<Pungent.Dialogue.WorldDialogueController>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();

            if (helper != null)
            {
                agent = helper.GetComponent<NavMeshAgent>();
                helperAnimator = helper.GetComponentInChildren<Animator>(true);
                if (voice == null) voice = helper.GetComponentInChildren<Pungent.Dialogue.NpcMumbleVoice>(true);
            }
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            switch (phase)
            {
                case Phase.Alone: TryAlone(); break;
                case Phase.Waiting: Lift(interactor); break;
                case Phase.Carrying: if (InPlace) PutDown(); break;
            }
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

        /// <summary>
        /// A tentativa sozinho. Nao e um falhanco — e o convite.
        ///
        /// O jogador tem de a fazer, porque e ela que estabelece que a peca e pesada.
        /// Sem isso, o Vitor a aparecer do nada a pegar na outra ponta le-se como o
        /// jogo a decidir por ele.
        /// </summary>
        private void TryAlone()
        {
            phase = Phase.Waiting;

            if (!string.IsNullOrWhiteSpace(tooHeavyThought))
                thoughts?.Think("shared_carry_heavy", tooHeavyThought, 4, true, 2.6f);

            if (strainSource != null && strainClip != null)
                strainSource.PlayOneShot(strainClip, 0.7f);

            if (!string.IsNullOrWhiteSpace(waitLine))
                Say(waitLine, 2.0f);

            StartCoroutine(HelperApproaches());
        }

        private IEnumerator HelperApproaches()
        {
            if (helper == null || agent == null || !agent.enabled) yield break;

            // Ele desliga o que estivesse a fazer e vem. O `SellerBeats` ja o punha a
            // aproximar-se do carro quando o jogador comeca a carregar; aqui e este
            // componente que manda, e dois donos no mesmo agente e um NPC a tremer
            // entre dois destinos.
            foreach (var beats in helper.GetComponents<SellerBeats>()) beats.enabled = false;

            agent.isStopped = false;
            agent.speed = 1.6f;
            SetHelperFlag("Walking", true);

            float giveUp = Time.time + 10f;
            while (Time.time < giveUp)
            {
                if (!agent.isOnNavMesh) break;
                agent.SetDestination(transform.position);
                if (!agent.pathPending &&
                    agent.remainingDistance <= Mathf.Max(1.0f, agent.stoppingDistance)) break;
                yield return null;
            }

            if (agent.isOnNavMesh) agent.isStopped = true;
            SetHelperFlag("Walking", false);
            PlayHelper("CarryIdle");
        }

        private void Lift(PlayerInteractor interactor)
        {
            var camera = interactor != null ? interactor.GetComponentInChildren<Camera>(true) : Camera.main;
            if (camera == null) return;

            phase = Phase.Carrying;
            carrier = camera.transform;

            player = interactor != null ? interactor.transform : null;
            motor = player != null ? player.GetComponent<Pungent.Player.PlayerMotor>() : null;
            if (motor != null) motor.SpeedMultiplier = carrySpeedFactor;

            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var body = GetComponent<Rigidbody>();
            if (body != null) { body.isKinematic = true; body.detectCollisions = false; }

            spoken = 0;
            nextLineAt = Time.time + 2.5f;
            PlayHelper("CarryIdle");
        }

        private void Update()
        {
            if (phase != Phase.Carrying) return;

            // A peca vai a camara, como a caixa do prologo. E o unico sitio onde ela
            // fica sempre visivel e nunca atravessa nada.
            if (carrier != null)
                transform.SetPositionAndRotation(carrier.TransformPoint(carryOffset), carrier.rotation);

            FollowWithHelper();
            Talk();
        }

        /// <summary>
        /// Ele segura a outra ponta: a frente do jogador, virado para ele.
        ///
        /// Pelo agente e nao por `transform`: no meio do percurso ha uma palete, o
        /// carro e o monte de pecas, e um homem arrastado em linha recta atravessava
        /// os tres. O agente contorna, e por isso a distancia entre os dois varia um
        /// pouco — o que e melhor do que uma distancia fixa, porque uma distancia fixa
        /// le-se como um objecto preso e nao como uma pessoa a carregar.
        /// </summary>
        private void FollowWithHelper()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh || player == null) return;

            Vector3 ahead = player.position + player.forward * helperGap;
            agent.speed = pace;
            agent.isStopped = false;
            agent.SetDestination(ahead);

            bool moving = agent.velocity.sqrMagnitude > 0.05f;
            SetHelperFlag("Walking", moving);
            PlayHelper(moving ? "CarryWalk" : "CarryIdle");

            // Virado para o jogador, sempre. Quem carrega a outra ponta anda de costas
            // ou de lado, e nunca de frente para onde vai.
            Vector3 toPlayer = player.position - helper.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.01f)
                helper.transform.rotation = Quaternion.Slerp(helper.transform.rotation,
                    Quaternion.LookRotation(toPlayer, Vector3.up), Time.deltaTime * 6f);

            // **O ritmo e dele.** Andar mais depressa nao o apressa: faz a peca
            // arrastar. E a unica maneira honesta de dizer "nao mandas nisto" sem
            // pregar o jogador ao chao.
            float gap = Vector3.Distance(player.position, helper.transform.position);
            if (gap > helperGap * 1.9f && Time.time - lastNagAt > 4f)
            {
                lastNagAt = Time.time;
                if (strainSource != null && strainClip != null)
                    strainSource.PlayOneShot(strainClip, 0.55f);
                if (!string.IsNullOrWhiteSpace(tooFastLine)) Say(tooFastLine, 1.6f);
            }
        }

        /// <summary>
        /// O que ele diz, espacado, e a pergunta quando o jogador para.
        ///
        /// **Parar tem resposta.** Um jogador que fique quieto a meio do patio a ver
        /// no que isto da recebe uma pergunta em vez de silencio — porque um homem
        /// que segura quarenta quilos e ve o outro parar pergunta alguma coisa. E e
        /// nesse momento que ele deixa de ser um mecanismo de transporte.
        /// </summary>
        private void Talk()
        {
            if (player == null) return;

            bool still = motor == null ||
                         player.GetComponent<CharacterController>() == null ||
                         player.GetComponent<CharacterController>().velocity.sqrMagnitude < 0.09f;

            if (still)
            {
                if (stillSince < 0f) stillSince = Time.time;
                if (Time.time - stillSince > 3.5f && Time.time - lastNagAt > 6f)
                {
                    lastNagAt = Time.time;
                    stillSince = Time.time;
                    if (!string.IsNullOrWhiteSpace(stalledLine)) Say(stalledLine, 2.0f);
                }
            }
            else stillSince = -1f;

            if (lines == null || spoken >= lines.Length) return;
            if (Time.time < nextLineAt) return;

            Say(lines[spoken], 2.8f);
            spoken++;
            nextLineAt = Time.time + secondsBetweenLines;
        }

        private void PutDown()
        {
            phase = Phase.Done;

            transform.SetParent(restParent, true);
            carrier = null;

            if (motor != null) motor.SpeedMultiplier = 1f;

            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = true;
            var body = GetComponent<Rigidbody>();
            if (body != null) { body.isKinematic = true; body.detectCollisions = true; }

            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            SetHelperFlag("Walking", false);
            PlayHelper("Idle");

            // Devolve o vendedor a quem o tinha: a partir daqui e o `SellerBeats` que
            // manda nele outra vez.
            if (helper != null)
                foreach (var beats in helper.GetComponents<SellerBeats>()) beats.enabled = true;

            if (dropZone != null) dropZone.Accept(null);

            if (!string.IsNullOrWhiteSpace(deliveredEvent))
            {
                director = ChapterDirector.Resolve(director);
                director?.Notify(deliveredEvent);
            }
        }

        private void Say(string line, float hold)
        {
            if (dialogue == null || string.IsNullOrWhiteSpace(line)) return;
            dialogue.ShowReaction("Vitor", line, voice, hold, null,
                autoClose: true, blocksInteraction: false);
        }

        private void PlayHelper(string state)
        {
            if (helperAnimator == null || helperAnimator.runtimeAnimatorController == null) return;
            if (!helperAnimator.HasState(0, Animator.StringToHash(state))) return;
            helperAnimator.CrossFadeInFixedTime(state, 0.25f);
        }

        private void SetHelperFlag(string parameter, bool value)
        {
            if (helperAnimator == null || helperAnimator.runtimeAnimatorController == null) return;
            foreach (var p in helperAnimator.parameters)
                if (p.name == parameter && p.type == AnimatorControllerParameterType.Bool)
                {
                    helperAnimator.SetBool(parameter, value);
                    return;
                }
        }

        /// <summary>
        /// A rede. Uma troca de cena com a peca ao colo deixava o jogador a 55% da
        /// velocidade para sempre — e o mesmo defeito que a `CarryableBox` ja teve.
        /// </summary>
        private void OnDisable()
        {
            if (phase != Phase.Carrying) return;
            if (motor != null) motor.SpeedMultiplier = 1f;
            carrier = null;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(GameObject who, BoxDropZone zone, string raised,
            AudioSource strain, AudioClip strainAudio, string[] spokenLines = null)
        {
            helper = who;
            dropZone = zone;
            deliveredEvent = raised;
            strainSource = strain;
            strainClip = strainAudio;
            if (spokenLines != null && spokenLines.Length > 0) lines = spokenLines;
        }
#endif
    }
}
