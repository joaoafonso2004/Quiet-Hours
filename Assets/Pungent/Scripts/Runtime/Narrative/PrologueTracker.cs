using Pungent.Interaction;
using Pungent.NPC;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Levanta os eventos do prologo a partir do que o jogador faz.
    ///
    /// Os passos do capitulo esperam por `prologue_unpacked`, `prologue_talked`,
    /// `prologue_door_closed` e `prologue_slept`. Sem alguem a levanta-los, o
    /// capitulo ficava parado no segundo passo — que e a mesma armadilha do fio do
    /// Pai preso a um evento que ninguem disparava.
    ///
    /// Vive a parte do <see cref="PrologueStage"/> porque sao trabalhos
    /// diferentes: o Stage poe a casa no estado desta noite, este ouve o jogador.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrologueTracker : MonoBehaviour
    {
        [SerializeField] private ChapterDirector director;
        [SerializeField] private PrologueStage stage;

        [Header("As caixas")]
        [SerializeField] private FlavourInteractable[] boxes = new FlavourInteractable[0];

        [Header("O Rui")]
        [SerializeField] private RuiStoryConversation conversation;

        [Header("A porta do quarto")]
        [SerializeField] private DoorDragInteractable bedroomDoor;
        [Tooltip("So conta depois deste acontecimento: fechar a porta antes de o Rui "
               + "aparecer no vao nao e o mesmo gesto.")]
        [SerializeField] private string doorAvailableEvent = "prologue_rui_arrives";

        [Header("Dormir")]
        [SerializeField] private PlayerSleep sleep;

        private bool unpackedSent;
        private bool talkedSent;
        private bool doorSent;
        private bool lockedSent;
        private bool sleptSent;
        private bool talkedPending;
        private bool sleptPending;
        private bool lockedPending;

        public void ResetForPrologue()
        {
            unpackedSent = false;
            talkedSent = false;
            doorSent = false;
            lockedSent = false;
            sleptSent = false;
            talkedPending = false;
            sleptPending = false;
            lockedPending = false;
            bedroomDoor?.SetLocked(false);
            bedroomDoor?.SetLockPromptArmed(false);
        }

        private void Awake()
        {
            director = ChapterDirector.Resolve(director);
            if (stage == null) stage = FindObjectOfType<PrologueStage>();
            if (conversation == null) conversation = FindObjectOfType<RuiStoryConversation>(true);
            if (sleep == null) sleep = FindObjectOfType<PlayerSleep>(true);

            if (conversation != null) conversation.Finished += OnConversationFinished;
            if (sleep != null) sleep.DaySlept += OnSlept;
            if (bedroomDoor != null) bedroomDoor.Locked += OnDoorLocked;
        }

        private void OnDestroy()
        {
            if (conversation != null) conversation.Finished -= OnConversationFinished;
            if (sleep != null) sleep.DaySlept -= OnSlept;
            if (bedroomDoor != null) bedroomDoor.Locked -= OnDoorLocked;
        }

        private void Update()
        {
            if (stage != null && !stage.IsRunning) return;

            director = ChapterDirector.Resolve(director);

            if (!talkedSent && talkedPending && TryNotify("prologue_talked"))
            {
                talkedSent = true;
                talkedPending = false;
            }

            if (!sleptSent && sleptPending && TryNotify("prologue_slept"))
            {
                sleptSent = true;
                sleptPending = false;
            }

            if (!lockedSent && lockedPending && TryNotify("prologue_door_locked"))
            {
                lockedSent = true;
                lockedPending = false;
            }

            if (!unpackedSent && AllBoxesOpened() && TryNotify("prologue_unpacked"))
            {
                unpackedSent = true;
            }

            // A porta fechada e o gesto que fecha o serao. O Rui permanece no vao
            // ate o jogador cumprir o objectivo e fechar a porta.
            bool doorAllowed = string.IsNullOrWhiteSpace(doorAvailableEvent)
                            || (director != null && director.HasSeen(doorAvailableEvent));
            if (!doorSent && doorAllowed && bedroomDoor != null && !bedroomDoor.IsOpen
                && TryNotify("prologue_door_closed"))
            {
                doorSent = true;
                bedroomDoor.SetLockPromptArmed(true);
            }
        }

        private bool AllBoxesOpened()
        {
            if (boxes == null || boxes.Length == 0) return false;
            foreach (var box in boxes)
                if (box == null || !box.HasBeenRead) return false;
            return true;
        }

        private void OnConversationFinished()
        {
            if (talkedSent) return;
            talkedPending = true;
        }

        private void OnSlept()
        {
            if (stage != null && !stage.IsRunning) return;
            if (!sleptSent) sleptPending = true;
        }

        private void OnDoorLocked()
        {
            if (!doorSent || lockedSent) return;
            lockedPending = true;
        }

        private bool TryNotify(string eventId)
        {
            director = ChapterDirector.Resolve(director);
            if (director == null) return false;
            director.Notify(eventId);
            return true;
        }
    }
}
