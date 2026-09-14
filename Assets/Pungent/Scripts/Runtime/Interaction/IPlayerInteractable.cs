using UnityEngine;

namespace Pungent.Interaction
{
    public interface IPlayerInteractable
    {
        string Prompt { get; }
        bool HoldToInteract { get; }
        void BeginInteraction(PlayerInteractor interactor);
        void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta);
        void EndInteraction(PlayerInteractor interactor);
    }
}
