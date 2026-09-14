using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// Onde o Tomas deixou o carro.
    ///
    /// O carro nao volta sozinho para sitio nenhum: fica exactamente onde o jogador
    /// o parou. Isto so guarda **a nota** de onde isso foi, para o resto do capitulo
    /// poder falar dela — o objectivo de regresso, o pensamento que aponta, a
    /// distancia que ainda falta correr.
    ///
    /// Guardado e nao assumido, de proposito. Um capitulo que assuma um lugar de
    /// estacionamento obriga o jogo a corrigir o jogador — ou a teleportar-lhe o
    /// carro, que e pior. Se ele parar de traves a vinte metros do portao, e a
    /// vinte metros do portao que o carro esta quando as coisas correrem mal, e a
    /// culpa e dele. E dessas decisoes que se lembra um jogador.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParkedCar : MonoBehaviour
    {
        [SerializeField] private CarSeat seat;
        [SerializeField] private CarDriver car;
        [SerializeField] private ChapterDirector director;

        [Tooltip("Levantado quando ele sai do carro pela primeira vez. E a chegada "
               + "a serio: nao 'passei por um volume', mas 'parei e desliguei'.")]
        [SerializeField] private string parkedEvent = "car_parked";

        [Tooltip("Velocidade abaixo da qual se considera parado, em m/s.")]
        [SerializeField, Min(0.05f)] private float stillSpeed = 0.4f;

        /// <summary>Onde ficou. So vale depois de <see cref="HasParked"/>.</summary>
        public Vector3 Position { get; private set; }
        public Quaternion Rotation { get; private set; }

        /// <summary>Ja saiu do carro uma vez.</summary>
        public bool HasParked { get; private set; }

        private void Awake()
        {
            if (seat == null) seat = FindObjectOfType<CarSeat>();
            if (car == null) car = FindObjectOfType<CarDriver>();
            if (director == null) director = FindObjectOfType<ChapterDirector>();
        }

        private void Update()
        {
            if (HasParked || seat == null || car == null) return;

            // Ainda sentado, ou ainda a rolar: nao esta estacionado.
            if (seat.Seated || Mathf.Abs(car.Speed) > stillSpeed) return;

            HasParked = true;
            Position = car.transform.position;
            Rotation = car.transform.rotation;

            if (!string.IsNullOrWhiteSpace(parkedEvent))
            {
                director = ChapterDirector.Resolve(director);
                director?.Notify(parkedEvent);
            }
        }

        /// <summary>Metros em linha recta entre um ponto e o carro.</summary>
        public float DistanceFrom(Vector3 point) =>
            car != null ? Vector3.Distance(point, car.transform.position) : 0f;

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(CarSeat carSeat, CarDriver driver, string raisedEvent)
        {
            seat = carSeat;
            car = driver;
            parkedEvent = raisedEvent;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            if (!HasParked) return;
            Gizmos.color = new Color(0.4f, 0.9f, 0.5f, 0.9f);
            Gizmos.DrawWireCube(Position + Vector3.up * 0.8f, new Vector3(2f, 1.6f, 4.2f));
        }
    }
}
