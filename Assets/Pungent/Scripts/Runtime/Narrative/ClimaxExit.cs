using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// A porta 3B, por dentro.
    ///
    /// A seccao 5 e explicita quanto ao fim deste capitulo: **o objectivo nao e
    /// derrotar o Rui, e sair.** Nao ha arma, nao ha luta e nao ha maneira de o
    /// vencer — ha uma porta que precisa de uma chave que esta no quarto dele.
    ///
    /// E por isso que este componente e tao pequeno. O climax inteiro esta montado
    /// para o jogador chegar aqui; o que este objecto faz e recusar ate ele ter a
    /// chave, e depois deixar.
    ///
    /// A recusa nao e muda. Uma porta que simplesmente nao responde le-se como um
    /// bug e manda o jogador a procurar noutro sitio qualquer; uma porta que diz
    /// "esta trancada e a chave nao esta comigo" manda-o procurar a chave, que e o
    /// capitulo. E a regra de clareza narrativa: o misterio e *porque*, nunca *o
    /// que*.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClimaxExit : MonoBehaviour, IPlayerInteractable
    {
        [Tooltip("Sem este acontecimento a porta so diz que esta trancada.")]
        [SerializeField] private string requiresEvent = "keys_found";

        [Tooltip("Levantado ao sair. E o dono deste acontecimento — o `RuiHunt` "
               + "levanta o dele, e nenhum dos dois fecha o passo do outro.")]
        [SerializeField] private string escapeEvent = "escaped";

        [Header("Texto")]
        [SerializeField] private Pungent.Dialogue.ThoughtLineSet lockedLines;

        [Header("Ligacoes")]
        [SerializeField] private DoorDragInteractable door;
        [SerializeField] private ChapterDirector director;
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private Pungent.Dialogue.WorldDialogueController dialogue;
        [SerializeField] private ScreenFade fade;

        [Tooltip("Suspensos assim que ele abre a porta: a partir dai o capitulo "
               + "acabou e andar mais um metro so estraga o corte.")]
        [SerializeField] private MonoBehaviour[] suppressedOnExit = new MonoBehaviour[0];

        private int cursor;
        private bool left;

        public string Prompt
        {
            get
            {
                if (left) return string.Empty;
                return HasKeys ? "Unlock the door" : "The door is locked";
            }
        }

        public bool HoldToInteract => false;

        private bool HasKeys
        {
            get
            {
                if (string.IsNullOrWhiteSpace(requiresEvent)) return true;
                director = ChapterDirector.Resolve(director);
                return director != null && director.HasSeen(requiresEvent);
            }
        }

        private void Awake()
        {
            if (director == null) director = FindObjectOfType<ChapterDirector>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            if (dialogue == null) dialogue = FindObjectOfType<Pungent.Dialogue.WorldDialogueController>();
            if (fade == null) fade = FindObjectOfType<ScreenFade>();
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (left) return;

            if (!HasKeys)
            {
                Say();
                return;
            }

            left = true;

            foreach (var component in suppressedOnExit)
                if (component != null) component.enabled = false;

            if (door != null)
            {
                door.SetLocked(false);
                door.ForceOpen();
            }

            // A saida e um corte e nao uma caminhada pelas escadas. O que ha do
            // outro lado desta porta e o epilogo, e o epilogo e tres dias depois.
            if (fade != null) fade.Blink(1.3f, 1.0f, 1.6f, Leave);
            else Leave();
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

        private void Leave()
        {
            director = ChapterDirector.Resolve(director);
            director?.Notify(escapeEvent);
        }

        private void Say()
        {
            if (lockedLines == null || lockedLines.Count == 0) return;

            if (dialogue != null && !dialogue.IsBusy)
            {
                dialogue.ShowThought(lockedLines.Pick(ref cursor), lockedLines.HoldSeconds);
                return;
            }

            thoughts?.Think("exit_locked", lockedLines.LineAt(0), 6, false, lockedLines.HoldSeconds);
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(DoorDragInteractable frontDoor,
            Pungent.Dialogue.ThoughtLineSet locked, MonoBehaviour[] toSuppress)
        {
            door = frontDoor;
            lockedLines = locked;
            suppressedOnExit = toSuppress;
        }
#endif
    }
}
