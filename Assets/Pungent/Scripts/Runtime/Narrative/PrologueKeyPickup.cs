using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// As chaves que o Rui deixa no suporte da entrada.
    ///
    /// Nao existe inventario para uma coisa que so tem um estado: antes de as
    /// apanhar a porta do quarto esta trancada; depois desaparecem do suporte, a
    /// porta abre e o capitulo recebe o acontecimento.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrologueKeyPickup : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private ChapterDirector director;
        [SerializeField] private DoorDragInteractable bedroomDoor;
        [SerializeField] private GameObject visual;
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private string eventId = "prologue_keys_taken";
        [SerializeField] private string prompt = "Take the keys";
        [SerializeField, TextArea] private string pickedUpThought = "Last door on the left.";

        private bool available;
        private bool eventPending;

        public string Prompt => available ? prompt : string.Empty;
        public bool HoldToInteract => false;
        public bool HasBeenTaken => !available;

        private void Awake()
        {
            director = ChapterDirector.Resolve(director);
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
        }

        private void Update()
        {
            if (eventPending) TryRaiseTakenEvent();
        }

        /// <summary>Repoe o estado da primeira noite, inclusive em Play Mode sem reload.</summary>
        public void BeginPrologue()
        {
            available = true;
            eventPending = false;
            if (visual != null) visual.SetActive(true);
            bedroomDoor?.ForceClosed();
            bedroomDoor?.SetLocked(true);
        }

        /// <summary>A chave nao volta a aparecer nos dias seguintes.</summary>
        public void EndPrologue()
        {
            available = false;
            eventPending = false;
            if (visual != null) visual.SetActive(false);
            bedroomDoor?.SetLocked(false);
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (!available) return;

            available = false;
            if (visual != null) visual.SetActive(false);
            bedroomDoor?.SetLocked(false);

            if (!string.IsNullOrWhiteSpace(pickedUpThought))
                thoughts?.Think("prologue_keys", pickedUpThought, 3, true, 3.4f);
            eventPending = true;
            TryRaiseTakenEvent();
        }

        private void TryRaiseTakenEvent()
        {
            if (!eventPending) return;
            if (string.IsNullOrWhiteSpace(eventId))
            {
                eventPending = false;
                return;
            }

            director = ChapterDirector.Resolve(director);
            if (director == null) return;

            director.Notify(eventId);
            eventPending = false;
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

#if UNITY_EDITOR
        public void EditorConfigure(
            ChapterDirector chapterDirector,
            DoorDragInteractable door,
            GameObject keyVisual)
        {
            director = chapterDirector;
            bedroomDoor = door;
            visual = keyVisual;
        }
#endif
    }
}
