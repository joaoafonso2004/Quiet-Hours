using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Um sitio da casa que diz alguma coisa quando se entra nele.
    ///
    /// Nao ha interaccao nem objectivo: o jogador chega a varanda e o Tomas pensa
    /// que aquela e a parte preferida dele da casa. E o mais barato que ha para
    /// fazer um espaco parecer habitado por alguem em concreto, em vez de ser um
    /// poligono com nome.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class HomeTaskZone : MonoBehaviour
    {
        [SerializeField] private string id = "balcony";
        [SerializeField, TextArea] private string thought = "My favourite part of the house.";
        [SerializeField, Min(0.8f)] private float holdSeconds = 3.0f;
        [Tooltip("Se falso, repete-se sempre que o jogador volta a entrar (com espera).")]
        [SerializeField] private bool onlyOnce = true;
        [Tooltip("Espera minima antes de repetir, quando nao e so uma vez.")]
        [SerializeField, Min(0f)] private float repeatCooldown = 90f;
        [Tooltip("Segundos dentro da zona antes de o pensamento sair. Evita que "
               + "atravessar a zona de passagem dispare a fala.")]
        [SerializeField, Min(0f)] private float dwellSeconds = 0.8f;

        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private HomeTaskDirector tasks;

        private BoxCollider volume;
        private Transform player;
        private float insideSince = -1f;
        private float nextAllowedTime;
        private bool fired;

        private void Awake()
        {
            volume = GetComponent<BoxCollider>();
            volume.isTrigger = true;

            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
            {
                player = tagged.transform;
                if (thoughts == null) thoughts = tagged.GetComponent<PlayerThoughtDirector>();
            }

            if (tasks == null) tasks = FindObjectOfType<HomeTaskDirector>();
        }

        private void Update()
        {
            if (player == null || volume == null) return;
            if (onlyOnce && fired) return;

            // Sondagem em vez de OnTriggerEnter: o CharacterController nem sempre
            // gera saidas fiaveis, e o jogador pode ser reposicionado por eventos.
            bool inside = volume.bounds.Contains(player.position + Vector3.up * 0.9f);
            if (!inside) { insideSince = -1f; return; }

            if (insideSince < 0f) insideSince = Time.time;
            if (Time.time - insideSince < dwellSeconds) return;
            if (Time.unscaledTime < nextAllowedTime) return;

            fired = true;
            nextAllowedTime = Time.unscaledTime + repeatCooldown;
            tasks?.Discover(id);
            if (!string.IsNullOrWhiteSpace(thought))
                thoughts?.Think($"zone_{id}", thought, 2, onlyOnce, holdSeconds);
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider box = volume != null ? volume : GetComponent<BoxCollider>();
            if (box == null) return;
            Gizmos.color = new Color(0.35f, 0.75f, 0.95f, 0.20f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.35f, 0.75f, 0.95f, 0.85f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
