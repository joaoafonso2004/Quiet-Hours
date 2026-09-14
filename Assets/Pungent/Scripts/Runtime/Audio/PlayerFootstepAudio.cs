using UnityEngine;

namespace Pungent.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(AudioSource))]
    public sealed class PlayerFootstepAudio : MonoBehaviour
    {
        [SerializeField] private CharacterController characterController;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] footstepClips;

        [Header("Cadence")]
        [SerializeField, Min(0.01f)] private float minimumWalkingSpeed = 0.25f;
        [SerializeField, Min(0.1f)] private float referenceWalkingSpeed = 2.8f;
        [SerializeField, Min(0.1f)] private float slowStepInterval = 0.62f;
        [SerializeField, Min(0.1f)] private float fastStepInterval = 0.38f;

        [Header("Variation")]
        [SerializeField, Range(0f, 1f)] private float volume = 0.28f;
        [SerializeField] private Vector2 pitchRange = new Vector2(0.94f, 1.04f);

        private float stepTimer;
        private int previousClipIndex = -1;

        private void Awake()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            stepTimer = 0.12f;
        }

        private void Update()
        {
            if (characterController == null || audioSource == null)
                return;

            Vector3 velocity = characterController.velocity;
            velocity.y = 0f;
            float planarSpeed = velocity.magnitude;

            if (!characterController.isGrounded || planarSpeed < minimumWalkingSpeed)
            {
                stepTimer = Mathf.Min(stepTimer, 0.1f);
                return;
            }

            float speedRatio = Mathf.Clamp01(planarSpeed / referenceWalkingSpeed);
            float interval = Mathf.Lerp(slowStepInterval, fastStepInterval, speedRatio);
            stepTimer -= Time.deltaTime;

            if (stepTimer > 0f)
                return;

            PlayFootstep();
            stepTimer = interval;
        }

        private void PlayFootstep()
        {
            if (footstepClips == null || footstepClips.Length == 0)
                return;

            int clipIndex = Random.Range(0, footstepClips.Length);
            if (footstepClips.Length > 1 && clipIndex == previousClipIndex)
                clipIndex = (clipIndex + 1) % footstepClips.Length;

            previousClipIndex = clipIndex;
            AudioClip clip = footstepClips[clipIndex];
            if (clip == null)
                return;

            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
            audioSource.PlayOneShot(clip, volume);
        }
    }
}

