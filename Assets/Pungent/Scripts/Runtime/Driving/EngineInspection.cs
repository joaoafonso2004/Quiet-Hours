using Pungent.Interaction;
using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// O capo aberto e o que se ve la dentro (§8.13).
    ///
    /// A regra da §8.13 e que a solucao "nao deve exigir conhecimento real de
    /// mecanica". Por isso nao ha diagnostico nenhum a fazer: ha tres coisas para
    /// olhar, duas estao bem, e a terceira esta obviamente errada mesmo para quem
    /// nunca abriu um capo — um cabo fora do sitio, com a ponta limpa.
    ///
    /// E ai que o capitulo vira. Ate esse momento o carro avariou; a partir dali
    /// alguem lhe mexeu. O jogador chega la sozinho, sem ninguem lho dizer, e e por
    /// isso que a pergunta do estranho a seguir — que ja sabe onde ele esteve — cai
    /// em cima de uma suspeita que ele proprio ja tinha.
    ///
    /// O capo primeiro: sem ele aberto nao ha o que olhar. E o gesto que separa
    /// "o carro parou" de "o que e que se passa aqui".
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class EngineInspection : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private string closedPrompt = "Open the hood";
        [SerializeField] private PlayerThoughtDirector thoughts;

        [Tooltip("O que ele pensa ao abrir. Antes de ver o que quer que seja.")]
        [SerializeField, TextArea] private string openThought =
            "Still warm. Whatever it is, it is not the heat.";

        [Tooltip("As pecas: so aparecem com o capo aberto.")]
        [SerializeField] private GameObject parts;

        [Tooltip("A que esta errada. Quando ele a le, o capitulo sabe.")]
        [SerializeField] private FlavourInteractable culprit;

        [Tooltip("O que ele pensa depois de perceber. Vem uma vez so.")]
        [SerializeField, TextArea] private string culpritThought =
            "That did not shake loose. Nothing shakes loose that cleanly.";

        [SerializeField] private AudioSource hoodAudio;
        [SerializeField] private AudioClip hoodClip;

        private bool open;
        private bool realised;

        /// <summary>O capo esta aberto.</summary>
        public bool IsOpen => open;

        /// <summary>Ele ja viu o cabo. E o que autoriza o estranho a aproximar-se.</summary>
        public bool CulpritFound => realised;

        [Tooltip("Para nao se oferecer o capo a quem vai a conduzir.")]
        [SerializeField] private CarSeat seat;

        // O capo esta na frente do carro e o jogador vai la dentro: a olhar em
        // frente, o raio de interaccao atravessava o para-brisas e apanhava-o. Dava
        // "Open the hood" a oitenta a hora.
        public string Prompt =>
            (open || (seat != null && seat.Seated)) ? string.Empty : closedPrompt;
        public bool HoldToInteract => false;

        private void Awake()
        {
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            if (seat == null) seat = FindObjectOfType<CarSeat>();
            if (parts != null) parts.SetActive(false);
        }

        private void Update()
        {
            if (!open || realised || culprit == null || !culprit.HasBeenRead) return;

            realised = true;
            if (!string.IsNullOrWhiteSpace(culpritThought))
                thoughts?.Think("engine_culprit", culpritThought, 5, true, 4f);
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (open) return;
            open = true;

            if (parts != null) parts.SetActive(true);
            if (hoodClip != null && hoodAudio != null) hoodAudio.PlayOneShot(hoodClip);

            if (!string.IsNullOrWhiteSpace(openThought))
                thoughts?.Think("engine_open", openThought, 4, true, 3.4f);
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }
    }
}
