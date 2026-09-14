using System.Collections.Generic;
using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// Carros a passar em sentido contrario — e, mais tarde, a deixarem de passar.
    ///
    /// ---
    ///
    /// **Porque e que isto nao e enfeite.**
    ///
    /// Uma estrada nacional vazia a noite nao le como solidao: le como cenario por
    /// acabar. O jogador sabe que aquilo e um corredor construido para ele porque
    /// **nao passa mais ninguem**, e a partir do momento em que sabe isso, deixa de
    /// haver mundo — ha um nivel. Nenhuma quantidade de nevoeiro, arvores ou relva
    /// corrige isso, porque o problema nao e o que se ve ao lado: e nao haver mais
    /// ninguem a ir para casa aquela hora.
    ///
    /// Tres ou quatro carros a passar em vinte segundos resolvem-no, e resolvem-no
    /// **de graca**: a noite, um carro em sentido contrario nao e um modelo. E dois
    /// farois a crescer, um segundo de barulho, e uma luz vermelha a afastar-se pelo
    /// retrovisor. O corpo quase nao se ve.
    ///
    /// ---
    ///
    /// **E depois param.** E aqui que isto deixa de ser ambiente e passa a ser o
    /// capitulo.
    ///
    /// Ate ao <see cref="stopsOnEvent"/> a estrada tem transito normal. A seguir nao
    /// tem mais nenhum, nunca mais. Ninguem diz nada, nao ha objectivo novo, nao ha
    /// pensamento — e a coisa mais silenciosa que este jogo faz, porque o jogador
    /// **nao repara na ausencia, repara na presenca de que se lembra**. Passaram-lhe
    /// sete carros nos primeiros dois minutos. Vai a meio do terceiro e ainda nao
    /// passou nenhum. E ele nao sabe dizer quando e que parou.
    ///
    /// A ausencia so funciona porque houve presenca. Sem estes carros, a estrada
    /// esteve sempre vazia e nao ha nada a mudar — que e exactamente o estado a que
    /// isto vem responder.
    ///
    /// ---
    ///
    /// **Andam num carril, e batem a serio.**
    ///
    /// Os dois nao sao contraditorios e a distincao e o que faz isto funcionar. A
    /// trajectoria e dada pela <see cref="RoadPath"/> — uma distancia a diminuir,
    /// deslocada para a faixa do lado — porque um carro com direccao autonoma numa
    /// estrada de 4,2 km e um problema de IA que este jogo nao precisa de ter. Mas o
    /// corpo e um `Rigidbody` **cinematico com colisores ligados**, movido por
    /// <c>MovePosition</c> no `FixedUpdate`: quem for para a faixa contraria bate, e
    /// bate com a massa toda.
    ///
    /// Sem isso a estrada nao tinha risco nenhum — atravessar o transito de frente
    /// era passar atraves dele, e a partir do primeiro carro que ele atravessasse o
    /// jogador sabia que aquilo era um cenario projectado.
    ///
    /// **`ContinuousSpeculative` nao e opcional.** Duas velocidades de 25 m/s em
    /// sentidos contrarios dao 50 m/s de aproximacao: a 50 Hz de fisica, sao um metro
    /// por passo. Com deteccao discreta os dois carros aparecem de um lado e do outro
    /// sem nunca se terem tocado, e o unico sintoma e o jogador a dizer que atravessou
    /// um carro.
    ///
    /// O que **e** desligado nos clones sao os *comportamentos*: um `SEDAN.prefab`
    /// instanciado traz `CarDriver`, radio, assento e ignicao, e nenhum deles deve
    /// acordar dentro de um carro que nao e conduzido por ninguem.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OncomingTraffic : MonoBehaviour
    {
        [Header("Estrada")]
        [SerializeField] private RoadPath path;
        [Tooltip("O carro do jogador. E a partir dele que se conta o que esta a "
               + "frente e o que ja passou.")]
        [SerializeField] private CarDriver player;

        [Tooltip("Desvio lateral, em metros, a partir do eixo da estrada. Negativo "
               + "para a esquerda de quem conduz — que e a faixa contraria em "
               + "circulacao pela direita.")]
        [SerializeField] private float laneOffset = -3.2f;

        [Header("O que passa")]
        [Tooltip("Modelos. Vazio = caixas escuras, que a noite e quase o mesmo: o que "
               + "se ve de um carro que vem de frente sao os farois.")]
        [SerializeField] private GameObject[] bodies = new GameObject[0];

        [Tooltip("Velocidade de quem vem em sentido contrario, em m/s. 20 a 26 sao "
               + "72 a 94 km/h — nacional a noite.")]
        [SerializeField] private Vector2 speedRange = new Vector2(20f, 26f);

        [Tooltip("Segundos entre carros. O intervalo largo e o que faz isto parecer "
               + "transito e nao um desfile: dois seguidos e depois meio minuto de "
               + "nada e o ritmo certo.")]
        [SerializeField] private Vector2 gapRange = new Vector2(9f, 34f);

        [Tooltip("A que distancia a frente aparecem. Tem de ser **para la do "
               + "nevoeiro** — um carro a materializar-se a vista e pior do que nao "
               + "haver carro nenhum.")]
        [SerializeField, Min(80f)] private float appearAhead = 330f;

        [Tooltip("Quantos metros atras do jogador sao recolhidos.")]
        [SerializeField, Min(10f)] private float vanishBehind = 70f;

        [Tooltip("Quantos podem existir ao mesmo tempo. Cada um traz duas luzes: "
               + "subir isto sem olhar para o custo de iluminacao e como se paga.")]
        [SerializeField, Range(1, 6)] private int maxAlive = 3;

        [Header("Luz")]
        [SerializeField] private bool headlights = true;
        [SerializeField, Min(1f)] private float beamRange = 45f;
        [SerializeField, Range(0f, 12f)] private float beamIntensity = 4.5f;
        [SerializeField] private bool tailLights = true;

        [Header("Som")]
        [Tooltip("Ciclo de motor/pneus. A passagem faz-se sozinha: a fonte anda com o "
               + "carro e o Unity trata do resto.")]
        [SerializeField] private AudioClip engineLoop;
        [SerializeField, Range(0f, 1f)] private float engineVolume = 0.55f;

        [Header("Quando param")]
        [Tooltip("Depois deste acontecimento nao passa mais nenhum carro, nunca mais. "
               + "Ver a nota da classe: e a peca inteira.\n\n"
               + "`road_followed` e o momento em que o Tomas repara nos farois de tras "
               + "— a estrada esvazia-se a partir de ai, e ele nao vai poder dizer "
               + "quando.")]
        [SerializeField] private string stopsOnEvent = "road_followed";

        [Tooltip("Os que ja estao na estrada quando isso acontece acabam a passagem. "
               + "Fazer um carro desaparecer a vista era denunciar o sistema todo.")]
        [SerializeField] private bool lastOnesFinish = true;

        [Header("Choque")]
        [Tooltip("Corpo solido. Desligado, os carros atravessam-se — o que so serve "
               + "para depurar a trajectoria.")]
        [SerializeField] private bool solid = true;

        [Tooltip("Massa de cada carro. Aproximada a de um utilitario: bater de frente "
               + "num objecto de 40 kg nao e bater num carro.")]
        [SerializeField, Min(100f)] private float mass = 1250f;

        [Tooltip("Acontecimento levantado quando o jogador bate num destes. Quem lhe "
               + "der consequencia e outro componente — aqui so se diz que aconteceu.")]
        [SerializeField] private string crashEvent = "road_crash";

        [Tooltip("Velocidade de aproximacao minima, em m/s, para contar como choque e "
               + "nao como raspao no espelho.")]
        [SerializeField, Min(1f)] private float crashSpeed = 8f;

        [SerializeField] private ChapterDirector director;

        private sealed class Car
        {
            public Transform Body;
            public Rigidbody Rigid;
            public float Distance;
            public float Speed;
        }

        private readonly List<Car> alive = new List<Car>();
        private float nextSpawnAt;
        private int hint;
        private bool stopped;
        private bool crashed;
        private float playerDistance;
        private Transform pool;

        /// <summary>Quantos vao na estrada. Util para inspeccionar e para os testes.</summary>
        public int Alive => alive.Count;

        /// <summary>Ja deixou de haver transito. Util para os testes.</summary>
        public bool Stopped => stopped;

        private void Awake()
        {
            if (path == null) path = FindObjectOfType<RoadPath>();
            if (player == null) player = FindObjectOfType<CarDriver>();

            var holder = new GameObject("ONCOMING_POOL");
            holder.transform.SetParent(transform, false);
            pool = holder.transform;

            nextSpawnAt = Time.time + Random.Range(gapRange.x, gapRange.y) * 0.35f;
        }

        /// <summary>
        /// O movimento vive no passo de fisica e nao no de imagem.
        ///
        /// Um `Rigidbody` cinematico movido por `transform.position` teleporta-se: nao
        /// gera contactos, e o carro do jogador atravessa-o ou e cuspido para fora da
        /// estrada conforme o frame em que calhar. `MovePosition` faz o motor
        /// interpolar o corpo entre os dois sitios e resolver o que estiver pelo meio,
        /// que a estas velocidades e a diferenca entre haver e nao haver choque.
        /// </summary>
        private void FixedUpdate()
        {
            if (path == null || player == null) return;

            playerDistance = path.DistanceOf(player.transform.position, ref hint);
            Advance(playerDistance);
        }

        private void Update()
        {
            if (path == null || player == null) return;

            if (!stopped && Stopping()) stopped = true;

            float here = playerDistance;

            if (stopped) return;
            if (Time.time < nextSpawnAt) return;
            if (alive.Count >= maxAlive) return;

            // Nao nasce nenhum se nao houver estrada suficiente a frente: um carro
            // que aparecesse depois do fim do percurso ficava parado no fim do
            // alcatrao a olhar para o jogador, que e um genero diferente de jogo.
            float birth = here + appearAhead;
            if (birth >= path.Length - 5f) return;

            Spawn(birth);
            nextSpawnAt = Time.time + Random.Range(gapRange.x, gapRange.y);
        }

        private void Advance(float here)
        {
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                var car = alive[i];
                if (car.Body == null) { alive.RemoveAt(i); continue; }

                car.Distance -= car.Speed * Time.fixedDeltaTime;

                if (car.Distance <= here - vanishBehind || car.Distance <= 0f)
                {
                    Destroy(car.Body.gameObject);
                    alive.RemoveAt(i);
                    continue;
                }

                Place(car);
            }
        }

        private void Place(Car car)
        {
            Vector3 forward = path.ForwardAt(car.Distance);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 position = path.PointAt(car.Distance) + right * laneOffset;

            // Virado ao contrario: e o unico sitio onde se decide que este carro vem
            // de la para ca. O sinal do `laneOffset` decide a faixa, e a rotacao
            // decide o sentido — as duas coisas sao independentes e ja se trocaram
            // uma pela outra numa versao anterior, com o resultado de meia dezena de
            // carros a andar de re na berma certa.
            Quaternion rotation = Quaternion.LookRotation(-forward, Vector3.up);

            // Pelo corpo quando ha corpo. Escrever no transform de um `Rigidbody`
            // cinematico salta o motor de fisica: os contactos nao chegam a existir e
            // o choque de frente passa a ser uma passagem atraves.
            if (car.Rigid != null)
            {
                car.Rigid.MovePosition(position);
                car.Rigid.MoveRotation(rotation);
                return;
            }

            car.Body.position = position;
            car.Body.rotation = rotation;
        }

        private void Spawn(float distance)
        {
            var go = Build();
            if (go == null) return;

            var car = new Car
            {
                Body = go.transform,
                Distance = distance,
                Speed = Random.Range(speedRange.x, speedRange.y)
            };

            // **Posto no sitio antes de ganhar corpo, e nao depois.**
            //
            // Um `MovePosition` da origem ate trezentos metros a frente e um varrimento
            // de trezentos metros: o motor procura contactos ao longo do caminho todo e
            // o carro nasce a empurrar o que houver pelo meio — incluindo o jogador, se
            // ele estiver algures nessa linha. A primeira colocacao tem de ser um
            // teleporte, e so a partir da segunda e que ha movimento a resolver.
            Vector3 forward = path.ForwardAt(distance);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            go.transform.SetPositionAndRotation(
                path.PointAt(distance) + right * laneOffset,
                Quaternion.LookRotation(-forward, Vector3.up));

            if (solid) car.Rigid = MakeSolid(go);

            alive.Add(car);
        }

        /// <summary>
        /// Da corpo ao carro: cinematico, com massa, e com deteccao continua.
        ///
        /// A massa nao e decorativa. Ela nao mexe no carro cinematico — mexe no
        /// **outro**: e ela que decide o que acontece ao carro do jogador quando os
        /// dois se encontram. Com o valor por omissao de um `Rigidbody` acabado de
        /// criar, bater de frente num carro pesava como bater num caixote.
        /// </summary>
        private Rigidbody MakeSolid(GameObject go)
        {
            var rigid = go.GetComponent<Rigidbody>();
            if (rigid == null) rigid = go.AddComponent<Rigidbody>();

            rigid.isKinematic = true;
            rigid.detectCollisions = true;
            rigid.mass = mass;
            rigid.interpolation = RigidbodyInterpolation.Interpolate;

            // Ver a nota da classe: a 50 m/s de aproximacao, a deteccao discreta perde
            // o contacto entre dois passos de fisica.
            rigid.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var reporter = go.AddComponent<CrashReporter>();
            reporter.Owner = this;
            return rigid;
        }

        /// <summary>
        /// Diz ao <see cref="OncomingTraffic"/> que este carro foi atingido.
        ///
        /// Num componente proprio porque o `OnCollisionEnter` tem de chegar ao objecto
        /// que bateu, e quem gere o transito vive noutro sitio da cena.
        /// </summary>
        [DisallowMultipleComponent]
        private sealed class CrashReporter : MonoBehaviour
        {
            public OncomingTraffic Owner;

            private void OnCollisionEnter(Collision collision)
            {
                if (Owner != null) Owner.ReportCrash(collision);
            }
        }

        /// <summary>
        /// Um choque. Levanta o acontecimento uma vez e mais nada.
        ///
        /// **A consequencia nao vive aqui de propósito.** O que deve acontecer a quem
        /// bate de frente numa nacional as onze da noite e uma decisao de capitulo — um
        /// corte a preto e acordar na berma com os farois de tras mais perto e outra
        /// coisa completamente diferente de um ecra de "tentar outra vez", e nenhuma
        /// das duas pertence ao componente que faz os carros passar.
        /// </summary>
        internal void ReportCrash(Collision collision)
        {
            if (crashed || string.IsNullOrWhiteSpace(crashEvent)) return;
            if (player == null || collision == null) return;

            // So o carro do jogador conta. Um carro do transito a raspar num poste nao
            // e um acontecimento de historia.
            var hitDriver = collision.collider.GetComponentInParent<CarDriver>();
            if (hitDriver != player) return;

            if (collision.relativeVelocity.magnitude < crashSpeed) return;

            crashed = true;
            director = ChapterDirector.Resolve(director);
            director?.Notify(crashEvent);
        }

        /// <summary>
        /// Um carro decorativo.
        ///
        /// **Tudo o que nao e malha e desligado**, e nao destruido: um `SEDAN.prefab`
        /// traz `Rigidbody`, colisores, `CarDriver`, radio e assento, e um clone com
        /// isso tudo vivo era um carro fisico a ser arrastado por transform contra o
        /// carro do jogador a noventa a hora. Destruir componentes em cadeia rebenta
        /// nos `RequireComponent`; desligar nao rebenta em lado nenhum e da o mesmo
        /// resultado.
        /// </summary>
        private GameObject Build()
        {
            GameObject go;

            if (bodies != null && bodies.Length > 0)
            {
                var prefab = bodies[Random.Range(0, bodies.Length)];
                if (prefab == null) return null;
                go = Instantiate(prefab, pool);
                Neutralise(go);
            }
            else
            {
                // Enquanto nao houver modelos, uma caixa escura. A noite, com farois a
                // vir de frente, e quase indistinguivel — e o jogo corre. Mesma regra
                // do `HeldTaskProp`: as primitivas existem para nao se ficar a
                // espera de arte.
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "OncomingCar";
                go.transform.SetParent(pool, false);
                go.transform.localScale = new Vector3(1.8f, 1.35f, 4.4f);

                // O colisor do cubo fica **ligado** quando o transito e solido: e ele
                // que da corpo ao carro enquanto nao houver modelo. Uma caixa preta
                // que se atravessa e pior do que nao haver carro nenhum.
                var box = go.GetComponent<Collider>();
                if (box != null) box.enabled = solid;

                var renderer = go.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = new Color(0.06f, 0.06f, 0.07f);
            }

            go.transform.localPosition = Vector3.zero;
            AttachLights(go.transform);
            AttachSound(go.transform);
            return go;
        }

        private void Neutralise(GameObject go)
        {
            // Cinematicos, mas a **detectar**. Um carro do transito nao e simulado —
            // anda no carril da estrada — e mesmo assim tem de existir para quem lhe
            // for ao encontro.
            foreach (var body in go.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = solid;
            }

            // Os colisores ficam como estao quando o transito e solido. As rodas nao:
            // um `WheelCollider` orfao — sem `Rigidbody` dinamico que o conduza —
            // aplica forcas de suspensao contra nada e faz o carro tremer no sitio.
            foreach (var wheel in go.GetComponentsInChildren<WheelCollider>(true))
                wheel.enabled = false;

            if (!solid)
                foreach (var collider in go.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;

            foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;

            // As luzes e o som do proprio prefab tambem: quem manda nos farois deste
            // carro e este componente, e dois donos dariam faroleiras a dobrar num
            // carro e nenhuma no seguinte.
            foreach (var light in go.GetComponentsInChildren<Light>(true))
                light.enabled = false;

            foreach (var source in go.GetComponentsInChildren<AudioSource>(true))
                source.enabled = false;
        }

        private void AttachLights(Transform car)
        {
            if (headlights)
            {
                var go = new GameObject("Beam");
                go.transform.SetParent(car, false);
                go.transform.localPosition = new Vector3(0f, 0.65f, 2.1f);

                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.spotAngle = 78f;
                light.range = beamRange;
                light.intensity = beamIntensity;
                light.color = new Color(1f, 0.96f, 0.88f);
                light.shadows = LightShadows.None;
            }

            if (tailLights)
            {
                var go = new GameObject("Tail");
                go.transform.SetParent(car, false);
                go.transform.localPosition = new Vector3(0f, 0.7f, -2.2f);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 6f;
                light.intensity = 1.4f;
                light.color = new Color(1f, 0.12f, 0.08f);
                light.shadows = LightShadows.None;
            }
        }

        private void AttachSound(Transform car)
        {
            if (engineLoop == null) return;

            var go = new GameObject("Engine");
            go.transform.SetParent(car, false);

            var source = go.AddComponent<AudioSource>();
            source.clip = engineLoop;
            source.loop = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 4f;
            source.maxDistance = 90f;

            // O efeito de passagem faz-se sozinho: a fonte anda a cinquenta metros por
            // segundo em relacao ao ouvinte e o Unity trata do desvio. Escrever um
            // "whoosh" a mao era encenar uma coisa que a fisica ja da de graca.
            source.dopplerLevel = 1f;
            source.volume = engineVolume;
            source.Play();
        }

        private bool Stopping()
        {
            if (string.IsNullOrWhiteSpace(stopsOnEvent)) return false;

            director = ChapterDirector.Resolve(director);
            if (director == null || !director.HasSeen(stopsOnEvent)) return false;

            if (!lastOnesFinish)
                for (int i = alive.Count - 1; i >= 0; i--)
                {
                    if (alive[i].Body != null) Destroy(alive[i].Body.gameObject);
                    alive.RemoveAt(i);
                }

            return true;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(RoadPath road, CarDriver playerCar, GameObject[] models,
            AudioClip loop, string stopsOn, float offset = -3.2f)
        {
            path = road;
            player = playerCar;
            bodies = models;
            engineLoop = loop;
            stopsOnEvent = stopsOn;
            laneOffset = offset;
        }
#endif
    }
}
