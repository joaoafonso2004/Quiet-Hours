using System.Collections;
using Pungent.Interaction;
using Pungent.Player;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Closes the prologue when the player first walks into the bedroom.
    ///
    /// The screen fades out, the written card and the two-week summary are read
    /// on the black, the player is moved to the authored day one spawn, and
    /// control comes back once the image is open again. Nothing here starts day
    /// one content: this block only owns the time skip itself.
    ///
    /// The text is an <see cref="IntroTextSequence"/> and not something drawn
    /// here. An earlier version painted it with IMGUI, which uses the system font
    /// at raw pixel sizes: next to the serif opening it read as a different game.
    /// One way of putting words on black, used twice.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class RebootPrologueEnd : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Transform day1Spawn;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private CameraPhysics cameraPhysics;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private ScreenFade fade;
        [SerializeField] private PrototypeHUD hud;

        [Tooltip("The card and the two-week summary, read on the black. Same "
               + "component as the opening framing, so both look identical.")]
        [SerializeField] private IntroTextSequence skipText;

        [Header("Move-in boxes")]
        [Tooltip("Objects that belong to the prologue and must be gone after the "
               + "time skip. The entrance boxes go here.")]
        [SerializeField] private GameObject[] hideOnSkip = new GameObject[0];

        [Tooltip("Objects that only exist after the time skip. The unpacked boxes "
               + "in the bedroom go here; leave them inactive in the scene.")]
        [SerializeField] private GameObject[] showOnSkip = new GameObject[0];

        [Header("Timing")]
        [Tooltip("Seconds of walking allowed inside the room before the skip "
               + "starts. Keep it short; it only stops the cut landing on the "
               + "exact frame the player crosses the threshold.")]
        [SerializeField, Min(0f)] private float entryGraceSeconds = 0.35f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 2.5f;

        [Header("Closing the door")]
        [Tooltip("Tomás's bedroom door. Shutting it is the last thing the prologue "
               + "asks for, and the habit it teaches is the one days two and three "
               + "spend breaking.")]
        [SerializeField] private DoorDragInteractable bedroomDoor;

        [SerializeField] private string closeObjective = "OBJECTIVE: Close the door";

        [Tooltip("How long to wait for him to do it himself before the night starts "
               + "without him. The scene must never stall on a door.")]
        [SerializeField, Min(0f)] private float closeWaitSeconds = 8f;

        [Tooltip("Silence on the black before the latch, when he did not close it.")]
        [SerializeField, Min(0f)] private float latchDelaySeconds = 0.4f;
        [SerializeField, Min(0f)] private float blackBeforeTextSeconds = 0.6f;
        [SerializeField, Min(0f)] private float blackAfterTextSeconds = 0.5f;
        [SerializeField, Min(0f)] private float fadeInSeconds = 1.4f;

        [Header("Day one")]
        [Tooltip("Left empty on purpose while day one has no content yet.")]
        [SerializeField] private string day1Objective = string.Empty;

        [Tooltip("What runs once the image is open again. While day one has no "
               + "content this points at the false day so the beat can be played "
               + "and judged; when day one exists, this field moves to it and the "
               + "false day is armed from the end of day one instead.")]
        [SerializeField] private RebootFalseDay nextBeat;

        private bool armed;
        private bool started;
        private bool completed;
        private bool playerInside;

        private void Awake()
        {
            var trigger = GetComponent<Collider>();
            if (!trigger.isTrigger)
            {
                Debug.LogError("[RebootPrologueEnd] The collider must be a trigger.", this);
                enabled = false;
                return;
            }

            if (!ResolveReferences()) { enabled = false; return; }
            ValidateHideList();
        }

        /// <summary>
        /// Refuses to hide this object or anything it lives under.
        ///
        /// Deactivating the host kills the running coroutine mid-transition, which
        /// leaves the screen black and movement suppressed with no way back. It is
        /// an easy mistake to make while dressing the entrance boxes, so it fails
        /// loud here instead of stranding the player later.
        /// </summary>
        private void ValidateHideList()
        {
            for (int i = 0; i < hideOnSkip.Length; i++)
            {
                var candidate = hideOnSkip[i];
                if (candidate == null) continue;
                if (!transform.IsChildOf(candidate.transform)) continue;

                Debug.LogError("[RebootPrologueEnd] hideOnSkip[" + i + "] is '" + candidate.name +
                               "', which this component lives under. Hiding it would kill the " +
                               "transition and trap the player. Reference removed.", this);
                hideOnSkip[i] = null;
            }
        }

        private bool ResolveReferences()
        {
            if (motor == null) motor = FindObjectOfType<PlayerMotor>();
            if (motor != null)
            {
                if (input == null) input = motor.GetComponent<PlayerInputReader>();
                if (cameraPhysics == null) cameraPhysics = motor.GetComponent<CameraPhysics>();
                if (characterController == null)
                    characterController = motor.GetComponent<CharacterController>();
                if (hud == null) hud = motor.GetComponent<PrototypeHUD>();
            }
            if (fade == null) fade = FindObjectOfType<ScreenFade>();

            bool valid = day1Spawn != null && input != null && motor != null &&
                         cameraPhysics != null && characterController != null &&
                         fade != null && hud != null && skipText != null;
            if (!valid)
                Debug.LogError("[RebootPrologueEnd] Missing required scene references.", this);
            return valid;
        }

        /// <summary>
        /// Called once the room key is collected. Until then the bedroom door is
        /// locked, but arming explicitly keeps the dependency visible instead of
        /// relying on the lock being the only way in.
        /// </summary>
        public void Arm()
        {
            armed = true;
            Debug.Log("[RebootPrologueEnd] Room entry armed.", this);

            // Arming while the player already stands inside would otherwise wait
            // for an OnTriggerEnter that never comes again.
            if (playerInside) TryStart();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerMotor>() != motor) return;
            playerInside = true;
            TryStart();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<PlayerMotor>() != motor) return;
            playerInside = false;
        }

        private void TryStart()
        {
            if (!armed || started || !enabled) return;

            started = true;
            StartCoroutine(SkipRoutine());
        }

        private IEnumerator SkipRoutine()
        {
            yield return new WaitForSecondsRealtime(entryGraceSeconds);

            bool closedByHand = false;
            yield return WaitForDoor(result => closedByHand = result);

            input.SetMoveSuppressed(true);
            motor.StopImmediately();
            hud.SetPrompt(string.Empty);
            hud.SetObjective(string.Empty);
            hud.SetReticleHidden(true);

            fade.FadeTo(1f, fadeOutSeconds);
            yield return WaitForFade();

            // If he never touched it, Tomás closes it once the picture is gone.
            //
            // **Heard and not seen, and that is the whole point.** A door that
            // swings shut by itself in front of the player is a ghost, and this
            // game has none — Rui frightens by being possible. On the black it is
            // just a man shutting his door before bed, which is what anyone does,
            // and the player simply was not watching.
            if (!closedByHand && bedroomDoor != null)
            {
                yield return new WaitForSecondsRealtime(latchDelaySeconds);
                bedroomDoor.SetOpen(false, true);
            }

            // Same reason as the arrival: on black there is nothing to aim at,
            // and free mouse here would land day one pointing wherever the hand
            // drifted rather than at the framing the spawn rotation authored.
            input.SetLookSuppressed(true);
            ApplyTimeSkipWhileBlack();

            yield return new WaitForSecondsRealtime(blackBeforeTextSeconds);
            yield return PlaySkipText();
            yield return new WaitForSecondsRealtime(blackAfterTextSeconds);

            fade.FadeTo(0f, fadeInSeconds);
            input.SetLookSuppressed(false);
            yield return WaitForFade();

            hud.SetReticleHidden(false);
            hud.SetObjective(day1Objective);
            input.SetMoveSuppressed(false);
            completed = true;

            Debug.Log("[RebootPrologueEnd] Time skip complete; day one starts at " +
                      motor.transform.position.ToString("F3") + ".", this);

            if (nextBeat != null) nextBeat.Begin();

            enabled = false;
        }

        /// <summary>
        /// Asks him to shut the door and waits, but only for a while.
        ///
        /// Reports whether he did it himself, because the two cases sound the same
        /// but are not: one is his hand on the door, the other is the game covering
        /// for him on the black.
        /// </summary>
        private IEnumerator WaitForDoor(System.Action<bool> closedByHand)
        {
            if (bedroomDoor == null) { closedByHand(false); yield break; }

            if (!bedroomDoor.IsOpen) { closedByHand(true); yield break; }

            hud.SetObjective(closeObjective);

            float waited = 0f;
            while (waited < closeWaitSeconds)
            {
                if (!bedroomDoor.IsOpen)
                {
                    // A breath after the latch, so the picture does not vanish on
                    // the same frame as his own hand.
                    yield return new WaitForSecondsRealtime(0.7f);
                    closedByHand(true);
                    yield break;
                }

                waited += Time.deltaTime;
                yield return null;
            }

            closedByHand(false);
        }

        private IEnumerator PlaySkipText()
        {
            // A sequence on an inactive object never runs its Update, so IsRunning
            // would stay true for ever and the player would sit in the dark.
            if (!skipText.isActiveAndEnabled)
            {
                Debug.LogWarning("[RebootPrologueEnd] Skip text is switched off; " +
                                 "cutting straight to day one.", this);
                yield break;
            }

            skipText.Play();
            while (skipText.IsRunning)
                yield return null;
        }

        private void ApplyTimeSkipWhileBlack()
        {
            PlacePlayer(day1Spawn.position, day1Spawn.rotation);

            foreach (var go in hideOnSkip)
                if (go != null) go.SetActive(false);

            foreach (var go in showOnSkip)
                if (go != null) go.SetActive(true);
        }

        private void PlacePlayer(Vector3 position, Quaternion rotation)
        {
            bool controllerWasEnabled = characterController.enabled;
            if (controllerWasEnabled) characterController.enabled = false;

            motor.transform.SetPositionAndRotation(position, rotation);

            if (controllerWasEnabled) characterController.enabled = true;
            cameraPhysics.AlignToBody();
            motor.StopImmediately();
        }

        private IEnumerator WaitForFade()
        {
            while (fade != null && fade.IsFading)
                yield return null;
        }

        private void OnDisable()
        {
            // If the sequence is cut short, give the body back rather than leaving
            // the player standing in the dark unable to walk.
            if (started && !completed)
            {
                if (input != null)
                {
                    input.SetMoveSuppressed(false);
                    input.SetLookSuppressed(false);
                }
                if (hud != null) hud.SetReticleHidden(false);
                if (fade != null) fade.FadeTo(0f, 0f);
                Debug.LogWarning("[RebootPrologueEnd] Transition interrupted; control restored.", this);
            }
        }
    }
}
