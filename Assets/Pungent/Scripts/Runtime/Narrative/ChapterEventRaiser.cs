using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Levanta um evento de capitulo a partir de uma coisa banal na cena: entrar
    /// num sitio, olhar para um objecto e carregar, ou passar um tempo parado.
    ///
    /// O <see cref="ChapterDefinition"/> tirou os capitulos do codigo, mas quem os
    /// faz andar continuou preso a ele: o prologo tem o seu `PrologueTracker`, uma
    /// classe escrita a mao a ouvir caixas, portas e conversas. Por esse caminho os
    /// capitulos 4 a 7 precisavam de mais quatro classes dessas, e e exactamente o
    /// que se queria evitar.
    ///
    /// Este componente e a outra metade do par. Um passo espera por
    /// `arrived_garage`; alguem tem de o levantar. Com isto esse alguem passa a ser
    /// um volume no chao da oficina, montado pela ferramenta de ligacao do
    /// capitulo, e nao mais uma classe.
    ///
    /// Nao substitui o `PrologueTracker`: aquilo que ele ouve — uma conversa
    /// terminar, um dia dormido — sao eventos de sistemas proprios e continuam a
    /// pertencer-lhe. Isto serve o caso comum, que e a maioria.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChapterEventRaiser : MonoBehaviour, IPlayerInteractable
    {
        public enum Trigger
        {
            /// <summary>Quando o jogador entra no volume.</summary>
            EnterArea,
            /// <summary>Quando o jogador olha e carrega.</summary>
            Interact,
            /// <summary>Ao fim de <see cref="delaySeconds"/> depois de o componente acordar.</summary>
            AfterDelay,
            /// <summary>So por codigo, com <see cref="Raise"/>.</summary>
            Manual
        }

        [Tooltip("Identificador que o passo do capitulo espera. Tem de ser igual ao "
               + "EventId no asset — um erro de escrita aqui bloqueia o capitulo "
               + "todo e nao da erro nenhum.")]
        [SerializeField] private string eventId;

        [SerializeField] private Trigger raiseOn = Trigger.EnterArea;

        [Tooltip("Prompt para Interact. Vazio = sem prompt, o que so faz sentido "
               + "para volumes.")]
        [SerializeField] private string prompt = "Look";

        [Tooltip("Para AfterDelay.")]
        [SerializeField, Min(0f)] private float delaySeconds = 3f;

        [Tooltip("Para EnterArea: tempo dentro do volume antes de contar. Evita que "
               + "atravessar uma zona de passagem levante o evento.")]
        [SerializeField, Min(0f)] private float dwellSeconds = 0.4f;

        [Tooltip("Levanta o evento quando este `FlavourInteractable` tiver sido lido "
               + "ate ao fim, em vez de ter prompt proprio.\n\n"
               + "Serve para objectos que ja tem quem fale por eles: a lanterna da "
               + "oficina tem quatro linhas de pensamento, e por-lhe um segundo "
               + "interagivel por cima punha dois prompts a disputar o mesmo "
               + "objecto — ganhava o que estivesse primeiro na lista de "
               + "componentes, que nao e coisa que se deva deixar ao acaso.")]
        [SerializeField] private Pungent.Interaction.FlavourInteractable afterReading;

        [Header("Condicoes")]
        [Tooltip("Se preenchido, so levanta depois de este outro evento ja ter sido "
               + "levantado. Serve para ordenar sem escrever codigo.")]
        [SerializeField] private string requiresEvent;

        [Tooltip("Pensamento ao levantar. Vazio = silencio.")]
        [SerializeField, TextArea] private string thought;

        [SerializeField] private ChapterDirector director;
        [SerializeField] private PlayerThoughtDirector thoughts;

        private Transform player;
        private BoxCollider volume;
        private float insideSince = -1f;
        private float awakeAt;
        private bool sent;

        // Sem prompt proprio quando quem fala e outro componente: dois prompts no
        // mesmo objecto disputam-no, e ganha o primeiro da lista de componentes.
        public string Prompt => (raiseOn == Trigger.Interact && !sent && afterReading == null && Allowed())
            ? prompt : string.Empty;
        public bool HoldToInteract => false;

        /// <summary>Verdadeiro depois de este ter disparado. Util para inspeccionar.</summary>
        public bool HasFired => sent;

        private void Awake()
        {
            if (director == null) director = FindObjectOfType<ChapterDirector>();

            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
            {
                player = tagged.transform;
                if (thoughts == null) thoughts = tagged.GetComponent<PlayerThoughtDirector>();
            }

            volume = GetComponent<BoxCollider>();
            if (volume != null && raiseOn == Trigger.EnterArea) volume.isTrigger = true;
            awakeAt = Time.time;
        }

        private void Update()
        {
            if (sent) return;

            if (afterReading != null)
            {
                if (afterReading.HasBeenRead) Raise();
                return;
            }

            switch (raiseOn)
            {
                case Trigger.AfterDelay:
                    if (Time.time - awakeAt >= delaySeconds) Raise();
                    break;

                case Trigger.EnterArea:
                    UpdateArea();
                    break;
            }
        }

        private void UpdateArea()
        {
            if (player == null || volume == null) return;

            // Sondagem em vez de OnTriggerEnter: o CharacterController nao gera
            // saidas fiaveis e o jogador e reposicionado por cortes de camara.
            bool inside = volume.bounds.Contains(player.position + Vector3.up * 0.9f);
            if (!inside) { insideSince = -1f; return; }

            if (insideSince < 0f) insideSince = Time.time;
            if (Time.time - insideSince >= dwellSeconds) Raise();
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (raiseOn == Trigger.Interact) Raise();
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

        /// <summary>Levanta o evento, uma vez so.</summary>
        public void Raise()
        {
            if (sent || !Allowed()) return;
            if (string.IsNullOrWhiteSpace(eventId))
            {
                Debug.LogWarning($"[Chapter] '{name}' sem eventId: nao levanta nada.");
                return;
            }

            sent = true;
            if (!string.IsNullOrWhiteSpace(thought))
                thoughts?.Think($"evt_{eventId}", thought, 3, true, 3.2f);

            director = ChapterDirector.Resolve(director);
            director?.Notify(eventId);
        }

        private bool Allowed()
        {
            if (string.IsNullOrWhiteSpace(requiresEvent)) return true;
            director = ChapterDirector.Resolve(director);
            return director != null && director.HasSeen(requiresEvent);
        }

#if UNITY_EDITOR
        /// <summary>Usado pelas ferramentas de ligacao, que vivem noutra assembly.</summary>
        public void EditorConfigure(string id, Trigger trigger, string promptText,
            string thoughtText, string requires,
            Pungent.Interaction.FlavourInteractable readThis = null)
        {
            eventId = id;
            raiseOn = trigger;
            prompt = promptText;
            thought = thoughtText;
            requiresEvent = requires;
            afterReading = readThis;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            var box = volume != null ? volume : GetComponent<BoxCollider>();
            if (box == null || raiseOn != Trigger.EnterArea) return;

            Gizmos.color = new Color(0.95f, 0.75f, 0.25f, 0.20f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.95f, 0.75f, 0.25f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
