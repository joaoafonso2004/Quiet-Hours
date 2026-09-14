using System.Collections;
using Pungent.Interaction;
using Pungent.NPC;
using Pungent.Player;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// A day that turns out not to have happened.
    ///
    /// Tomás wakes, a knock pulls his eyes down the hall, and Rui is already
    /// running at him. The cut lands before he can do anything about it, and then
    /// he is back in bed, breathing hard, and only now does the real day start.
    ///
    /// **Rui is not in the flat until he is.** During the false day the whole NPC
    /// is switched off — no roaming body in the kitchen, no coughing from the next
    /// room. A nightmare with a housemate quietly making coffee in it is not a
    /// nightmare, and seeing him twice is what gives the trick away.
    ///
    /// **The knock does the directing.** Firing the scare on the doorway alone
    /// puts it behind whoever turned the other way, and a jumpscare nobody saw is
    /// a loud noise. A knock on the front door earns the look instead of forcing
    /// it, and it comes from exactly where Rui will.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RebootFalseDay : MonoBehaviour
    {
        private enum Phase { Idle, Running, Done }

        [Header("Poses")]
        [Tooltip("Where Tomás lies — placed at EYE height, not at his feet.\n\n"
               + "Whoever drags this in the scene is looking for the shot: the "
               + "pillow, the ceiling above it, the angle. Asking them to place a "
               + "point 1.65 m under the head they can see is asking them to do "
               + "arithmetic instead of composition, and it put the first waking "
               + "up near the ceiling.")]
        [SerializeField] private Transform bedPose;

        [Tooltip("Where he ends up standing once he is on his feet.")]
        [SerializeField] private Transform standPose;

        [Tooltip("Where Rui appears. Distance from the bedroom door decides how "
               + "long the charge lasts.")]
        [SerializeField] private Transform ruiStart;

        [Tooltip("Where the knock comes from. The front door, so the sound and the "
               + "thing that follows it arrive from the same side.")]
        [SerializeField] private Transform knockAt;

        [Tooltip("Crossing this is what arms the look check. Sits in the doorway.")]
        [SerializeField] private Collider exitVolume;

        [Header("Scene references")]
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private CameraPhysics cameraPhysics;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private ScreenFade fade;
        [SerializeField] private PrototypeHUD hud;
        [SerializeField] private PrototypeNpcRoutine rui;
        [SerializeField] private Camera playerCamera;

        [Header("Sound")]
        [SerializeField] private AudioSource sound;

        [Tooltip("A 3D source sitting at the front door. Separate from the flat "
               + "one above because the knocking has to come from a place — that "
               + "is the whole job it does — and because it has to be stoppable: "
               + "the clip knocks for as long as it takes the player to look, and "
               + "carrying on under the scream would be absurd.")]
        [SerializeField] private AudioSource knockSource;
        [SerializeField] private AudioClip knockClip;
        [SerializeField] private AudioClip screamClip;
        [SerializeField] private AudioClip stingClip;
        [SerializeField] private AudioClip breathClip;
        [SerializeField, Range(0f, 1f)] private float knockVolume = 0.95f;
        [SerializeField, Range(0f, 1f)] private float screamVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float stingVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float breathVolume = 0.9f;

        [Header("Timing")]
        [SerializeField, Min(0.1f)] private float wakeRiseSeconds = 1.1f;

        [Tooltip("How high above the pillow he stares while flat on his back. This "
               + "is the first thing he sees on both wakings, and it is why the "
               + "second one reads as waking rather than as a cut.")]
        [SerializeField, Min(0.5f)] private float ceilingLookHeight = 2.2f;

        [Tooltip("Eye height sitting on the edge of the bed.")]
        [SerializeField, Min(0.5f)] private float sitEyeHeight = 1.15f;

        [Tooltip("The sit-up. Fast on purpose: it is the body reacting, not a "
               + "camera move.")]
        [SerializeField, Min(0.05f)] private float sitUpSeconds = 0.38f;

        [Tooltip("Seconds into the black before the knocking starts. It has to "
               + "begin BEFORE the image opens: that way the knocking is what woke "
               + "him, rather than something that happens to start once he is up.")]
        [SerializeField, Min(0f)] private float knockDelay = 0.15f;

        [Tooltip("If he leaves the room and never looks down the hall, the scare "
               + "waits for ever. After this long the scream fires anyway — a "
               + "scream behind you turns you round on its own.")]
        [SerializeField, Min(0.5f)] private float lookTimeout = 5f;

        [Tooltip("How long he lies there breathing after the second waking, before "
               + "his body is his again.")]
        [SerializeField, Min(0f)] private float lyingSeconds = 5f;

        [SerializeField, Min(0.1f)] private float secondRiseSeconds = 1.3f;

        [Tooltip("How far off centre Rui can be and still count as seen.")]
        [SerializeField, Range(10f, 90f)] private float lookAngle = 40f;

        [SerializeField, Min(1f)] private float chargeSpeed = 20f;

        [Tooltip("Where the run ends and the jumpscare takes over.")]
        [SerializeField, Min(0.5f)] private float cutDistance = 1.8f;

        [Header("Jumpscare")]
        [Tooltip("Distance from the camera to HIS HEAD, not to his feet.\n\n"
               + "Measuring to the root put the face 16 cm further away than the "
               + "number said, because this model's head sits well behind its "
               + "origin. At 0.13 the head covers most of the frame; the near "
               + "clip is at 0.05, so there is room but not much.")]
        [SerializeField, Min(0.08f)] private float faceDistance = 0.13f;

        [Tooltip("How far below eye level his face lands. Slightly under, so he "
               + "is looking up into the camera.")]
        [SerializeField] private float faceDrop = 0.02f;

        [Tooltip("Height of his eyes above the head bone.\n\nThe bone sits at the "
               + "base of the skull on this rig, so aiming straight at it framed "
               + "the shot on his mouth. This lifts the target to where a face "
               + "actually is.")]
        [SerializeField] private float faceEyeOffset = 0.09f;

        [Header("Chase look")]
        [Tooltip("The global post volume. The vignette is squeezed while he runs "
               + "and let go afterwards, so the walls appear to close in without "
               + "anything in the room actually moving.")]
        [SerializeField] private UnityEngine.Rendering.Volume postVolume;

        [SerializeField, Range(0f, 1f)] private float chaseVignette = 0.55f;

        [SerializeField, Min(0.05f)] private float jumpscareSeconds = 0.3f;
        [SerializeField, Min(0.1f)] private float blackSeconds = 1.1f;
        [SerializeField, Min(1f)] private float maxChargeSeconds = 6f;
        [SerializeField] private string objectiveDuringFalseDay = string.Empty;

        [Tooltip("Switched off for the length of the dream and put back afterwards.\n\n"
               + "Rui himself is switched off during the false day, so anything "
               + "that would summon him — his room's territory, above all — has to "
               + "go quiet too. Otherwise walking into his room mid-nightmare calls "
               + "for a man who is not in the building.")]
        [SerializeField] private Behaviour[] suspendDuringDream = new Behaviour[0];

        [Header("Next beat")]
        [Tooltip("The first Day 1 objective, armed the moment he gets up from the "
               + "second waking. "
               + "Mandatory. The false day clears the objective line on the way in "
               + "and nothing else puts one back: without this the player stands up "
               + "from the nightmare into a flat with no objective, no prompt and no "
               + "error in the console. That is how the owner found it.")]
        [SerializeField] private RebootEatFromFridge nextBeat;

        [Header("Thoughts")]
        [SerializeField] private Pungent.Dialogue.WorldDialogueController dialogue;

        [Tooltip("Thought while he is still on his back, with the knocking going.")]
        [SerializeField] private Pungent.Dialogue.ThoughtLineSet wakeThought;

        [Tooltip("Thought on the second waking, once the breathing has settled.")]
        [SerializeField] private Pungent.Dialogue.ThoughtLineSet afterScareThought;

        private Phase phase = Phase.Idle;
        private Vector3 ruiHomePosition;
        private Quaternion ruiHomeRotation;
        private bool ruiWasRoaming;
        private UnityEngine.Rendering.Universal.Vignette vignette;
        private float baseVignette;
        private Collider ruiBody;
        private bool[] suspendedWere;

        private void Awake()
        {
            if (motor == null) motor = FindObjectOfType<PlayerMotor>();
            if (motor != null)
            {
                if (input == null) input = motor.GetComponent<PlayerInputReader>();
                if (cameraPhysics == null) cameraPhysics = motor.GetComponent<CameraPhysics>();
                if (characterController == null) characterController = motor.GetComponent<CharacterController>();
                if (hud == null) hud = motor.GetComponent<PrototypeHUD>();
            }
            if (fade == null) fade = FindObjectOfType<ScreenFade>();
            if (rui == null) rui = FindObjectOfType<PrototypeNpcRoutine>();
            if (playerCamera == null) playerCamera = Camera.main;

            bool valid = bedPose != null && standPose != null && ruiStart != null &&
                         exitVolume != null && input != null && motor != null &&
                         cameraPhysics != null && characterController != null &&
                         fade != null && hud != null && rui != null &&
                         playerCamera != null && nextBeat != null;
            if (!valid)
            {
                Debug.LogError("[RebootFalseDay] Missing required scene references.", this);
                enabled = false;
            }
        }

        /// <summary>Starts the false day. Nothing happens until something calls this.</summary>
        public void Begin()
        {
            if (phase != Phase.Idle || !enabled) return;
            phase = Phase.Running;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            ruiHomePosition = rui.transform.position;
            ruiHomeRotation = rui.transform.rotation;

            // If something else already had hold of him — a conversation, a staged
            // beat — handing him back to the roaming routine afterwards would be
            // this component quietly ending someone else's scene.
            ruiWasRoaming = rui.enabled && !rui.InConversation;

            rui.gameObject.SetActive(false);
            SuspendDreamConflicts(true);

            // The knocking starts under the black and keeps going. He is not woken
            // by a clock; he is woken by someone at the door who will not stop.
            StartCoroutine(KnockAfterDelay());

            yield return LieDown(0f);
            Speak(wakeThought);
            yield return SitUp();
            yield return Rise(wakeRiseSeconds);

            hud.SetObjective(objectiveDuringFalseDay);

            yield return WaitForPlayerToLeaveRoom();
            if (knockSource != null) knockSource.Stop();

            yield return WaitForPlayerToLook();

            yield return Charge();
            yield return Jumpscare();

            // Hard cut. No fade: a jumpscare that dissolves is a transition, and a
            // transition gives the player time to understand what he saw.
            fade.FadeTo(1f, 0f);
            SetVignette(baseVignette);
            RestoreRui();

            yield return new WaitForSecondsRealtime(blackSeconds);

            // Second waking. The breath starts on the black, so the sound arrives
            // before the picture and the player is already short of air when the
            // ceiling comes back.
            // Spoken part way through the lying hold, and from a coroutine of its
            // own: splitting the hold in two would drop the frame-by-frame hold on
            // the ceiling look and let the camera drift mid-thought.
            Play(breathClip, breathVolume);
            StartCoroutine(SpeakAfter(lyingSeconds * 0.45f, afterScareThought));
            yield return LieDown(lyingSeconds);
            yield return SitUp();
            yield return Rise(secondRiseSeconds);

            // The real day starts here, and it has to be said out loud. The waking
            // hands control back and then stops caring; if nobody arms the next
            // step the player is left standing in the bedroom with an empty
            // objective line, which reads as the game having forgotten him.
            nextBeat.SetArmed(true);

            phase = Phase.Done;
            Debug.Log("[RebootFalseDay] False day finished; Day 1 armed.", this);
        }

        private IEnumerator KnockAfterDelay()
        {
            yield return new WaitForSecondsRealtime(knockDelay);
            if (knockClip == null || knockSource == null) yield break;

            if (knockAt != null) knockSource.transform.position = knockAt.position;
            knockSource.clip = knockClip;
            knockSource.volume = knockVolume;

            // Loops until he is out of the room. Whoever is at that door is not
            // going to give up and go away, and that is the whole reason he gets up.
            knockSource.loop = true;
            knockSource.Play();
        }

        private IEnumerator SpeakAfter(float seconds, Pungent.Dialogue.ThoughtLineSet lines)
        {
            yield return new WaitForSeconds(seconds);
            Speak(lines);
        }

        private void Speak(Pungent.Dialogue.ThoughtLineSet lines)
        {
            if (lines == null || lines.Count == 0) return;
            if (dialogue == null) dialogue = FindObjectOfType<Pungent.Dialogue.WorldDialogueController>();
            if (dialogue == null) return;

            int cursor = lines.NewCursor;
            dialogue.ShowThought(lines.Pick(ref cursor), lines.HoldSeconds);
        }

        /// <summary>
        /// Puts the player in bed on black and opens the image on him lying there.
        ///
        /// This is not the lying-down mechanic the reboot cut. It is two authored
        /// transforms and an interpolation between them, which buys the same
        /// feeling for none of the systems.
        /// </summary>
        /// <summary>
        /// Flat on his back, looking at the ceiling.
        ///
        /// The ceiling is the point. Opening on a room at standing height is a
        /// camera being placed; opening on a ceiling and then sitting up is a
        /// person waking, and the second waking only lands as a waking because
        /// the first one taught the player what it looks like.
        /// </summary>
        private IEnumerator LieDown(float holdSeconds)
        {
            TakeControl();
            fade.FadeTo(1f, 0f);

            // The controller stays off for the whole time he is down. With it on,
            // gravity and the floor collider argue with a body whose head is at
            // mattress height and shove him back upright mid-breath.
            characterController.enabled = false;
            MoveBody(RootFor(bedPose.position), FlatBedRotation());
            cameraPhysics.ForceLookAt(CeilingPoint());

            yield return new WaitForSecondsRealtime(0.35f);

            fade.FadeTo(0f, 0.6f);
            while (fade.IsFading)
            {
                cameraPhysics.ForceLookAt(CeilingPoint());
                yield return null;
            }

            float elapsed = 0f;
            while (elapsed < holdSeconds)
            {
                elapsed += Time.deltaTime;
                cameraPhysics.ForceLookAt(CeilingPoint());
                yield return null;
            }
        }

        /// <summary>
        /// Sits him up: the eyes travel from the pillow to sitting height while the
        /// look drops from the ceiling to straight ahead, along the bed rather than
        /// at the door.
        /// </summary>
        private IEnumerator SitUp()
        {
            Vector3 fromEye = bedPose.position;
            Vector3 toEye = new Vector3(bedPose.position.x, sitEyeHeight, bedPose.position.z);
            Vector3 ahead = ForwardPoint(sitEyeHeight);
            Vector3 ceiling = CeilingPoint();

            float elapsed = 0f;
            while (elapsed < sitUpSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Ease(Mathf.Clamp01(elapsed / sitUpSeconds));
                MoveBody(RootFor(Vector3.Lerp(fromEye, toEye, t)), FlatBedRotation());

                // The look target travels along an arc rather than a straight line:
                // lerping between a point overhead and a point ahead passes through
                // the middle of the room, and the camera swings through the wall on
                // its way down. Slerp keeps it on the sphere around the head, which
                // is how a neck works.
                cameraPhysics.ForceLookAt(ArcBetween(ceiling, ahead, EyeNow(), t));
                yield return null;
            }

            MoveBody(RootFor(toEye), FlatBedRotation());
            cameraPhysics.ForceLookAt(ahead);
        }

        /// <summary>
        /// Slow in, slow out, but weighted to leave quickly — a body coming up off
        /// a mattress starts fast and settles, it does not ease in from nothing.
        /// </summary>
        private static float Ease(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        private Vector3 EyeNow() => playerCamera.transform.position;

        private static Vector3 ArcBetween(Vector3 from, Vector3 to, Vector3 pivot, float t)
        {
            Vector3 a = from - pivot;
            Vector3 b = to - pivot;
            float radius = Mathf.Lerp(a.magnitude, b.magnitude, t);
            return pivot + Vector3.Slerp(a.normalized, b.normalized, t) * radius;
        }

        /// <summary>
        /// Gives the body back its collider.
        ///
        /// <see cref="LieDown"/> switches the controller off so the floor stops
        /// arguing with a head at mattress height, and <see cref="Place"/> restores
        /// whatever state it found — which, after lying down, is off. That left the
        /// player standing beside his bed unable to walk, with nothing in the
        /// console to say why.
        /// </summary>
        private void EnableBody()
        {
            if (!characterController.enabled) characterController.enabled = true;
        }

        /// <summary>Body position that puts the eyes at a given world point.</summary>
        private Vector3 RootFor(Vector3 eyePosition)
        {
            float eyeHeight = playerCamera.transform.position.y - motor.transform.position.y;
            return eyePosition - Vector3.up * eyeHeight;
        }

        /// <summary>
        /// The bed's yaw with the tilt thrown away. The roll the owner authored
        /// sells lying down, but carrying it into the body would leave the room
        /// crooked once he is upright.
        /// </summary>
        private Quaternion FlatBedRotation() =>
            Quaternion.Euler(0f, bedPose.eulerAngles.y, 0f);

        private Vector3 CeilingPoint() =>
            bedPose.position + Vector3.up * ceilingLookHeight;

        private Vector3 ForwardPoint(float height)
        {
            Vector3 forward = FlatBedRotation() * Vector3.forward;
            Vector3 point = bedPose.position + forward * 3f;
            point.y = height;
            return point;
        }

        /// <summary>
        /// Standing up and walking clear of the bed, with the eyes led rather than
        /// dragged.
        ///
        /// The first version moved the body and re-aligned the camera to it every
        /// frame, which is two systems writing the same rotation — the align
        /// snapped it back to the body's yaw and the next frame's look pulled it
        /// off again. That fight is what read as jank. Now the body is moved and
        /// the camera is aimed, and only one of them owns the rotation.
        /// </summary>
        private IEnumerator Rise(float seconds)
        {
            float elapsed = 0f;
            Vector3 from = motor.transform.position;
            Quaternion fromRotation = motor.transform.rotation;
            Vector3 lookFrom = ForwardPoint(sitEyeHeight);
            Vector3 lookTo = standPose.position + standPose.rotation * Vector3.forward * 3f;
            lookTo.y = standPose.position.y + (playerCamera.transform.position.y - motor.transform.position.y);

            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                float t = Ease(Mathf.Clamp01(elapsed / seconds));
                MoveBody(Vector3.Lerp(from, standPose.position, t),
                         Quaternion.Slerp(fromRotation, standPose.rotation, t));
                cameraPhysics.ForceLookAt(ArcBetween(lookFrom, lookTo, EyeNow(), t));
                yield return null;
            }

            EnableBody();
            Place(standPose.position, standPose.rotation);
            GiveControlBack();
        }

        /// <summary>
        /// Moves the body and nothing else — no camera align, no controller state.
        /// Whoever is aiming the camera this frame stays in charge of it.
        /// </summary>
        private void MoveBody(Vector3 position, Quaternion rotation)
        {
            motor.transform.SetPositionAndRotation(position, rotation);
            motor.StopImmediately();
        }

        private IEnumerator WaitForPlayerToLeaveRoom()
        {
            while (!exitVolume.bounds.Contains(motor.transform.position))
                yield return null;
        }

        /// <summary>
        /// Waits for him to face the hall — but not for ever.
        ///
        /// Walking out and turning left towards the kitchen used to strand the
        /// whole beat: Rui waited politely at the far end for a look that never
        /// came. The timeout fires the scream anyway, which is the better version
        /// of the same idea — a scream behind you turns you round by itself, and
        /// the camera takes over from there.
        /// </summary>
        private IEnumerator WaitForPlayerToLook()
        {
            float waited = 0f;
            while (waited < lookTimeout)
            {
                Vector3 toRui = ruiStart.position - playerCamera.transform.position;
                toRui.y = 0f;
                Vector3 facing = playerCamera.transform.forward;
                facing.y = 0f;
                if (Vector3.Angle(facing, toRui) <= lookAngle) yield break;

                waited += Time.deltaTime;
                yield return null;
            }

            Debug.Log("[RebootFalseDay] Look never came; firing from behind him.", this);
        }

        private IEnumerator Charge()
        {
            if (knockSource != null) knockSource.Stop();
            CacheVignette();

            rui.gameObject.SetActive(true);
            rui.HoldAt(ruiStart);

            // Reactivating the object runs the agent's OnEnable, which puts the
            // body back on the navmesh where it last stood — after HoldAt has
            // already moved it. Turning the agent off and writing the position
            // again is what actually makes him appear at the far end of the hall
            // instead of back in the kitchen.
            var agent = rui.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled) agent.enabled = false;
            rui.transform.SetPositionAndRotation(ruiStart.position, ruiStart.rotation);

            // His capsule has to go. Left on, it meets the player's controller and
            // shoves him backwards: asking for a face at 13 cm produced one at 67,
            // because the physics moved the camera away from what had just been
            // placed in front of it. He cannot touch the player anyway — the screen
            // is black before he arrives.
            ruiBody = rui.GetComponent<Collider>();
            if (ruiBody != null) ruiBody.enabled = false;

            // HoldAt parks him and disables the agent, but the routine keeps
            // updating and takes the body back on the next frame — its own state
            // machine wins over a transform written from outside. Switching the
            // component off for the length of the charge is the only way to own
            // the movement; RestoreRui turns it back on through ResumeRoutine.
            rui.enabled = false;

            Play(screamClip, screamVolume);

            // The look is taken away the moment he starts moving. Being able to
            // turn away from this would let the player opt out of the only thing
            // the beat exists to do.
            input.SetLookSuppressed(true);

            var animator = rui.Animator;
            var head = rui.transform;
            float elapsed = 0f;
            float startDistance = Vector3.Distance(head.position, motor.transform.position);

            while (elapsed < maxChargeSeconds)
            {
                elapsed += Time.deltaTime;

                Vector3 target = motor.transform.position;
                Vector3 flat = new Vector3(target.x, head.position.y, target.z);
                head.position = Vector3.MoveTowards(head.position, flat, chargeSpeed * Time.deltaTime);

                Vector3 facing = flat - head.position;
                if (facing.sqrMagnitude > 0.0001f)
                    head.rotation = Quaternion.LookRotation(facing, Vector3.up);

                if (animator != null && animator.runtimeAnimatorController != null)
                    animator.SetFloat("Speed", chargeSpeed);

                // Aim at his chest rather than his feet, so the camera does not
                // tip downwards as he closes.
                cameraPhysics.ForceLookAt(head.position + Vector3.up * 1.5f);

                // Tightens as he closes, so the squeeze is the distance and not a
                // timer — a player who backs away buys himself a slower vignette
                // as well as a longer run.
                float remaining = Vector3.Distance(head.position, target);
                float closed = startDistance <= 0.01f ? 1f
                    : Mathf.Clamp01(1f - remaining / startDistance);
                SetVignette(Mathf.Lerp(baseVignette, chaseVignette, closed));

                if (remaining <= cutDistance) yield break;

                yield return null;
            }
        }

        /// <summary>
        /// Reads the vignette once and remembers where it was.
        ///
        /// Uses <c>volume.profile</c> and never <c>sharedProfile</c>: the shared
        /// one is the asset on disk, and squeezing that would leave the whole game
        /// dark after a single play in the editor.
        /// </summary>
        private void CacheVignette()
        {
            if (postVolume == null || vignette != null) return;
            if (postVolume.profile != null &&
                postVolume.profile.TryGet(out UnityEngine.Rendering.Universal.Vignette found))
            {
                vignette = found;
                baseVignette = vignette.intensity.value;
            }
        }

        private void SetVignette(float value)
        {
            if (vignette == null) return;
            vignette.intensity.overrideState = true;
            vignette.intensity.value = value;
        }

        /// <summary>
        /// The scare itself: he is slammed into the camera, the sting fires, and
        /// the image holds just long enough to know what is in front of you.
        ///
        /// The run alone was not a jumpscare — it ended in a cut, and a cut is a
        /// full stop, not a shock.
        /// </summary>
        private IEnumerator Jumpscare()
        {
            TakeControl();

            Vector3 eye = playerCamera.transform.position;
            Vector3 forward = playerCamera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            var animator = rui.Animator;
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetFloat("Speed", 0f);

            SetVignette(chaseVignette);
            Play(stingClip, stingVolume);

            // Held every frame, not set once. `ForceLookAt` is a single write and
            // the camera drifts back to the body over the next few frames; the
            // elevator intro reasserts it in a loop for the same reason. Holding
            // the position too means a player still sliding cannot walk out of his
            // own jumpscare.
            float elapsed = 0f;
            while (elapsed < jumpscareSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                StickToFace(forward);
                yield return null;
            }
        }

        /// <summary>
        /// Puts his head exactly <see cref="faceDistance"/> in front of the eyes and
        /// aims at it.
        ///
        /// Placed by the head bone and not by the root: this model's head sits
        /// 16 cm behind its origin, so asking for 13 cm at the root delivered a
        /// face at 29.
        /// </summary>
        private void StickToFace(Vector3 forward)
        {
            rui.transform.rotation =
                Quaternion.LookRotation(new Vector3(-forward.x, 0f, -forward.z), Vector3.up);

            Vector3 eye = playerCamera.transform.position;
            Vector3 wanted = eye + forward * faceDistance - Vector3.up * faceDrop;

            Transform head = HeadBone();
            Vector3 faceOffset = head != null
                ? head.position + Vector3.up * faceEyeOffset - rui.transform.position
                : Vector3.up * 1.64f;

            rui.transform.position = wanted - faceOffset;
            cameraPhysics.ForceLookAt(rui.transform.position + faceOffset);
        }

        /// <summary>
        /// His head bone, or null if the rig has none. Used because the model's
        /// root is not under its head, and the scare is framed on the face.
        /// </summary>
        private Transform HeadBone()
        {
            var animator = rui.Animator;
            if (animator == null || !animator.isHuman) return null;
            return animator.GetBoneTransform(HumanBodyBones.Head);
        }

        /// <summary>
        /// Silences, or gives back, everything that would compete with the dream.
        /// Remembers what was already off so it never switches something on that
        /// the scene had deliberately left disabled.
        /// </summary>
        private void SuspendDreamConflicts(bool suspend)
        {
            if (suspendDuringDream == null) return;

            if (suspend)
            {
                suspendedWere = new bool[suspendDuringDream.Length];
                for (int i = 0; i < suspendDuringDream.Length; i++)
                {
                    if (suspendDuringDream[i] == null) continue;
                    suspendedWere[i] = suspendDuringDream[i].enabled;
                    suspendDuringDream[i].enabled = false;
                }
                return;
            }

            if (suspendedWere == null) return;
            for (int i = 0; i < suspendDuringDream.Length && i < suspendedWere.Length; i++)
                if (suspendDuringDream[i] != null) suspendDuringDream[i].enabled = suspendedWere[i];
            suspendedWere = null;
        }

        private void RestoreRui()
        {
            SuspendDreamConflicts(false);
            if (ruiBody != null) { ruiBody.enabled = true; ruiBody = null; }
            rui.transform.SetPositionAndRotation(ruiHomePosition, ruiHomeRotation);

            var animator = rui.Animator;
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetFloat("Speed", 0f);

            // Put him back on his feet before handing the body over, or the agent
            // comes back on somewhere it cannot path from.
            rui.enabled = true;
            if (ruiWasRoaming) rui.ResumeRoutine();
            else rui.SetConversationActive(true);
        }

        private void TakeControl()
        {
            input.SetMoveSuppressed(true);
            input.SetLookSuppressed(true);
            motor.StopImmediately();
            hud.SetPrompt(string.Empty);
            hud.SetReticleHidden(true);
        }

        private void GiveControlBack()
        {
            input.SetLookSuppressed(false);
            input.SetMoveSuppressed(false);
            hud.SetReticleHidden(false);
        }

        private void Place(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = characterController.enabled;
            if (wasEnabled) characterController.enabled = false;

            motor.transform.SetPositionAndRotation(position, rotation);

            if (wasEnabled) characterController.enabled = true;
            cameraPhysics.AlignToBody();
            motor.StopImmediately();
        }

        private void Play(AudioClip clip, float volume)
        {
            if (sound == null || clip == null) return;
            sound.PlayOneShot(clip, volume);
        }

        private void OnDisable()
        {
            // Never leave the player black and frozen, or Rui switched off, because
            // something turned this component off mid-beat.
            if (phase != Phase.Running) return;
            if (rui != null && !rui.gameObject.activeSelf) rui.gameObject.SetActive(true);
            SuspendDreamConflicts(false);
            if (ruiBody != null) { ruiBody.enabled = true; ruiBody = null; }
            if (characterController != null) characterController.enabled = true;
            SetVignette(baseVignette);
            if (input != null) { input.SetMoveSuppressed(false); input.SetLookSuppressed(false); }
            if (hud != null) hud.SetReticleHidden(false);
            if (fade != null) fade.FadeTo(0f, 0f);
            Debug.LogWarning("[RebootFalseDay] Interrupted mid-beat; control restored.", this);
        }
    }
}
