using System.Collections;
using Pungent.Dialogue;
using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Uma coisa que apetece fazer em casa, e que demora o seu tempo.
    ///
    /// Ao contrario do `FlavourInteractable`, que so devolve um pensamento e acaba
    /// ali, isto ocupa o jogador durante alguns segundos: fumar um cigarro na
    /// varanda, lavar a loica, ver televisao. O que interessa nao e a recompensa —
    /// nao ha nenhuma — e o intervalo. Um jogo de tensao precisa de momentos em que
    /// nao se esta a fazer nada de util, senao a tensao nao tem contra o que
    /// existir.
    ///
    /// Durante a tarefa o jogador fica parado mas continua a poder olhar em volta.
    /// E de proposito: e nesses segundos, encostado a varanda, que ele pode reparar
    /// que a luz do Rui se acendeu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeTaskInteractable : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private string id = "smoke";
        [SerializeField] private string prompt = "Smoke a cigarette";
        [Tooltip("Prompt depois de ja ter sido feita. Vazio = deixa de se oferecer.")]
        [SerializeField] private string repeatPrompt = "Have another one";
        [Tooltip("Quantas vezes pode ser feita ao todo. Passado isso deixa de se "
               + "oferecer: um macao nao e infinito, e oferecer sempre o mesmo "
               + "convite tira-lhe o pouco peso que tem.")]
        [SerializeField, Min(1)] private int maximumTimes = 2;
        [Tooltip("Pensamento quando ja nao ha mais. Vazio = fica so sem prompt.")]
        [SerializeField, TextArea] private string exhaustedThought = "That was the last one.";
        [SerializeField] private string completionLabel = "";

        [Header("Ritmo")]
        [SerializeField, Min(1f)] private float seconds = 14f;
        [Tooltip("Pensamentos ditos ao longo da tarefa, espalhados pela duracao.")]
        [SerializeField] private ThoughtLineSet beatLines;

        // Formato antigo, mantido so para a migracao poder le-lo.
        [SerializeField, HideInInspector] private string[] beats =
        {
            "Cold out here.",
            "The city never actually goes quiet. It just gets further away.",
            "One more and I sleep. That is the deal."
        };
        [Tooltip("Impede o jogador de andar enquanto faz a tarefa. Olhar continua livre.")]
        [SerializeField] private bool holdPlayerStill = true;

        [Header("Trela (opcional)")]
        [Tooltip("Se preenchido, o jogador pode andar durante a tarefa mas nao pode "
               + "sair deste volume.\n\n"
               + "Serve o cigarro. Prender os pes durante catorze segundos numa "
               + "varanda e prender o jogador a um temporizador; deixa-lo passear a "
               + "casa com um cigarro aceso e outra coisa errada. Com isto ele anda "
               + "na varanda, encosta-se a guarda, olha para a rua — que e o que a "
               + "tarefa existe para lhe dar — e nao entra.")]
        [SerializeField] private Collider confineTo;

        [Tooltip("Dito quando ele tenta sair. E o Tomas a explicar-se a si proprio, "
               + "nao uma regra do jogo.")]
        [SerializeField, TextArea] private string leashThought =
            "Rui would kill me if I smoked inside.";

        [Header("Apresentacao (opcional)")]
        [Tooltip("O que se ve na mao e como se gasta ao longo da tarefa. Tem de "
               + "implementar ITaskProgressVisual — ex.: HeldCigarette.")]
        [SerializeField] private MonoBehaviour progressVisual;

        [Tooltip("Visible source object for tasks such as washing a mug. It is hidden "
               + "while carried and after the task is complete.")]
        [SerializeField] private GameObject worldProp;

        [Tooltip("Outros visuais que acompanham o progresso, alem do objecto na mao.\n\n"
               + "E por aqui que a loica muda de cor enquanto se lava: o objecto na mao "
               + "e a caneca, e o que muda de aspecto e o lava-loica. Sao duas coisas "
               + "diferentes e cada uma tem o seu componente.")]
        [SerializeField] private MonoBehaviour[] extraVisuals = new MonoBehaviour[0];

        private readonly System.Collections.Generic.List<ITaskProgressVisual> extraProgress =
            new System.Collections.Generic.List<ITaskProgressVisual>();

        [SerializeField] private Pungent.Interaction.PrototypeHUD hud;
        [Tooltip("Fumo, vapor de agua, etc. Ligado durante a tarefa.")]
        [SerializeField] private ParticleSystem effect;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip startClip;
        [SerializeField] private AudioClip loopClip;
        [SerializeField, Range(0f, 1f)] private float clipVolume = 0.55f;

        [Header("Quando e que se oferece")]
        [Tooltip("So a partir deste acontecimento. Vazio = desde o inicio.\n\n"
               + "Sem isto a casa oferecia \"Wash the mug\" na noite do prologo, em "
               + "que ele ainda tem as caixas na sala, e outra vez no Dia 5 com o "
               + "Rui a procura dele. Uma tarefa domestica so quer dizer alguma "
               + "coisa no dia a que pertence.")]
        [SerializeField] private string requiresEvent;

        [Tooltip("Deixa de se oferecer a partir deste acontecimento.")]
        [SerializeField] private string silencedByEvent;

        [Header("Ligacoes")]
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private HomeTaskDirector tasks;
        [SerializeField] private ChapterDirector director;

        private bool running;
        private int timesDone;
        private ITaskProgressVisual visual;

        private bool Exhausted => timesDone >= maximumTimes;

        /// <summary>Ja foi feita pelo menos uma vez. Serve os objectivos opcionais.</summary>
        public bool HasBeenDone => timesDone > 0;

        /// <summary>O id desta tarefa, para quem a procure pelo nome.</summary>
        public string TaskId => id;

        /// <summary>
        /// Como esta tarefa se chama quando e preciso **dize-la** ao jogador.
        ///
        /// E o prompt e nao o `completionLabel`: o primeiro e uma instrucao ("Wash
        /// the dishes") e o segundo e um aviso de fim ("Dishes done"). O que se poe
        /// num objectivo e o que ha para fazer.
        ///
        /// Le o campo directamente em vez de passar pelo <see cref="Prompt"/>, que
        /// devolve vazio quando a tarefa esta fora da sua janela ou a decorrer — e
        /// quem monta um objectivo precisa do nome dela mesmo nesse momento.
        /// </summary>
        public string ObjectiveLabel => string.IsNullOrWhiteSpace(prompt) ? id : prompt;

        public string Prompt
        {
            get
            {
                if (running || Exhausted || !Allowed) return string.Empty;
                if (timesDone == 0) return prompt;
                return string.IsNullOrEmpty(repeatPrompt) ? string.Empty : repeatPrompt;
            }
        }

        /// <summary>
        /// A janela em que esta tarefa existe.
        ///
        /// Devolver vazio no `Prompt` e o que resolve, sozinho, a disputa com
        /// qualquer outro interagivel no mesmo movel: o `PlayerInteractor` escolhe
        /// o **primeiro componente com prompt nao vazio**, e nao o primeiro
        /// componente. Fora do seu dia, esta tarefa simplesmente nao esta la.
        ///
        /// Resolve o director na leitura, como o resto do projecto, e nao no
        /// `Awake`: a cena do apartamento e recarregada para o climax e apanhar o
        /// exemplar condenado deixava a tarefa muda para sempre.
        /// </summary>
        private bool Allowed
        {
            get
            {
                bool gated = !string.IsNullOrWhiteSpace(requiresEvent)
                          || !string.IsNullOrWhiteSpace(silencedByEvent);
                if (!gated) return true;

                director = ChapterDirector.Resolve(director);
                if (director == null) return false;

                if (!string.IsNullOrWhiteSpace(requiresEvent) && !director.HasSeen(requiresEvent))
                    return false;
                if (!string.IsNullOrWhiteSpace(silencedByEvent) && director.HasSeen(silencedByEvent))
                    return false;

                return true;
            }
        }

        public bool HoldToInteract => false;

        private void Awake()
        {
            if (tasks == null) tasks = FindObjectOfType<HomeTaskDirector>();
            if (thoughts == null)
            {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) thoughts = tagged.GetComponent<PlayerThoughtDirector>();
            }

            visual = progressVisual as ITaskProgressVisual;
            if (progressVisual != null && visual == null)
                Debug.LogWarning($"[HomeTask] '{name}': {progressVisual.GetType().Name} " +
                                 "nao implementa ITaskProgressVisual.");

            extraProgress.Clear();
            for (int i = 0; i < extraVisuals.Length; i++)
            {
                if (extraVisuals[i] == null) continue;
                if (extraVisuals[i] is ITaskProgressVisual extra) extraProgress.Add(extra);
                else Debug.LogWarning($"[HomeTask] '{name}': {extraVisuals[i].GetType().Name} " +
                                      "nao implementa ITaskProgressVisual.");
            }

            if (hud == null)
            {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) hud = tagged.GetComponent<Pungent.Interaction.PrototypeHUD>();
            }

            SetPresentation(false);
            RefreshWorldProp();
        }

        private void Update() => RefreshWorldProp();

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (running || Exhausted || !Allowed) return;
            tasks?.Discover(id);
            StartCoroutine(Run(interactor));
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

        private IEnumerator Run(PlayerInteractor interactor)
        {
            running = true;
            RefreshWorldProp();

            var input = interactor != null
                ? interactor.GetComponentInParent<Pungent.Player.PlayerInputReader>()
                : null;
            // So o andar. O olhar fica livre de propósito: e a olhar em volta,
            // parado, que o jogador repara nas coisas.
            //
            // Com trela, o andar fica livre tambem: quem tem por onde andar nao
            // precisa de ser pregado ao chao, e a trela e um limite mais honesto do
            // que a paralisia — diz onde ele nao pode ir em vez de lhe tirar as
            // pernas sem explicacao.
            bool leashed = confineTo != null;
            if (holdPlayerStill && !leashed) input?.SetMoveSuppressed(true);

            Transform body = input != null ? input.transform : null;

            SetPresentation(true);
            if (audioSource != null && startClip != null)
                audioSource.PlayOneShot(startClip, clipVolume);
            if (audioSource != null && loopClip != null)
            {
                audioSource.clip = loopClip;
                audioSource.loop = true;
                audioSource.volume = clipVolume;
                audioSource.Play();
            }

            float elapsed = 0f;
            int spoken = 0;

            // O asset e a fonte; o array antigo so serve enquanto a migracao nao correu.
            int beatCount = beatLines != null && beatLines.Count > 0
                ? beatLines.Count
                : (beats != null ? beats.Length : 0);

            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);

                // Pensamentos espalhados pela duracao, nao todos de rajada.
                if (spoken < beatCount && t >= (spoken + 0.5f) / beatCount)
                {
                    string beat = beatLines != null && beatLines.Count > 0
                        ? beatLines.LineAt(spoken)
                        : beats[spoken];
                    // Prioridade de historia e nao de banalidade: o jogador carregou
                    // num botao e esta parado a fazer a tarefa. O arrefecimento longo
                    // dos pensamentos de passagem deixaria a tarefa a correr em
                    // silencio, e ai nao se percebe que ela esta a acontecer.
                    thoughts?.Think($"{id}_{spoken}", beat, 2, false, 3.0f);
                    spoken++;
                }

                if (leashed) HoldInside(body);

                visual?.SetTaskProgress(t);

                // A barra. O progresso ja estava a ser calculado para o objecto que vai
                // na mao — o que faltava era mostra-lo a quem esta preso no sitio
                // durante quinze segundos sem saber se carregou mesmo no botao.
                //
                // Os `extraVisuals` recebem o mesmo numero: e por ai que a loica muda
                // de cor enquanto se lava.
                hud?.SetTaskProgress(ObjectiveLabel, t);
                for (int i = 0; i < extraProgress.Count; i++) extraProgress[i].SetTaskProgress(t);

                yield return null;
            }

            hud?.ClearTaskProgress();
            for (int i = 0; i < extraProgress.Count; i++) extraProgress[i].EndTask();
            visual?.EndTask();
            SetPresentation(false);
            if (audioSource != null && loopClip != null) audioSource.Stop();

            if (holdPlayerStill && !leashed) input?.SetMoveSuppressed(false);

            timesDone++;
            running = false;
            RefreshWorldProp();
            tasks?.Complete(id, completionLabel);

            if (Exhausted && !string.IsNullOrWhiteSpace(exhaustedThought))
                thoughts?.Think($"{id}_exhausted", exhaustedThought, 2, true, 3.0f);
        }

        /// <summary>
        /// Devolve-o ao volume se ele tentar sair, e diz-lhe porque.
        ///
        /// Empurrado para o ponto mais proximo do volume e nao teleportado para o
        /// centro: o que se quer e uma parede invisivel onde ele encosta, e nao um
        /// puxao. Do lado de dentro, andar continua a nao ter limite nenhum.
        ///
        /// O `CharacterController` tem de ser desligado para mover a raiz — mexer
        /// nela com ele ligado e ignorado no frame seguinte, que e a mesma nota que
        /// o `HidingSpot` e o `CarSeat` ja tem.
        ///
        /// O pensamento sai pelo `PlayerThoughtDirector` com prioridade baixa, e por
        /// isso nao interrompe as falas da propria tarefa: encostar-se a barreira
        /// tres vezes seguidas nao enche o ecra de texto.
        /// </summary>
        private void HoldInside(Transform body)
        {
            if (body == null || confineTo == null) return;

            Vector3 position = body.position;
            Vector3 nearest = confineTo.ClosestPoint(position);

            // Dentro: `ClosestPoint` devolve o proprio ponto.
            if ((nearest - position).sqrMagnitude < 0.0004f) return;

            var controller = body.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            // Um pouco para dentro da superficie, senao ele fica exactamente na
            // fronteira e volta a sair no frame seguinte.
            Vector3 inward = nearest - position;
            body.position = nearest + inward.normalized * 0.05f;

            if (controller != null) controller.enabled = true;

            // Este tem de sair **sempre**. E a unica coisa que explica ao jogador
            // porque e que ele acabou de ser puxado para tras; calado, a trela le-se
            // como o jogo a prender-se.
            if (!string.IsNullOrWhiteSpace(leashThought))
                thoughts?.Think($"{id}_leash", leashThought, 3, false, 2.6f);
        }

        private void SetPresentation(bool active)
        {
            if (active) visual?.BeginTask();
            if (effect != null)
            {
                if (active) effect.Play();
                else effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void RefreshWorldProp()
        {
            if (worldProp == null) return;
            bool visible = Allowed && !running && !Exhausted;
            if (worldProp.activeSelf != visible) worldProp.SetActive(visible);
        }
    }
}
