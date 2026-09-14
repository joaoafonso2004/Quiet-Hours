using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>Ponto de interacao de uma das fases de StagedHomeTask.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class StagedHomeTaskStep : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private StagedHomeTask task;
        [SerializeField] private StagedHomeTask.Step step;

        public string Prompt => task != null ? task.PromptFor(step) : string.Empty;
        public bool HoldToInteract => false;

        public void BeginInteraction(PlayerInteractor interactor) => task?.Perform(step);
        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

#if UNITY_EDITOR
        public void EditorConfigure(StagedHomeTask owner, StagedHomeTask.Step taskStep)
        {
            task = owner;
            step = taskStep;
        }
#endif
    }
}
