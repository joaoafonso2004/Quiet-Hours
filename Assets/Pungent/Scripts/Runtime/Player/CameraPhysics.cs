using UnityEngine;

namespace Pungent.Player
{
    /// <summary>Câmara corporal: atraso do pescoço, respiração, headbob, lean e aterragem.</summary>
    [DisallowMultipleComponent]
    public sealed class CameraPhysics : MonoBehaviour
    {
        [Header("Rig")]
        [SerializeField] private Transform viewYaw;
        [SerializeField] private Transform viewPitch;
        [SerializeField] private Transform leanPivot;
        [SerializeField] private Transform cameraSpring;

        [Header("Look")]
        [SerializeField, Min(0.01f)] private float yawLagSharpness = 20f;
        [SerializeField, Min(0.01f)] private float pitchLagSharpness = 22f;
        [SerializeField] private Vector2 pitchLimits = new Vector2(-82f, 82f);

        [Header("Head motion")]
        [SerializeField, Min(0.01f)] private float springFrequency = 7.5f;
        [SerializeField, Min(0f)] private float dampingRatio = 0.82f;
        [SerializeField, Min(0f)] private float walkCyclesPerSecond = 1.75f;
        [SerializeField, Min(0.1f)] private float fullBobSpeed = 3.2f;
        [SerializeField] private Vector3 bobPosition = new Vector3(0.01f, 0.016f, 0f);
        [SerializeField] private Vector3 bobRotation = new Vector3(0.35f, 0.15f, 0.55f);

        [Header("Breathing")]
        [SerializeField, Min(0f)] private float calmBreathFrequency = 0.18f;
        [SerializeField, Min(0f)] private float panicBreathFrequency = 0.52f;
        [SerializeField] private Vector3 calmBreathPosition = new Vector3(0.0015f, 0.0025f, 0.001f);
        [SerializeField] private Vector3 panicBreathPosition = new Vector3(0.004f, 0.008f, 0.003f);
        [SerializeField] private Vector3 calmBreathRotation = new Vector3(0.09f, 0.07f, 0.05f);
        [SerializeField] private Vector3 panicBreathRotation = new Vector3(0.65f, 0.5f, 0.4f);

        [Header("Lean")]
        [SerializeField, Min(0f)] private float leanDistance = 0.22f;
        [SerializeField, Min(0f)] private float leanRoll = 7f;
        [SerializeField, Min(0.01f)] private float leanFrequency = 5.5f;

        private Quaternion yawBase;
        private Quaternion pitchBase;
        private Vector3 leanBasePosition;
        private Quaternion leanBaseRotation;
        private Vector3 springBasePosition;
        private Quaternion springBaseRotation;

        private Vector2 pendingLook;
        private float targetYaw;
        private float targetPitch;
        private Vector3 localVelocity;
        private bool grounded;
        private float panic;
        private float leanTarget;
        private float bobPhase;
        private float leanValue;
        private float leanVelocity;
        private Vector3 motionPosition;
        private Vector3 motionPositionVelocity;
        private Vector3 motionRotation;
        private Vector3 motionRotationVelocity;

        public Vector3 ViewForward => viewYaw != null ? viewYaw.forward : transform.forward;
        public Transform ViewYaw => viewYaw;

        private void Awake()
        {
            if (viewYaw == null || viewPitch == null || leanPivot == null || cameraSpring == null)
            {
                Debug.LogError("CameraPhysics requires all four rig references.", this);
                enabled = false;
                return;
            }

            yawBase = viewYaw.localRotation;
            pitchBase = viewPitch.localRotation;
            leanBasePosition = leanPivot.localPosition;
            leanBaseRotation = leanPivot.localRotation;
            springBasePosition = cameraSpring.localPosition;
            springBaseRotation = cameraSpring.localRotation;
        }

        public void SetLookDelta(Vector2 degrees) => pendingLook += degrees;

        public void ForceLookAt(Vector3 worldTarget)
        {
            if (viewYaw == null || viewYaw.parent == null) return;
            // Isto e uma direccao a partir dos olhos, nao uma posicao relativa ao
            // PlayerRoot. O pivot do PlayerRoot esta no chao; usar
            // InverseTransformPoint(worldTarget) fazia um alvo a altura da cabeca
            // parecer cerca de 50 graus acima da camara. O lock disparava e o sting
            // tocava, mas a camara subia em vez de agarrar a cara do Rui.
            Vector3 worldDirection = worldTarget - viewYaw.position;
            if (worldDirection.sqrMagnitude < 0.001f) return;

            Vector3 localTarget = viewYaw.parent.InverseTransformDirection(worldDirection);
            localTarget = Quaternion.Inverse(yawBase) * localTarget;
            localTarget.Normalize();

            float yaw = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(localTarget.y) * Mathf.Rad2Deg;
            
            targetYaw = yaw;
            targetPitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
        }

        /// <summary>
        /// Poe o olhar a direito, alinhado com o corpo, **sem lag**.
        ///
        /// ---
        ///
        /// **Existe porque virar o corpo nao vira a camara.**
        ///
        /// O yaw deste jogo nao vive no `PlayerRoot`: vive no `viewYaw`, um filho, e
        /// e este componente que o acumula. Toda a encenacao que escrevia
        /// `player.rotation = Quaternion.Euler(0, yaw, 0)` — a abertura a secretaria,
        /// o acordar do Dia 3 — estava a rodar um objecto que a camara nao segue. O
        /// jogador acordava a olhar exactamente para onde estava a olhar quando o dia
        /// anterior acabou.
        ///
        /// Media-se assim: o Dia 1 acaba, o Tomas e sentado a secretaria virado a
        /// sul — que e onde a secretaria esta — e ele abre os olhos virado a oeste,
        /// para a cama, porque era para la que estava a olhar. Nao ha erro nenhum: o
        /// corpo **esta** virado para a secretaria.
        ///
        /// **Sem interpolacao.** Isto e chamado com o ecra preto ou a meio de um
        /// piscar de olhos; um `Slerp` a apanhar o alinhamento durante meio segundo
        /// depois de as palpebras subirem le-se como a camara a corrigir-se sozinha,
        /// que e pior do que o defeito.
        /// </summary>
        public void AlignToBody()
        {
            targetYaw = 0f;
            targetPitch = 0f;
            pendingLook = Vector2.zero;

            if (viewYaw != null) viewYaw.localRotation = yawBase;
            if (viewPitch != null) viewPitch.localRotation = pitchBase;
        }

        public void SetMotionState(Vector3 velocityRelativeToView, bool isGrounded)
        {
            localVelocity = velocityRelativeToView;
            grounded = isGrounded;
        }

        public void SetLean(float value) => leanTarget = Mathf.Clamp(value, -1f, 1f);
        public void SetPanic(float value) => panic = Mathf.Clamp01(value);

        public void NotifyLanded(float downwardSpeed)
        {
            float strength = Mathf.InverseLerp(1.5f, 8f, Mathf.Abs(downwardSpeed));
            motionPositionVelocity += Vector3.down * (0.018f * strength);
            motionRotationVelocity += Vector3.right * (0.8f * strength);
        }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            targetYaw = Mathf.DeltaAngle(0f, targetYaw + pendingLook.x);
            targetPitch = Mathf.Clamp(targetPitch - pendingLook.y, pitchLimits.x, pitchLimits.y);
            pendingLook = Vector2.zero;

            float yawBlend = 1f - Mathf.Exp(-yawLagSharpness * dt);
            float pitchBlend = 1f - Mathf.Exp(-pitchLagSharpness * dt);
            viewYaw.localRotation = Quaternion.Slerp(viewYaw.localRotation,
                yawBase * Quaternion.Euler(0f, targetYaw, 0f), yawBlend);
            viewPitch.localRotation = Quaternion.Slerp(viewPitch.localRotation,
                pitchBase * Quaternion.Euler(targetPitch, 0f, 0f), pitchBlend);

            SpringMath.Step(ref leanValue, ref leanVelocity, leanTarget,
                leanFrequency, 0.9f, dt);
            leanPivot.localPosition = leanBasePosition + Vector3.right * (leanValue * leanDistance);
            leanPivot.localRotation = leanBaseRotation * Quaternion.Euler(0f, 0f, -leanValue * leanRoll);

            UpdateSpring(dt);
        }

        private void UpdateSpring(float dt)
        {
            float planarSpeed = new Vector2(localVelocity.x, localVelocity.z).magnitude;
            float speed01 = Mathf.Clamp01(planarSpeed / fullBobSpeed);
            if (grounded && planarSpeed > 0.05f)
                bobPhase += dt * walkCyclesPerSecond * Mathf.Lerp(0.65f, 1.35f, speed01) * Mathf.PI * 2f;

            // Desligado nas opcoes, o balanco vai a zero e mais nada muda: a
            // respiracao, o atraso do pescoco e a aterragem ficam. Quem desliga
            // isto costuma faze-lo por enjoo, e o que enjoa e o passo — nao e a
            // camara ter corpo.
            float amount = grounded && Pungent.Menu.GameSettings.HeadBobbing ? speed01 : 0f;
            float verticalWave = Mathf.Sin(bobPhase * 2f);
            float lateralWave = Mathf.Sin(bobPhase);
            Vector3 bobPos = new Vector3(lateralWave * bobPosition.x,
                -Mathf.Abs(verticalWave) * bobPosition.y, verticalWave * bobPosition.z) * amount;
            Vector3 bobRot = new Vector3(verticalWave * bobRotation.x,
                lateralWave * bobRotation.y, -lateralWave * bobRotation.z) * amount;

            float breathFrequency = Mathf.Lerp(calmBreathFrequency, panicBreathFrequency, panic);
            float t = Time.time * breathFrequency;
            Vector3 noise = new Vector3(SignedPerlin(t, 17.31f), SignedPerlin(t, 83.17f), SignedPerlin(t, 191.73f));
            Vector3 breathPos = Vector3.Scale(noise,
                Vector3.Lerp(calmBreathPosition, panicBreathPosition, panic));
            Vector3 breathRot = Vector3.Scale(noise,
                Vector3.Lerp(calmBreathRotation, panicBreathRotation, panic));

            SpringMath.Step(ref motionPosition, ref motionPositionVelocity,
                bobPos + breathPos, springFrequency, dampingRatio, dt);
            SpringMath.Step(ref motionRotation, ref motionRotationVelocity,
                bobRot + breathRot, springFrequency, dampingRatio, dt);

            cameraSpring.localPosition = springBasePosition + motionPosition;
            cameraSpring.localRotation = springBaseRotation * Quaternion.Euler(motionRotation);
        }

        private static float SignedPerlin(float x, float y)
        {
            return Mathf.PerlinNoise(x, y) * 2f - 1f;
        }
    }
}
