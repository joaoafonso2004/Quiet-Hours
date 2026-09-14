using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// Conduz o carro sozinho pela estrada. **Ferramenta de teste, nao de jogo.**
    ///
    /// A viagem dura 4m23, que e o tempo das duas musicas. Nao ha maneira de saber
    /// se esse encadeamento fecha — musica, motor a morrer, saida, capo, estranho —
    /// sem o correr do principio ao fim em tempo real, e ninguem vai ficar quatro
    /// minutos e meio a carregar no W para verificar um bug.
    ///
    /// Isto nao substitui jogar: valida que a cadeia nao encrava e que a fisica
    /// aguenta 5,7 km. **Nao valida o toque** — se o volante e pesado, se oitenta
    /// parece depressa, se a curva assusta. Isso so se sabe com as maos.
    ///
    /// Fica desligado. Liga-se a mao no inspector, ou por codigo num teste.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadAutopilot : MonoBehaviour
    {
        [SerializeField] private CarDriver car;
        [SerializeField] private RoadPath path;

        [Tooltip("Quantos metros a frente ele olha. Curto faz zigue-zague, longo "
               + "corta as curvas.")]
        [SerializeField, Min(4f)] private float lookAhead = 22f;

        [Tooltip("Fraccao da velocidade maxima.")]
        [SerializeField, Range(0.1f, 1f)] private float throttle = 0.92f;

        private int hint = -1;

        private void Awake()
        {
            if (car == null) car = FindObjectOfType<CarDriver>();
            if (path == null) path = FindObjectOfType<RoadPath>();
        }

        private void FixedUpdate()
        {
            if (car == null || path == null || !car.EngineRunning) return;

            float here = path.DistanceOf(car.transform.position, ref hint);
            Vector3 target = path.PointAt(here + lookAhead);

            Vector3 toTarget = target - car.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return;

            // Roda o carro para o alvo e mantem o pe posto. O `CarDriver` continua a
            // tratar da velocidade e de assentar na rampa — isto so lhe da direccao,
            // que e o que o jogador daria.
            Quaternion want = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            var body = car.GetComponent<Rigidbody>();
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, want, 90f * Time.fixedDeltaTime));

            car.SetTestThrottle(throttle);
        }
    }
}
