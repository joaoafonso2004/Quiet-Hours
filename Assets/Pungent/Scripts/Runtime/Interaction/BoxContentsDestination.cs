using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>Destino de um objecto retirado de uma caixa.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class BoxContentsDestination : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private BoxContentsTask task;

        public string Prompt => task != null ? task.PlacePrompt : string.Empty;
        public bool HoldToInteract => false;
        public void BeginInteraction(PlayerInteractor interactor) => task?.Place();
        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

#if UNITY_EDITOR
        public void EditorConfigure(BoxContentsTask owner) => task = owner;
#endif
    }
}
