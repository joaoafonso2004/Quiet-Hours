using UnityEngine;

namespace Pungent.Player
{
    /// <summary>Locomoção humana simples, sem salto, sprint ou barras de stamina.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private CharacterController characterController;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private CameraPhysics cameraPhysics;
        [SerializeField] private Transform movementReference;

        [Header("Movement")]
        [SerializeField, Min(0.1f)] private float walkingSpeed = 2.8f;

        /// <summary>
        /// Multiplicador do passo, para quem tenha uma razao para o abrandar — hoje
        /// so as caixas do prologo, que se levam ao colo.
        ///
        /// Multiplicador e nao uma velocidade nova: assim quem abranda nao precisa
        /// de saber qual e o passo normal, e afinar o passo no inspector continua a
        /// funcionar sem ninguem se lembrar de vir aqui.
        /// </summary>
        public float SpeedMultiplier { get; set; } = 1f;
        [SerializeField, Min(0.1f)] private float acceleration = 14f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedVerticalSpeed = -2f;

        private Vector3 planarVelocity;
        private float verticalSpeed;
        private bool wasGrounded;

        /// <summary>Clears movement immediately when an interaction takes control.</summary>
        public void StopImmediately()
        {
            planarVelocity = Vector3.zero;
            if (characterController != null && characterController.enabled)
                characterController.Move(Vector3.zero);
            if (cameraPhysics != null)
                cameraPhysics.SetMotionState(Vector3.zero,
                    characterController != null && characterController.isGrounded);
        }

        private void Awake()
        {
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (input == null) input = GetComponent<PlayerInputReader>();

            if (characterController == null || input == null || cameraPhysics == null || movementReference == null)
            {
                Debug.LogError("PlayerMotor has missing references.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Sem controlador ligado nao ha por onde andar, e chamar-lhe `Move`
            // enche a consola de erros. Acontece de verdade: ao volante o corpo do
            // jogador esta desligado, e o cartao de capitulo volta a ligar este
            // componente quando sai do ecra — ver `ChapterDirector.SetSuppressed`.
            // Olhar em volta continua a funcionar, que e o que se quer sentado.
            if (characterController == null || !characterController.enabled)
            {
                cameraPhysics.SetLookDelta(input.LookDeltaDegrees);
                cameraPhysics.SetLean(input.Lean);
                return;
            }

            cameraPhysics.SetLookDelta(input.LookDeltaDegrees);
            cameraPhysics.SetLean(input.Lean);

            Vector3 forward = movementReference.forward;
            Vector3 right = movementReference.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 desiredDirection = forward * input.Move.y + right * input.Move.x;
            if (desiredDirection.sqrMagnitude > 1f) desiredDirection.Normalize();
            Vector3 desiredVelocity = desiredDirection * (walkingSpeed * SpeedMultiplier);
            planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, acceleration * dt);

            bool groundedBeforeMove = characterController.isGrounded;
            float landingSpeed = verticalSpeed;
            if (groundedBeforeMove && verticalSpeed < 0f)
                verticalSpeed = groundedVerticalSpeed;

            verticalSpeed += gravity * dt;
            Vector3 motion = (planarVelocity + Vector3.up * verticalSpeed) * dt;
            CollisionFlags flags = characterController.Move(motion);
            bool groundedNow = (flags & CollisionFlags.Below) != 0 || characterController.isGrounded;

            if (groundedNow && verticalSpeed < groundedVerticalSpeed)
                verticalSpeed = groundedVerticalSpeed;

            if (!wasGrounded && groundedNow && landingSpeed < -1.5f)
                cameraPhysics.NotifyLanded(landingSpeed);

            wasGrounded = groundedNow;
            Vector3 velocityForCamera = movementReference.InverseTransformDirection(planarVelocity);
            cameraPhysics.SetMotionState(velocityForCamera, groundedNow);
        }
    }
}
