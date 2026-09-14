using Pungent.Player;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// Acelerar, travar e virar. Mais nada.
    ///
    /// A §22 avisa que a conducao pode comer o projecto, e a mitigacao escrita la e
    /// "fisica simples, rotas limitadas". Isto e a leitura literal disso: a
    /// velocidade e um numero, a direccao e uma rotacao, e o `Rigidbody` so serve
    /// para o carro assentar no alcatrao e bater no muro em vez de o atravessar.
    /// Nao ha suspensao, nem `WheelCollider`, nem caixa de velocidades — nada disso
    /// poe uma linha de historia no ecra que hoje nao possa la estar.
    ///
    /// O volante e o mesmo `Move` que faz o Tomas andar a pe. Enquanto conduz, o
    /// `PlayerMotor` fica desligado — suprimir o input em vez disso punha o `Move` a
    /// zero e deixava o carro sem volante.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarDriver : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;

        [Header("Andamento")]
        [Tooltip("Velocidade de cruzeiro. 22 m/s sao uns 80 km/h, que numa estrada "
               + "de dez metros a noite ja parece depressa.")]
        [SerializeField] private float maxSpeed = 22f;
        [SerializeField] private float acceleration = 6f;
        [SerializeField] private float braking = 12f;
        [Tooltip("Travao motor: tirar o pe abranda sozinho.")]
        [SerializeField] private float coasting = 2.5f;
        [SerializeField] private float reverseSpeed = 4f;

        [Header("Direccao")]
        [Tooltip("Graus por segundo a velocidade de cruzeiro.")]
        [SerializeField] private float steerDegreesPerSecond = 48f;
        [Tooltip("Parado nao se vira. Isto e a velocidade a partir da qual o volante "
               + "responde todo.")]
        [SerializeField] private float fullSteerAtSpeed = 8f;

        [Header("Motor")]
        [Tooltip("Com o motor morto ele so abranda. E o segundo passo do capitulo.")]
        [SerializeField] private bool engineRunning = true;

        [Header("Assentar na estrada")]
        [Tooltip("Onde os raios procuram chao. So a estrada: com tudo, o raio "
               + "apanhava o proprio carro.")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Tooltip("Quao depressa o carro roda para casar com a rampa. Alto treme, "
               + "baixo parece barco.")]
        [SerializeField, Min(0.1f)] private float alignSharpness = 8f;

        [Tooltip("Empurra o carro contra o alcatrao enquanto ha chao: sem isto, "
               + "no cimo de uma lomba ele levantava voo.")]
        [SerializeField, Min(0f)] private float groundStick = 1.5f;

        private Rigidbody body;
        private BoxCollider box;
        private float speed;
        private float steerValue;
        private float testThrottle = -1f;
        private Vector3 groundNormal = Vector3.up;

        /// <summary>Ha alcatrao debaixo dos dois eixos?</summary>
        public bool Grounded { get; private set; } = true;

        /// <summary>Metros andados desde que arrancou. E por isto que o capitulo se guia.</summary>
        public float Odometer { get; private set; }

        public float Speed => speed;
        public float MaxSpeed => maxSpeed;
        public bool EngineRunning => engineRunning;

        /// <summary>Velocidade em km/h, para painel ou depuracao.</summary>
        public float SpeedKmh => speed * 3.6f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            box = GetComponent<BoxCollider>();

            // Toda a rotacao e nossa (volante + rampa, via MoveRotation); a fisica
            // nao roda o carro nem num toque. O centro de massa baixo e a segunda
            // rede: mesmo que um dia a rotacao se liberte, isto nao capota.
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.centerOfMass = new Vector3(0f, -0.35f, 0f);
            body.interpolation = RigidbodyInterpolation.Interpolate;

            if (input == null) input = FindObjectOfType<PlayerInputReader>();
        }

        /// <summary>
        /// Acelerador imposto por um teste. Negativo = o jogador manda, que e o
        /// normal. Existe so para o <see cref="RoadAutopilot"/>.
        /// </summary>
        public void SetTestThrottle(float value) => testThrottle = Mathf.Clamp(value, -1f, 1f);

        public void ClearTestThrottle() => testThrottle = -1f;

#if UNITY_EDITOR
        /// <summary>
        /// Poe o carro noutro sitio da estrada, parado. **So para depuracao** — e o
        /// que deixa saltar os quatro minutos de alcatrao para ir ver a cena do fim.
        ///
        /// A velocidade e zerada nos dois sitios onde ela existe: no numero interno e
        /// no `Rigidbody`. So num deles, o carro continuava a andar depois de
        /// aterrar, ou parava e voltava a arrancar sozinho no `FixedUpdate` seguinte.
        /// </summary>
        public void EditorTeleport(Vector3 position, Quaternion rotation)
        {
            speed = 0f;
            steerValue = 0f;

            if (body == null) body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = position;
                body.rotation = rotation;
            }

            transform.SetPositionAndRotation(position, rotation);
        }
#endif

        /// <summary>Mata o motor. O radio vai atras (ver <see cref="CarRadio"/>).</summary>
        public void StopEngine() => engineRunning = false;

        public void StartEngine() => engineRunning = true;

        /// <summary>
        /// Poe o carro noutro sitio, parado, **em jogo**.
        ///
        /// O `EditorTeleport` faz o mesmo e vive dentro de um `#if UNITY_EDITOR`:
        /// existe para saltar quatro minutos de alcatrao a depurar e nao chega a
        /// compilar num build. O despiste da noite precisa exactamente do mesmo gesto
        /// e precisa dele a serio — o carro tem de acordar na berma, e nao no sitio
        /// onde bateu, virado para onde estava virado, a noventa a hora.
        ///
        /// A velocidade e zerada nos dois sitios onde ela existe. So num deles, o
        /// carro continuava a andar depois de aterrar, ou parava e voltava a arrancar
        /// sozinho no `FixedUpdate` seguinte.
        /// </summary>
        public void PlaceStopped(Vector3 position, Quaternion rotation)
        {
            speed = 0f;
            steerValue = 0f;

            if (body == null) body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = position;
                body.rotation = rotation;
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        private void FixedUpdate()
        {
            float throttle = 0f;
            float steer = 0f;

            if (input != null)
            {
                throttle = Mathf.Clamp(input.Move.y, -1f, 1f);
                steer = Mathf.Clamp(input.Move.x, -1f, 1f);
            }

            // O autopilot de teste (ver `RoadAutopilot`) so manda no acelerador; a
            // direccao dele e feita a rodar o corpo, e nao pelo volante. Sem isto
            // nao havia forma de correr os 4m23 sem alguem a segurar uma tecla.
            if (testThrottle >= 0f) throttle = testThrottle;

            UpdateSpeed(throttle);
            UpdateSteering(steer);
            AlignToRoad();

            // Com chao, a velocidade segue a rampa (e o forward ja inclinado, mais
            // um empurrao contra o alcatrao); no ar, a gravidade decide a queda.
            Vector3 velocity;
            if (Grounded)
            {
                velocity = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized * speed;
                velocity.y -= groundStick;
            }
            else
            {
                velocity = transform.forward * speed;
                velocity.y = body.velocity.y;
            }
            body.velocity = velocity;

            Odometer += Mathf.Abs(speed) * Time.fixedDeltaTime;
        }

        /// <summary>
        /// Casa o carro com a inclinacao da estrada: um raio em cada eixo, a media
        /// das normais, e uma rotacao suave ate la. Sem isto o carro subia rampas
        /// de nariz espetado no alcatrao — a rotacao X/Z esta congelada para a
        /// fisica, por isso ninguem mais o endireitava.
        /// </summary>
        private void AlignToRoad()
        {
            float axle = box != null ? box.size.z * 0.35f : 1.2f;
            Vector3 centre = box != null ? box.center : Vector3.zero;

            Vector3 summed = Vector3.zero;
            int hits = 0;
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 origin = transform.TransformPoint(centre + new Vector3(0f, 0f, sign * axle));
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 3f,
                        groundMask, QueryTriggerInteraction.Ignore))
                {
                    summed += hit.normal;
                    hits++;
                }
            }

            Grounded = hits > 0;
            if (!Grounded) return;

            groundNormal = (summed / hits).normalized;
            Quaternion target = Quaternion.FromToRotation(transform.up, groundNormal) * body.rotation;
            float blend = 1f - Mathf.Exp(-alignSharpness * Time.fixedDeltaTime);
            body.MoveRotation(Quaternion.Slerp(body.rotation, target, blend));
        }

        private void UpdateSpeed(float throttle)
        {
            if (!engineRunning)
            {
                // Sem motor nao ha acelerador. Travar ainda trava.
                float drag = throttle < -0.1f ? braking : coasting;
                speed = Mathf.MoveTowards(speed, 0f, drag * Time.fixedDeltaTime);
                return;
            }

            if (throttle > 0.1f)
            {
                // Curva em vez de forca constante — o padrao dos controladores
                // arcade: punch a arrancar, a esmorecer perto do maximo. Constante
                // sentia-se a empurrar um movel do principio ao fim.
                float punch = Mathf.Lerp(1.7f, 0.45f, speed / maxSpeed);
                speed = Mathf.MoveTowards(speed, maxSpeed * throttle,
                    acceleration * punch * Time.fixedDeltaTime);
            }
            else if (throttle < -0.1f)
                speed = speed > 0.5f
                    ? Mathf.MoveTowards(speed, 0f, braking * Time.fixedDeltaTime)
                    : Mathf.MoveTowards(speed, -reverseSpeed, acceleration * Time.fixedDeltaTime);
            else
                speed = Mathf.MoveTowards(speed, 0f, coasting * Time.fixedDeltaTime);
        }

        private void UpdateSteering(float steer)
        {
            // O input digital de teclado e um degrau 0→1; alisado, vira rampa — o
            // volante "roda" em vez de estalar. E a viragem perde forca a alta
            // velocidade: a autoestrada nao se conduz como o parque de terra.
            steerValue = Mathf.Lerp(steerValue, steer, 1f - Mathf.Exp(-7f * Time.fixedDeltaTime));

            if (Mathf.Abs(steerValue) < 0.01f || Mathf.Abs(speed) < 0.2f) return;

            // Um carro parado nao vira sobre si proprio, e a alta velocidade o
            // volante tem de pesar — senao, com rato e teclado, isto faz zigue-zague.
            float authority = Mathf.Clamp01(Mathf.Abs(speed) / fullSteerAtSpeed)
                            * Mathf.Lerp(1f, 0.55f, Mathf.Abs(speed) / maxSpeed);
            float direction = Mathf.Sign(speed);

            float degrees = steerValue * steerDegreesPerSecond * authority * direction * Time.fixedDeltaTime;
            body.MoveRotation(body.rotation * Quaternion.Euler(0f, degrees, 0f));
        }
    }
}
