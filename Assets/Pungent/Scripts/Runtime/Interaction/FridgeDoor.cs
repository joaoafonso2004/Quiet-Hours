using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>
    /// A porta do frigorifico, com a luz de dentro.
    ///
    /// Ao contrario das portas do apartamento, esta nao e fisica: nao ha nada para
    /// empurrar nem massa para simular, e uma porta de frigorifico nao oscila.
    /// Roda por interpolacao e para onde e mandada.
    ///
    /// A ferramenta de wiring cria a dobradica no lado livre do vao. O pivo que vem
    /// no modelo fica encostado a parede e nao permite abrir a folha.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FridgeDoor : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private Transform door;
        [Tooltip("Negativo abre para a cozinha. Escrito pelo `Wire Fridge`, que mede "
               + "ate onde a folha vai sem entrar na bancada ou na parede.")]
        [SerializeField, Range(-140f, 140f)] private float openAngle = -105f;
        [Tooltip("Graus por segundo. Abrir e rapido; fechar leva o mesmo.")]
        [SerializeField, Min(20f)] private float speed = 190f;

        [Header("Luz de dentro")]
        [Tooltip("Acende com a porta aberta. A unica luz da cozinha as 02:47.")]
        [SerializeField] private Light interiorLight;
        [Tooltip("0.9 com a lampada a meia altura queimava as prateleiras a branco. "
               + "A lampada subiu para o tejadilho e isto desceu na mesma proporcao.")]
        [SerializeField, Min(0f)] private float lightIntensity = 0.18f;

        [Header("Jogador")]
        [Tooltip("Pensamento dito da primeira vez que o jogador a abre. Vazio = nenhum.")]
        [SerializeField, TextArea] private string firstLookThought =
            "Half a burger and someone else's beer. Shopping, then.";
        [SerializeField] private Pungent.Dialogue.WorldDialogueController dialogue;

        private bool open;
        private float current;
        private bool playerHasLooked;

        public bool IsOpen => open;

        // --- interacao do jogador ---

        public string Prompt => open ? "Close the fridge" : "Open the fridge";
        public bool HoldToInteract => false;

        public void BeginInteraction(PlayerInteractor interactor)
        {
            open = !open;

            if (!open || playerHasLooked || string.IsNullOrWhiteSpace(firstLookThought)) return;
            playerHasLooked = true;
            if (dialogue == null) dialogue = FindObjectOfType<Pungent.Dialogue.WorldDialogueController>();
            dialogue?.ShowThought(firstLookThought, 3.0f);
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

        private void Awake()
        {
            if (door == null) door = transform.Find("Door");
            Apply();
        }

        public void SetOpen(bool value) => open = value;
        public void Open() => open = true;
        public void Close() => open = false;

        private void Update()
        {
            float target = open ? openAngle : 0f;
            if (Mathf.Approximately(current, target)) return;

            current = Mathf.MoveTowards(current, target, speed * Time.deltaTime);
            Apply();
        }

        private void Apply()
        {
            if (door != null) door.localRotation = Quaternion.Euler(0f, current, 0f);

            if (interiorLight == null) return;
            // A luz acompanha a abertura em vez de ligar de golpe: a frincha larga
            // antes de a porta chegar ao fim.
            float t = Mathf.Approximately(openAngle, 0f) ? 0f : Mathf.Clamp01(current / openAngle);
            interiorLight.enabled = t > 0.02f;
            interiorLight.intensity = lightIntensity * t;
        }
    }
}
