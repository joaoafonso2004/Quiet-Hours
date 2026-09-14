using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// O para-brisas a dizer a velocidade: o FOV abre uns graus com o andamento.
    ///
    /// Num carro sem agulha de velocimetro funcional, e o unico instrumento que o
    /// corpo le sem olhar. Sobe pouco (7 graus no maximo) e devagar — mais que
    /// isso e efeito de arcada, e isto e um homem cansado numa estrada nacional.
    ///
    /// A camara em si fica trancada ao chassis (e filha do banco, sem amortecimento
    /// em espaco de mundo): abanar a camara com a estrada e o que faz enjoo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarCameraFeel : MonoBehaviour
    {
        [SerializeField] private CarDriver car;
        [SerializeField] private CarSeat seat;

        [Tooltip("Graus a mais no batente da velocidade.")]
        [SerializeField, Min(0f)] private float extraFovAtTopSpeed = 7f;

        [Tooltip("Quao depressa o FOV persegue a velocidade.")]
        [SerializeField, Min(0.01f)] private float sharpness = 2.5f;

        private Camera view;
        private float baseFov = -1f;

        private void Awake()
        {
            if (car == null) car = GetComponentInParent<CarDriver>();
            if (seat == null) seat = GetComponentInParent<CarSeat>();
        }

        private void LateUpdate()
        {
            // A camara chega com o jogador, depois do Awake de toda a gente — e
            // vai-se embora com ele quando sai do carro. Procurada tarde e sem
            // guardar em cache o FOV de outra camara qualquer.
            if (view == null)
            {
                view = Camera.main;
                if (view == null) return;
                baseFov = view.fieldOfView;
            }

            bool driving = seat != null && seat.Seated && car != null;
            float target = driving
                ? baseFov + extraFovAtTopSpeed * Mathf.Clamp01(Mathf.Abs(car.Speed) / car.MaxSpeed)
                : baseFov;

            float blend = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, target, blend);
        }
    }
}
