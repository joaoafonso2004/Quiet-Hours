using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O movel da entrada, nas duas vezes desta noite.
    ///
    /// A primeira e um gesto de habito: chega-se a casa e pousam-se as chaves,
    /// como se faz ha quatro meses. E o objectivo mundano com que a seccao 7.1
    /// quer que cada volta do loop comece, e o jogador cumpre-o sem pensar.
    ///
    /// A segunda e o capitulo a arrancar. A prateleira esta vazia. As chaves nao
    /// desapareceram antes de ele chegar — desapareceram **enquanto ele esteve
    /// dentro de casa**, nos dois minutos que levou a ir ao quarto e voltar. E essa
    /// a conta que se quer que o jogador faca sozinho, e por isso e que este passo
    /// nao pode ser um bilhete nem um susto.
    ///
    /// Um so componente para os dois momentos de proposito. Dois
    /// `ChapterEventRaiser` no mesmo movel eram dois donos a disputar o mesmo
    /// objecto — a armadilha que este projecto ja pagou seis vezes — e o
    /// `PlayerInteractor` resolve o alvo pelo primeiro componente que encontra, nao
    /// pelo que tem prompt.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClimaxKeyShelf : MonoBehaviour, IPlayerInteractable
    {
        private enum Phase
        {
            /// <summary>Ele acabou de entrar e ainda tem as chaves na mao.</summary>
            KeysInHand,
            /// <summary>Pousadas. A prateleira e so uma prateleira.</summary>
            KeysOnShelf,
            /// <summary>Ele voltou. Nao estao la.</summary>
            Empty,
            /// <summary>Ja as encontrou no quarto dele. O movel deixa de falar.</summary>
            Done
        }

        [Tooltip("Levantado ao pousar as chaves.")]
        [SerializeField] private string putDownEvent = "keys_down";

        [Tooltip("Levantado quando ele volta e a prateleira esta vazia.")]
        [SerializeField] private string missingEvent = "climax_keys_gone";

        [Tooltip("Quando este acontecer, o movel cala-se: as chaves ja estao com ele.")]
        [SerializeField] private string recoveredEvent = "keys_found";

        [Tooltip("So passa a estar vazia depois de o jogador ter ido ao quarto. Sem "
               + "isto, ficar parado na entrada a carregar duas vezes seguidas "
               + "queimava a descoberta inteira em dois segundos.")]
        [SerializeField] private string emptyAfterEvent = "climax_door_wrong";

        [Header("Texto")]
        [SerializeField] private Pungent.Dialogue.ThoughtLineSet putDownLines;
        [SerializeField] private Pungent.Dialogue.ThoughtLineSet emptyLines;

        [SerializeField] private ChapterDirector director;
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private Pungent.Dialogue.WorldDialogueController dialogue;

        private Phase phase = Phase.KeysInHand;

        // Um cursor por conjunto. Partilhar um so fazia a prateleira vazia comecar
        // a meio da lista dela, porque o gesto de pousar ja o tinha adiantado.
        private int putDownCursor;
        private int emptyCursor;

        public string Prompt
        {
            get
            {
                switch (phase)
                {
                    case Phase.KeysInHand: return "Put your keys down";
                    case Phase.KeysOnShelf: return Ready ? "Take your keys" : string.Empty;
                    case Phase.Empty: return "Look again";
                    default: return string.Empty;
                }
            }
        }

        public bool HoldToInteract => false;

        /// <summary>Ja foi ao quarto? So depois disso e que vale a pena voltar aqui.</summary>
        private bool Ready
        {
            get
            {
                if (string.IsNullOrWhiteSpace(emptyAfterEvent)) return true;
                director = ChapterDirector.Resolve(director);
                return director != null && director.HasSeen(emptyAfterEvent);
            }
        }

        private void Awake()
        {
            if (director == null) director = FindObjectOfType<ChapterDirector>();
            if (dialogue == null) dialogue = FindObjectOfType<Pungent.Dialogue.WorldDialogueController>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
        }

        private void Update()
        {
            if (phase == Phase.Done) return;
            director = ChapterDirector.Resolve(director);
            if (director != null && !string.IsNullOrWhiteSpace(recoveredEvent)
                && director.HasSeen(recoveredEvent))
                phase = Phase.Done;
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            switch (phase)
            {
                case Phase.KeysInHand:
                    phase = Phase.KeysOnShelf;
                    Say(putDownLines, ref putDownCursor);
                    director = ChapterDirector.Resolve(director);
                    director?.Notify(putDownEvent);
                    break;

                case Phase.KeysOnShelf:
                    if (!Ready) return;
                    // A mao chega a prateleira e nao ha nada. O passo do capitulo
                    // fecha aqui, e e este o unico sitio de onde pode fechar.
                    phase = Phase.Empty;
                    Say(emptyLines, ref emptyCursor);
                    director = ChapterDirector.Resolve(director);
                    director?.Notify(missingEvent);
                    break;

                case Phase.Empty:
                    Say(emptyLines, ref emptyCursor);
                    break;
            }
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

        /// <summary>
        /// Fala pelo canal do dialogo directamente e nao pelo
        /// <see cref="PlayerThoughtDirector"/> porque este e um gesto do jogador: o
        /// arrefecimento global de seis segundos dos pensamentos de passagem podia
        /// engolir a linha que responde ao clique que ele acabou de dar.
        /// </summary>
        private void Say(Pungent.Dialogue.ThoughtLineSet lines, ref int cursor)
        {
            if (lines == null || lines.Count == 0 || dialogue == null) return;

            if (dialogue.IsBusy)
            {
                // Ha outra coisa a falar. A linha entra em fila em vez de se perder
                // — e o clique dele nao pode ficar sem resposta nenhuma.
                thoughts?.Think(name + phase, lines.LineAt(0), 6, false, lines.HoldSeconds);
                return;
            }

            dialogue.ShowThought(lines.Pick(ref cursor), lines.HoldSeconds);
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(Pungent.Dialogue.ThoughtLineSet onPutDown,
            Pungent.Dialogue.ThoughtLineSet onEmpty)
        {
            putDownLines = onPutDown;
            emptyLines = onEmpty;
        }
#endif
    }
}
