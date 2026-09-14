using Pungent.Player;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// O corpo do carro inclina; a fisica nao.
    ///
    /// E o truque central dos controladores arcade (fisica num rig, corpo visual
    /// noutro): a carrocaria rola para fora nas curvas e mergulha do nariz a
    /// travar, tudo fingido por cima da malha, sem tocar no Rigidbody. De dentro
    /// le-se no capo e no tablier — a cabeca do condutor fica direita, que e o que
    /// uma cabeca faz, e e o carro que se mexe a volta dela.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarBodyTilt : MonoBehaviour
    {
        [Tooltip("A malha que inclina. O filho SEDAN, nao a raiz com a fisica.")]
        [SerializeField] private Transform body;
        [SerializeField] private CarDriver car;
        [SerializeField] private PlayerInputReader input;

        [Tooltip("Graus de rolamento no batente do volante, a velocidade cheia.")]
        [SerializeField, Min(0f)] private float maxRoll = 2.6f;

        [Tooltip("Graus de mergulho por m/s2 de travagem (e de levantar ao acelerar).")]
        [SerializeField, Min(0f)] private float pitchPerAccel = 0.22f;

        [SerializeField, Min(0f)] private float maxPitch = 2.2f;

        [Tooltip("Quao depressa o corpo persegue a inclinacao alvo.")]
        [SerializeField, Min(0.1f)] private float sharpness = 5f;

        private Quaternion baseRotation;
        private float lastSpeed;
        private float smoothAccel;
        private float roll;
        private float pitch;

        private void Awake()
        {
            if (car == null) car = GetComponentInParent<CarDriver>();
            if (input == null) input = FindObjectOfType<PlayerInputReader>(true);
            if (body != null) baseRotation = body.localRotation;
        }

        private void Update()
        {
            if (body == null || car == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Aceleracao alisada: o valor cru por frame e ruido puro, e o corpo de
            // um carro e pesado — reage tarde e devagar.
            float accel = (car.Speed - lastSpeed) / dt;
            lastSpeed = car.Speed;
            smoothAccel = Mathf.Lerp(smoothAccel, accel, 1f - Mathf.Exp(-4f * dt));

            float steer = input != null ? Mathf.Clamp(input.Move.x, -1f, 1f) : 0f;
            float speed01 = Mathf.Clamp01(Mathf.Abs(car.Speed) / car.MaxSpeed);

            // Rola para FORA da curva (volante a direita, corpo para a esquerda) e
            // levanta o nariz a acelerar — sinais de carro, nao de mota.
            float targetRoll = steer * speed01 * maxRoll;
            float targetPitch = Mathf.Clamp(-smoothAccel * pitchPerAccel, -maxPitch, maxPitch);

            float blend = 1f - Mathf.Exp(-sharpness * dt);
            roll = Mathf.Lerp(roll, targetRoll, blend);
            pitch = Mathf.Lerp(pitch, targetPitch, blend);

            body.localRotation = baseRotation * Quaternion.Euler(pitch, 0f, roll);
        }
    }
}
