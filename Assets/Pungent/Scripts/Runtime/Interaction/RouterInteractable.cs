using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Interaction
{
    [DisallowMultipleComponent]
    public sealed class RouterInteractable : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private PrototypeStoryDirector storyDirector;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip restartClip;
        [SerializeField, Range(0f, 1f)] private float restartVolume = 0.55f;

        [Tooltip("Os cabos a ligar. Vazio = o router volta a ser um clique so.\n\n"
               + "A ausencia dele nao pode partir nada: a quest das 02:47 espera pelo "
               + "router e um painel em falta seria o jogo preso no corredor.")]
        [SerializeField] private RouterCablePuzzle puzzle;

        private bool restarted;
        private bool interactionEnabled = true;

        public string Prompt
        {
            get
            {
                if (!interactionEnabled) return string.Empty;
                if (restarted) return "Router online";

                // Enquanto o painel esta aberto o router nao se oferece outra vez:
                // sem isto o clique que fecha um cabo podia reabrir o painel por
                // baixo dele.
                if (puzzle != null && puzzle.IsOpen) return string.Empty;

                return "Restart router";
            }
        }

        public bool HoldToInteract => false;

        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (puzzle == null)
                puzzle = FindObjectOfType<RouterCablePuzzle>();
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (!interactionEnabled || restarted) return;
            if (puzzle != null && puzzle.IsOpen) return;

            if (puzzle != null) { puzzle.Open(Restart); return; }

            // Sem painel, o comportamento antigo. Nao ha caminho nenhum em que
            // carregar no router nao faca nada.
            Restart();
        }

        private void Restart()
        {
            if (restarted) return;
            restarted = true;
            if (audioSource != null && restartClip != null)
                audioSource.PlayOneShot(restartClip, restartVolume);
            storyDirector?.OnRouterRestarted();
        }

        /// <summary>
        /// Muda o estado narrativo e visual do router.
        ///
        /// No prologo esta ligado mas nao e uma tarefa. A queda da ligacao so
        /// acontece quando a quest das 02:47 arranca.
        /// </summary>
        public void SetState(bool canRestart, bool online)
        {
            interactionEnabled = canRestart;
            restarted = online;
            storyDirector?.SetRouterOnline(online);
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }
    }
}
