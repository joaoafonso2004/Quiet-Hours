using Pungent.Player;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// As rodas da frente viram com a direccao e as quatro rolam com o andamento.
    ///
    /// E so encenacao: a fisica do <see cref="CarDriver"/> nao passa pelas rodas
    /// (a §22 pede fisica simples), mas rodas paradas numa curva quebram o carro
    /// inteiro a quem olhar pela janela.
    ///
    /// O volante em si nao vira: no modelo do SEDAN ele e parte da malha do
    /// habitaculo, nao um objecto proprio — nao ha transform para rodar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarWheelVisuals : MonoBehaviour
    {
        [SerializeField] private CarDriver car;
        [SerializeField] private PlayerInputReader input;

        [Tooltip("Os pivots que viram com a direccao: FL_PARENT e FR_PARENT.")]
        [SerializeField] private Transform[] steering = new Transform[0];

        [Tooltip("As que rolam com o andamento: os quatro *_WHEEL.")]
        [SerializeField] private Transform[] rolling = new Transform[0];

        [Tooltip("Quanto viram as rodas da frente no batente.")]
        [SerializeField] private float maxSteerDegrees = 28f;

        [Tooltip("Velocidade do visual a ir e a voltar, em graus/s.")]
        [SerializeField] private float steerSpeed = 160f;

        [Tooltip("Raio da roda em metros do mundo, para o rolamento bater com o chao.")]
        [SerializeField, Min(0.05f)] private float wheelRadius = 0.31f;

        private float steerAngle;
        private float rollAngle;

        private void Awake()
        {
            if (car == null) car = GetComponentInParent<CarDriver>();
            if (input == null) input = FindObjectOfType<PlayerInputReader>(true);
        }

        private void Update()
        {
            float target = input != null ? Mathf.Clamp(input.Move.x, -1f, 1f) * maxSteerDegrees : 0f;
            steerAngle = Mathf.MoveTowards(steerAngle, target, steerSpeed * Time.deltaTime);

            float speed = car != null ? car.Speed : 0f;
            rollAngle = Mathf.Repeat(
                rollAngle + speed / wheelRadius * Mathf.Rad2Deg * Time.deltaTime, 360f);

            foreach (var pivot in steering)
                if (pivot != null) pivot.localRotation = Quaternion.Euler(0f, steerAngle, 0f);

            foreach (var wheel in rolling)
                if (wheel != null) wheel.localRotation = Quaternion.Euler(rollAngle, 0f, 0f);
        }
    }
}
