using Pungent.Dialogue;
using Pungent.Interaction;
using UnityEngine;

namespace Pungent.NPC
{
    /// <summary>
    /// Ir ter com o Rui depois de o apanhar, e ouvi-lo dizer que nao sabe do que e
    /// que o outro esta a falar.
    ///
    /// ---
    ///
    /// **Isto e o remate das duas encenacoes, e o remate e o que as torna dificeis
    /// de arrumar.**
    ///
    /// Sem ele, uma espreitadela e um grito sao coisas que acontecem *ao* jogador, e
    /// coisas que acontecem a uma pessoa arrumam-se: viu, ouviu, seguiu em frente.
    /// Poder ir ter com ele muda o sentido — a duvida passa a ser uma coisa que o
    /// jogador **testou** e que continuou de pe.
    ///
    /// ---
    ///
    /// **Ele nao mente, e essa e a escolha toda.**
    ///
    /// Um Rui que responde "sim, estava" acaba o jogo. Um Rui que se justifica —
    /// "estava a ver se estavas bem" — tambem, porque uma justificacao e uma
    /// confissao com boa educacao. O que nao fecha nada e ele **nao reconhecer o
    /// acontecimento**: nao houve grito nenhum, nao esteve a porta nenhuma. A partir
    /// dai o jogador tem duas versoes e nenhuma testemunha, e e ele que tem de
    /// escolher em qual acredita.
    ///
    /// Por isso o texto nao pode ter ironia nem ameaca. Ele diz aquilo como quem
    /// responde a uma pergunta esquisita, e volta ao que estava a fazer.
    ///
    /// ---
    ///
    /// **Porque e prioritario.** O Rui e varios verbos no mesmo corpo e este dura
    /// dois minutos. Ver <see cref="IPriorityInteractable"/>: sem isso, quem decidia
    /// se o jogador consegue perguntar era a ordem por que os componentes foram
    /// arrastados para o GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuiDenial : MonoBehaviour, IPlayerInteractable, IPriorityInteractable
    {
        [SerializeField] private RuiScreamingFit screaming;
        [SerializeField] private WorldDialogueController dialogue;
        [SerializeField] private NpcMumbleVoice voice;
        [SerializeField] private PrototypeNpcRoutine routine;

        [SerializeField] private string speaker = "Rui";
        [SerializeField] private string prompt = "Ask him about it";

        [Header("O que ele diz")]
        [Tooltip("Depois do episodio no quarto. A segunda metade e o que faz o "
               + "trabalho — ele nao nega so o grito, devolve a duvida ao jogador.")]
        [SerializeField, TextArea]
        private string screamDenial =
            "I do not know what you are talking about. You probably imagined it.";

        [Tooltip("Segunda linha, dita a seguir a negacao, se houver. Vazio = so uma.")]
        [SerializeField, TextArea]
        private string followUp = "";

        [SerializeField, Min(1f)] private float lineHold = 3.2f;

        /// <summary>
        /// O que ha para perguntar agora, se houver alguma coisa.
        ///
        /// Chegou a haver duas coisas para perguntar — a espreitadela e o grito — e o
        /// grito ganhava. A espreitadela saiu do jogo: era o sistema com mais estados
        /// e mais maneiras de encravar, e o que dava era uma duvida que o
        /// head-tracking da <see cref="HumanoidHeadLook"/> ja da sem estado nenhum.
        /// Fica o grito, que e um acontecimento guionado e nao uma maquina.
        /// </summary>
        private bool HasSomethingToAsk => screaming != null && screaming.HasFreshEpisode;

        public string Prompt
        {
            get
            {
                if (!HasSomethingToAsk) return string.Empty;

                // Nao interrompe o que ele esta a fazer nem a si proprio: a meio do
                // episodio o Rui esta trancado no quarto e nao esta ali para falar.
                if (screaming != null && screaming.IsScreaming) return string.Empty;
                if (routine != null && routine.InConversation) return string.Empty;
                if (dialogue != null && dialogue.IsBusy) return string.Empty;

                return prompt;
            }
        }

        public bool HoldToInteract => false;

        private void Awake()
        {
            if (screaming == null) screaming = GetComponent<RuiScreamingFit>();
            if (routine == null) routine = GetComponent<PrototypeNpcRoutine>();
            if (voice == null) voice = GetComponentInChildren<NpcMumbleVoice>();
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (!HasSomethingToAsk || dialogue == null) return;

            // Marcado **antes** de falar. A fala pode demorar segundos e o prompt
            // continua vivo durante eles; sem isto, insistir no botao punha o Rui a
            // negar a mesma coisa tres vezes seguidas, e um homem que repete a mesma
            // frase palavra por palavra deixa de ser uma pessoa.
            screaming.MarkConfronted();

            // **Parado, e nao so a olhar.**
            //
            // O `BeginDialogueStare` troca a pose e poe a cabeca virada para o Tomas —
            // e mais nada. O corpo continua no circuito domestico: o Rui negava ter
            // gritado enquanto **se afastava a caminho da cozinha**, com a cabeca
            // torcida por cima do ombro. A leitura que isso da e a de alguem que nao
            // liga nenhuma ao que lhe estao a perguntar, que e o oposto exacto de um
            // homem a esconder uma coisa.
            //
            // Quem para o corpo e o `SetConversationActive` — o
            // `RuiStoryConversation` ja o chamava, e esta cena, que e a mais tensa
            // das duas, nao. Nao ha nada de novo aqui: e a chamada que faltava.
            routine?.SetConversationActive(true);
            routine?.BeginDialogueStare();
            dialogue.ShowReaction(speaker, screamDenial, voice, lineHold, OnDenialFinished,
                autoClose: true, blocksInteraction: true);
        }

        private void OnDenialFinished()
        {
            if (!string.IsNullOrWhiteSpace(followUp) && dialogue != null)
            {
                string second = followUp;
                followUp = string.Empty;   // uma vez so por episodio
                dialogue.ShowReaction(speaker, second, voice, lineHold, OnDenialFinished,
                    autoClose: true, blocksInteraction: true);
                return;
            }

            routine?.EndDialogueStare();
            routine?.SetConversationActive(false);
        }

        /// <summary>
        /// A rede: se a cena for interrompida a meio, o corpo dele nao pode ficar
        /// parado para sempre.
        ///
        /// Uma troca de cena, um capitulo a suspender o componente, o jogador a fugir
        /// da sala — qualquer uma delas mata a corrente de `OnDenialFinished` e o
        /// `SetConversationActive(false)` nunca chega a acontecer. O Rui ficava
        /// plantado no corredor com o agente travado, e nada no ecra dizia porque.
        /// </summary>
        private void OnDisable()
        {
            routine?.EndDialogueStare();
            routine?.SetConversationActive(false);
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(RuiScreamingFit fit,
            WorldDialogueController talk, NpcMumbleVoice mumble, PrototypeNpcRoutine npcRoutine)
        {
            screaming = fit;
            dialogue = talk;
            voice = mumble;
            routine = npcRoutine;
        }
#endif
    }
}
