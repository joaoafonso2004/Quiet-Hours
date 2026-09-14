using Pungent.Dialogue;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Tarefa domestica com dois gestos em sitios diferentes. Serve as refeicoes
    /// (prato -> frigorifico -> comer a andar) e a roupa (pilha -> comoda).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StagedHomeTask : MonoBehaviour
    {
        public enum Mode { WalkingMeal, CarryToDestination }
        public enum Step { First, Second }

        [SerializeField] private Mode mode;
        [SerializeField] private string id;
        [SerializeField] private string firstPrompt;
        [SerializeField] private string secondPrompt;
        [SerializeField, Min(0f)] private float seconds = 14f;
        [SerializeField] private string requiresEvent;
        [SerializeField] private string silencedByEvent;
        [SerializeField] private MonoBehaviour progressVisual;
        [SerializeField] private GameObject worldPickup;
        [SerializeField] private ThoughtLineSet beatLines;
        [SerializeField] private HomeTaskDirector tasks;
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private ChapterDirector director;

        private int stage;
        private float elapsed;
        private int spoken;
        private ITaskProgressVisual visual;

        public bool IsDone => stage >= 3;

        /// <summary>O id desta tarefa, para quem a procure pelo nome.</summary>
        public string TaskId => id;

        /// <summary>
        /// O gesto que falta **agora**, e nao a tarefa toda.
        ///
        /// E aqui que o "um de cada vez" ja existia sem estar a ser mostrado: uma
        /// refeicao sao dois sitios diferentes — tirar o prato e ir comer — e a
        /// tarefa ja sabe em qual deles vai. O objectivo dizia "Settle in" e mais
        /// nada, e o jogador ficava sem saber que tinha de ir ao frigorifico
        /// primeiro.
        ///
        /// Le os campos directamente e nao pelo <see cref="PromptFor"/>, que devolve
        /// vazio fora da janela da tarefa: quem monta um objectivo precisa do nome do
        /// gesto mesmo quando o prompt nao esta a ser oferecido naquele instante.
        /// </summary>
        public string ObjectiveLabel
        {
            get
            {
                if (IsDone) return string.Empty;
                if (stage == 0) return string.IsNullOrWhiteSpace(firstPrompt) ? id : firstPrompt;
                return string.IsNullOrWhiteSpace(secondPrompt) ? id : secondPrompt;
            }
        }

        private bool Allowed
        {
            get
            {
                director = ChapterDirector.Resolve(director);
                if (director == null) return false;
                if (!string.IsNullOrWhiteSpace(requiresEvent) && !director.HasSeen(requiresEvent))
                    return false;
                if (!string.IsNullOrWhiteSpace(silencedByEvent) && director.HasSeen(silencedByEvent))
                    return false;
                return true;
            }
        }

        private void Awake()
        {
            if (tasks == null) tasks = FindObjectOfType<HomeTaskDirector>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            visual = progressVisual as ITaskProgressVisual;
            RefreshWorldPickup();
        }

        private void Update()
        {
            RefreshWorldPickup();
            if (mode != Mode.WalkingMeal || stage != 2) return;

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, seconds));
            visual?.SetTaskProgress(progress);
            SpeakBeats(progress);
            if (progress >= 1f) Complete();
        }

        public string PromptFor(Step requested)
        {
            if (!Allowed || IsDone) return string.Empty;
            if (requested == Step.First && stage == 0) return firstPrompt;
            if (requested == Step.Second && stage == 1) return secondPrompt;

            // **Fora da sua vez, diz o que falta em vez de ficar mudo.**
            //
            // Ate aqui devolvia vazio, e vazio tira o objecto da mira: o frigorifico
            // ficava um movel morto ate alguem ter ido buscar um prato do outro lado
            // da cozinha. Quem quer comer vai ao frigorifico primeiro — e o gesto
            // obvio — e encontrava nada. O relatorio de teste chama-lhe *"e dificil
            // tirar a comida do frigorifico"*, e nao era dificil: era impossivel, e
            // o jogo nao dizia porque.
            //
            // Agora aponta para o gesto que falta, e clicar diz a mesma coisa em voz
            // alta (ver `Perform`). E a mesma regra do `PlayerSleep`, que nao deixa
            // dormir com a luz acesa mas **diz** que e a luz.
            if (requested == Step.Second && stage == 0 && !string.IsNullOrWhiteSpace(firstPrompt))
                return outOfTurnPrefix + firstPrompt.ToLowerInvariant();

            return string.Empty;
        }

        [Tooltip("Posto a frente do gesto que falta quando o jogador tenta o segundo "
               + "primeiro. Ver `PromptFor`.")]
        [SerializeField] private string outOfTurnPrefix = "First, ";

        [Tooltip("Dito quando ele tenta o segundo gesto sem ter feito o primeiro.")]
        [SerializeField, TextArea] private string outOfTurnThought = "A plate first.";

        public void Perform(Step requested)
        {
            if (string.IsNullOrEmpty(PromptFor(requested))) return;

            // Clicar no gesto que ainda nao e a vez dele responde em voz alta e nao
            // faz nada. Um prompt que nao responde ao clique e pior do que nenhum:
            // le-se como o jogo a falhar.
            if (requested == Step.Second && stage == 0)
            {
                thoughts?.Think($"staged_{id}_order", outOfTurnThought, 4, false, 2.8f);
                return;
            }

            if (requested == Step.First)
            {
                tasks?.Discover(id);
                stage = 1;
                visual?.BeginTask();

                // Um prato apanhado da bancada ainda esta vazio. O frigorifico
                // volta a enche-lo no segundo gesto.
                if (mode == Mode.WalkingMeal) visual?.SetTaskProgress(1f);
                RefreshWorldPickup();
                return;
            }

            stage = 2;
            if (mode == Mode.WalkingMeal)
            {
                elapsed = 0f;
                spoken = 0;
                visual?.SetTaskProgress(0f);
            }
            else
            {
                Complete();
            }
        }

        private void SpeakBeats(float progress)
        {
            int count = beatLines != null ? beatLines.Count : 0;
            if (spoken >= count || count == 0) return;
            if (progress < (spoken + 0.5f) / count) return;

            string line = beatLines.LineAt(spoken);
            // Ver a nota igual no `HomeTaskInteractable`: isto sai enquanto o jogador
            // esta a fazer a tarefa que pediu, e nao ao passar ao lado de uma coisa.
            thoughts?.Think($"{id}_{spoken}", line, 2, false, 3f);
            spoken++;
        }

        private void Complete()
        {
            visual?.EndTask();
            stage = 3;
            tasks?.Complete(id, string.Empty);
            RefreshWorldPickup();
        }

        private void RefreshWorldPickup()
        {
            if (worldPickup == null) return;
            bool visible = Allowed && stage == 0;
            if (worldPickup.activeSelf != visible) worldPickup.SetActive(visible);
        }

#if UNITY_EDITOR
        public void EditorConfigure(Mode taskMode, string taskId, string first, string second,
            float duration, string opens, string closes, MonoBehaviour held,
            GameObject pickupVisual, ThoughtLineSet lines, HomeTaskDirector taskDirector,
            PlayerThoughtDirector thoughtDirector)
        {
            mode = taskMode;
            id = taskId;
            firstPrompt = first;
            secondPrompt = second;
            seconds = duration;
            requiresEvent = opens;
            silencedByEvent = closes;
            progressVisual = held;
            worldPickup = pickupVisual;
            beatLines = lines;
            tasks = taskDirector;
            thoughts = thoughtDirector;
        }
#endif
    }
}
