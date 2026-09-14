using Pungent.Dialogue;
using Pungent.NPC;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Da ao Rui a conversa que este momento da historia pede, e — quando e o
    /// momento a falar e nao o jogador — comeca-a sozinha.
    ///
    /// ---
    ///
    /// **Nao ha componente novo de dialogo aqui, e isso e o ponto.** O
    /// <see cref="RuiStoryConversation"/> ja sabe conduzir uma conversa com
    /// escolhas, tons, olhares e ameaca oculta; o que lhe faltava era alguem que
    /// lhe trocasse o guiao fora do prologo. O `SetConversation` ja existia e ja
    /// era usado pelo `PrologueStage` para trocar entre a noite da mudanca e a das
    /// 02:47 — isto e a terceira e a quarta vez que se faz o mesmo.
    ///
    /// ---
    ///
    /// **Os dois modos, e porque sao dois.**
    ///
    /// - <see cref="Start.WhenSpokenTo"/> — instala o guiao e sai da frente. O
    ///   prompt "Talk to Rui" volta a acender e e o jogador que decide falar. E o
    ///   Dia 3: o Rui esta a porta, o Tomas esta de saida, e ignora-lo tambem e
    ///   uma resposta.
    ///
    /// - <see cref="Start.WhenClose"/> — instala e comeca sozinha. E o climax:
    ///   o Rui esta do lado de fora de uma porta fechada e o jogador **nao lhe
    ///   consegue tocar**. Esperar por uma interaccao que a geometria impede era
    ///   escrever uma conversa que nunca acontecia. Aqui quem fala e a situacao.
    ///
    /// ---
    ///
    /// **O `showAfterThought` vai a falso nas duas.** O pensamento de fecho do
    /// `RuiStoryConversation` — *"I never told him about Tuesday."* — pertence a
    /// noite das 02:47 e so a ela. Deixa-lo ligado punha o Tomas a estranhar a
    /// aula de terca no meio do climax, tres dias depois de ja ter estranhado.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScriptedConversationCue : MonoBehaviour
    {
        public enum Start
        {
            /// <summary>Instala o guiao. Quem comeca e o jogador.</summary>
            WhenSpokenTo,
            /// <summary>Instala e comeca sozinha quando o jogador se aproxima.</summary>
            WhenClose
        }

        [Header("O guiao")]
        [SerializeField] private DialogueSequenceDefinition conversation;
        [SerializeField] private RuiStoryConversation target;

        [Header("Quando")]
        [Tooltip("O acontecimento que instala o guiao. Vazio = desde o inicio.")]
        [SerializeField] private string requiresEvent;

        [Tooltip("Deixa de valer depois deste. Uma conversa do Dia 3 que sobreviva "
               + "ao Dia 5 poe o Rui a perguntar pela viagem depois de ela ter "
               + "acontecido.")]
        [SerializeField] private string silencedByEvent;

        [SerializeField] private Start startMode = Start.WhenSpokenTo;

        [Header("So para WhenClose")]
        [Tooltip("O sitio a partir do qual se mede — tipicamente a porta. Vazio = "
               + "este objecto.")]
        [SerializeField] private Transform measureFrom;

        [SerializeField, Min(0.5f)] private float radius = 3.2f;

        [Tooltip("Silencio antes de ele comecar a falar. Do outro lado de uma porta "
               + "fechada, um homem que fala no instante em que o jogador chega ao "
               + "quarto le-se como um gatilho; um que espera um pouco le-se como "
               + "alguem que estava ali a ouvir.")]
        [SerializeField, Min(0f)] private float delaySeconds = 2.5f;

        [SerializeField] private ChapterDirector director;

        private bool installed;
        private bool started;
        private Transform player;
        private float inRangeSince = -1f;

        /// <summary>Ja instalou o guiao. Util para inspeccionar e para os testes.</summary>
        public bool Installed => installed;

        private void Update()
        {
            if (started) return;

            if (!installed)
            {
                if (!Allowed()) return;
                Install();
                if (startMode == Start.WhenSpokenTo) { started = true; return; }
            }

            if (!ResolvePlayer()) return;

            Transform anchor = measureFrom != null ? measureFrom : transform;
            if (Vector3.Distance(player.position, anchor.position) > radius)
            {
                inRangeSince = -1f;
                return;
            }

            if (inRangeSince < 0f) inRangeSince = Time.time;
            if (Time.time - inRangeSince < delaySeconds) return;

            Begin();
        }

        private void Install()
        {
            if (target == null || conversation == null) return;

            // `ignoreQuestGate` porque a quest da noite das 02:47 ja acabou nestes
            // dois momentos, e o portao dela mediria um passo que nao existe.
            target.SetConversation(conversation, ignoreQuestGate: true, showAfterThought: false);
            installed = true;
        }

        /// <summary>
        /// Comeca a conversa sem passar pelo interactor.
        ///
        /// O `BeginInteraction` nao usa o argumento — passar nulo e legitimo e nao
        /// um atalho. O que ele faz e verificar se ja esta a correr, travar a
        /// rotina e tocar o primeiro beat, e e exactamente isso que aqui se quer.
        /// </summary>
        private void Begin()
        {
            if (target == null) return;
            started = true;
            target.BeginInteraction(null);
        }

        private bool ResolvePlayer()
        {
            if (player != null) return true;
            var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
            if (motor == null) return false;
            player = motor.transform;
            return true;
        }

        private bool Allowed()
        {
            bool gated = !string.IsNullOrWhiteSpace(requiresEvent)
                      || !string.IsNullOrWhiteSpace(silencedByEvent);
            if (!gated) return true;

            director = ChapterDirector.Resolve(director);
            if (director == null) return false;

            if (!string.IsNullOrWhiteSpace(requiresEvent) && !director.HasSeen(requiresEvent))
                return false;
            if (!string.IsNullOrWhiteSpace(silencedByEvent) && director.HasSeen(silencedByEvent))
                return false;

            return true;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(DialogueSequenceDefinition script, RuiStoryConversation who,
            string requires, string silencedBy, Start mode, Transform measure = null,
            float range = 3.2f, float delay = 2.5f)
        {
            conversation = script;
            target = who;
            requiresEvent = requires;
            silencedByEvent = silencedBy;
            startMode = mode;
            measureFrom = measure;
            radius = range;
            delaySeconds = delay;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            if (startMode != Start.WhenClose) return;
            Transform anchor = measureFrom != null ? measureFrom : transform;
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.25f);
            Gizmos.DrawWireSphere(anchor.position, radius);
        }
    }
}
