using Pungent.Narrative;
using Pungent.Dialogue;
using UnityEngine;

namespace Pungent.Interaction
{
    [DisallowMultipleComponent]
    public sealed class SinkInteractable : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private OpeningQuestDirector questDirector;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip waterClip;
        [SerializeField, Range(0f, 1f)] private float waterVolume = 0.8f;

        [Header("Flavour Fallback")]
        [SerializeField] private WorldDialogueController dialogue;
        [SerializeField] private ThoughtLineSet flavourLines;
        [SerializeField, Min(0.8f)] private float holdSeconds = 2.6f;
        [SerializeField, Min(0f)] private float cooldown = 0.5f;

        // Formato antigo, mantido so para a migracao poder le-lo.
        [SerializeField, HideInInspector] private string[] flavourThoughts;

        private bool drank;
        private int flavourIndex;
        private float nextFlavourAllowed;

        /// <summary>
        /// **Sai da frente quando a tarefa a serio estiver a oferecer-se.**
        ///
        /// O lava-loica tinha dois donos para o mesmo momento. Este fechava o passo
        /// `DrinkWater` **num clique**, e ao lado dele — no mesmo movel, no mesmo
        /// sitio — vive um `HomeTaskInteractable` de doze segundos com o id
        /// `d2_water` e o prompt "Get a glass of water", com a janela certa
        /// (`day_one_done` -> `day_two`). Ninguem o pedia.
        ///
        /// O resultado a jogar e o do relatorio de teste: *"a tarefa de beber agua
        /// nao tem nada, o jogador so precisa de clicar no lava-loica"*. Tinha, e
        /// estava a dois centimetros dali, calada.
        ///
        /// Com a tarefa disponivel, este componente devolve prompt vazio e sai da
        /// mira — o `PlayerInteractor` escolhe o primeiro com prompt nao vazio, que
        /// e como este projecto resolve dois interagiveis no mesmo movel. Sem
        /// tarefa (uma cena de teste, ou a ferramenta por correr), volta a fechar o
        /// passo sozinho: e melhor um passo curto do que um passo impossivel.
        /// </summary>
        public string Prompt
        {
            get
            {
                bool drinking = questDirector != null
                             && questDirector.Current == OpeningQuestDirector.Step.DrinkWater;

                if (!drinking) return "Look";
                if (drank) return "Already drank water";
                if (WaterTaskIsOffering) return string.Empty;
                return "Drink water";
            }
        }

        /// <summary>
        /// A tarefa de doze segundos esta mesmo a oferecer-se agora?
        ///
        /// Perguntado pelo prompt dela e nao pela janela: e o prompt que decide
        /// quem apanha o clique, portanto e o prompt que decide quem manda.
        /// Procurada uma vez — nao nascem tarefas a meio da noite.
        /// </summary>
        private bool WaterTaskIsOffering
        {
            get
            {
                if (!searchedTask)
                {
                    searchedTask = true;
                    foreach (var task in GetComponentsInChildren<Narrative.HomeTaskInteractable>(true))
                    {
                        if (task.TaskId != waterTaskId) continue;
                        waterTask = task;
                        break;
                    }

                    if (waterTask == null)
                        Debug.LogWarning("[LavaLoica] Sem a tarefa `" + waterTaskId + "` por baixo do " +
                                         "lava-loica. O passo da agua volta a fechar num clique. " +
                                         "Correr 'Wire Daily Pressure (chores + changes)'.", this);
                }

                return waterTask != null &&
                       !string.IsNullOrEmpty(((IPlayerInteractable)waterTask).Prompt);
            }
        }

        [Tooltip("A tarefa que fecha este passo a serio. Vive num filho do lava-loica.")]
        [SerializeField] private string waterTaskId = "d2_water";

        private Narrative.HomeTaskInteractable waterTask;
        private bool searchedTask;
        public bool HoldToInteract => false;

        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (questDirector == null)
                questDirector = FindObjectOfType<OpeningQuestDirector>();
            if (dialogue == null)
                dialogue = FindObjectOfType<WorldDialogueController>();

            if (flavourLines != null)
                flavourIndex = flavourLines.NewCursor;

            // O lava-loica tem os dois componentes: o Flavour para quando nao ha
            // objetivo activo, e este para beber agua. O PlayerInteractor devolve o
            // primeiro IPlayerInteractable que encontra, por isso o outro tem de sair
            // ou nunca se chega aqui.
            //
            // Antes o texto era copiado do Flavour por reflection sobre um campo
            // privado — silenciosamente partido por qualquer mudanca de nome. Agora
            // os dois apontam ao mesmo ThoughtLineSet e nao ha nada para copiar.
            var flavour = GetComponent<FlavourInteractable>();
            if (flavour != null) Destroy(flavour);
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (questDirector != null && questDirector.Current != OpeningQuestDirector.Step.DrinkWater)
            {
                // Can only drink when objective is active. Fallback to flavour text.
                if (dialogue == null) return;
                if (Time.unscaledTime < nextFlavourAllowed || dialogue.IsBusy) return;

                string line;
                float hold;

                if (flavourLines != null && flavourLines.Count > 0)
                {
                    line = flavourLines.Pick(ref flavourIndex);
                    hold = flavourLines.HoldSeconds;
                }
                else if (flavourThoughts != null && flavourThoughts.Length > 0)
                {
                    line = flavourThoughts[Mathf.Min(flavourIndex, flavourThoughts.Length - 1)];
                    if (flavourIndex < flavourThoughts.Length - 1) flavourIndex++;
                    hold = holdSeconds;
                }
                else return;

                nextFlavourAllowed = Time.unscaledTime + cooldown;
                dialogue.ShowThought(line, hold);
                return;
            }

            if (drank) return;
            drank = true;

            if (audioSource != null && waterClip != null)
                audioSource.PlayOneShot(waterClip, waterVolume);

            if (questDirector != null)
                questDirector.NotifyWaterDrank();
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }
    }
}
