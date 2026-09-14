using UnityEngine;

namespace Pungent.NPC
{
    /// <summary>
    /// Pose de descanso provisória para humanoides sem clips de animação.
    /// O pack PSX traz apenas a bind pose, por isso a personagem ficaria em T-pose.
    ///
    /// Em vez de rodar os ossos à volta de eixos fixos — que depende da convenção
    /// de cada rig — aponta cada osso numa direção pedida, usando o osso filho para
    /// saber para onde ele aponta agora. Funciona em qualquer esqueleto humanoide.
    ///
    /// É descartável: assim que existirem clips Mixamo, desativa este componente.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class HumanoidRestPose : MonoBehaviour
    {
        [Header("Braços em repouso")]
        [Tooltip("Afastamento dos braços em relação ao tronco. 0 = colados.")]
        [SerializeField, Range(0f, 0.6f)] private float armSpread = 0.20f;
        [Tooltip("Quanto os braços caem à frente do corpo.")]
        [SerializeField, Range(-0.3f, 0.5f)] private float armForward = 0.07f;
        [Tooltip("Dobra do cotovelo, para os braços não ficarem rígidos.")]
        [SerializeField, Range(0f, 0.6f)] private float elbowBend = 0.24f;
        [SerializeField, Range(0f, 1f)] private float armWeight = 1f;

        [Header("Vida")]
        [SerializeField, Range(0f, 3f)] private float breathAmplitude = 0.9f;
        [SerializeField, Range(0.05f, 1f)] private float breathSpeed = 0.26f;
        [SerializeField, Range(0f, 3f)] private float swayAmplitude = 0.8f;
        [SerializeField, Range(0.02f, 1f)] private float swaySpeed = 0.15f;

        private Animator animator;
        private Transform leftUpperArm, leftLowerArm, leftHand;
        private Transform rightUpperArm, rightLowerArm, rightHand;
        private Transform chest;
        private float seed;

        // Sem clip no Animator nada repõe a pose entre frames, por isso qualquer
        // rotação relativa acumularia indefinidamente. Guardamos a pose base e
        // repomo-la no início de cada LateUpdate.
        private Quaternion chestBase;
        private bool cached;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (!animator.isHuman)
            {
                enabled = false;
                return;
            }

            leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);

            if (chest != null) chestBase = chest.localRotation;
            cached = true;
            seed = Random.value * 100f;
        }

        private void LateUpdate()
        {
            if (!cached) return;

            // Repõe a pose base antes de voltar a aplicar a respiração.
            if (chest != null) chest.localRotation = chestBase;

            Vector3 right = transform.right;
            Vector3 forward = transform.forward;
            Vector3 down = Vector3.down;

            float t = Time.time;
            float breath = breathAmplitude > 0f
                ? Mathf.Sin((t + seed) * breathSpeed * Mathf.PI * 2f) * breathAmplitude
                : 0f;
            float sway = swayAmplitude > 0f
                ? Mathf.Sin((t + seed * 1.7f) * swaySpeed * Mathf.PI * 2f) * swayAmplitude
                : 0f;

            // Respiração: inclinação mínima do tronco, aplicada antes dos braços
            // para que estes acompanhem o movimento em vez de flutuarem.
            if (chest != null && Mathf.Abs(breath) > 0.001f)
                chest.rotation = Quaternion.AngleAxis(breath, right) * chest.rotation;

            // O braço esquerdo do modelo aponta para o lado esquerdo do corpo (-right).
            Vector3 leftUpperTarget = (down + (-right * armSpread) + forward * armForward).normalized;
            Vector3 rightUpperTarget = (down + (right * armSpread) + forward * armForward).normalized;
            Vector3 leftLowerTarget = (down + (-right * armSpread * 0.5f) + forward * (armForward + elbowBend)).normalized;
            Vector3 rightLowerTarget = (down + (right * armSpread * 0.5f) + forward * (armForward + elbowBend)).normalized;

            // A oscilação lenta do peso entra como um desvio residual nos braços.
            Quaternion swayRotation = Quaternion.AngleAxis(sway, forward);

            PointBone(leftUpperArm, leftLowerArm, swayRotation * leftUpperTarget, armWeight);
            PointBone(rightUpperArm, rightLowerArm, swayRotation * rightUpperTarget, armWeight);
            PointBone(leftLowerArm, leftHand, swayRotation * leftLowerTarget, armWeight);
            PointBone(rightLowerArm, rightHand, swayRotation * rightLowerTarget, armWeight);
        }

        /// <summary>
        /// Roda <paramref name="bone"/> para que a direção osso→filho passe a ser
        /// <paramref name="desiredDirection"/>, sem depender dos eixos locais do rig.
        /// </summary>
        private static void PointBone(Transform bone, Transform child, Vector3 desiredDirection, float weight)
        {
            if (bone == null || child == null || weight <= 0f) return;

            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 0.000001f) return;

            Quaternion delta = Quaternion.FromToRotation(current.normalized, desiredDirection.normalized);
            bone.rotation = Quaternion.Slerp(Quaternion.identity, delta, Mathf.Clamp01(weight)) * bone.rotation;
        }
    }
}
