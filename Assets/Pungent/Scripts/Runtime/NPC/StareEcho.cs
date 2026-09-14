using Pungent.Dialogue;
using Pungent.Narrative;
using UnityEngine;

namespace Pungent.NPC
{
    /// <summary>
    /// "You look at me a lot." — o jogo a devolver ao jogador um comportamento dele
    /// que o jogador nao sabia estar a ter.
    ///
    /// ---
    ///
    /// **Nao ha nada de novo a acontecer aqui.** O `StarePressure` conta olhares
    /// sustentados desde o prologo e nunca foi lido por ninguem. Isto le-o. O
    /// jogador nao foi avisado de que aquilo contava, nao havia indicacao de escolha
    /// moral em lado nenhum, e e precisamente por isso que a fala funciona: ele
    /// reconhece-se na frase antes de perceber como e que o jogo soube.
    ///
    /// ---
    ///
    /// **Uma fala, uma vez, sem seguimento.** Nao abre conversa, nao pede resposta,
    /// nao muda objectivo nenhum. Ele diz aquilo e continua o que estava a fazer. A
    /// regra da `HouseChange` aplicada a uma pessoa: pequeno, uma vez so, e sempre
    /// com uma explicacao inocente possivel — um homem pode dizer isto a rir.
    ///
    /// **E nao acusa.** "You look at me a lot" nao e uma queixa nem uma ameaca; e
    /// uma observacao. O desconforto vem de ser verdade e de ninguem ter dito ao
    /// jogador que estava a ser contado.
    ///
    /// ---
    ///
    /// **So no Dia 3, e so com ele a vista.** Dita de costas ou atraves de uma
    /// parede lia-se como o jogo a falar, e nao ele. A cena espera por os dois
    /// estarem na mesma divisao, com o jogador a olhar mesmo para ele — que e, com
    /// alguma graca, a condicao de que a fala trata.
    ///
    /// **Nao bloqueia as maos.** O `ShowReaction` bloqueia por omissao, e este
    /// projecto ja pagou isso uma vez: com o bloqueio ligado, uma fala solta
    /// prendia o jogador de pe em frente ao Rui ate a dispensar. Aqui ele esta a
    /// atravessar a casa a fazer outra coisa, e a frase e para o apanhar em
    /// movimento.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StareEcho : MonoBehaviour
    {
        [Header("Quando")]
        [Tooltip("Quantos olhares sustentados fazem dele um padrao digno de "
               + "comentario. Mais alto do que o limiar da memoria do "
               + "`PrototypeNpcRoutine`: ele repara mais depressa muito antes de "
               + "dizer alguma coisa em voz alta.")]
        [SerializeField, Min(1)] private int staresBeforeSpeaking = 5;

        [Tooltip("Janela do Dia 3. Antes disso ele ainda e o senhorio simpatico e a "
               + "frase chega cedo de mais para ter peso.")]
        [SerializeField] private string requiresEvent = "day_two";
        [SerializeField] private string silencedByEvent = "day3_done";

        [Header("Onde")]
        [SerializeField, Min(1f)] private float maximumDistance = 6f;

        [Tooltip("Tempo seguido a olhar para ele antes de a frase sair. Curto de "
               + "mais e ela dispara enquanto o jogador vira a cabeca.")]
        [SerializeField, Min(0.2f)] private float dwellSeconds = 1.2f;

        [SerializeField] private LayerMask sightBlockers = ~0;
        [SerializeField, Min(0.1f)] private float headHeight = 1.6f;

        [Header("O que ele diz")]
        [SerializeField] private string speaker = "Rui";

        [SerializeField, TextArea]
        private string line = "You look at me a lot.";

        [SerializeField, Min(0.8f)] private float lineHold = 2.8f;

        [Tooltip("Levantado depois de ele falar. Vazio = nenhum.")]
        [SerializeField] private string raisesEvent;

        [Header("Ligacoes")]
        [SerializeField] private WorldDialogueController dialogue;
        [SerializeField] private NpcMumbleVoice voice;
        [SerializeField] private PrototypeNpcRoutine routine;
        [SerializeField] private ChapterDirector director;

        private bool said;
        private Transform player;
        private Camera eye;
        private float heldSince = -1f;

        /// <summary>Ja disse. Util para inspeccionar e para os testes.</summary>
        public bool HasSpoken => said;

        private void Awake()
        {
            if (routine == null) routine = GetComponent<PrototypeNpcRoutine>();
            if (voice == null) voice = GetComponentInChildren<NpcMumbleVoice>(true);
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
        }

        private void Update()
        {
            if (said) return;
            if (!Allowed()) { heldSince = -1f; return; }
            if (!Enough()) { heldSince = -1f; return; }
            if (!ResolvePlayer()) return;

            // Ele nao interrompe uma conversa para dizer isto, nem fala por cima de
            // outra coisa que ja esteja no ecra.
            if (dialogue == null || dialogue.IsBusy) { heldSince = -1f; return; }
            if (routine != null && routine.InConversation) { heldSince = -1f; return; }

            if (!Seen()) { heldSince = -1f; return; }

            if (heldSince < 0f) heldSince = Time.time;
            if (Time.time - heldSince < dwellSeconds) return;

            Speak();
        }

        private bool Enough()
        {
            var blackboard = NarrativeBlackboard.Instance;
            if (blackboard == null) return false;
            return blackboard.Get(NarrativeBlackboard.Variable.StarePressure) >= staresBeforeSpeaking;
        }

        /// <summary>Na mesma divisao, e o jogador a olhar mesmo para ele.</summary>
        private bool Seen()
        {
            if (Vector3.Distance(player.position, transform.position) > maximumDistance) return false;
            if (eye == null) return true;

            return PlayerSight.CanPlayerSee(eye, transform, headHeight, sightBlockers,
                0.04f, maximumDistance + 5f);
        }

        private void Speak()
        {
            said = true;

            // Vira a cabeca ao dize-lo. Sem isto sai da nuca, e uma frase sobre
            // olhares dita de costas desfaz-se a si propria.
            routine?.GlanceAtPlayer(lineHold + 1.2f);

            dialogue.ShowReaction(speaker, line, voice, lineHold, null,
                autoClose: true, blocksInteraction: false);

            if (!string.IsNullOrWhiteSpace(raisesEvent))
            {
                director = ChapterDirector.Resolve(director);
                director?.Notify(raisesEvent);
            }
        }

        private bool ResolvePlayer()
        {
            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor == null) return false;
                player = motor.transform;
            }

            if (eye == null && player != null) eye = player.GetComponentInChildren<Camera>(true);
            if (eye == null) eye = Camera.main;
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
        public void EditorConfigure(int stares, string requires, string silencedBy,
            WorldDialogueController controller, NpcMumbleVoice mumble)
        {
            staresBeforeSpeaking = stares;
            requiresEvent = requires;
            silencedByEvent = silencedBy;
            dialogue = controller;
            voice = mumble;
        }
#endif
    }
}
