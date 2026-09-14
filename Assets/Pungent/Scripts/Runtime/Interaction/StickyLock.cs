using UnityEngine;
using Pungent.Narrative;

namespace Pungent.Interaction
{
    /// <summary>
    /// A fechadura que nao pega a primeira. Uma vez, num dia marcado.
    ///
    /// ---
    ///
    /// **A promessa que estava por cumprir.** O Rui diz isto na primeira fala do
    /// jogo — *"Here, spare key. The lock sticks if you rush it."* — e depois a
    /// fechadura funcionava sempre, todas as noites, ao primeiro clique. Uma frase
    /// dita na primeira cena e nunca confirmada nao e um detalhe esquecido: e o
    /// jogador a aprender que o que as personagens dizem nao descreve o mundo.
    ///
    /// **Porque e que isto e terror e nao uma chatice.** O que assusta neste genero
    /// nao e o monstro, e a coisa banal que escolhe o pior momento para nao
    /// funcionar. No Dia 3 o Tomas ja sabe que alguem lhe mexeu na gaveta. Vai
    /// trancar a porta, e a porta nao tranca. Nesse segundo e meio ele nao esta em
    /// perigo nenhum — esta so a perceber que a unica coisa que o separava do
    /// corredor e um mecanismo que tambem pode falhar.
    ///
    /// **Uma vez so, e nunca a bloquear.** A segunda tentativa pega sempre. Isto
    /// nao e um puzzle, nao tem falha possivel e nao pode prender ninguem: o passo
    /// do capitulo que espera pela porta trancada fecha-se na tentativa seguinte.
    /// Uma fechadura que falhasse ao acaso seria uma mecanica; falhando uma vez, e
    /// uma memoria.
    ///
    /// **O som faz o trabalho todo.** O clique errado — metal que anda e nao encaixa
    /// — e o que diz ao jogador que aquilo falhou, antes de qualquer texto. Ver a
    /// lista de assets no fim: sem esse clip isto le-se como o botao nao ter
    /// funcionado.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DoorDragInteractable))]
    public sealed class StickyLock : MonoBehaviour
    {
        [Tooltip("So a partir deste acontecimento e que ela emperra. Vazio = desde o "
               + "inicio, o que nao e o que se quer: a primeira noite tem de ser normal "
               + "para a terceira poder nao ser.")]
        [SerializeField] private string armOnEvent = "day_two";

        [Tooltip("Deixa de emperrar depois disto. Serve para a noite do climax, em que "
               + "uma fechadura a falhar seria crueldade e nao tensao.")]
        [SerializeField] private string disarmOnEvent = "climax";

        [Tooltip("Quantas vezes falha antes de pegar. Uma. Ver a nota da classe — "
               + "duas ja e um obstaculo, e isto nao e um obstaculo.")]
        [SerializeField, Min(1)] private int failuresBeforeGiving = 1;

        [Tooltip("O que ele pensa quando ela nao pega. Sem drama: uma pessoa a "
               + "constatar que a fechadura esta a fazer o que lhe disseram que fazia.")]
        [SerializeField, TextArea]
        private string jamThought = "It is not catching. He did say it sticks.";

        [Tooltip("Segunda tentativa, ja com ela trancada. Vazio = silencio.")]
        [SerializeField, TextArea]
        private string settleThought = "";

        [Header("Som")]
        [SerializeField] private AudioSource audioSource;
        [Tooltip("Metal a andar sem encaixar. E isto que diz que falhou.")]
        [SerializeField] private AudioClip jamClip;
        [SerializeField, Range(0f, 1f)] private float jamVolume = 0.85f;

        [SerializeField] private ChapterDirector director;
        [SerializeField] private PlayerThoughtDirector thoughts;

        private DoorDragInteractable door;
        private int failures;
        private bool armed;
        private bool spent;

        /// <summary>Ja falhou pelo menos uma vez nesta partida.</summary>
        public bool HasJammed => failures > 0;

        private void Awake()
        {
            door = GetComponent<DoorDragInteractable>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
        }

        private void Update()
        {
            if (spent) return;

            director = ChapterDirector.Resolve(director);
            if (director == null) return;

            if (!armed && !string.IsNullOrWhiteSpace(armOnEvent) && director.HasSeen(armOnEvent))
                armed = true;

            if (armed && !string.IsNullOrWhiteSpace(disarmOnEvent) && director.HasSeen(disarmOnEvent))
            {
                armed = false;
                spent = true;
            }
        }

        /// <summary>
        /// Chamado pela porta antes de trancar. Verdadeiro = a fechadura nao pegou
        /// desta vez.
        ///
        /// A porta pergunta em vez de este componente lhe mexer no estado: quem sabe
        /// se uma porta esta trancada e a porta, e dois donos do mesmo booleano e
        /// como se perdem tardes.
        /// </summary>
        public bool ConsumeJam()
        {
            if (!armed || spent) return false;
            if (failures >= failuresBeforeGiving)
            {
                // Ja falhou o que tinha a falhar. Pega, e nao volta a emperrar hoje.
                spent = true;
                if (!string.IsNullOrWhiteSpace(settleThought))
                    thoughts?.Think("lock_settle", settleThought, 3, true, 2.6f);
                return false;
            }

            failures++;
            if (audioSource != null && jamClip != null)
                audioSource.PlayOneShot(jamClip, jamVolume);

            if (!string.IsNullOrWhiteSpace(jamThought))
                thoughts?.Think("lock_jam", jamThought, 4, true, 3.0f);

            return true;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(string arm, string disarm, string jamLine, AudioClip clip)
        {
            armOnEvent = arm;
            disarmOnEvent = disarm;
            if (!string.IsNullOrWhiteSpace(jamLine)) jamThought = jamLine;
            if (clip != null) jamClip = clip;
        }
#endif
    }
}
