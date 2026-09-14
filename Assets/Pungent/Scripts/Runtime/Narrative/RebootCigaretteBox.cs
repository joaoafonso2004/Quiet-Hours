using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// A caixa de cigarros da varanda. Tirar um é opcional.
    ///
    /// **Opcional a sério, e é por isso que existe.** A história diz que olhar da
    /// varanda é obrigatório e fumar não; o Tomás fuma se o jogador quiser que ele
    /// fume. Um objectivo que exige o cigarro transformava um gesto de carácter
    /// numa tarefa, e a pausa deixava de ser uma pausa.
    ///
    /// Não move a caixa nem a consome: esconde **um** cigarro do maço e acende o
    /// que está na mão. O maço continua na mesa com menos um, que é o que se vê
    /// quando alguém fuma.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class RebootCigaretteBox : MonoBehaviour, IPlayerInteractable
    {
        [Tooltip("O cigarro na mão — o `SMOKE_RIG` da câmara. Desligado até alguém "
               + "tirar um.")]
        [SerializeField] private GameObject heldRig;

        [Tooltip("Os cigarros do maço. Ao tirar um, desliga-se o primeiro que ainda "
               + "esteja ligado. Vazio = o maço não muda.")]
        [SerializeField] private GameObject[] packCigarettes = new GameObject[0];

        [SerializeField] private string prompt = "Take a cigarette";

        [Header("Som")]
        [SerializeField] private AudioSource sound;
        [SerializeField] private AudioClip takeClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

        private bool taken;

        // Ao contrario do queijo, isto nao precisa de ser armado por ninguem: a
        // caixa esta na varanda e a varanda so se visita no Dia 1. Se um dia a
        // varanda abrir mais cedo, arma-se aqui.
        public string Prompt => taken ? string.Empty : prompt;
        public bool HoldToInteract => false;

        private void Awake()
        {
            if (sound == null) sound = GetComponent<AudioSource>();
            if (heldRig != null) heldRig.SetActive(false);
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (taken) return;
            taken = true;

            for (int i = 0; i < packCigarettes.Length; i++)
            {
                if (packCigarettes[i] == null || !packCigarettes[i].activeSelf) continue;
                packCigarettes[i].SetActive(false);
                break;
            }

            if (heldRig != null) heldRig.SetActive(true);
            if (sound != null && takeClip != null) sound.PlayOneShot(takeClip, volume);
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }
    }
}
