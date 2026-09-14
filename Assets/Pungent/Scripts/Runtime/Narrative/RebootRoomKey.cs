using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Owns the first physical objective after Rui's arrival conversation.
    /// The authored key transform stays untouched; this component only controls
    /// interaction, the objective text and the bedroom door lock.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class RebootRoomKey : MonoBehaviour,
        IPlayerInteractable, IPriorityInteractable
    {
        [SerializeField] private DoorDragInteractable bedroomDoor;
        [SerializeField] private PrototypeHUD hud;
        [SerializeField] private RebootPrologueEnd prologueEnd;

        [Tooltip("The father's thread waits on the moment the keys are in hand. "
               + "Raising it here, and not on the door or the conversation, keeps "
               + "the message tied to the thing the father actually asked about.")]
        [SerializeField] private PhoneMessageService phoneMessages;

        private const string KeysTakenEvent = "prologue_keys_taken";

        private bool available;
        private bool collected;

        public string Prompt => available && !collected ? "Take room key" : string.Empty;
        public bool HoldToInteract => false;

        private void Awake()
        {
            if (hud == null) hud = FindObjectOfType<PrototypeHUD>();

            if (bedroomDoor == null)
            {
                Debug.LogError("[RebootRoomKey] Bedroom door reference is missing.", this);
                return;
            }

            bedroomDoor.SetLockPromptArmed(false);
            bedroomDoor.SetLocked(true);
        }

        public void Arm()
        {
            if (collected) return;
            if (bedroomDoor == null || hud == null)
            {
                Debug.LogError("[RebootRoomKey] Cannot arm the room key objective.", this);
                return;
            }

            available = true;
            // Says where it is. "Take your room key" reads like an instruction to
            // someone who already knows the flat, and the player has been in it
            // for ninety seconds.
            hud.SetObjective("OBJECTIVE: Take your key from the shelf by the front door");
            Debug.Log("[RebootRoomKey] Room key objective armed.", this);
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (!available || collected) return;

            available = false;
            collected = true;
            // This reboot never asks the player to lock this door during arrival.
            // Clear any authored state from the old storyline before unlocking it,
            // otherwise the first click can be consumed by the obsolete lock action.
            bedroomDoor.SetLockPromptArmed(false);
            bedroomDoor.SetLocked(false);
            hud.SetObjective("OBJECTIVE: Go to your room");

            // Arming here, and not from the door itself, keeps the prologue end
            // owned by the moment the player is actually told to go to the room.
            if (prologueEnd != null) prologueEnd.Arm();
            else Debug.LogError("[RebootRoomKey] Prologue end reference is missing.", this);

            if (phoneMessages != null) phoneMessages.RaiseEvent(KeysTakenEvent);
            else Debug.LogError("[RebootRoomKey] Phone message service reference is missing.", this);

            Debug.Log("[RebootRoomKey] Key collected; bedroom door unlocked.", this);
            gameObject.SetActive(false);
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }
    }
}
