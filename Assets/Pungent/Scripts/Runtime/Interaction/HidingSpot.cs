using Pungent.Player;
using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>
    /// Um sitio onde caber: debaixo da cama, dentro do roupeiro, atras das caixas
    /// dos arrumos, por tras do sofa.
    ///
    /// A seccao 5 pede que o jogador use "portas, luzes, esconderijos e conhecimento
    /// do apartamento". As portas e as luzes ja existiam; isto e a terceira peca.
    ///
    /// **Esconder-se nao e seguro.** Um esconderijo que garanta a sobrevivencia
    /// transforma a caca num jogo de encontrar o botao certo e carregar nele — e a
    /// seguir a isso a casa deixa de ter tensao nenhuma. Aqui esconder-se torna o
    /// jogador invisivel a linha de vista, mas se o Rui **procurar este sitio em
    /// concreto**, encontra-o. O calculo passa a ser quando sair, e nao se entrar.
    ///
    /// A camara nao muda de dono nem entra em animacao: baixa-se o pivo e
    /// suspende-se o corpo, exactamente como o <see cref="Pungent.Narrative.PlayerSleep"/>
    /// faz para deitar. Nao ha um unico clip de animacao neste projecto e nao e
    /// aqui que se comeca a precisar de um.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HidingSpot : MonoBehaviour, IPlayerInteractable
    {
        /// <summary>
        /// O esconderijo onde o jogador esta, ou nulo.
        ///
        /// Estatico de proposito: quem precisa de saber isto e a percepcao de um
        /// NPC, chamada muitas vezes por segundo, e faze-la varrer a cena a procura
        /// de esconderijos era pagar uma busca para responder a uma pergunta que
        /// tem sempre uma resposta so.
        /// </summary>
        public static HidingSpot Occupied { get; private set; }

        public static bool PlayerIsHidden => Occupied != null;

        [SerializeField] private string enterPrompt = "Hide";

        [Tooltip("Onde a cabeca fica. Em metros do mundo — o ponto e do sitio, nao "
               + "do jogador: debaixo de uma cama olha-se de 40 cm do chao e atras "
               + "de um sofa de 90.")]
        [SerializeField] private Vector3 viewPoint;

        [Tooltip("Para onde ele fica virado ao entrar. Um esconderijo em que se "
               + "acorda virado para a parede e um esconderijo cego.")]
        [SerializeField] private float viewYaw;

        [Tooltip("Tempo minimo la dentro antes de o botao voltar a responder. Sem "
               + "isto o mesmo clique que o mete la dentro tira-o outra vez.")]
        [SerializeField, Min(0.1f)] private float settleSeconds = 0.45f;

        [SerializeField, Min(0.05f)] private float moveSeconds = 0.5f;

        [Header("Ligacoes")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PrototypeHUD hud;

        [Tooltip("Suspensos enquanto la esta. O `PlayerInteractor` **nao** entra "
               + "aqui: e ele que ha-de aceitar o clique para sair.")]
        [SerializeField] private MonoBehaviour[] suppressed = new MonoBehaviour[0];

        private bool inside;
        private float enteredAt;
        private float blend;
        private Vector3 outsidePosition;
        private Quaternion outsideRotation;
        private Vector3 standingPivot;

        /// <summary>Onde o Rui tem de chegar para dar com quem esta aqui.</summary>
        public Vector3 SearchPoint => transform.position;

        public string Prompt => inside
            ? (Time.time - enteredAt >= settleSeconds ? "Come out" : string.Empty)
            : enterPrompt;

        public bool HoldToInteract => false;

        private void Awake()
        {
            if (player == null)
            {
                var motor = FindObjectOfType<PlayerMotor>();
                if (motor != null) player = motor.transform;
            }
            if (player != null)
            {
                if (input == null) input = player.GetComponent<PlayerInputReader>();
                if (hud == null) hud = player.GetComponent<PrototypeHUD>();
                if (cameraPivot == null) cameraPivot = player.Find("ViewYaw");
            }
        }

        private void OnDisable()
        {
            // A cena pode desaparecer com o jogador la dentro — trocar de cena a
            // meio da caca, por exemplo. Deixar o estatico apontado para um objecto
            // destruido dava um jogador invisivel para sempre no capitulo seguinte.
            if (Occupied == this) Occupied = null;
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (!inside) Enter();
            else if (Time.time - enteredAt >= settleSeconds) Leave();
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

        private void Enter()
        {
            if (player == null || Occupied != null) return;

            inside = true;
            enteredAt = Time.time;
            blend = 0f;
            Occupied = this;

            outsidePosition = player.position;
            outsideRotation = player.rotation;
            if (cameraPivot != null) standingPivot = cameraPivot.localPosition;

            SetSuppressed(true);
            hud?.SetPrompt(string.Empty);
        }

        /// <summary>
        /// Sair. Publico porque quem obriga a sair nem sempre e o jogador: o Rui,
        /// ao dar com o esconderijo, tira-o de la.
        /// </summary>
        public void Leave()
        {
            if (!inside) return;

            inside = false;
            blend = 0f;
            if (Occupied == this) Occupied = null;

            if (player != null)
            {
                var body = player.GetComponent<CharacterController>();
                if (body != null) body.enabled = false;
                player.position = outsidePosition;
                player.rotation = outsideRotation;
                if (body != null) body.enabled = true;
            }

            if (cameraPivot != null) cameraPivot.localPosition = standingPivot;
            SetSuppressed(false);
        }

        private void Update()
        {
            if (!inside || player == null) return;

            // Reafirmado a cada frame: outros directores voltam a ligar estes
            // componentes a meio dos seus proprios Updates. E a mesma nota que o
            // `PlayerSleep` ja tinha aprendido a ter.
            SetSuppressed(true);

            blend = Mathf.Min(1f, blend + Time.deltaTime / moveSeconds);
            float eased = blend * blend * (3f - 2f * blend);

            var body = player.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            player.position = Vector3.Lerp(outsidePosition,
                new Vector3(viewPoint.x, outsidePosition.y, viewPoint.z), eased);
            player.rotation = Quaternion.Slerp(outsideRotation,
                Quaternion.Euler(0f, viewYaw, 0f), eased);
            if (body != null) body.enabled = true;

            if (cameraPivot != null)
            {
                Vector3 crouched = standingPivot;
                crouched.y = viewPoint.y;
                cameraPivot.localPosition = Vector3.Lerp(standingPivot, crouched, eased);
            }
        }

        private void SetSuppressed(bool value)
        {
            foreach (var component in suppressed)
                if (component != null) component.enabled = !value;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(string promptText, Vector3 head, float yaw,
            MonoBehaviour[] toSuppress)
        {
            enterPrompt = promptText;
            viewPoint = head;
            viewYaw = yaw;
            suppressed = toSuppress;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.75f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);
            Gizmos.DrawLine(transform.position, viewPoint);
            Gizmos.DrawWireCube(viewPoint, Vector3.one * 0.2f);
        }
    }
}
