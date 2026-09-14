using UnityEngine;

namespace Pungent.NPC
{
    /// <summary>
    /// Faz a cabeça (e um pouco do pescoço e do tronco) de um humanoide virar-se
    /// para um alvo, com limites de ângulo para nunca parecer sobrenatural.
    ///
    /// Corre em LateUpdate depois do <see cref="HumanoidRestPose"/> e de qualquer
    /// Animator, aplicando uma rotação-delta em espaço de mundo. Assim funciona
    /// independentemente dos eixos locais de cada osso do rig.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Animator))]
    public sealed class HumanoidHeadLook : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("Distribuição pelo corpo")]
        // O tronco pertence ao HumanoidRestPose (respiração). Aqui só pescoço e
        // cabeça, para os dois componentes nunca escreverem no mesmo osso.
        [SerializeField, Range(0f, 1f)] private float headWeight = 0.80f;
        [SerializeField, Range(0f, 1f)] private float neckWeight = 0.55f;

        [Header("Limites")]
        [Tooltip("Rotação horizontal máxima em relação à frente do corpo.")]
        [SerializeField, Range(10f, 110f)] private float maxYaw = 76f;
        [SerializeField, Range(5f, 70f)] private float maxPitch = 30f;
        [Tooltip("Acima deste ângulo o NPC desiste de olhar em vez de torcer o pescoço.")]
        [SerializeField, Range(60f, 180f)] private float giveUpAngle = 115f;

        [Header("Suavização")]
        [SerializeField, Min(0.1f)] private float blendSpeed = 3.4f;

        private Animator animator;
        private Transform head, neck;
        private float currentWeight;
        private float requestedWeight;
        private float forcedUntil;

        // Sem clip a repor a pose, uma rotação relativa acumularia todos os frames
        // e a personagem entrava em rotação infinita. Guardamos a pose base.
        // Com um AnimatorController atribuído é o Animator que repõe a pose, e
        // repô-la aqui apagaria a animação da cabeça: por isso é condicional.
        private Quaternion headBase, neckBase;
        private bool cached;
        private bool restoreBasePose;

        /// <summary>Peso pedido pelo estado do NPC. 0 = ignora o jogador.</summary>
        public void SetLookWeight(float weight) => requestedWeight = Mathf.Clamp01(weight);

        /// <summary>Olhar forçado durante alguns segundos, para falas do género "he started looking at you".</summary>
        public void Glance(float seconds) => forcedUntil = Mathf.Max(forcedUntil, Time.time + Mathf.Max(0f, seconds));

        public void SetTarget(Transform value) => target = value;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            if (!animator.isHuman)
            {
                enabled = false;
                return;
            }

            head = animator.GetBoneTransform(HumanBodyBones.Head);
            neck = animator.GetBoneTransform(HumanBodyBones.Neck);

            if (head != null) headBase = head.localRotation;
            if (neck != null) neckBase = neck.localRotation;
            cached = true;
            restoreBasePose = animator.runtimeAnimatorController == null;

            if (target == null)
            {
                Camera main = Camera.main;
                if (main != null) target = main.transform;
            }
        }

        private void LateUpdate()
        {
            if (head == null || !cached) return;

            // Só quando não há Animator a repor a pose por nós.
            if (restoreBasePose)
            {
                head.localRotation = headBase;
                if (neck != null) neck.localRotation = neckBase;
            }

            float wanted = Time.time < forcedUntil ? 1f : requestedWeight;

            if (target != null && wanted > 0f)
            {
                // Se o alvo estiver demasiado atrás, não torce o pescoço: desiste.
                Vector3 flat = Vector3.ProjectOnPlane(target.position - head.position, Vector3.up);
                if (flat.sqrMagnitude > 0.0001f &&
                    Vector3.Angle(Vector3.ProjectOnPlane(transform.forward, Vector3.up), flat) > giveUpAngle)
                    wanted = 0f;
            }

            currentWeight = Mathf.MoveTowards(currentWeight, wanted, Time.deltaTime * blendSpeed);
            if (currentWeight <= 0.001f || target == null) return;

            Vector3 desired = ClampToCone(target.position - head.position);
            Vector3 bodyForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Quaternion delta = Quaternion.FromToRotation(bodyForward, desired);

            Apply(neck, delta, neckWeight);
            Apply(head, delta, headWeight);
        }

        private void Apply(Transform bone, Quaternion delta, float weight)
        {
            if (bone == null || weight <= 0f) return;
            Quaternion scaled = Quaternion.Slerp(Quaternion.identity, delta, weight * currentWeight);
            bone.rotation = scaled * bone.rotation;
        }

        /// <summary>Direção para o alvo, limitada a um cone à frente do corpo.</summary>
        private Vector3 ClampToCone(Vector3 toTarget)
        {
            Vector3 bodyForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 flat = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f) return bodyForward;
            flat.Normalize();

            float yaw = Mathf.Clamp(Vector3.SignedAngle(bodyForward, flat, Vector3.up), -maxYaw, maxYaw);
            Vector3 yawed = Quaternion.AngleAxis(yaw, Vector3.up) * bodyForward;

            float pitch = Mathf.Clamp(
                Mathf.Asin(Mathf.Clamp(toTarget.normalized.y, -1f, 1f)) * Mathf.Rad2Deg,
                -maxPitch, maxPitch);
            Vector3 rightAxis = Vector3.Cross(Vector3.up, yawed).normalized;
            return (Quaternion.AngleAxis(-pitch, rightAxis) * yawed).normalized;
        }
    }
}
