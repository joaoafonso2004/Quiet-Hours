using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>
    /// O canto do quarto onde as caixas ficam.
    ///
    /// Larga de proposito, e sem marca nenhuma no chao. Um rectangulo desenhado a
    /// dizer "pousa aqui" transformava a mudanca numa tarefa de jogo; assim, o
    /// jogador pousa a caixa no quarto e o quarto aceita-a. Falhar nao e possivel —
    /// so se falha se se pousar a caixa noutra divisao, e isso e uma decisao e nao
    /// um erro.
    ///
    /// Conta quantas ja chegaram e levanta um acontecimento quando estiverem todas.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BoxDropZone : MonoBehaviour
    {
        [Tooltip("Levantado quando a ultima caixa e pousada aqui.")]
        [SerializeField] private string allDeliveredEvent = "boxes_carried";

        [Tooltip("Quantas se espera. Zero = conta as que existirem na cena ao arrancar.")]
        [SerializeField] private int expected;

        [SerializeField, TextArea] private string lastBoxThought =
            "That is everything. It fits in a corner.";

        [SerializeField] private ChapterDirector director;
        [SerializeField] private PlayerThoughtDirector thoughts;

        private BoxCollider volume;
        private int delivered;
        private bool announced;

        public int Delivered => delivered;
        public int Expected => expected;

        public void ResetProgress()
        {
            delivered = 0;
            announced = false;
        }

        private void Awake()
        {
            volume = GetComponent<BoxCollider>();
            volume.isTrigger = true;

            if (director == null) director = FindObjectOfType<ChapterDirector>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();

            if (expected <= 0)
            {
                // Contadas na cena e nao escritas a mao: acrescentar uma quarta caixa
                // passa a ser arrastar um prefab, sem ninguem se lembrar de vir aqui
                // mudar um numero — que e como um passo fica preso para sempre.
                foreach (var b in FindObjectsOfType<CarryableBox>(true)) expected++;
            }
        }

        /// <summary>Este ponto esta dentro da zona?</summary>
        public bool Contains(Vector3 worldPosition)
        {
            if (volume == null) volume = GetComponent<BoxCollider>();
            return volume.bounds.Contains(worldPosition);
        }

        /// <summary>Chamado por uma caixa que acabou de ser pousada aqui dentro.</summary>
        public void Accept(CarryableBox box)
        {
            delivered++;
            TryAnnounce();
        }

        private void Update()
        {
            // A ultima caixa pode ser pousada no mesmo frame em que um GAME_SYSTEMS
            // duplicado e substituido. Nesse instante a referencia serializada do
            // director e um "null falso" e a notificacao nao tem destino. Tenta de
            // novo ate existir um director vivo; as caixas nao ficam entregues com
            // o objectivo antigo preso no HUD.
            TryAnnounce();
        }

        private void TryAnnounce()
        {
            if (announced || delivered < expected) return;
            director = ChapterDirector.Resolve(director);
            if (director == null) return;

            announced = true;
            if (!string.IsNullOrWhiteSpace(lastBoxThought))
                thoughts?.Think("boxes_done", lastBoxThought, 3, true, 3.4f);
            if (!string.IsNullOrWhiteSpace(allDeliveredEvent))
                director.Notify(allDeliveredEvent);
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(string raisedEvent, int expectedBoxes)
        {
            allDeliveredEvent = raisedEvent;
            expected = expectedBoxes;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            var b = GetComponent<BoxCollider>();
            if (b == null) return;

            Gizmos.color = new Color(0.45f, 0.85f, 0.55f, 0.18f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(b.center, b.size);
            Gizmos.color = new Color(0.45f, 0.85f, 0.55f, 0.8f);
            Gizmos.DrawWireCube(b.center, b.size);
        }
    }
}
