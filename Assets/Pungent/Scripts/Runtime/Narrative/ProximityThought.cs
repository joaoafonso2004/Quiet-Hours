using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Um pensamento por passar ao pe de alguma coisa.
    ///
    /// O `FlavourInteractable` cobre o olhar e carregar; isto cobre o resto, que e a
    /// maior parte do tempo de jogo. Passar ao pe do Rui na cozinha, entrar na
    /// casa de banho, chegar ao pe da cama as tres da manha — momentos em que
    /// ninguem carrega em nada e o jogo, hoje, fica calado.
    ///
    /// **E o silencio que faz um sitio parecer um cenario.** Uma casa onde se anda
    /// dez minutos sem um unico pensamento nao e uma casa, e um conjunto de
    /// paredes. E quando a unica coisa que o protagonista comenta e o que interessa
    /// a historia, o jogador aprende em dois minutos que tudo o que ele diz e uma
    /// pista — e a partir dai nao ha surpresas. Pensamentos sobre coisas que nao
    /// interessam nada sao o que torna os que interessam invisiveis.
    ///
    /// Por isso: escrever muitos, e escrever banalidades. O cheiro de alguem, uma
    /// torneira que pinga, o frio do corredor.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProximityThought : MonoBehaviour
    {
        public enum Repeat
        {
            /// <summary>Uma vez e nunca mais. Para observacoes que so tem graca a primeira.</summary>
            Once,
            /// <summary>Volta a poder sair depois do arrefecimento.</summary>
            Sometimes
        }

        [Tooltip("A que distancia comeca. Curto para objectos, largo para pessoas.")]
        [SerializeField, Min(0.3f)] private float radius = 2.2f;

        [Tooltip("Alvo que se move. Vazio = fica onde foi posto.\n\n"
               + "Uma cadeira fica quieta; o Rui nao. Sem isto, o pensamento sobre "
               + "ele ficava colado ao sitio onde ele estava quando a cena foi "
               + "montada, e passar-lhe ao lado na cozinha nao dizia nada.")]
        [SerializeField] private Transform follow;

        [Tooltip("O que ele pensa. Varias = escolhida por ordem, a ultima repete-se.")]
        [SerializeField] private Pungent.Dialogue.ThoughtLineSet lines;

        [Tooltip("Alternativa a um asset, para uma linha so.")]
        [SerializeField, TextArea] private string singleLine;

        [SerializeField] private Repeat repeat = Repeat.Once;

        [Tooltip("Silencio minimo antes de este voltar a poder sair.")]
        [SerializeField, Min(5f)] private float cooldownSeconds = 90f;

        [Tooltip("Prioridade do pensamento. Baixa para banalidades: assim nunca "
               + "atropelam uma fala de historia.")]
        [SerializeField, Range(0, 9)] private int priority = 1;

        [Tooltip("So sai depois deste acontecimento. Vazio = desde o inicio.")]
        [SerializeField] private string requiresEvent;

        [Tooltip("Deixa de sair depois deste acontecimento. Serve para trocar o que "
               + "um sitio diz quando a historia avanca.")]
        [SerializeField] private string silencedByEvent;

        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private ChapterDirector director;

        private Transform player;
        private bool inside;

        // "Ja disparou alguma vez" em vez de um `lastAt` iniciado a um numero muito
        // negativo. O truque do numero negativo depende do inicializador do campo, e
        // este campo apareceu a zero em Play: com `Time.time` ainda abaixo do
        // arrefecimento, a conta `Time.time - 0 < 90` dava verdade e **nenhum
        // pensamento de passagem podia sair nos primeiros noventa segundos de jogo**.
        // Um bool nao tem como falhar assim.
        private bool everFired;
        private float lastAt;
        private bool spent;
        private int cursor;

        private void Awake()
        {
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            if (lines != null) cursor = lines.NewCursor;
        }

        private void Update()
        {
            if (spent) return;

            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor == null) return;
                player = motor.transform;
            }

            // Ao quadrado: isto vai correr em muitos objectos ao mesmo tempo e uma
            // raiz quadrada por cada um, por frame, nao se justifica para saber se
            // alguem esta perto de um lavatorio.
            Vector3 centre = follow != null ? follow.position : transform.position;
            bool near = (player.position - centre).sqrMagnitude <= radius * radius;

            // So no momento em que entra. Sem isto, ficar parado ao pe de uma coisa
            // repetia o pensamento sem parar.
            if (near && !inside) Fire();
            inside = near;
        }

        private void Fire()
        {
            if (everFired && Time.time - lastAt < cooldownSeconds) return;
            if (!Allowed()) return;

            string line = lines != null && lines.Count > 0 ? lines.Pick(ref cursor) : singleLine;
            if (string.IsNullOrWhiteSpace(line)) return;

            lastAt = Time.time;
            everFired = true;
            if (repeat == Repeat.Once) spent = true;

            float hold = lines != null ? lines.HoldSeconds : 3f;
            thoughts?.Think($"prox_{GetInstanceID()}", line, priority, false, hold);
        }

        /// <summary>
        /// Se este pensamento pode sair agora.
        ///
        /// Duas coisas que ja custaram caro, e ambas so se veem no Dia 5.
        ///
        /// **O director resolve-se aqui e nao no `Awake`.** A cena do apartamento e
        /// recarregada para o climax, e nessa altura ha dois `ChapterDirector` vivos
        /// no mesmo frame: o persistente e o da cena nova, que se desactiva sozinho.
        /// Um `FindObjectOfType` no `Awake` pode apanhar o condenado, e a partir do
        /// fim do frame este componente ficava com uma referencia morta. E a mesma
        /// armadilha que o `ChapterEventRaiser` ja pagou.
        ///
        /// **E na duvida cala-se.** Antes, um director nulo devolvia `true`: com a
        /// referencia morta, um pensamento com porta ficava sem porta nenhuma e a
        /// casa comentava a torneira da cozinha com o Rui a procura do jogador.
        /// Silencio a mais e uma casa apagada; silencio a menos, naquele momento, e
        /// o capitulo inteiro desfeito.
        /// </summary>
        private bool Allowed()
        {
            bool gated = !string.IsNullOrWhiteSpace(requiresEvent)
                      || !string.IsNullOrWhiteSpace(silencedByEvent);
            if (!gated) return true;

            director = ChapterDirector.Resolve(director);
            if (director == null) return false;

            if (!string.IsNullOrWhiteSpace(requiresEvent) && !director.HasSeen(requiresEvent))
                return false;
            if (!string.IsNullOrWhiteSpace(silencedByEvent) && director.HasSeen(silencedByEvent))
                return false;

            return true;
        }

#if UNITY_EDITOR
        /// <summary>Usado pelas ferramentas de ligacao, que vivem noutra assembly.</summary>
        public void EditorConfigure(string line, float range, Repeat mode, int thoughtPriority,
            string requires = null, string silencedBy = null, Transform followTarget = null)
        {
            singleLine = line;
            radius = range;
            repeat = mode;
            priority = thoughtPriority;
            requiresEvent = requires;
            silencedByEvent = silencedBy;
            follow = followTarget;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 0.85f, 1f, 0.45f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
