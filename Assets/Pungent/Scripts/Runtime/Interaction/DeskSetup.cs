using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>
    /// Tirar o portatil da caixa e pousa-lo na secretaria.
    ///
    /// E a ultima tarefa do prologo e a que tem mais peso escondido: **esta e a
    /// secretaria onde a internet vai cair as 02:47 do Dia 2**. O jogador monta-a
    /// com as proprias maos, escolhe onde fica o portatil, e duas noites depois e
    /// ali que tudo comeca a correr mal. Um portatil que ja estivesse na mesa
    /// quando o jogo comeca e mobiliario; um que o jogador pousou e dele.
    ///
    /// O portatil nao e criado aqui — ja existe na cena, escondido. O
    /// `PlayerDeskOpening` do Dia 2 aponta para o ecra e para a luz dele, e criar um
    /// segundo partia essas duas ligacoes sem dar erro nenhum.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class DeskSetup : MonoBehaviour, IPlayerInteractable
    {
        [Tooltip("A caixa aberta de onde ele sai. Tem de estar ja no quarto.")]
        [SerializeField] private CarryableBox sourceBox;

        [Tooltip("O portatil que ja existe na cena. Nasce escondido.")]
        [SerializeField] private GameObject laptop;

        [Tooltip("Luz do ecra, ligada com ele.")]
        [SerializeField] private Light laptopGlow;

        [SerializeField] private string takePrompt = "Take the laptop out";
        [SerializeField] private string placePrompt = "Put it on the desk";

        [SerializeField] private string doneEvent = "desk_ready";

        [SerializeField, TextArea] private string takeThought =
            "Two more scenes to grade and it uploads overnight.";
        [SerializeField, TextArea] private string placeThought =
            "There. That is the room working, at least.";

        [SerializeField] private ChapterDirector director;
        [SerializeField] private PlayerThoughtDirector thoughts;

        private bool holding;
        private bool done;
        private bool eventSent;

        /// <summary>
        /// So se oferece depois de a caixa aberta estar no quarto. Antes disso o
        /// portatil ainda esta dentro dela, do outro lado da casa.
        /// </summary>
        public string Prompt
        {
            get
            {
                if (done) return string.Empty;
                if (holding) return placePrompt;
                return sourceBox != null && sourceBox.Delivered ? takePrompt : string.Empty;
            }
        }

        public bool HoldToInteract => false;

        private void Awake()
        {
            director = ChapterDirector.Resolve(director);
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();

            if (laptop != null) laptop.SetActive(false);
            if (laptopGlow != null) laptopGlow.enabled = false;
        }

        private void Update()
        {
            // Pousar o portatil e irreversivel. Se a referencia serializada ainda
            // apontar para o director antigo, conserva o evento pendente ate o
            // GAME_SYSTEMS vivo estar disponivel.
            if (done && !eventSent) TryRaiseDoneEvent();
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (done) return;

            if (!holding)
            {
                if (sourceBox == null || !sourceBox.Delivered) return;
                holding = true;
                if (!string.IsNullOrWhiteSpace(takeThought))
                    thoughts?.Think("desk_take", takeThought, 3, true, 3.2f);
                return;
            }

            done = true;
            holding = false;

            if (laptop != null) laptop.SetActive(true);
            if (laptopGlow != null) laptopGlow.enabled = true;

            if (!string.IsNullOrWhiteSpace(placeThought))
                thoughts?.Think("desk_done", placeThought, 3, true, 3.4f);
            TryRaiseDoneEvent();
        }

        private void TryRaiseDoneEvent()
        {
            if (eventSent) return;
            if (string.IsNullOrWhiteSpace(doneEvent))
            {
                eventSent = true;
                return;
            }

            director = ChapterDirector.Resolve(director);
            if (director == null) return;

            director.Notify(doneEvent);
            eventSent = true;
        }

        /// <summary>Repoe a tarefa ao recomecar o prologo no Editor.</summary>
        public void ResetForPrologue()
        {
            holding = false;
            done = false;
            eventSent = false;
            if (laptop != null) laptop.SetActive(false);
            if (laptopGlow != null) laptopGlow.enabled = false;
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(CarryableBox box, GameObject laptopObject, Light glow)
        {
            sourceBox = box;
            laptop = laptopObject;
            laptopGlow = glow;
        }
#endif
    }
}
