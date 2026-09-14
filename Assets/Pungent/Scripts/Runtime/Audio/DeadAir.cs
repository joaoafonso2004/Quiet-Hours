using System.Collections;
using System.Collections.Generic;
using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Audio
{
    /// <summary>
    /// O momento em que a casa deixa de fazer barulho nenhum.
    ///
    /// ---
    ///
    /// **Isto e um efeito de som feito de ausencia de som, e por isso nao precisa de
    /// nenhum ficheiro novo.** O apartamento tem oito room tones, o frigorifico, a
    /// cidade por baixo da varanda e a chuva. O jogador deixa de os ouvir ao fim de
    /// dois minutos — e exactamente para isso que eles servem. O que ele nao deixa de
    /// ouvir e o buraco que fica quando se vao todos embora ao mesmo tempo.
    ///
    /// Quatro segundos de nada, e a casa volta. Ninguem comenta.
    ///
    /// ---
    ///
    /// **As tres decisoes que fazem a diferenca entre isto e um bug de audio:**
    ///
    /// 1. **Sair depressa, voltar devagar.** A queda e de um quarto de segundo, para
    ///    se notar que aconteceu. O regresso demora mais de um segundo, para nao se
    ///    notar quando acabou. Ao contrario, isto le-se como um erro de mistura.
    /// 2. **O som que traz o mundo de volta.** Opcional, e vale por todos os outros:
    ///    quatro segundos de nada e depois **uma** tabua a estalar. O silencio esteve
    ///    la para aquela tabua ter onde cair.
    /// 3. **A escuta pode ir abaixo tambem** (<see cref="listenerFloor"/>). Com ela a
    ///    um valor baixo, ate os passos do proprio jogador ficam longe — a sensacao e
    ///    de ter agua nos ouvidos, e e desconfortavel de uma maneira que nao tem
    ///    explicacao possivel. Reservado para uma ou duas vezes no jogo todo; usado
    ///    mais do que isso, passa a ser um efeito.
    ///
    /// ---
    ///
    /// **Nunca durante uma conversa.** Um dialogo com o mundo em silencio le-se como
    /// o jogo a partir. A janela de acontecimentos trata disso — isto e posto nos
    /// intervalos e nao por cima deles.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeadAir : MonoBehaviour
    {
        public enum Trigger
        {
            /// <summary>Quando o acontecimento de capitulo passar.</summary>
            OnEvent,
            /// <summary>Quando o jogador chegar perto deste objecto.</summary>
            NearBy,
            /// <summary>So por codigo ou UnityEvent, com <see cref="Fire"/>.</summary>
            Manual
        }

        [Header("Quando")]
        [SerializeField] private Trigger fires = Trigger.OnEvent;

        [SerializeField] private string firesOnEvent;

        [SerializeField, Min(0.5f)] private float nearDistance = 3f;

        [SerializeField] private string armOnEvent;
        [SerializeField] private string silencedByEvent;

        [Tooltip("Espera depois de o gatilho acontecer. Serve para o silencio nao cair "
               + "em cima do acontecimento que o pediu — cair um segundo depois e o "
               + "que faz o jogador nao ligar os dois.\n\n"
               + "Em NearBy conta como permanencia: ele tem de ficar la este tempo.")]
        [SerializeField, Min(0f)] private float delaySeconds = 0.8f;

        [Header("Forma")]
        [SerializeField, Min(0.05f)] private float fadeOutSeconds = 0.25f;
        [SerializeField, Min(0.5f)] private float holdSeconds = 4f;
        [SerializeField, Min(0.2f)] private float fadeInSeconds = 1.6f;

        [Tooltip("Ate onde a escuta desce. Um = nao mexe em nada e so as fontes "
               + "listadas se calam. Ver a nota da classe: abaixo de 1 e uma coisa "
               + "para uma ou duas vezes no jogo todo.")]
        [SerializeField, Range(0.05f, 1f)] private float listenerFloor = 1f;

        [Header("O que se cala")]
        [Tooltip("Vazio = apanha sozinho tudo o que estiver em ciclo na cena quando "
               + "isto disparar. E o que se quer quase sempre: os room tones sao "
               + "montados por outra ferramenta e uma lista escrita a mao fica "
               + "desactualizada sem dar erro nenhum.")]
        [SerializeField] private AudioSource[] sources = new AudioSource[0];

        [Tooltip("Fontes que continuam a tocar. A voz e o dialogo vivem aqui — um "
               + "personagem a falar com o mundo em silencio le-se como o jogo a "
               + "partir.")]
        [SerializeField] private AudioSource[] exceptions = new AudioSource[0];

        [Header("O regresso (opcional)")]
        [Tooltip("Tocado no instante em que o mundo volta. Uma tabua, uma dobradica, "
               + "um puxador. Curto e sozinho.")]
        [SerializeField] private AudioSource returnSource;
        [SerializeField] private AudioClip returnClip;
        [SerializeField, Range(0f, 1f)] private float returnVolume = 0.6f;

        [Header("Depois")]
        [SerializeField, TextArea] private string thought;
        [SerializeField] private string raisesEvent;

        [SerializeField] private ChapterDirector director;
        [SerializeField] private PlayerThoughtDirector thoughts;

        private Transform player;
        private bool spent;
        private bool running;

        /// <summary>
        /// Quando o gatilho ficou satisfeito — **e nao quando o componente ficou
        /// armado**.
        ///
        /// A diferenca nao e cosmetica. Com a espera contada a partir do momento em que
        /// o capitulo abre, o `climax_keys_gone` chegava minutos depois de `climax` e a
        /// espera de um segundo e meio ja estava gasta: a casa calava-se no mesmo frame
        /// em que o jogador dava pela prateleira vazia, e os dois liam-se como uma
        /// coisa so. O buraco tem de cair **depois** da descoberta, com o jogador ja a
        /// virar-se para procurar as chaves noutro sitio.
        /// </summary>
        private float pendingSince = -1f;

        private readonly List<AudioSource> silenced = new List<AudioSource>();
        private readonly List<float> before = new List<float>();
        private float listenerBefore = 1f;

        public bool HasFired => spent;

        private void Awake()
        {
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
        }

        private void Update()
        {
            if (spent || running) return;
            if (!Armed()) { pendingSince = -1f; return; }

            bool triggered;
            switch (fires)
            {
                case Trigger.OnEvent:
                    triggered = !string.IsNullOrWhiteSpace(firesOnEvent) && Seen(firesOnEvent);
                    break;

                case Trigger.NearBy:
                    triggered = ResolvePlayer() &&
                        Vector3.Distance(player.position, transform.position) <= nearDistance;
                    break;

                default:
                    return;
            }

            if (!triggered) { pendingSince = -1f; return; }

            if (pendingSince < 0f) pendingSince = Time.time;
            if (Time.time - pendingSince < delaySeconds) return;

            Fire();
        }

        /// <summary>Cala a casa. Publico para UnityEvents.</summary>
        public void Fire()
        {
            if (spent || running) return;
            spent = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            running = true;
            Collect();

            yield return Ramp(1f, 0f, fadeOutSeconds);

            float until = Time.time + holdSeconds;
            while (Time.time < until) yield return null;

            // O som que traz o mundo de volta toca **antes** do fade in e nao depois:
            // ele tem de cair dentro do silencio, senao chega ja com a casa a
            // trabalhar por cima e nao se ouve.
            if (returnSource != null && returnClip != null)
                returnSource.PlayOneShot(returnClip, returnVolume);

            yield return Ramp(0f, 1f, fadeInSeconds);

            Restore();

            if (!string.IsNullOrWhiteSpace(thought))
                thoughts?.Think($"deadair_{GetInstanceID()}", thought, 3, true, 3.2f);

            if (!string.IsNullOrWhiteSpace(raisesEvent))
            {
                director = ChapterDirector.Resolve(director);
                director?.Notify(raisesEvent);
            }

            running = false;
        }

        private IEnumerator Ramp(float from, float to, float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
                ApplyScale(k);
                yield return null;
            }
            ApplyScale(to);
        }

        private void ApplyScale(float k)
        {
            for (int i = 0; i < silenced.Count; i++)
            {
                var source = silenced[i];
                if (source == null) continue;
                source.volume = before[i] * k;
            }

            if (listenerFloor < 1f)
                AudioListener.volume = Mathf.Lerp(listenerFloor, listenerBefore, k);
        }

        /// <summary>
        /// Quem se cala, decidido no momento e nao na montagem.
        ///
        /// Escrito assim de proposito: os room tones sao montados pelo
        /// `RoomToneBalance`, o frigorifico pelo dressing e a cidade pelo
        /// `CityExteriorBuilder`. Uma lista serializada com oito nomes ficava
        /// desactualizada a primeira vez que alguem corresse outra ferramenta, e o
        /// silencio passava a ser um silencio com um frigorifico dentro — que nao e
        /// silencio nenhum e nao da erro nenhum.
        /// </summary>
        private void Collect()
        {
            silenced.Clear();
            before.Clear();
            listenerBefore = AudioListener.volume;

            IEnumerable<AudioSource> candidates = sources != null && sources.Length > 0
                ? sources
                : FindObjectsOfType<AudioSource>(false);

            foreach (var source in candidates)
            {
                if (source == null || source == returnSource) continue;
                if (System.Array.IndexOf(exceptions, source) >= 0) continue;

                // Sem lista escrita a mao, so o que estiver mesmo a tocar em ciclo
                // conta: um `PlayOneShot` a meio nao se apaga, apaga-se o fundo.
                if ((sources == null || sources.Length == 0) && !(source.loop && source.isPlaying))
                    continue;

                silenced.Add(source);
                before.Add(source.volume);
            }
        }

        private void Restore()
        {
            for (int i = 0; i < silenced.Count; i++)
                if (silenced[i] != null) silenced[i].volume = before[i];

            if (listenerFloor < 1f) AudioListener.volume = listenerBefore;

            silenced.Clear();
            before.Clear();
        }

        /// <summary>
        /// A rede. Um corte de cena a meio do silencio destroi a corrotina e a casa
        /// ficava muda para o resto do jogo — e a escuta ficava a quinze por cento
        /// sem nada a dizer porque.
        /// </summary>
        private void OnDisable()
        {
            if (!running) return;
            running = false;
            Restore();
        }

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
            float hold, float delay = 0.8f, float floor = 1f, float near = 3f,
            AudioSource comeback = null, AudioClip comebackClip = null, string thoughtText = null)
        {
            fires = when;
            firesOnEvent = firesOn;
            armOnEvent = arms;
            silencedByEvent = silencedBy;
            holdSeconds = hold;
            delaySeconds = delay;
            listenerFloor = floor;
            nearDistance = near;
            returnSource = comeback;
            returnClip = comebackClip;
            thought = thoughtText;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            if (fires != Trigger.NearBy) return;
            Gizmos.color = new Color(0.25f, 0.25f, 0.35f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, nearDistance);
        }
    }
}
