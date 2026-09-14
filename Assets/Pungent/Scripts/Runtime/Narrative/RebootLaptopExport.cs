using System.Collections;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.Player;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O segundo objectivo do Dia 1: pôr a exportação a correr e voltar a ela.
    ///
    /// **O ponto é o intervalo, não a tarefa.** Arrancar uma exportação leva um
    /// clique; o que este passo compra são os dois minutos em que o Tomás anda pela
    /// casa com trabalho a decorrer atrás dele — é aí que o Rui prepara café, abre o
    /// frigorífico e pára no corredor. A história é explícita: *"não fica preso à
    /// secretária; pode circular enquanto o trabalho termina"*. Por isso o passo não
    /// prende a câmara nem bloqueia o movimento em momento nenhum.
    ///
    /// O ecrã diz em que estado está sem uma linha de interface: apagado, a exportar,
    /// terminado. Quem olha da porta sabe se já pode voltar.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class RebootLaptopExport : MonoBehaviour, IPlayerInteractable
    {
        [Header("Ecrã")]
        [Tooltip("O quad da tampa. Colocado à mão pelo dono sobre a malha do "
               + "portátil, que não traz superfície de ecrã própria.")]
        [SerializeField] private Renderer screen;

        [SerializeField] private Texture exportingImage;
        [SerializeField] private Texture doneImage;

        [Tooltip("Brilho do ecrã. Acima de 1 apanha o Bloom do Global Volume, que é o "
               + "que faz um portátil aceso ler-se como fonte de luz e não como um "
               + "autocolante na secretária.")]
        [SerializeField, Min(0f)] private float screenBrightness = 1.25f;

        [Header("Cena")]
        [SerializeField] private PrototypeHUD hud;
        [SerializeField] private WorldDialogueController dialogue;

        [Header("Texto")]
        [SerializeField] private string startPrompt = "Start the export";
        [SerializeField] private string checkPrompt = "Check the export";
        [SerializeField] private string objective = "OBJECTIVE: Check the export on the laptop";
        [SerializeField] private string objectiveWhenDone = string.Empty;

        [Tooltip("Dito quando a exportação arranca, para o jogador saber que pode "
               + "sair dali. Sem isto ele fica à espera em frente ao ecrã, que é "
               + "exactamente o contrário do que este passo existe para fazer.")]
        [SerializeField, TextArea] private string startedThought = "That'll take a while. I'll do something else.";

        [SerializeField, TextArea] private string finishedThought = string.Empty;

        [Tooltip("O passo seguinte do Dia 1: a pausa na varanda. Armado quando o "
               + "jogador vê a exportação terminada — a história diz `depois da "
               + "exportação`, e não durante.")]
        [SerializeField] private RebootBalconyBreak nextBeat;

        [Header("Tempo")]
        [Tooltip("Quanto dura a exportação. Longo o suficiente para o jogador sair "
               + "da secretária e curto o suficiente para não o deixar sem nada que "
               + "fazer: é a janela em que o roaming do Rui é o conteúdo.")]
        [SerializeField, Min(5f)] private float exportSeconds = 75f;

        [Header("Som")]
        [SerializeField] private AudioSource sound;
        [SerializeField] private AudioClip startClip;
        [SerializeField] private AudioClip doneClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        private enum Phase { Asleep, Exporting, Done, Checked }

        // Desarmado ate alguem o armar, pela mesma razao que o queijo: sem isto o
        // portatil respondia desde o primeiro frame e gastava-se o objectivo do
        // Dia 1 no prologo. Quem arma isto e o `RebootEatFromFridge`, quando o
        // susto do frigorifico acaba.
        private bool armed;
        private Phase phase = Phase.Asleep;
        private MaterialPropertyBlock block;

        public bool HoldToInteract => false;

        public string Prompt
        {
            get
            {
                if (!armed) return string.Empty;
                if (phase == Phase.Asleep) return startPrompt;
                if (phase == Phase.Done) return checkPrompt;
                // A exportar ou ja visto: calado de proposito. Um prompt vazio quer
                // dizer "isto nao da agora" e o `PlayerInteractor` deixa a mira
                // passar para o que estiver atras — ver `Resolve`.
                return string.Empty;
            }
        }

        private void Awake()
        {
            if (hud == null) hud = FindObjectOfType<PrototypeHUD>();
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
            if (sound == null) sound = GetComponent<AudioSource>();

            if (screen == null || exportingImage == null || doneImage == null)
            {
                Debug.LogError("[RebootLaptopExport] Faltam o ecrã ou as imagens.", this);
                enabled = false;
                return;
            }

            ShowScreen(null);
        }

        /// <summary>Liga o passo. Chamado pelo beat anterior.</summary>
        public void SetArmed(bool value)
        {
            armed = value;
            if (value && phase == Phase.Asleep && !string.IsNullOrEmpty(objective))
                hud.SetObjective(objective);
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (!armed) return;

            if (phase == Phase.Asleep)
            {
                phase = Phase.Exporting;
                ShowScreen(exportingImage);
                Play(startClip);
                Speak(startedThought);
                StartCoroutine(RunExport());
                return;
            }

            if (phase != Phase.Done) return;

            phase = Phase.Checked;
            hud.SetObjective(objectiveWhenDone);
            Speak(finishedThought);
            if (nextBeat != null) nextBeat.SetArmed(true);
            NarrativeBlackboard.Instance?.OnEvent("day1_export_checked");
            Debug.Log("[RebootLaptopExport] Exportação vista; segundo objectivo cumprido.", this);
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

        private IEnumerator RunExport()
        {
            yield return new WaitForSeconds(exportSeconds);

            phase = Phase.Done;
            ShowScreen(doneImage);

            // O som toca no portatil e nao na cabeca do jogador: se ele estiver na
            // cozinha tem de o ouvir ao longe e saber de onde veio. E a unica coisa
            // que o chama de volta — nao ha cartao nem seta.
            Play(doneClip);
        }

        /// <summary>
        /// Poe uma imagem no ecrã, ou apaga-o com `null`.
        ///
        /// Por `MaterialPropertyBlock` e não trocando o material: o material é um
        /// asset do projecto, e escrever-lhe a textura em Play deixava-a lá gravada
        /// depois de sair — o portátil abria o dia seguinte já com a exportação
        /// terminada, sem ninguém perceber porquê.
        /// </summary>
        private void ShowScreen(Texture image)
        {
            if (screen == null) return;

            if (image == null)
            {
                screen.enabled = false;
                return;
            }

            if (block == null) block = new MaterialPropertyBlock();
            screen.GetPropertyBlock(block);
            block.SetTexture(BaseMap, image);
            block.SetColor(BaseColour, Color.white * screenBrightness);
            screen.SetPropertyBlock(block);
            screen.enabled = true;
        }

        private void Play(AudioClip clip)
        {
            if (sound == null || clip == null) return;
            sound.PlayOneShot(clip, volume);
        }

        private void Speak(string line)
        {
            if (dialogue == null || string.IsNullOrWhiteSpace(line)) return;
            dialogue.ShowThought(line, 3.0f);
        }
    }
}
