using System.Collections;
using Pungent.NPC;
using Pungent.Player;
using TMPro;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// First playable block of the apartment reboot.
    ///
    /// It temporarily moves the existing player into the isolated elevator set,
    /// leaves camera look active, suppresses walking, then returns the player to
    /// the apartment while the screen is fully black.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RebootElevatorIntro : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private GameObject introRoot;
        [SerializeField] private Transform elevatorSpawn;
        [SerializeField] private Transform apartmentArrivalSpawn;
        [SerializeField] private Transform initialLookTarget;
        [SerializeField] private Transform[] movingVisuals = new Transform[0];
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private CameraPhysics cameraPhysics;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private ScreenFade fade;

        [Tooltip("Written framing that runs on black before the cabin starts moving.")]
        [SerializeField] private IntroTextSequence introText;

        [Header("Rui introduction")]
        [SerializeField] private PrototypeNpcRoutine ruiRoutine;
        [SerializeField] private Transform ruiGreetingAnchor;
        [SerializeField] private RebootRuiIntroduction ruiIntroduction;

        [Header("Floor display")]
        [SerializeField] private TMP_Text floorDisplay;
        [SerializeField] private int startingFloor;
        [SerializeField] private int destinationFloor = 8;

        [Header("Audio")]
        [SerializeField] private AudioSource elevatorAudio;
        [SerializeField] private AudioClip elevatorLoop;
        [SerializeField, Range(0f, 1f)] private float elevatorVolume = 0.35f;
        [SerializeField, Min(0f)] private float elevatorFadeOutSeconds = 0.45f;

        [Header("Arrival door audio")]
        [SerializeField] private AudioSource arrivalAudio;
        [SerializeField] private AudioClip doorKnock;
        [SerializeField] private AudioClip doorOpening;
        [SerializeField, Range(0f, 1f)] private float knockVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float doorOpeningVolume = 0.72f;
        [SerializeField, Min(0f)] private float blackBeforeKnockSeconds = 0.45f;
        [SerializeField, Min(0f)] private float doorResponseSeconds = 1.65f;
        [SerializeField, Min(0f)] private float blackAfterDoorSeconds = 0.25f;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float openingBlackSeconds = 0.35f;
        [SerializeField, Min(0f)] private float openingFadeSeconds = 1.15f;
        [SerializeField, Min(0.5f)] private float rideSeconds = 8f;

        [Header("Thought on the way up")]
        [Tooltip("Spoken in the cabin, on the picture, not on the black. The cards "
               + "before this are Tomás explaining himself; this is him with "
               + "nothing left to do but stand there and wonder.")]
        [SerializeField] private Pungent.Dialogue.ThoughtLineSet rideThought;

        [Tooltip("Seconds into the ride. Late enough that the player has looked "
               + "around the cabin first.")]
        [SerializeField, Min(0f)] private float rideThoughtDelay = 3.2f;

        [SerializeField] private Pungent.Dialogue.WorldDialogueController dialogue;
        [SerializeField, Min(0f)] private float closingFadeSeconds = 0.75f;
        [SerializeField, Min(0f)] private float apartmentFadeSeconds = 1.1f;

        [Header("Cabin motion")]
        [SerializeField, Min(0f)] private float verticalAmplitude = 0.0035f;
        [SerializeField, Min(0.1f)] private float verticalFrequency = 2.1f;
        [SerializeField, Min(0f)] private float settleKick = 0.012f;

        private Vector3 apartmentPosition;
        private Quaternion apartmentRotation;
        private Vector3[] visualBasePositions;
        private float rideStartedAt;
        private bool rideActive;
        private bool transitionCompleted;

        private IEnumerator Start()
        {
            if (!ResolveReferences())
            {
                enabled = false;
                yield break;
            }

            CaptureVisualBases();
            apartmentPosition = apartmentArrivalSpawn.position;
            apartmentRotation = apartmentArrivalSpawn.rotation;
            SetDisplayedFloor(startingFloor);

            fade.FadeTo(1f, 0f);
            PlacePlayer(elevatorSpawn.position, elevatorSpawn.rotation);
            input.SetMoveSuppressed(true);
            motor.StopImmediately();

            // The player is already standing in the cabin behind the black while
            // this reads, so nothing has to be staged again afterwards.
            yield return PlayIntroText();

            StartElevatorAudio();
            rideStartedAt = Time.time;
            rideActive = true;

            yield return HoldInitialLook(openingBlackSeconds);
            fade.FadeTo(0f, openingFadeSeconds);
            yield return WaitForFade();

            StartCoroutine(SpeakRideThought());

            yield return new WaitForSeconds(rideSeconds);
            rideActive = false;
            SetDisplayedFloor(destinationFloor);
            RestoreVisualBases();
            yield return FadeOutElevatorAudio();
            cameraPhysics.NotifyLanded(-Mathf.Lerp(2f, 5f, Mathf.Clamp01(settleKick / 0.02f)));

            fade.FadeTo(1f, closingFadeSeconds);
            yield return WaitForFade();

            // There is nothing to look at on a black screen, and the mouse would
            // otherwise carry whatever it did during the ride straight into the
            // arrival — the apartment opening on a wall instead of the framing
            // the spawn rotation authored.
            input.SetLookSuppressed(true);

            CompleteTransitionWhileBlack();
            yield return PlayArrivalDoorSequence();

            fade.FadeTo(0f, apartmentFadeSeconds);
            // Looking comes back as the image opens, the same handover the
            // opening black already uses.
            input.SetLookSuppressed(false);
            yield return WaitForFade();

            ruiIntroduction.Arm();
            Debug.Log("[RebootElevator] Intro complete; player arrived at the apartment entrance " +
                      "at " + motor.transform.position.ToString("F3") + ".", this);
            input.SetMoveSuppressed(false);
            transitionCompleted = true;
            enabled = false;
        }

        private void LateUpdate()
        {
            if (!rideActive || movingVisuals == null || visualBasePositions == null)
                return;

            float elapsed = Time.time - rideStartedAt;
            UpdateFloorDisplay(elapsed);
            float wave = Mathf.Sin(elapsed * verticalFrequency * Mathf.PI * 2f);
            float uneven = Mathf.PerlinNoise(elapsed * 1.7f, 31.7f) * 2f - 1f;
            float y = wave * verticalAmplitude + uneven * verticalAmplitude * 0.45f;

            for (int i = 0; i < movingVisuals.Length; i++)
            {
                if (movingVisuals[i] != null)
                    movingVisuals[i].localPosition = visualBasePositions[i] + Vector3.up * y;
            }
        }

        private void UpdateFloorDisplay(float elapsed)
        {
            if (floorDisplay == null) return;

            int direction = destinationFloor >= startingFloor ? 1 : -1;
            int floorCount = Mathf.Abs(destinationFloor - startingFloor) + 1;
            float progress = rideSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / rideSeconds);
            int step = Mathf.Min(floorCount - 1, Mathf.FloorToInt(progress * floorCount));
            SetDisplayedFloor(startingFloor + step * direction);
        }

        private void SetDisplayedFloor(int floor)
        {
            if (floorDisplay != null)
                floorDisplay.text = floor.ToString();
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
            }

            bool valid = introRoot != null && elevatorSpawn != null &&
                         apartmentArrivalSpawn != null && input != null &&
                         motor != null && cameraPhysics != null &&
                         characterController != null && fade != null &&
                         introText != null &&
                         ruiRoutine != null && ruiGreetingAnchor != null &&
                         ruiIntroduction != null && arrivalAudio != null &&
                         doorKnock != null && doorOpening != null;
            if (!valid)
                Debug.LogError("[RebootElevator] Missing required scene references.", this);
            return valid;
        }

        private void CaptureVisualBases()
        {
            if (movingVisuals == null)
            {
                visualBasePositions = new Vector3[0];
                return;
            }

            visualBasePositions = new Vector3[movingVisuals.Length];
            for (int i = 0; i < movingVisuals.Length; i++)
            {
                if (movingVisuals[i] != null)
                    visualBasePositions[i] = movingVisuals[i].localPosition;
            }
        }

        private void RestoreVisualBases()
        {
            if (movingVisuals == null || visualBasePositions == null) return;
            for (int i = 0; i < movingVisuals.Length; i++)
            {
                if (movingVisuals[i] != null)
                    movingVisuals[i].localPosition = visualBasePositions[i];
            }
        }

        private void StartElevatorAudio()
        {
            if (elevatorAudio == null || elevatorLoop == null) return;

            elevatorAudio.clip = elevatorLoop;
            elevatorAudio.loop = true;
            elevatorAudio.playOnAwake = false;
            elevatorAudio.spatialBlend = 0f;
            elevatorAudio.volume = elevatorVolume;
            elevatorAudio.Play();
        }

        private IEnumerator FadeOutElevatorAudio()
        {
            if (elevatorAudio == null || !elevatorAudio.isPlaying)
                yield break;

            float initialVolume = elevatorAudio.volume;
            float elapsed = 0f;
            while (elapsed < elevatorFadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elevatorFadeOutSeconds <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / elevatorFadeOutSeconds);
                elevatorAudio.volume = Mathf.Lerp(initialVolume, 0f, t);
                yield return null;
            }

            elevatorAudio.Stop();
            elevatorAudio.volume = elevatorVolume;
        }

        private IEnumerator PlayArrivalDoorSequence()
        {
            arrivalAudio.playOnAwake = false;
            arrivalAudio.loop = false;
            arrivalAudio.spatialBlend = 0f;

            yield return new WaitForSecondsRealtime(blackBeforeKnockSeconds);
            PlayArrivalClip(doorKnock, knockVolume);
            Debug.Log("[RebootElevator] Arrival knock played while black.", this);
            yield return new WaitForSecondsRealtime(doorKnock.length + doorResponseSeconds);

            PlayArrivalClip(doorOpening, doorOpeningVolume);
            Debug.Log("[RebootElevator] Arrival door opened while black.", this);
            yield return new WaitForSecondsRealtime(doorOpening.length + blackAfterDoorSeconds);
        }

        private void PlayArrivalClip(AudioClip clip, float volume)
        {
            arrivalAudio.pitch = 1f;
            arrivalAudio.PlayOneShot(clip, volume);
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

        private void CompleteTransitionWhileBlack()
        {
            RestoreVisualBases();
            PlacePlayer(apartmentPosition, apartmentRotation);
            if (ruiRoutine.HoldAt(ruiGreetingAnchor))
            {
                Debug.Log("[RebootRuiIntro] Rui staged at " +
                          ruiGreetingAnchor.position.ToString("F3") + ".", ruiRoutine);
            }
            if (introRoot != null) introRoot.SetActive(false);
        }

        private IEnumerator PlayIntroText()
        {
            // A sequence on an inactive object never runs its Update, so IsRunning
            // would stay true for ever and the cabin would never start moving.
            if (!introText.isActiveAndEnabled)
            {
                Debug.LogWarning("[RebootElevator] Intro text is switched off; " +
                                 "starting the ride without it.", this);
                yield break;
            }

            introText.Play();
            while (introText.IsRunning)
                yield return null;

            // The sequence hands the components it silenced back on its way out.
            // Walking must stay locked: the cabin ride owns the body from here.
            input.SetMoveSuppressed(true);
            motor.StopImmediately();
        }

        private IEnumerator WaitForFade()
        {
            while (fade != null && fade.IsFading)
                yield return null;
        }

        /// <summary>
        /// One line, part way up, using the same subtitle the rest of the game
        /// thinks in — not another card. A card is the game talking to the player;
        /// this has to read as the man in the lift talking to himself.
        /// </summary>
        private IEnumerator SpeakRideThought()
        {
            if (rideThought == null || rideThought.Count == 0) yield break;

            yield return new WaitForSeconds(rideThoughtDelay);

            if (dialogue == null) dialogue = FindObjectOfType<Pungent.Dialogue.WorldDialogueController>();
            if (dialogue == null)
            {
                Debug.LogWarning("[RebootElevatorIntro] No dialogue controller for the ride thought.", this);
                yield break;
            }

            int cursor = rideThought.NewCursor;
            dialogue.ShowThought(rideThought.Pick(ref cursor), rideThought.HoldSeconds);
        }

        private IEnumerator HoldInitialLook(float seconds)
        {
            float elapsed = 0f;
            do
            {
                if (initialLookTarget != null)
                    cameraPhysics.ForceLookAt(initialLookTarget.position);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            while (elapsed < seconds);

            if (initialLookTarget != null)
                cameraPhysics.ForceLookAt(initialLookTarget.position);
        }

        private void OnDisable()
        {
            RestoreVisualBases();
            if (elevatorAudio != null)
            {
                elevatorAudio.Stop();
                elevatorAudio.volume = elevatorVolume;
            }
            if (arrivalAudio != null) arrivalAudio.Stop();
            if (!transitionCompleted && input != null)
            {
                input.SetMoveSuppressed(false);
                input.SetLookSuppressed(false);
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(GameObject root, Transform spawn, Transform arrivalSpawn,
            Transform lookTarget,
            Transform[] visuals,
            PlayerInputReader playerInput, PlayerMotor playerMotor,
            CameraPhysics playerCamera, CharacterController controller, ScreenFade screenFade,
            TMP_Text display, AudioSource mechanicalAudio, AudioClip mechanicalLoop)
        {
            introRoot = root;
            elevatorSpawn = spawn;
            apartmentArrivalSpawn = arrivalSpawn;
            initialLookTarget = lookTarget;
            movingVisuals = visuals ?? new Transform[0];
            input = playerInput;
            motor = playerMotor;
            cameraPhysics = playerCamera;
            characterController = controller;
            fade = screenFade;
            floorDisplay = display;
            elevatorAudio = mechanicalAudio;
            elevatorLoop = mechanicalLoop;
        }
#endif
    }
}
