using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// Corre o capitulo da estrada: os farois atras, o motor que morre, e quem vem
    /// a pe pela estrada acima.
    ///
    /// Os tres passos do `CH_Night_Road` esperam por `road_followed`, `car_stopped`
    /// e `road_resolved`. Nenhum tinha dono — e a mesma armadilha que este projecto
    /// ja pagou cinco vezes, por isso fica aqui resolvida antes de haver estrada.
    ///
    /// **O relogio e a musica, nao a distancia.** O carro morre quando o radio
    /// acaba as duas faixas, e nao aos tantos metros: assim a viagem dura sempre os
    /// 4m21 das musicas, conduza-se depressa ou devagar. Uma distancia fixa cortava
    /// a segunda musica a quem tivesse pe pesado.
    ///
    /// Os farois de tras nao perseguem: mantem a distancia. Um carro que se aproxima
    /// le-se como perseguicao e assusta uma vez; um que fica sempre a mesma distancia
    /// le-se como companhia, e e isso que nao larga. A fala do passo — "the same pair
    /// of lights behind me for six miles" — pede exactamente isso.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NightRoadDirector : MonoBehaviour
    {
        [SerializeField] private ChapterDirector director;
        [SerializeField] private RoadPath path;
        [SerializeField] private CarDriver car;
        [SerializeField] private CarRadio radio;
        [SerializeField] private CarSeat seat;
        [SerializeField] private EngineInspection engine;
        [SerializeField] private StrangerEncounter stranger;
        [SerializeField] private CarIgnition ignition;
        [SerializeField] private CarDoorInteractable door;

        [Tooltip("Se ele nunca abrir o capo, o estranho vem na mesma ao fim disto. "
               + "Ninguem fica preso por nao ter percebido o que havia para fazer.")]
        [SerializeField, Min(5f)] private float strangerWaitsAtMost = 26f;

        [Tooltip("Metros a andar depois de pegar, antes de o capitulo fechar.")]
        [SerializeField, Min(10f)] private float escapeDistance = 220f;

        [Tooltip("Metros de estrada guardados para o fim do capitulo: ele avaria "
               + "aqui, mesmo que o radio ainda va a meio. Tem de chegar para o "
               + "carro rolar ate parar e para a fuga a seguir.")]
        [SerializeField, Min(120f)] private float endMargin = 420f;

        [Header("Onde olhar")]
        [Tooltip("Dito ao sair do carro. Aponta o capo sem mandar abri-lo.")]
        [SerializeField, TextArea] private string hoodHint =
            "Nothing on the dash. Whatever it is, it is under the bonnet.";

        [Tooltip("Dito quando o estranho chega. Lembra-lhe que a porta esta ali.")]
        [SerializeField, TextArea] private string doorHint =
            "Driver's door is still open behind me.";

        [SerializeField] private PlayerThoughtDirector thoughts;

        [Tooltip("Pausa entre o motor morrer e ele sair. Tempo para o pensamento "
               + "cair e para o carro acabar de abrandar.")]
        [SerializeField, Min(0f)] private float secondsBeforeGettingOut = 3.5f;

        [Header("Os farois de tras")]
        [SerializeField] private Transform follower;
        [Tooltip("Metros atras do carro. Longe que baste para nao se ver o que e, "
               + "perto que baste para o caminho dele ate ao Tomas nao ser uma "
               + "caminhada de um minuto depois de o carro morrer.")]
        [SerializeField] private float followGap = 48f;
        [Tooltip("So aparece depois destes metros: no primeiro minuto a estrada tem "
               + "de estar vazia, senao nao ha nada a mudar.")]
        [SerializeField] private float followerAppearsAfter = 900f;

        [Header("Quem vem a pe")]
        [Tooltip("A figura que se aproxima depois de o carro parar.")]
        [SerializeField] private Transform walker;
        [SerializeField] private float walkerStartsBack = 70f;

        [Tooltip("Passo de quem se aproxima. Estava a 1,25 — 58 segundos a andar, e "
               + "a terceira musica dava a volta antes de ele chegar. 1,9 e passo "
               + "de quem vem a ter com alguem, e nao um passeio.")]
        [SerializeField] private float walkerSpeed = 1.9f;
        [Tooltip("A que distancia do carro o passo fecha.")]
        [SerializeField] private float walkerReaches = 12f;

        [Header("Minimos")]
        [Tooltip("Nao morre antes disto, mesmo que o radio acabe: ficar sem carro a "
               + "duzentos metros da oficina nao e a mesma cena.")]
        [SerializeField] private float minimumDistance = 1200f;

        /// <summary>Os farois de tras ja apareceram. Lido pelo <see cref="RoadThoughts"/>.</summary>
        public bool FollowerVisible => follower != null && follower.gameObject.activeSelf;

        /// <summary>
        /// Encurta a distancia a que quem vem atras se mantem.
        ///
        /// Chamado pelo <see cref="RoadCrash"/>: quem se despista perde tempo, e o
        /// tempo que ele perde e a unica coisa que o outro carro nao perdeu. Nao ha
        /// ecra de derrota nesta estrada — ha o preco, e o preco e aqueles farois
        /// estarem mais perto quando ele voltar a si.
        ///
        /// Nunca aumenta e nunca sobe do valor de origem: e um encurtamento, e uma
        /// segunda batida nao deve poder afasta-lo outra vez.
        /// </summary>
        public void CloseFollowerGap(float metres)
        {
            followGap = Mathf.Clamp(Mathf.Min(followGap, metres), 8f, followGap);
        }

        private int hint = -1;
        private float carDistance;
        private bool raisedFollowed;
        private bool raisedStopped;
        private bool raisedResolved;
        private float walkerDistance;
        private bool followerParked;
        private float parkedDistance;
        private float stoppedAt;
        private bool strangerMoving;
        private bool strangerArrived;
        private float escapeFrom = -1f;
        private Animator walkerAnimator;
        private float restingSince = -1f;
        private bool gotOut;

        private void Awake()
        {
            if (director == null) director = FindObjectOfType<ChapterDirector>();
            if (path == null) path = FindObjectOfType<RoadPath>();
            if (car == null) car = FindObjectOfType<CarDriver>();
            if (radio == null) radio = FindObjectOfType<CarRadio>();
            if (seat == null) seat = FindObjectOfType<CarSeat>();
            if (engine == null) engine = FindObjectOfType<EngineInspection>(true);
            if (stranger == null) stranger = FindObjectOfType<StrangerEncounter>(true);
            if (ignition == null) ignition = FindObjectOfType<CarIgnition>(true);
            if (door == null) door = FindObjectOfType<CarDoorInteractable>(true);
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();

            if (follower != null) follower.gameObject.SetActive(false);
            if (walker != null) walker.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (car == null || path == null) return;

            carDistance = path.DistanceOf(car.transform.position, ref hint);

            UpdateFollower();
            UpdateEngine();
            UpdateExit();
            UpdateEscape();
            UpdateWalker();
        }

        /// <summary>
        /// Poe os farois na estrada, atras. Colados a linha central e nao ao carro:
        /// numa curva, um objecto atras do carro em linha recta ia parar ao milho.
        /// </summary>
        private void UpdateFollower()
        {
            if (follower == null) return;

            // Encostado: fica onde parou, aceso, a fazer contraluz ao que vem a pe.
            if (followerParked)
            {
                follower.position = path.PointAt(parkedDistance) + Vector3.up * 0.65f;
                follower.rotation = Quaternion.LookRotation(path.ForwardAt(parkedDistance), Vector3.up);
                return;
            }

            bool due = carDistance >= followerAppearsAfter;
            if (follower.gameObject.activeSelf != due) follower.gameObject.SetActive(due);
            if (!due) return;

            float behind = Mathf.Max(0f, carDistance - followGap);
            follower.position = path.PointAt(behind) + Vector3.up * 0.65f;
            follower.rotation = Quaternion.LookRotation(path.ForwardAt(behind), Vector3.up);

            // Ja e a segunda musica e ele ja reparou: o passo pode fechar.
            if (!raisedFollowed && radio != null && radio.TrackIndex >= 2)
            {
                raisedFollowed = true;
                director = ChapterDirector.Resolve(director);
                director?.Notify("road_followed");
            }
        }

        /// <summary>
        /// Mata o motor: o que vier primeiro, a musica a acabar ou a estrada a
        /// acabar.
        ///
        /// Era so a musica. Isso amarrava o comprimento da estrada aos 4m23 das duas
        /// faixas — encurtar o alcatrao punha o carro a bater na barreira do fim
        /// ainda a andar, com o capitulo por comecar. Com as duas condicoes, a
        /// estrada pode ter o comprimento que a pena pedir: quem for depressa avaria
        /// mais cedo e ouve menos musica, que e o que acontece a quem tem pressa.
        /// </summary>
        private void UpdateEngine()
        {
            if (raisedStopped || radio == null) return;

            // Margem para ele rolar ate parar e ainda sobrar berma para a cena toda.
            float lastSafe = path.Length - endMargin;
            bool outOfRoad = carDistance >= lastSafe;

            if (!radio.Finished && !outOfRoad) return;
            if (carDistance < minimumDistance) return;

            raisedStopped = true;
            stoppedAt = Time.time;
            car.StopEngine();
            director = ChapterDirector.Resolve(director);
            director?.Notify("car_stopped");

            // "Um carro para atras. O estranho oferece ajuda" (§5). Os farois nao se
            // apagam: encostam. Ficam onde estao, acesos, a iluminar a estrada por
            // tras — e e de la que ele sai. Apagados, o estranho aparecia do nada e
            // perdia-se a unica coisa que o jogador andou a ver no espelho durante
            // quatro minutos.
            followerParked = true;
            parkedDistance = Mathf.Max(0f, carDistance - followGap);

            if (walker != null)
            {
                // Sai do carro que parou, e nao de um ponto qualquer atras.
                walkerDistance = parkedDistance;
                walker.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Tira-o do carro. O relogio so conta com o carro mesmo parado — o motor
        /// morre em andamento e ele ainda rebola uns metros — e a pausa e a do
        /// inspector: tempo para o pensamento cair antes de a porta abrir. La fora,
        /// o radio continua a tocar de dentro do carro (ver <see cref="CarRadio"/>).
        /// </summary>
        private void UpdateExit()
        {
            if (!raisedStopped || gotOut || seat == null) return;

            if (Mathf.Abs(car.Speed) > 0.4f) { restingSince = -1f; return; }
            if (restingSince < 0f) { restingSince = Time.time; return; }
            if (Time.time - restingSince < secondsBeforeGettingOut) return;

            gotOut = true;
            seat.Exit();

            // Aponta-lhe o capo. Sem isto o jogador fica de pe numa estrada preta,
            // com uma lanterna acesa e nada a dizer-lhe que ha alguma coisa para
            // abrir — e a unica accao disponivel no capitulo esta a tres metros
            // dele, invisivel. Nao diz "abre o capo": diz que ha um capo.
            thoughts?.Think("road_hood", hoodHint, 4, true, 4f);
        }

        /// <summary>
        /// Ele vem a pe do carro que encostou.
        ///
        /// So se poe a andar depois de o Tomas ter percebido o cabo, ou ao fim de
        /// <see cref="strangerWaitsAtMost"/> se ele nunca abrir o capo. A ordem
        /// importa: chegar antes da descoberta fazia dele um susto qualquer; chegar
        /// depois faz dele a resposta a pergunta que o Tomas acabou de fazer a si
        /// proprio.
        /// </summary>
        private void UpdateWalker()
        {
            if (!raisedStopped || walker == null) return;

            if (!strangerMoving)
            {
                // Ao pe do carro dele, parado, desde o instante em que aparece.
                // Sem isto ficava na origem do mundo — de pe no principio da
                // estrada, a quilometros, visivel para quem olhasse para tras.
                walker.position = path.PointAt(walkerDistance);
                walker.rotation = Quaternion.LookRotation(path.ForwardAt(walkerDistance), Vector3.up);
                SetWalking(false);

                bool found = engine != null && engine.CulpritFound;
                bool waited = Time.time - stoppedAt >= strangerWaitsAtMost;
                if (!found && !waited) return;
                strangerMoving = true;
            }

            if (!strangerArrived)
            {
                walkerDistance = Mathf.MoveTowards(walkerDistance, carDistance, walkerSpeed * Time.deltaTime);
                walker.position = path.PointAt(walkerDistance);
                walker.rotation = Quaternion.LookRotation(path.ForwardAt(walkerDistance), Vector3.up);

                SetWalking(true);
                if (carDistance - walkerDistance > walkerReaches) return;

                strangerArrived = true;
                SetWalking(false);   // chegou: para de andar e comeca a falar
                stranger?.Begin();

                // So agora e que a porta se oferece. Antes disto, entrar no carro
                // era fugir de nada.
                door?.MakeAvailable();

                // E aponta-lhe a porta. Quando ele decidir que quer sair dali, tem
                // de saber que pode — descobrir a porta do proprio carro por
                // tentativa e erro, com alguem a falar-lhe ao lado, e o momento
                // todo desfeito.
                thoughts?.Think("road_getin", doorHint, 5, true, 4f);
            }
        }

        /// <summary>
        /// Liga ou desliga o passo dele. O `Animator` esta no modelo, que e filho do
        /// objecto que o director move — mover e animar sao coisas separadas aqui.
        /// </summary>
        private void SetWalking(bool walking)
        {
            if (walkerAnimator == null && walker != null)
                walkerAnimator = walker.GetComponentInChildren<Animator>(true);

            if (walkerAnimator != null && walkerAnimator.runtimeAnimatorController != null)
                walkerAnimator.SetBool("Walking", walking);
        }

        /// <summary>
        /// A fuga: entrar, trancar, pegar, e por-se a andar.
        ///
        /// O `road_resolved` mudou de sitio. Estava a ser levantado quando o estranho
        /// chegava ao carro — e como esse evento levanta o `climax`, o capitulo
        /// saltava para o apartamento no momento exacto em que ele aparecia a
        /// janela. A §5 poe a fuga aqui: "sequencia curta de fuga... o jogador
        /// consegue regressar a cidade". So depois e que se vai embora.
        /// </summary>
        private void UpdateEscape()
        {
            if (raisedResolved || !strangerArrived || seat == null) return;
            if (!seat.Seated || ignition == null || !ignition.Started) return;

            if (escapeFrom < 0f) { escapeFrom = carDistance; return; }
            if (carDistance - escapeFrom < escapeDistance) return;

            raisedResolved = true;
            director = ChapterDirector.Resolve(director);
            director?.Notify("road_resolved");
        }

#if UNITY_EDITOR
        /// <summary>
        /// Salta a viagem e poe a cena no instante exacto em que ele chega ao carro.
        /// **So para depuracao.**
        ///
        /// Existe porque a escolha deste capitulo esta no fim de quatro minutos de
        /// alcatrao, e testar as duas respostas — ficar ou entrar no carro — custava
        /// oito minutos de conducao por par de tentativas. Uma decisao que so se
        /// consegue experimentar de meia em meia hora nao chega a ser afinada.
        ///
        /// **Nao simula a cena: poe o mesmo estado que a cena poria.** Cada linha
        /// aqui e a linha correspondente do `UpdateFollower`, `UpdateEngine`,
        /// `UpdateExit` e `UpdateWalker`, pela mesma ordem e com os mesmos
        /// acontecimentos levantados. Um atalho que fizesse as coisas de outra
        /// maneira testava o atalho e nao o capitulo.
        ///
        /// O que fica de fora, e de propósito: o `night_road`. Levantá-lo aqui
        /// mandava o `ChapterSceneLoader` recarregar esta cena por cima de si
        /// propria. Quem entrar por aqui nao ve o cartao do capitulo — ve a cena.
        /// </summary>
        public void EditorSkipToEncounter()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Estrada] O atalho so funciona em Play.");
                return;
            }

            if (path == null || car == null)
            {
                Debug.LogWarning("[Estrada] Sem `RoadPath` ou sem carro nesta cena.");
                return;
            }

            // Onde o carro morreria se a viagem tivesse sido feita a serio.
            float at = Mathf.Max(minimumDistance, path.Length - endMargin);
            car.EditorTeleport(path.PointAt(at),
                Quaternion.LookRotation(path.ForwardAt(at), Vector3.up));

            hint = -1;
            carDistance = at;

            // Os farois de tras: ja tinham aparecido, e ja tinham sido notados.
            if (follower != null) follower.gameObject.SetActive(true);
            if (!raisedFollowed)
            {
                raisedFollowed = true;
                director = ChapterDirector.Resolve(director);
                director?.Notify("road_followed");
            }
            followerParked = true;
            parkedDistance = Mathf.Max(0f, at - followGap);

            // A avaria.
            if (!raisedStopped)
            {
                raisedStopped = true;
                stoppedAt = Time.time;
                car.StopEngine();
                director = ChapterDirector.Resolve(director);
                director?.Notify("car_stopped");
            }

            // E a saida. O `Exit` acende-lhe a lanterna, tal e qual como no fio
            // normal — sem ela, o teste era feito as escuras e nao se via nada do
            // que ha para ver.
            if (!gotOut && seat != null)
            {
                gotOut = true;
                restingSince = Time.time;
                seat.Exit();
            }

            // Ele ja la esta. Nao vem a andar: **aparecer ja chegado e a unica parte
            // que o atalho falseia**, e falseia-a de propósito, porque os 20 s a ve-lo
            // aproximar-se sao a unica coisa desta cena que nao tem decisao nenhuma.
            if (walker != null)
            {
                walker.gameObject.SetActive(true);
                walkerDistance = at - walkerReaches;
                walker.position = path.PointAt(walkerDistance);
                walker.rotation = Quaternion.LookRotation(path.ForwardAt(walkerDistance), Vector3.up);
            }
            SetWalking(false);
            strangerMoving = true;
            strangerArrived = true;

            stranger?.Begin();
            door?.MakeAvailable();
            thoughts?.Think("road_getin", doorHint, 5, true, 4f);

            Debug.Log("[Estrada] Saltado para o encontro, ao metro " +
                      Mathf.RoundToInt(at) + " de " + Mathf.RoundToInt(path.Length) +
                      ". Ficar ate ao fim = aceitar; entrar no carro = recusar.");
        }
#endif
    }
}
