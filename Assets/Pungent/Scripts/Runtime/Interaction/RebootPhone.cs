using Pungent.Dialogue;
using Pungent.Player;
using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>
    /// The phone as an object you hold, not a panel that appears over the game.
    ///
    /// It lives parented to the camera and moves between two authored poses: one
    /// out of frame at the bottom, one raised into reading position. Tab moves it
    /// between them. Nothing else here knows what is on the screen; that is a
    /// separate component, so the pose can be tuned without touching the messages
    /// and the messages without touching the pose.
    ///
    /// **Why an object and not an overlay.** On day three Rui asks for the phone
    /// under the door. That beat only costs the player something if the phone has
    /// been a thing in his hand for three days. An overlay is an abstraction, and
    /// handing over an abstraction is free.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RebootPhone : MonoBehaviour
    {
        [Header("Scene references")]
        [Tooltip("The phone model, parented under the camera. Both poses below are "
               + "local to whatever this sits under.")]
        [SerializeField] private Transform model;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private WorldDialogueController dialogue;

        [Header("Poses")]
        [Tooltip("Out of frame, below the bottom edge. The player never sees this "
               + "one; it only has to be far enough down to clear the screen.")]
        [SerializeField] private Vector3 loweredPosition = new Vector3(0.18f, -0.34f, 0.32f);
        [SerializeField] private Vector3 loweredRotation = new Vector3(-55f, 12f, 8f);

        [Tooltip("Reading position. This one is authored by eye: it decides how much "
               + "of the room stays visible while the phone is up, which is the whole "
               + "tension of reading a message with someone else in the flat.")]
        [SerializeField] private Vector3 raisedPosition = new Vector3(0.10f, -0.13f, 0.34f);
        [SerializeField] private Vector3 raisedRotation = new Vector3(-12f, 8f, 2f);

        [SerializeField, Min(0.05f)] private float raiseSeconds = 0.28f;

        [Header("Sound")]
        [SerializeField] private AudioSource sound;
        [SerializeField] private AudioClip raiseClip;
        [SerializeField] private AudioClip lowerClip;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 0.5f;

        private bool open;
        private float blend;

        /// <summary>Raised and readable. The screen component listens to this.</summary>
        public bool IsOpen => open;

        /// <summary>
        /// Asked before Tab puts the phone away. The screen returns true when it
        /// had somewhere to go back to, and the phone stays up.
        ///
        /// One key for both jobs on purpose. A separate back key is a second thing
        /// to learn for a device the player opens to read four messages, and the
        /// alternative — Tab always closing — buries the contact list one level
        /// down with no way out except closing and reopening.
        /// </summary>
        public System.Func<bool> BackRequest;

        /// <summary>Fully raised, so the screen can wait before it starts drawing.</summary>
        public bool IsSettled => open && blend >= 0.999f;

        private void Awake()
        {
            if (input == null) input = FindObjectOfType<PlayerInputReader>();
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();

            if (model == null || input == null)
            {
                Debug.LogError("[RebootPhone] Missing model or input reference.", this);
                enabled = false;
                return;
            }

            blend = 0f;
            ApplyPose();
        }

        private void Update()
        {
            if (input.PhonePressed && CanToggle())
            {
                if (open && BackRequest != null && BackRequest()) { /* went back a level */ }
                else Toggle();
            }

            float target = open ? 1f : 0f;
            if (!Mathf.Approximately(blend, target))
            {
                blend = Mathf.MoveTowards(blend, target, Time.deltaTime / raiseSeconds);
                ApplyPose();
            }
        }

        /// <summary>
        /// Refuses to open on top of a conversation. Reading a message while Rui is
        /// mid-sentence throws away the sentence, and the sentences are the game.
        /// </summary>
        private bool CanToggle()
        {
            if (dialogue != null && dialogue.IsBusy) return false;
            return true;
        }

        private void Toggle()
        {
            open = !open;
            Play(open ? raiseClip : lowerClip);
        }

        /// <summary>Puts the phone away without asking, for scripted moments.</summary>
        public void ForceClose()
        {
            if (!open) return;
            open = false;
            Play(lowerClip);
        }

        private void ApplyPose()
        {
            // Smoothstep rather than linear: a phone comes up fast and settles, it
            // does not travel at one speed and stop dead.
            float t = blend * blend * (3f - 2f * blend);
            model.localPosition = Vector3.Lerp(loweredPosition, raisedPosition, t);
            model.localRotation = Quaternion.Slerp(
                Quaternion.Euler(loweredRotation), Quaternion.Euler(raisedRotation), t);
        }

        private void Play(AudioClip clip)
        {
            if (sound == null || clip == null) return;
            sound.PlayOneShot(clip, soundVolume);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Previews a pose in the editor so the two transforms can be authored by
        /// dragging the model and reading the numbers back, instead of guessing
        /// euler angles in the inspector.
        /// </summary>
        public void EditorPreview(bool raised)
        {
            if (model == null) return;
            blend = raised ? 1f : 0f;
            open = raised;
            ApplyPose();
        }

        public void EditorCapturePose(bool raised)
        {
            if (model == null) return;
            if (raised)
            {
                raisedPosition = model.localPosition;
                raisedRotation = model.localRotation.eulerAngles;
            }
            else
            {
                loweredPosition = model.localPosition;
                loweredRotation = model.localRotation.eulerAngles;
            }
        }
#endif
    }
}
