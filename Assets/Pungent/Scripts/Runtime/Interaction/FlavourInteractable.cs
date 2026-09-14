using Pungent.Dialogue;
using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>
    /// Objeto do mundo que responde com um pensamento do protagonista em vez de
    /// uma acao mecanica. Serve para o apartamento parecer habitado: nem toda a
    /// interacao tem de fazer alguma coisa acontecer.
    ///
    /// Se houver varias linhas, sao ditas por ordem e a ultima fica a repetir-se,
    /// para que insistir num objeto nao produza texto novo indefinidamente.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlavourInteractable : MonoBehaviour, IPlayerInteractable, IAmbientInteractable
    {
        [SerializeField] private string prompt = "Look";
        [SerializeField] private ThoughtLineSet thoughtLines;
        [Tooltip("Quanto tempo a legenda fica no ecra depois de escrita. "
               + "Ignorado quando ha um ThoughtLineSet: o asset traz o seu.")]
        [SerializeField, Min(0.8f)] private float holdSeconds = 2.6f;
        [Tooltip("Impede repetir o mesmo objeto imediatamente.")]
        [SerializeField, Min(0f)] private float cooldown = 0.5f;
        [SerializeField] private WorldDialogueController dialogue;
        [Tooltip("So pode ser olhado depois deste acontecimento. Vazio = desde o inicio.")]
        [SerializeField] private string requiresEvent;
        [Tooltip("Deixa de poder ser olhado depois deste acontecimento.\n\n"
               + "O par com o de cima abre uma janela: entre um dia e outro. Serve o "
               + "objecto que so tem aquilo para dizer naquela altura — e evita que a "
               + "casa fale do vizinho de baixo na noite em que o Rui anda a procura "
               + "do jogador.")]
        [SerializeField] private string silencedByEvent;
        [SerializeField] private ChapterDirector director;

        // Formato antigo, mantido so para a migracao poder le-lo. Removido assim que
        // "Pungent/Dialogue/Migrate Scene Text" tiver corrido e sido validado.
        [SerializeField, HideInInspector] private string[] thoughts;

        private int index;
        private float nextAllowed;

        /// <summary>
        /// Ja foi olhado pelo menos uma vez. Serve os passos que dependem de o
        /// jogador ter reparado nalguma coisa — o prologo espera pelas tres caixas.
        /// </summary>
        public bool HasBeenRead { get; private set; }

        public string Prompt => Allowed ? prompt : string.Empty;
        public bool HoldToInteract => false;

        /// <summary>
        /// Troca o que este objeto diz, em jogo.
        ///
        /// Serve as coisas que mudam de significado durante a historia: a comoda
        /// diz "as minhas coisas, exatamente onde as deixei" ate a manha em que a
        /// gaveta aparece aberta, e a partir dai tem de dizer outra coisa. Sem isto
        /// o objeto continuava a tranquilizar o jogador com a gaveta aberta a
        /// frente dele.
        ///
        /// Recebe um <see cref="ThoughtLineSet"/> e nao strings de proposito: o
        /// texto vive nos assets, nao nos campos dos componentes.
        /// </summary>
        public void SetThoughtLines(ThoughtLineSet lines)
        {
            if (lines == null) return;
            thoughtLines = lines;
            index = lines.NewCursor;
            // O objeto volta a poder ser lido: e uma fala nova, nao a repeticao da
            // que ele ja ouviu.
            HasBeenRead = false;
            nextAllowed = 0f;
        }

        private void Awake()
        {
            if (dialogue == null)
                dialogue = FindObjectOfType<WorldDialogueController>();
            if (thoughtLines != null)
                index = thoughtLines.NewCursor;
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (!Allowed) return;
            if (dialogue == null) return;
            if (Time.unscaledTime < nextAllowed || dialogue.IsBusy) return;

            string line;
            float hold;

            if (thoughtLines != null && thoughtLines.Count > 0)
            {
                line = thoughtLines.Pick(ref index);
                hold = thoughtLines.HoldSeconds;
            }
            else if (thoughts != null && thoughts.Length > 0)
            {
                line = thoughts[Mathf.Min(index, thoughts.Length - 1)];
                if (index < thoughts.Length - 1) index++;
                hold = holdSeconds;
            }
            else return;

            nextAllowed = Time.unscaledTime + cooldown;
            HasBeenRead = true;
            dialogue.ShowThought(line, hold);
        }

        /// <summary>
        /// A janela em que este objecto tem alguma coisa a dizer.
        ///
        /// O director e resolvido aqui e nao no `Awake`: a cena do apartamento e
        /// recarregada para o climax e, nesse frame, ha dois exemplares vivos — o
        /// persistente e o da cena nova, que se desactiva sozinho. Guardar o
        /// resultado de um `FindObjectOfType` feito nesse instante podia deixar este
        /// componente agarrado ao condenado, e a partir dai **todos os objectos da
        /// casa ficavam mudos**, sem erro nenhum a dizer porque.
        /// </summary>
        private bool Allowed
        {
            get
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
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }
    }
}
