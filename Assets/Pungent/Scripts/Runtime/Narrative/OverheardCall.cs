using System.Collections;
using Pungent.Dialogue;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Meia conversa ao telefone, ouvida de longe. E o pivo do jogo inteiro.
    ///
    /// ---
    ///
    /// **A funcao do Dia 4, escrita no §5, e uma so:** *converter suspeita domestica
    /// em perigo concreto.* Ate aqui tudo o que aconteceu tem explicacao inocente —
    /// uma gaveta mal fechada, um vizinho que olha para a rua. Isto e a primeira
    /// coisa que nao tem, e tem de acontecer **sem ninguem a dizer**.
    ///
    /// ---
    ///
    /// **Porque e que e o vendedor a falar e nao o Rui.** Por o Rui ao telefone em
    /// alta voz resolveria a duvida num segundo, e a duvida e o jogo. Aqui o jogador
    /// ouve **metade** de uma conversa: as respostas de um homem a alguem que nao se
    /// ouve. Tudo o que ele diz e banal. O que nao e banal e ele saber uma coisa que
    /// so pode ter vindo de uma pessoa.
    ///
    /// **A frase que fecha o triangulo.** No Dia 1 o Tomas pensa, da varanda,
    /// *"Passenger door still only opens from the inside."* Minutos depois o Rui
    /// devolve-lhe a porta: *"You want to get that passenger door looked at."* Agora um
    /// estranho, a quarenta minutos de casa, usa **o mesmo defeito para identificar o
    /// carro ao telefone**. O jogador ouviu aquele detalhe tres vezes, de tres bocas, e
    /// so ele sabe que as tres estao ligadas.
    ///
    /// **E aqui a explicacao inocente acaba**, e e essa a diferenca em relacao ao que
    /// isto foi antes. A primeira versao usava o lugar onde o carro estava
    /// estacionado — *"third space from the corner"* — e um lugar de estacionamento ve
    /// -o qualquer pessoa que passe na rua. O estranho explicava-se sozinho e nao
    /// sobrava desconforto nenhum.
    ///
    /// Uma porta que so abre por dentro nao se ve da rua: sabe-a quem entrou no carro
    /// ou quem viu o Tomas dar a volta para abrir. Ele nunca esteve perto do carro. So
    /// ha uma pessoa que podia ter-lhe dito, e essa pessoa dorme do outro lado do
    /// corredor.
    ///
    /// A regra do <see cref="NPC.RuiCarRemark"/> continua a mandar no Dia 1 — dramatico
    /// le-se como guiao, banal le-se como vigilancia — e o que muda aqui e so que a
    /// terceira vez ja nao tem saida.
    ///
    /// ---
    ///
    /// **A mecanica e a tensao.** Ele afasta-se para atender. Para ouvir e preciso
    /// **aproximar-se**, e se o jogador se aproximar de mais ele repara e cala-se —
    /// e perde-se a frase que importa. Ouvir bem exige estar mais perto do que e
    /// confortavel de um homem que acabou de vender pecas roubadas. Nao ha prompt,
    /// nao ha objectivo, nao ha nada no ecra a dizer que se pode ouvir: quem estiver
    /// distraido a carregar a bagageira ouve um murmurio e perde o dia inteiro.
    ///
    /// **Nao bloqueia nada.** O passo do capitulo espera pelo `parts_loaded`, nao por
    /// isto. Perder a chamada e uma consequencia, nao um beco: o `heard_call` fica
    /// por levantar e o Dia 5 le-o para saber quanto e que o jogador percebeu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OverheardCall : MonoBehaviour
    {
        [Header("Quando")]
        [Tooltip("Acontecimento que faz o telemovel dele tocar. Depois de o jogador "
               + "ver os numeros raspados: primeiro a prova, depois a ligacao.")]
        [SerializeField] private string armOnEvent = "evidence_found";

        [Tooltip("Levantado se o jogador ouvir a frase que importa. Nao e obrigatorio "
               + "para o capitulo — e para o Dia 5 saber o que ele sabe.")]
        [SerializeField] private string heardEvent = "heard_call";

        [Tooltip("Segundos antes de o telemovel tocar, depois de armado. Da tempo ao "
               + "jogador de estar a fazer outra coisa — que e quando isto tem de o "
               + "apanhar.")]
        [SerializeField, Min(0f)] private float delaySeconds = 6f;

        [Header("Quem e onde")]
        [SerializeField] private GameObject seller;
        [Tooltip("Para onde ele se afasta a atender. Um canto, de costas.")]
        [SerializeField] private Transform callSpot;
        [SerializeField, Min(0.5f)] private float walkSpeed = 1.1f;

        [Header("Distancias")]
        [Tooltip("A partir daqui ouve-se. Tem de ser desconfortavelmente perto.")]
        [SerializeField, Min(1f)] private float earshot = 6.5f;

        [Tooltip("A partir daqui ele repara e cala-se.\n\n"
               + "E o preco: ouvir bem exige estar mais perto do que se quer estar. "
               + "Muito curto e impossivel; muito largo e nao ha decisao nenhuma.")]
        [SerializeField, Min(0.5f)] private float noticeRange = 2.4f;

        [Header("O que se ouve")]
        [Tooltip("As falas dele, por ordem. So se ouve o lado dele.\n\n"
               + "A ULTIMA e a que fecha o triangulo — ver a nota da classe. As "
               + "primeiras existem para o jogador ter tempo de decidir aproximar-se.")]
        [SerializeField, TextArea]
        private string[] lines =
        {
            "Yeah. No, he is here now.",
            "…I know what I said. He is paying cash, so what does it matter.",
            "He has not asked anything. He just wants the discs on.",
            "Blue one. Passenger door does not open, that is the one.",
        };

        [Tooltip("Dita se ele reparar no jogador. Corta a chamada e a cena.")]
        [SerializeField, TextArea]
        private string caughtLine = "…I will call you back.";

        [Tooltip("O que o Tomas pensa depois de ouvir a ultima. Chega tarde de "
               + "proposito: primeiro percebe-se, depois e que doi.")]
        [SerializeField, TextArea]
        private string realisationThought = "He has never been near my car.";

        [SerializeField, Min(1f)] private float lineHold = 3.2f;
        [SerializeField, Min(0f)] private float betweenLines = 1.8f;

        [Header("Som")]
        [SerializeField] private AudioSource phoneSource;
        [Tooltip("O telemovel dele a tocar. E o que puxa o jogador para la.")]
        [SerializeField] private AudioClip ringClip;
        [SerializeField, Range(0f, 1f)] private float ringVolume = 0.7f;

        [Header("Ligacoes")]
        [SerializeField] private WorldDialogueController dialogue;
        [SerializeField] private NpcMumbleVoice voice;
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private ChapterDirector director;

        private enum Phase { Idle, Ringing, Walking, Talking, Done }

        private Phase phase = Phase.Idle;
        private float armedAt;
        private Transform player;
        private Coroutine running;

        /// <summary>Quantas falas o jogador chegou mesmo a ouvir.</summary>
        public int LinesHeard { get; private set; }

        /// <summary>Ouviu a que importa.</summary>
        public bool HeardTheLine { get; private set; }

        private void Awake()
        {
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            if (seller != null && voice == null)
                voice = seller.GetComponentInChildren<NpcMumbleVoice>(true);
            if (phoneSource == null && seller != null)
                phoneSource = seller.GetComponentInChildren<AudioSource>(true);
        }

        private void Update()
        {
            if (phase != Phase.Idle) return;

            director = ChapterDirector.Resolve(director);
            if (director == null) return;
            if (string.IsNullOrWhiteSpace(armOnEvent) || !director.HasSeen(armOnEvent)) return;

            if (armedAt <= 0f) { armedAt = Time.time; return; }
            if (Time.time - armedAt < delaySeconds) return;

            running = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            phase = Phase.Ringing;

            if (phoneSource != null && ringClip != null)
                phoneSource.PlayOneShot(ringClip, ringVolume);

            // A mao ao ouvido comeca com o toque e nao com a conversa: quem atende
            // levanta o telemovel antes de dizer o que quer que seja, e sao esses
            // dois segundos que dizem ao jogador que aquilo e uma chamada e nao um
            // homem a ir para o fundo do patio por outra razao qualquer.
            SetFlag("OnPhone", true);

            yield return new WaitForSeconds(1.6f);

            // Afasta-se a andar. Nunca teleporta: o jogador tem de o **ver** ir para
            // o canto, senao nao percebe que ha alguma coisa a acontecer ali.
            phase = Phase.Walking;
            yield return WalkToCallSpot();

            phase = Phase.Talking;
            yield return Talk();

            phase = Phase.Done;
            SetFlag("OnPhone", false);
            running = null;
        }

        /// <summary>
        /// Poe uma bandeira no animator do vendedor, se ele tiver um.
        ///
        /// Tolerante de propósito: o vendedor pode estar sem modelo (o
        /// `PsxCharacter.Place` avisa e continua), e nesse caso a chamada tem de
        /// acontecer na mesma. A cena vale pelo que se ouve; a pose e o que a impede
        /// de parecer partida.
        /// </summary>
        private void SetFlag(string parameter, bool value)
        {
            var animator = seller != null ? seller.GetComponentInChildren<Animator>(true) : null;
            if (animator == null || animator.runtimeAnimatorController == null) return;

            foreach (var p in animator.parameters)
                if (p.name == parameter && p.type == AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool(parameter, value);
                    return;
                }
        }

        private IEnumerator WalkToCallSpot()
        {
            var agent = seller != null ? seller.GetComponent<UnityEngine.AI.NavMeshAgent>() : null;
            if (agent == null || callSpot == null || !agent.enabled || !agent.isOnNavMesh)
                yield break;

            float wasSpeed = agent.speed;
            agent.speed = walkSpeed;
            agent.isStopped = false;
            agent.SetDestination(callSpot.position);

            // O `SellerBeats` ja poe esta bandeira quando e ele a mandar no vendedor.
            // Aqui quem manda e esta corrotina, e sem isto os dez metros ate ao canto
            // eram percorridos com o homem a deslizar em pose de idle.
            SetFlag("Walking", true);

            float giveUp = Time.time + 12f;
            while (Time.time < giveUp)
            {
                if (!agent.pathPending &&
                    agent.remainingDistance <= Mathf.Max(0.2f, agent.stoppingDistance)) break;
                yield return null;
            }

            agent.isStopped = true;
            agent.ResetPath();
            agent.speed = wasSpeed;
            SetFlag("Walking", false);

            // De costas para o patio. Falar de frente para o jogador desfazia a
            // ideia de que aquilo nao e para os ouvidos dele.
            if (callSpot != null) seller.transform.rotation = callSpot.rotation;
        }

        private IEnumerator Talk()
        {
            for (int i = 0; i < lines.Length; i++)
            {
                // Espera que o dialogo esteja livre: atropelar um pensamento do
                // jogador com uma fala que ele tem de ouvir bem seria perde-la.
                float wait = Time.time + 6f;
                while (dialogue != null && dialogue.IsBusy && Time.time < wait) yield return null;

                float distance = DistanceToPlayer();

                // Perto de mais: ele repara. A chamada morre e a frase perde-se.
                if (distance <= noticeRange)
                {
                    if (!string.IsNullOrWhiteSpace(caughtLine))
                        dialogue?.ShowReaction(SellerName(), caughtLine, voice, 2.6f, null,
                            autoClose: true, blocksInteraction: false);
                    yield break;
                }

                // Longe de mais: a fala acontece na mesma, e o jogador nao a ouve.
                // O tempo passa a correr de qualquer maneira — quem ficou na
                // bagageira perde a conversa e nunca sabe que a perdeu.
                bool audible = distance <= earshot;
                if (audible)
                {
                    dialogue?.ShowReaction(SellerName(), lines[i], voice, lineHold, null,
                        autoClose: true, blocksInteraction: false);
                    LinesHeard++;

                    if (i == lines.Length - 1)
                    {
                        HeardTheLine = true;
                        director = ChapterDirector.Resolve(director);
                        if (!string.IsNullOrWhiteSpace(heardEvent)) director?.Notify(heardEvent);
                    }
                }

                yield return new WaitForSeconds(lineHold + betweenLines);
            }

            if (HeardTheLine && !string.IsNullOrWhiteSpace(realisationThought))
            {
                // Depois de ele se calar, e nao por cima. O silencio entre a frase e
                // a percepcao e onde o jogador faz a conta sozinho.
                yield return new WaitForSeconds(1.4f);
                thoughts?.Think("day4_call", realisationThought, 5, true, 4f);
            }
        }

        private string SellerName() => "Vitor";

        private float DistanceToPlayer()
        {
            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor == null) return float.MaxValue;
                player = motor.transform;
            }
            if (seller == null) return float.MaxValue;
            return Vector3.Distance(player.position, seller.transform.position);
        }

        private void OnDisable()
        {
            if (running != null) StopCoroutine(running);
            running = null;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(GameObject who, Transform spot, AudioSource phone)
        {
            seller = who;
            callSpot = spot;
            phoneSource = phone;
        }
#endif
    }
}
