using System.Collections;
using Pungent.Player;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Um segundo e meio em que a cabeca do Tomas vira para uma coisa e o jogador
    /// nao manda nela.
    ///
    /// ---
    ///
    /// **Porque e que isto e preciso, se ja existe o <see cref="CameraLockEvent"/>.**
    ///
    /// Aquele componente resolve um caso e so um: o susto da casa de banho, na noite
    /// do prologo, com um volume no chao e uma referencia dura ao
    /// <see cref="OpeningQuestDirector"/> — a classe que so existe naquela noite. Uma
    /// segunda utilizacao no Dia 3 ou no Dia 5 obrigava a arrastar aquele director
    /// para capitulos onde ele nao corre, e o campo ficava nulo: o teste
    /// `questDirector.Current != requiredStep` passava a nunca poder ser satisfeito.
    ///
    /// Isto e a mesma ideia sem essa amarra. Fala a lingua do resto do jogo — um
    /// evento de capitulo — e por isso pode ser posto em qualquer sitio de qualquer
    /// dia sem conhecer o dono do dia.
    ///
    /// ---
    ///
    /// **A regra que faz isto funcionar, e que se paga cara se for quebrada:
    /// o jogador continua a andar.**
    ///
    /// A tentacao e suspender tambem o movimento, porque assim o plano fica limpo.
    /// Um plano limpo e uma cena de video, e uma cena de video diz ao jogador que ele
    /// nao esta a jogar — a partir daquele segundo, o que acontecer no ecra acontece
    /// a outra pessoa. O que assusta e o contrario: ele continua a poder andar, a
    /// poder fugir, e **a cabeca dele nao o obedece**. Tres segundos disso valem mais
    /// do que trinta de encenacao.
    ///
    /// Por isso <see cref="holdMovement"/> comeca desligado. Ligue-se onde houver
    /// razao mesmo — e nao ha muitas.
    ///
    /// ---
    ///
    /// **Nunca atraves de paredes.** Licao ja paga: o susto do quarto do Rui deixou de
    /// disparar quando alguem exigiu que o alvo estivesse tambem no enquadramento, e
    /// disparava para uma parede quando ninguem exigia nada. O teste certo e o do
    /// meio — linha de vista e mais nada. E o mesmo <see cref="Pungent.NPC.PlayerSight"/>
    /// que o resto do jogo usa.
    ///
    /// **E uma vez so.** Um plano que se repete e uma mecanica; o jogador aprende a
    /// esperar por ele e comeca a ler os cantos da sala a procura do proximo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AttentionSnap : MonoBehaviour
    {
        public enum Trigger
        {
            /// <summary>Quando o acontecimento de capitulo passar.</summary>
            OnEvent,
            /// <summary>Quando o jogador chegar perto de <see cref="transform"/>.</summary>
            NearBy,
            /// <summary>So por codigo ou UnityEvent, com <see cref="Fire"/>.</summary>
            Manual
        }

        [Header("Quando")]
        [SerializeField] private Trigger fires = Trigger.NearBy;

        [Tooltip("Para OnEvent: o acontecimento que puxa o plano.")]
        [SerializeField] private string firesOnEvent;

        [Tooltip("Para NearBy: a que distancia deste objecto.")]
        [SerializeField, Min(0.5f)] private float nearDistance = 3.2f;

        [Tooltip("So depois deste acontecimento. Vazio = desde sempre.")]
        [SerializeField] private string armOnEvent;

        [Tooltip("Deixa de poder acontecer depois deste. Um plano do Dia 3 que "
               + "dispare no Dia 5 chega tarde e estraga outra coisa qualquer.")]
        [SerializeField] private string silencedByEvent;

        [Tooltip("Espera depois de o gatilho acontecer — nao depois de o componente "
               + "ficar armado. Serve para o plano nao cair no mesmo frame que a coisa "
               + "que o pediu.\n\nEm NearBy conta como permanencia.")]
        [SerializeField, Min(0f)] private float delaySeconds = 0f;

        [Header("Para onde")]
        [Tooltip("O que a camara vai agarrar. Vazio = este objecto.")]
        [SerializeField] private Transform lookTarget;

        [Tooltip("Deslocamento a partir do alvo, em metros de mundo. Uma porta "
               + "agarra-se a altura da fechadura e nao ao nivel do chao, que e onde "
               + "esta o transform dela.")]
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1.5f, 0f);

        [Tooltip("Quanto tempo a cabeca dele nao obedece. Um segundo le-se como "
               + "acidente; tres ja e o jogo a mandar. Um e meio e onde isto vive.")]
        [SerializeField, Range(0.4f, 4f)] private float holdSeconds = 1.5f;

        [Tooltip("Suspender tambem o movimento. Ver a nota da classe: quase sempre "
               + "errado.")]
        [SerializeField] private bool holdMovement = false;

        [Header("Linha de vista")]
        [Tooltip("So agarra se o alvo estiver mesmo a vista. Desligar apenas para "
               + "planos que apontem para uma direccao e nao para um objecto.")]
        [SerializeField] private bool requireLineOfSight = true;

        [SerializeField] private LayerMask sightBlockers = ~0;

        [Tooltip("Altura do alvo, para os raios de visibilidade.")]
        [SerializeField, Min(0.2f)] private float targetHeight = 1.6f;

        [Header("Som (opcional)")]
        [Tooltip("Baixo. Isto nao e um jump scare — e uma pessoa a virar a cabeca "
               + "porque ouviu qualquer coisa. Se o som for grande, a coisa que ele "
               + "ve tem de ser grande, e quase nunca e.")]
        [SerializeField] private AudioSource sound;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.55f;

        [Header("Depois")]
        [Tooltip("Pensamento ao largar a camara. Vazio = silencio, que costuma ser "
               + "melhor: o plano ja disse o que tinha a dizer.")]
        [SerializeField, TextArea] private string thought;

        [SerializeField, Min(0)] private int thoughtPriority = 3;

        [Tooltip("Acontecimento levantado ao largar. Vazio = nao levanta nada.")]
        [SerializeField] private string raisesEvent;

        [SerializeField] private ChapterDirector director;
        [SerializeField] private PlayerThoughtDirector thoughts;

        private Transform player;
        private bool spent;
        private bool running;

        /// <summary>
        /// Quando o gatilho ficou satisfeito, e nao quando o plano ficou armado. Ver a
        /// nota igual no <see cref="Pungent.Audio.DeadAir"/>: com a espera contada do
        /// arranque do capitulo, um plano preso a um acontecimento tardio dispara no
        /// mesmo frame que ele e os dois leem-se como uma coisa so.
        /// </summary>
        private float pendingSince = -1f;

        /// <summary>Ja disparou. Util para inspeccionar e para os testes.</summary>
        public bool HasFired => spent;

        private void Awake()
        {
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
        }

        private void Update()
        {
            if (spent || running) return;
            if (!Armed()) { pendingSince = -1f; return; }
            if (!ResolvePlayer()) return;

            bool triggered;
            switch (fires)
            {
                case Trigger.OnEvent:
                    triggered = !string.IsNullOrWhiteSpace(firesOnEvent) && Seen(firesOnEvent);
                    break;

                case Trigger.NearBy:
                    triggered = Vector3.Distance(player.position, transform.position) <= nearDistance;
                    break;

                default:
                    return;
            }

            if (!triggered) { pendingSince = -1f; return; }

            if (pendingSince < 0f) pendingSince = Time.time;
            if (Time.time - pendingSince < delaySeconds) return;

            Fire();
        }

        /// <summary>
        /// Agarra a camara. Publico para UnityEvents e para quem tenha uma razao
        /// propria — uma porta a bater, uma conversa a acabar.
        /// </summary>
        public void Fire()
        {
            if (spent || running) return;
            if (!ResolvePlayer()) return;

            Transform target = lookTarget != null ? lookTarget : transform;

            var camera = player.GetComponentInChildren<Camera>(true);
            if (camera == null) camera = Camera.main;
            Vector3 eye = camera != null ? camera.transform.position
                                         : player.position + Vector3.up * 1.6f;

            // **Nunca atraves de paredes — e so isso.** Exigir tambem que o alvo ja
            // esteja no enquadramento matava o efeito todo: e a camara que o vai
            // levar la, e quem ja esta a olhar nao precisa de ser virado.
            if (requireLineOfSight &&
                !Pungent.NPC.PlayerSight.HasLineOfSight(eye, target, targetHeight, sightBlockers))
                return;

            var input = player.GetComponentInParent<PlayerInputReader>();
            var physics = player.GetComponentInChildren<CameraPhysics>();
            if (input == null || physics == null) return;

            spent = true;
            StartCoroutine(Hold(input, physics, target));
        }

        private IEnumerator Hold(PlayerInputReader input, CameraPhysics physics, Transform target)
        {
            running = true;

            input.SetLookSuppressed(true);
            if (holdMovement) input.SetMoveSuppressed(true);

            if (sound != null && clip != null) sound.PlayOneShot(clip, volume);

            float elapsed = 0f;
            while (elapsed < holdSeconds)
            {
                // Reapontado a cada frame de proposito: se o alvo andar — o Rui, um
                // carro — a cabeca acompanha-o. E se for o jogador a andar, o
                // enquadramento muda com ele, que e o que faz isto parecer uma pessoa
                // a olhar e nao uma camara pousada num carril.
                if (target != null) physics.ForceLookAt(target.position + lookOffset);
                elapsed += Time.deltaTime;
                yield return null;
            }

            input.SetLookSuppressed(false);
            if (holdMovement) input.SetMoveSuppressed(false);

            if (!string.IsNullOrWhiteSpace(thought))
                thoughts?.Think($"snap_{GetInstanceID()}", thought, thoughtPriority, true, 3.2f);

            if (!string.IsNullOrWhiteSpace(raisesEvent))
            {
                director = ChapterDirector.Resolve(director);
                director?.Notify(raisesEvent);
            }

            running = false;
        }

        /// <summary>
        /// Se este plano ainda esta dentro da sua janela.
        ///
        /// **Na duvida cala-se**, como o <see cref="ProximityThought"/> ja aprendeu: um
        /// director nulo devolve falso e nao verdadeiro. A cena do apartamento e
        /// recarregada para o climax e ha um instante com dois directores vivos; um
        /// plano do Dia 3 a disparar nessa transicao virava a cabeca do jogador para
        /// uma divisao que ja nao interessa a ninguem.
        /// </summary>
        private bool Armed()
        {
            bool gated = !string.IsNullOrWhiteSpace(armOnEvent)
                      || !string.IsNullOrWhiteSpace(silencedByEvent)
                      || fires == Trigger.OnEvent;
            if (!gated) return true;

            director = ChapterDirector.Resolve(director);
            if (director == null) return false;

            if (!string.IsNullOrWhiteSpace(armOnEvent) && !director.HasSeen(armOnEvent))
                return false;
            if (!string.IsNullOrWhiteSpace(silencedByEvent) && director.HasSeen(silencedByEvent))
                return false;

            return true;
        }

        private bool Seen(string id)
        {
            director = ChapterDirector.Resolve(director);
            return director != null && director.HasSeen(id);
        }

        private bool ResolvePlayer()
        {
            if (player != null) return true;

            var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
            if (motor == null) return false;
            player = motor.transform;
            return true;
        }

#if UNITY_EDITOR
        /// <summary>Usado pelas ferramentas de ligacao, que vivem noutra assembly.</summary>
        public void EditorConfigure(Trigger when, string firesOn, string arms, string silencedBy,
            Transform target, Vector3 offset, float hold, string thoughtText = null,
            string raises = null, float near = 3.2f, float delay = 0f,
            bool lineOfSight = true, AudioSource source = null, AudioClip audio = null)
        {
            fires = when;
            firesOnEvent = firesOn;
            armOnEvent = arms;
            silencedByEvent = silencedBy;
            lookTarget = target;
            lookOffset = offset;
            holdSeconds = hold;
            thought = thoughtText;
            raisesEvent = raises;
            nearDistance = near;
            delaySeconds = delay;
            requireLineOfSight = lineOfSight;
            sound = source;
            clip = audio;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            var target = lookTarget != null ? lookTarget : transform;

            Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.9f);
            Gizmos.DrawWireCube(target.position + lookOffset, Vector3.one * 0.3f);

            if (fires == Trigger.NearBy)
            {
                Gizmos.color = new Color(1f, 0.55f, 0.15f, 0.25f);
                Gizmos.DrawWireSphere(transform.position, nearDistance);
                Gizmos.DrawLine(transform.position, target.position + lookOffset);
            }
        }
    }
}
