using System.Collections.Generic;
using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Corre capitulos definidos em assets.
    ///
    /// Um so componente na cena aguenta o jogo todo: os capitulos entram na lista,
    /// cada um arranca com o seu evento, e um passo fecha por tempo, por chegada a
    /// um sitio ou por alguem chamar <see cref="Notify"/>.
    ///
    /// O `Notify` e a peca que liga isto ao resto sem acoplar nada: um
    /// `RoutineActionAnchor`, um `FlavourInteractable` ou uma porta chamam-no pelo
    /// UnityEvent e nao precisam de saber que capitulos existem.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChapterDirector : MonoBehaviour, IRebindable
    {
        [SerializeField] private ChapterDefinition[] chapters = new ChapterDefinition[0];

        [Header("Ligacoes")]
        [SerializeField] private PrototypeHUD hud;
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private Transform player;
        [Tooltip("Os eventos levantados aqui tambem soltam mensagens do telemovel.")]
        [SerializeField] private PhoneMessageService messages;
        [Tooltip("Ecra preto com a data e a hora, antes de cada capitulo.")]
        [SerializeField] private ChapterCard card;

        [Tooltip("As variaveis ocultas da seccao 6.2. Opcional: sem elas o jogo corre "
               + "na mesma, so nao acumula nada.")]
        [SerializeField] private NarrativeBlackboard blackboard;

        [Tooltip("Componentes suspensos enquanto o cartao esta no ecra. O jogador "
               + "nao deve poder andar por tras dele.")]
        [SerializeField] private MonoBehaviour[] suppressedDuringCard = new MonoBehaviour[0];

        [Header("Relogio")]
        [Tooltip("Minutos de jogo por segundo real. A 0,2 uma hora de jogo passa em "
               + "cinco minutos reais, que e o ritmo a que um capitulo se joga.\n\n"
               + "Zero para o relogio parar.")]
        [SerializeField, Min(0f)] private float gameMinutesPerSecond = 0.2f;

        [Tooltip("Hora a que o relogio arranca enquanto nenhum capitulo lhe deu uma.\n\n"
               + "O prologo e o unico capitulo sem cartao — nao ha corte, o jogo "
               + "comeca ali — e por isso nao tem hora escrita em lado nenhum. Sem "
               + "isto o telemovel abria o jogo na hora do Dia 2.")]
        [SerializeField] private string startingClock = "23:41";

        private ChapterDefinition current;
        private int stepIndex = -1;
        private float stepStartedAt;
        private readonly HashSet<string> fired = new HashSet<string>();
        private readonly HashSet<string> finishedChapters = new HashSet<string>();

        /// <summary>Minuto do dia com que o capitulo actual abriu. Negativo = nenhum ainda.</summary>
        private float clockBaseMinutes = -1f;
        private float clockStartedAt;

        public string CurrentChapter => current != null ? current.Title : null;
        public int CurrentStep => stepIndex;

        /// <summary>
        /// A hora do jogo, para quem a precise de mostrar.
        ///
        /// O telemovel mostrava `02:47` fixo — um campo serializado com a hora do
        /// cartao do `CH_Day2_0247` la escrita a mao. Ficava nas 02:47 de manha ao
        /// Dia 3, o que e pior do que nao ter relogio: um relogio parado diz ao
        /// jogador que o tempo nao anda.
        ///
        /// A hora vem de onde ja estava escrita — do cartao do capitulo — e anda a
        /// partir dai. Nao ha um segundo sitio a inventar horas: mudar o cartao de
        /// um capitulo muda o que o telemovel diz, e a noite das 02:47 continua a
        /// comecar as 02:47.
        ///
        /// `Time.time` e nao `unscaledTime`: com o jogo em pausa o tempo tambem para.
        /// </summary>
        public string ClockLabel
        {
            get
            {
                float minutes = clockBaseMinutes;
                if (minutes < 0f) minutes = 0f;
                minutes += (Time.time - clockStartedAt) * gameMinutesPerSecond;

                int whole = Mathf.FloorToInt(minutes) % (24 * 60);
                if (whole < 0) whole += 24 * 60;
                return string.Format("{0:00}:{1:00}", whole / 60, whole % 60);
            }
        }

        /// <summary>
        /// Poe o relogio na hora do cartao deste capitulo.
        ///
        /// Um capitulo sem cartao (ou com uma hora que nao se leia) nao mexe no
        /// relogio: ele continua de onde estava, que e o que faz sentido — nao ter
        /// cartao quer dizer que nao houve corte no tempo.
        /// </summary>
        private void SetClockFrom(ChapterDefinition chapter)
        {
            if (chapter != null) SetClock(chapter.CardTime);
        }

        private void SetClock(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return;

            string[] parts = label.Trim().Split(':');
            if (parts.Length < 2) return;
            if (!int.TryParse(parts[0], out int hours)) return;
            if (!int.TryParse(parts[1], out int minutes)) return;

            clockBaseMinutes = Mathf.Clamp(hours, 0, 23) * 60 + Mathf.Clamp(minutes, 0, 59);
            clockStartedAt = Time.time;
        }

        /// <summary>
        /// Devolve o director vivo que conserva o estado narrativo.
        ///
        /// As cenas ainda podem guardar referencias serializadas para o antigo
        /// director do PlayerRoot. Quando o GAME_SYSTEMS persistente ganha, esse
        /// componente e destruido e a referencia passa a ser o "null falso" do
        /// Unity. Resolver no momento de levantar um evento impede que uma accao
        /// irreversivel (apanhar chaves, entregar uma caixa, dormir) fale com um
        /// objecto morto e deixe o capitulo preso.
        /// </summary>
        public static ChapterDirector Resolve(ChapterDirector candidate = null)
        {
            if (candidate != null) return candidate;

            var root = GameSystemsRoot.Instance;
            if (root != null)
            {
                var persistent = root.GetComponentInChildren<ChapterDirector>(true);
                if (persistent != null) return persistent;
            }

            return Object.FindObjectOfType<ChapterDirector>(true);
        }

        /// <summary>
        /// Se um acontecimento ja passou. O registo existia so para uso interno, mas
        /// quem levanta eventos precisa dele para se ordenar: um volume na oficina
        /// nao deve contar antes de o jogador ter sequer chegado la.
        /// </summary>
        public bool HasSeen(string id) =>
            !string.IsNullOrWhiteSpace(id) && fired.Contains(id);

        private void Awake()
        {
            // Antes do Rebind, que ja pode arrancar um capitulo — e o cartao desse
            // capitulo, se tiver hora, e quem deve mandar no relogio a partir dai.
            SetClock(startingClock);
            Rebind();
        }

        /// <summary>
        /// Volta a encontrar o jogador. Corre no `Awake` e outra vez a cada cena
        /// carregada, porque este componente passou a sobreviver a troca de cena
        /// (ver <see cref="GameSystemsRoot"/>) e o jogador nao: as referencias de
        /// antes ficam a apontar para objectos destruidos, que comparam como nulos
        /// e por isso se deixam substituir pelos da cena nova.
        /// </summary>
        public void Rebind()
        {
            if (hud == null) hud = FindObjectOfType<PrototypeHUD>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            if (messages == null) messages = FindObjectOfType<PhoneMessageService>();
            if (blackboard == null) blackboard = FindObjectOfType<NarrativeBlackboard>();
            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor != null) player = motor.transform;
            }

            RebuildSuppressed();

            // `Start` nao volta a correr depois de um domain reload durante Play.
            // Se a raiz persistente se reatar nessa altura, o director tem de
            // retomar tambem o arranque do primeiro capitulo. Idempotente: com um
            // capitulo activo, TryStartChapters sai imediatamente.
            if (Application.isPlaying) TryStartChapters();
        }

        /// <summary>
        /// Os componentes que o cartao de capitulo suspende vivem no jogador, e o
        /// jogador de cada cena e outro objecto. Sem isto a lista ficava cheia de
        /// destruidos e o cartao deixava de suspender coisa nenhuma — o jogador
        /// andava e interagia por tras do ecra preto, que e exactamente o mesmo erro
        /// que a sequencia de abertura ja tinha custado uma vez.
        /// </summary>
        private void RebuildSuppressed()
        {
            var keep = new List<MonoBehaviour>();
            foreach (var component in suppressedDuringCard)
                if (component != null) keep.Add(component);

            AddIfMissing(keep, FindObjectOfType<Pungent.Player.PlayerMotor>());
            AddIfMissing(keep, FindObjectOfType<PlayerInteractor>());
            AddIfMissing(keep, FindObjectOfType<PrototypePhoneUI>());

            suppressedDuringCard = keep.ToArray();
        }

        private static void AddIfMissing(List<MonoBehaviour> list, MonoBehaviour item)
        {
            if (item != null && !list.Contains(item)) list.Add(item);
        }

        private void Start() => TryStartChapters();

        /// <summary>
        /// Marca um acontecimento. Fecha o passo actual se for por ele que espera, e
        /// arranca capitulos que dependam dele.
        /// </summary>
        public void Notify(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            fired.Add(id);

            // O telemovel partilha o mesmo vocabulario de eventos: um passo de
            // capitulo e um passo de um fio de mensagens podem esperar pelo mesmo.
            messages?.RaiseEvent(id);

            // E as variaveis ocultas da seccao 6.2 tambem. Ligadas aqui e nao em
            // cada sistema porque este e o unico sitio por onde os acontecimentos
            // deste jogo passam todos: uma consequencia esquecida num sistema
            // qualquer nao daria erro nenhum, so um final que nunca acontece.
            blackboard?.OnEvent(id);

            var step = CurrentStepDefinition();
            if (step != null && step.Ends == ChapterDefinition.Completion.Event &&
                step.EventId == id)
                AdvanceStep();

            TryStartChapters();
        }

        private void TryStartChapters()
        {
            if (current != null) return;

            foreach (var chapter in chapters)
            {
                if (chapter == null || finishedChapters.Contains(chapter.Title)) continue;

                bool ready = string.IsNullOrEmpty(chapter.StartsOnEvent)
                             || fired.Contains(chapter.StartsOnEvent);
                if (!ready) continue;

                current = chapter;
                stepIndex = -1;
                SetClockFrom(chapter);

                // O cartao entra antes do primeiro passo. Num jogo em que o
                // apartamento e sempre o mesmo, e a unica coisa que diz ao jogador
                // que passou uma noite.
                if (card != null && chapter.HasCard)
                    StartCoroutine(ShowCardThenStart(chapter));
                else
                    AdvanceStep();
                return;
            }
        }

        private System.Collections.IEnumerator ShowCardThenStart(ChapterDefinition chapter)
        {
            SetSuppressed(true);
            hud?.SetObjective(string.Empty);
            hud?.SetSideObjective(string.Empty);

            yield return card.Show(chapter.CardDate, chapter.CardTime);

            SetSuppressed(false);
            AdvanceStep();
        }

        private void SetSuppressed(bool value)
        {
            foreach (var component in suppressedDuringCard)
                if (component != null) component.enabled = !value;
        }

        private ChapterDefinition.Step CurrentStepDefinition() =>
            current != null ? current.StepAt(stepIndex) : null;

        private void AdvanceStep()
        {
            var finished = CurrentStepDefinition();
            if (finished != null)
                foreach (var id in finished.RaiseOnComplete)
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        fired.Add(id);
                        messages?.RaiseEvent(id);
                        blackboard?.OnEvent(id);
                    }

            stepIndex++;
            var step = CurrentStepDefinition();

            if (step == null)
            {
                // Fim do capitulo.
                if (current != null) finishedChapters.Add(current.Title);
                current = null;
                stepIndex = -1;
                hud?.SetObjective(string.Empty);
                hud?.SetSideObjective(string.Empty);
                TryStartChapters();
                return;
            }

            stepStartedAt = Time.time;
            hud?.SetObjective(step.Objective ?? string.Empty);
            hud?.SetSideObjective(step.SideObjective ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(step.Thought))
                thoughts?.Think($"{current.Title}_{stepIndex}", step.Thought, 4, true, 3.4f);

            // Immediate encadeia sem esperar. Feito aqui e nao no Update para duas
            // falas seguidas nao gastarem um frame cada.
            if (step.Ends == ChapterDefinition.Completion.Immediate)
            {
                AdvanceStep();
                return;
            }

            // O acontecimento por que este passo espera **ja aconteceu**.
            //
            // O `Notify` so fecha o passo que estiver aberto nesse instante, por isso
            // um acontecimento levantado fora de ordem ficava no registo sem nunca
            // fechar nada: o passo abria mais tarde a espera de uma coisa que ja
            // tinha passado, e o capitulo parava ali sem erro nenhum a dizer porque.
            //
            // Nao e uma hipotese teorica — e a armadilha que este projecto ja pagou
            // seis vezes, so que pelo outro lado. Basta o jogador encontrar uma coisa
            // antes de o jogo lha pedir, e a casa esta cheia de sitios onde isso e
            // possivel: as chaves na secretaria do Rui podem ser vistas antes de se
            // dar pela prateleira vazia.
            if (step.Ends == ChapterDefinition.Completion.Event &&
                !string.IsNullOrWhiteSpace(step.EventId) && fired.Contains(step.EventId))
                AdvanceStep();
        }

        private void Update()
        {
            var step = CurrentStepDefinition();
            if (step == null) return;

            switch (step.Ends)
            {
                case ChapterDefinition.Completion.Timer:
                    if (Time.time - stepStartedAt >= step.Seconds) AdvanceStep();
                    break;

                case ChapterDefinition.Completion.ReachArea:
                    if (player == null) break;
                    Vector3 a = new Vector3(player.position.x, 0f, player.position.z);
                    Vector3 b = new Vector3(step.Place.x, 0f, step.Place.z);
                    if (Vector3.Distance(a, b) <= step.Radius) AdvanceStep();
                    break;
            }
        }

        private void OnDrawGizmosSelected()
        {
            foreach (var chapter in chapters)
            {
                if (chapter == null) continue;
                for (int i = 0; i < chapter.StepCount; i++)
                {
                    var step = chapter.StepAt(i);
                    if (step == null || step.Ends != ChapterDefinition.Completion.ReachArea) continue;
                    Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.55f);
                    Gizmos.DrawWireSphere(step.Place, step.Radius);
                }
            }
        }
    }
}
