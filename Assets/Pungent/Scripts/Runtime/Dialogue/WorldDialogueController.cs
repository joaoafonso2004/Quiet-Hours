using System;
using Pungent.Interaction;
using Pungent.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pungent.Dialogue
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldDialogueView))]
    public sealed class WorldDialogueController : MonoBehaviour
    {
        private enum DialogueState
        {
            Hidden,
            Typing,
            Choosing,
            Holding
        }

        [SerializeField, Range(10f, 80f)] private float charactersPerSecond = 34f;
        [SerializeField, Min(0f)] private float commaPause = 0.1f;
        [SerializeField, Min(0f)] private float sentencePause = 0.26f;

        [Header("Ritmo")]
        [Tooltip("Tempo minimo com a fala inteira no ecra antes de o avanco ser aceite. "
               + "Evita que um clique dado durante a escrita salte tambem a linha.")]
        [SerializeField, Min(0f)] private float minimumReadSeconds = 0.45f;
        [Tooltip("Se falso, a fala escreve-se sempre ate ao fim e nao pode ser apressada.")]
        [SerializeField] private bool allowTypewriterSkip = true;

        private WorldDialogueView view;
        private PlayerInputReader input;
        private PlayerInteractor interactor;
        private PrototypePhoneUI phoneUi;
        private DialogueState state;

        /// <summary>Se foi esta fala que tirou as maos ao jogador. Ver `BeginLine`.</summary>
        private bool blockedInteraction;
        private string fullLine = string.Empty;
        private string firstChoice;
        private string secondChoice;

        /// <summary>
        /// Todas as respostas da fala actual. `firstChoice`/`secondChoice` ficam
        /// como estao para os chamadores antigos nao partirem.
        /// </summary>
        private string[] choiceTexts;
        private int visibleCharacters;
        private float nextCharacterTime;
        private float readyAt;
        private bool inputArmed;
        private NpcMumbleVoice voice;
        private DialogueChoiceAudio choiceAudio;

        /// <summary>
        /// A resposta que ja tocou. Guardada para o som sair na **transicao** e nao
        /// a cada frame: o rato parado em cima de uma opcao continua a ser um hover
        /// valido em todos os frames, e sem isto isso era uma metralhadora.
        /// </summary>
        private int lastHoveredChoice = -1;

        /// <summary>A voz do Tomas. Nao vem de um sitio do mundo; vem de dentro.</summary>
        private NpcMumbleVoice thoughtVoice;

        /// <summary>
        /// Monta a voz dos pensamentos num filho proprio.
        ///
        /// Filho, e nao neste objecto: o `NpcMumbleVoice` exige um `AudioSource`, e
        /// um `RequireComponent` a apanhar o do `PlayerRoot` roubava a fonte as
        /// passadas do jogador.
        /// </summary>
        private NpcMumbleVoice BuildThoughtVoice()
        {
            const string Name = "TomasThoughtVoice";
            Transform existing = transform.Find(Name);
            GameObject holder = existing != null ? existing.gameObject : new GameObject(Name);
            if (existing == null) holder.transform.SetParent(transform, false);

            var found = holder.GetComponent<NpcMumbleVoice>();
            if (found == null) found = holder.AddComponent<NpcMumbleVoice>();

            // Mais grave e bastante mais baixo do que a voz do Rui (0.92–1.07 a
            // 0.32): pensar nao e falar, e a 34 caracteres por segundo um resmungo
            // ao volume de uma conversa passa a ser barulho.
            found.MakeInnerVoice(new Vector2(0.80f, 0.90f), 0.19f);
            return found;
        }
        private Action<int> choiceCallback;
        private Action finishedCallback;
        private bool waitsForInput;
        private float holdUntil;
        private float holdSecondsAfterTyping;

        public bool IsBusy => state != DialogueState.Hidden;

        private void Awake()
        {
            view = GetComponent<WorldDialogueView>();
            input = GetComponent<PlayerInputReader>();
            interactor = GetComponent<PlayerInteractor>();
            phoneUi = GetComponent<PrototypePhoneUI>();

            // Como o `view.Build()`, monta-se sozinho se ninguem o ligou na cena.
            // O painel do dialogo e construido por codigo; o som dele nao devia
            // depender de alguem se lembrar de arrastar um componente.
            choiceAudio = GetComponentInChildren<DialogueChoiceAudio>(true);
            if (choiceAudio == null) choiceAudio = gameObject.AddComponent<DialogueChoiceAudio>();

            thoughtVoice = BuildThoughtVoice();

            view.Build();
        }

        private void Update()
        {
            // Nada avanca enquanto o jogador ainda tiver um botao de avancar em
            // baixo: e assim que o mesmo clique deixa de atravessar varias falas.
            if (!inputArmed && !AnyAdvanceHeld())
                inputArmed = true;

            switch (state)
            {
                case DialogueState.Typing:
                    UpdateTyping();
                    break;
                case DialogueState.Choosing:
                    UpdateChoices();
                    break;
                case DialogueState.Holding:
                    UpdateHolding();
                    break;
            }
        }

        /// <summary>
        /// Pergunta com duas respostas. Nao tem limite de tempo: a conversa fica
        /// parada ate o jogador escolher. `choiceSeconds` sobra da versao anterior
        /// e e ignorado de proposito — um relogio a escolher por ele era
        /// exatamente o que fazia o dialogo parecer que saltava sozinho.
        /// </summary>
        /// <summary>
        /// Pergunta com ate tres respostas. A terceira e tipicamente o silencio.
        /// </summary>
        public bool BeginChoice(string speaker, string line, NpcMumbleVoice mumbleVoice,
            string[] choices, float choiceSeconds, Action<int> onChoice)
        {
            if (IsBusy) return false;

            choiceTexts = choices;
            firstChoice = choices != null && choices.Length > 0 ? choices[0] : null;
            secondChoice = choices != null && choices.Length > 1 ? choices[1] : null;
            choiceCallback = onChoice;
            finishedCallback = null;
            waitsForInput = true;
            BeginLine(speaker, line, mumbleVoice);
            view.SetThoughtMode(false);
            return true;
        }

        public bool BeginChoice(string speaker, string line, NpcMumbleVoice mumbleVoice,
            string choiceOne, string choiceTwo, float choiceSeconds, Action<int> onChoice)
        {
            if (IsBusy)
                return false;

            firstChoice = choiceOne;
            secondChoice = choiceTwo;
            choiceCallback = onChoice;
            finishedCallback = null;
            waitsForInput = true;
            BeginLine(speaker, line, mumbleVoice);
            view.SetThoughtMode(false);
            return true;
        }

        /// <summary>
        /// Pensamento do protagonista. Sem interlocutor, sem voz de NPC e sem
        /// escolhas: serve os objetos do mundo que apenas comentam alguma coisa.
        ///
        /// Estes sim desaparecem sozinhos — sao ambiente, disparam enquanto o
        /// jogador anda, e obrigar a carregar numa tecla por cada um seria pior
        /// do que o problema que se esta a resolver.
        /// </summary>
        public bool ShowThought(string line, float holdSeconds)
        {
            if (IsBusy)
                return false;

            ResetCurrent();
            firstChoice = null;
            secondChoice = null;
            choiceTexts = null;
            holdUntil = 0f;
            holdSecondsAfterTyping = Mathf.Max(0.8f, holdSeconds);
            waitsForInput = false;
            choiceCallback = null;
            finishedCallback = null;
            BeginLine(null, line, thoughtVoice, blocksInteraction: false);
            view.SetThoughtMode(true);
            return true;
        }

        /// <summary>
        /// Fala de NPC sem escolha. Fica no ecra ate o jogador mandar seguir:
        /// `holdSeconds` passou a ser so o tempo minimo de leitura, nao um
        /// temporizador que despacha a fala por ele.
        /// </summary>
        /// <param name="autoClose">
        /// Fecha sozinha ao fim de <paramref name="holdSeconds"/> em vez de esperar
        /// por um clique. Serve a ultima fala de uma conversa: ficar a espera de que
        /// o jogador carregue para dispensar uma despedida deixa a caixa pendurada
        /// no ecra depois de a conversa ja ter acabado.
        /// </param>
        /// <param name="blocksInteraction">
        /// Falso quando a fala e ambiente e nao conversa: o Rui a falar atraves de
        /// uma porta durante o climax, ou a comentar enquanto procura. Tirar as maos
        /// ao jogador a meio de uma perseguicao e uma sentenca; e conversa e o que
        /// ele **nao** esta a ter com o jogador nesses momentos.
        /// </param>
        public void ShowReaction(string speaker, string line, NpcMumbleVoice mumbleVoice,
            float holdSeconds, Action onFinished, bool autoClose = false,
            bool blocksInteraction = true)
        {
            ResetCurrent();
            firstChoice = null;
            secondChoice = null;
            choiceTexts = null;
            holdUntil = 0f;
            holdSecondsAfterTyping = autoClose ? Mathf.Max(0.8f, holdSeconds) : 0f;
            waitsForInput = !autoClose;
            choiceCallback = null;
            finishedCallback = onFinished;
            BeginLine(speaker, line, mumbleVoice, blocksInteraction);
            view.SetThoughtMode(false);
        }

        public void Cancel()
        {
            ResetCurrent();
        }

        /// <param name="blocksInteraction">
        /// Verdadeiro numa conversa: enquanto se fala com alguem nao se anda a abrir
        /// gavetas. **Falso num pensamento** — um pensamento e interior e nao devia
        /// tirar as maos ao jogador.
        ///
        /// Enquanto isto foi sempre verdadeiro, cada pensamento congelava a
        /// interaccao ate ao fim do texto. Com meia duzia deles por capitulo passava
        /// despercebido; com os pensamentos de passagem espalhados pela casa, o
        /// jogador passa a apanhar com isto de dez em dez segundos e o apartamento
        /// fica cheio de momentos em que o frigorifico simplesmente nao abre.
        /// </param>
        private void BeginLine(string speaker, string line, NpcMumbleVoice mumbleVoice,
            bool blocksInteraction = true)
        {
            fullLine = line ?? string.Empty;
            visibleCharacters = 0;
            voice = mumbleVoice;
            nextCharacterTime = Time.unscaledTime + 0.08f;
            state = DialogueState.Typing;
            // Cada fala nova volta a exigir que o jogador largue o botao antes de
            // o proximo toque contar.
            inputArmed = false;
            readyAt = float.MaxValue;
            blockedInteraction = blocksInteraction;
            if (blocksInteraction)
            {
                interactor?.SetInteractionBlocked(true);
                phoneUi?.SetDialogueSuppressed(true);

                // **E os pes e a cabeca, e nao so as maos.**
                //
                // Isto bloqueava a interaccao e mais nada: durante uma conversa a
                // serio dava para sair da sala a andar, ou rodar a camara para o
                // lado oposto, com a caixa de dialogo a correr sozinha e o Rui a
                // falar para uma parede. Uma conversa em que o interlocutor pode ir
                // dar uma volta nao e uma conversa.
                //
                // As falas ditas por cima do ombro — o `RuiCarRemark`, o estranho da
                // estrada — passam `blocksInteraction: false` e continuam a nao
                // tirar nada ao jogador. E essa a linha que separa as duas coisas, e
                // ja existia; so nao estava a ser usada para isto.
                input?.SetMoveSuppressed(true);
                input?.SetLookSuppressed(true);
            }
            view.Show(speaker);
            view.SetChoices(null, null);
            view.SetContinueHint(false);
            view.SetLine(string.Empty);
        }

        private void UpdateTyping()
        {
            if (allowTypewriterSkip && ConsumeSkip())
            {
                RevealFullLine();
                CompleteTyping();
                return;
            }

            float interval = 1f / Mathf.Max(1f, charactersPerSecond);
            while (visibleCharacters < fullLine.Length && Time.unscaledTime >= nextCharacterTime)
            {
                char current = fullLine[visibleCharacters];
                char previous = visibleCharacters > 0 ? fullLine[visibleCharacters - 1] : '\0';
                visibleCharacters++;
                view.SetLine(fullLine.Substring(0, visibleCharacters));
                voice?.SpeakCharacter(current, visibleCharacters - 1, previous);

                nextCharacterTime += interval + GetPunctuationPause(current);
                if (Time.unscaledTime < nextCharacterTime)
                    break;
            }

            if (visibleCharacters >= fullLine.Length)
                CompleteTyping();
        }

        private void CompleteTyping()
        {
            voice?.Stop();
            view.SetLine(fullLine);
            // A partir daqui conta o tempo minimo de leitura, e o botao tem de ser
            // largado outra vez: quem carregou para despachar a escrita nao fecha
            // com esse mesmo toque a fala que acabou de aparecer.
            readyAt = Time.unscaledTime + minimumReadSeconds;
            inputArmed = false;

            if (!string.IsNullOrWhiteSpace(firstChoice) || !string.IsNullOrWhiteSpace(secondChoice))
            {
                state = DialogueState.Choosing;
                // A lista e nova: o que estava por baixo do cursor na escolha
                // anterior nao conta como "ja tocou" para esta.
                lastHoveredChoice = -1;
                input?.SetUiPointerActive(true);
                view.SetChoices(choiceTexts ?? new[] { firstChoice, secondChoice });
            }
            else
            {
                state = DialogueState.Holding;
                // O tempo prometido e tempo com a frase inteira legivel, nao tempo
                // desde a primeira letra. Antes, uma frase longa gastava quase todo
                // o hold no typewriter e desaparecia mal acabava de ser escrita.
                if (!waitsForInput)
                    holdUntil = Time.unscaledTime + holdSecondsAfterTyping;
                if (waitsForInput) view.SetContinueHint(true);
            }
        }

        /// <summary>
        /// Escolha sem relogio. Fica aqui indefinidamente ate o jogador responder:
        /// e a unica saida deste estado.
        /// </summary>
        private void UpdateChoices()
        {
            Mouse mouse = Mouse.current;
            int hovered = mouse != null ? view.GetHoveredChoice(mouse.position.ReadValue()) : -1;
            view.SetHoveredChoice(hovered);

            // So a entrar numa resposta. Sair de uma para o vazio nao toca: o
            // silencio ja diz que nao ha nada por baixo do cursor, e um som ao
            // sair fazia o dobro dos toques a atravessar a lista.
            if (hovered != lastHoveredChoice)
            {
                if (hovered >= 0) choiceAudio?.PlayHover();
                lastHoveredChoice = hovered;
            }

            if (!InputAccepted())
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
                {
                    ResolveChoice(0);
                    return;
                }

                if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
                {
                    ResolveChoice(1);
                    return;
                }

                // A terceira, tipicamente o silencio. So responde se ela existir:
                // carregar no 3 numa fala com duas opcoes nao deve escolher nada.
                if ((keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
                    && choiceTexts != null && choiceTexts.Length > 2
                    && !string.IsNullOrWhiteSpace(choiceTexts[2]))
                {
                    ResolveChoice(2);
                    return;
                }
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && hovered >= 0)
                ResolveChoice(hovered);
        }

        /// <summary>
        /// Fala de NPC ja escrita por inteiro. So sai daqui por vontade do jogador;
        /// os pensamentos do proprio Tomas continuam a sair sozinhos.
        /// </summary>
        private void UpdateHolding()
        {
            if (!waitsForInput)
            {
                if (Time.unscaledTime >= holdUntil) FinishCurrentDialogue();
                return;
            }

            if (ConsumeAdvance())
                FinishCurrentDialogue();
        }

        private void ResolveChoice(int index)
        {
            // Antes do `ResetCurrent`, que e quem desmonta o estado da escolha.
            choiceAudio?.PlayConfirm();

            Action<int> callback = choiceCallback;
            ResetCurrent();
            callback?.Invoke(index);
        }

        private void FinishCurrentDialogue()
        {
            Action callback = finishedCallback;
            ResetCurrent();
            callback?.Invoke();
        }

        private void RevealFullLine()
        {
            visibleCharacters = fullLine.Length;
            view.SetLine(fullLine);
        }

        private float GetPunctuationPause(char character)
        {
            if (character == ',' || character == ';' || character == ':')
                return commaPause;
            if (character == '.' || character == '!' || character == '?')
                return sentencePause;
            return 0f;
        }

        /// <summary>
        /// O toque so conta se ja passou o tempo minimo de leitura e se o jogador
        /// largou o botao desde que este estado comecou. As duas condicoes juntas
        /// sao o que impede uma fala de arrastar a seguinte com ela.
        /// </summary>
        private bool InputAccepted() => inputArmed && Time.unscaledTime >= readyAt;

        private bool ConsumeAdvance() => InputAccepted() && WantsToAdvance();

        /// <summary>
        /// Despachar a escrita **nao** espera pelo tempo minimo de leitura.
        ///
        /// O `allowTypewriterSkip` e o `RevealFullLine` estavam ca desde sempre e
        /// nunca dispararam uma vez: o `BeginLine` poe `readyAt = float.MaxValue`, o
        /// `InputAccepted` exige `Time.unscaledTime >= readyAt`, e so o
        /// `CompleteTyping` e que baixa esse valor. Ou seja, a condicao para poder
        /// despachar a escrita era a escrita ja ter acabado. O clique nao fazia
        /// nada e era preciso esperar pela maquina de escrever ate ao fim.
        ///
        /// O `readyAt` continua a valer o que valia — e ele que impede o clique que
        /// despachou a frase de fechar tambem a frase que acabou de aparecer, e
        /// isso mede-se a partir do momento em que ela esta legivel. O que sobra
        /// aqui e o `inputArmed`, que e a condicao certa para este estado: o botao
        /// tem de ter sido largado desde que a fala comecou, e por isso o clique que
        /// abriu a conversa nao salta a primeira linha dela.
        /// </summary>
        private bool ConsumeSkip() => inputArmed && WantsToAdvance();

        /// <summary>
        /// Avancar e so o botao esquerdo do rato.
        ///
        /// O E esta ocupado: e o lean para a direita (o Q e para a esquerda). Ter o
        /// E tambem a passar falas fazia o Tomas inclinar-se de lado a meio da
        /// conversa. O espaco e o enter ficam de fora por opcao — uma conversa
        /// avanca com o rato, que e o mesmo botao com que se fala com as pessoas.
        /// </summary>
        private static bool WantsToAdvance()
        {
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        private static bool AnyAdvanceHeld()
        {
            Keyboard keyboard = Keyboard.current;
            bool keyboardHeld = keyboard != null &&
                                (keyboard.digit1Key.isPressed || keyboard.numpad1Key.isPressed ||
                                 keyboard.digit2Key.isPressed || keyboard.numpad2Key.isPressed ||
                                 keyboard.digit3Key.isPressed || keyboard.numpad3Key.isPressed);
            Mouse mouse = Mouse.current;
            return keyboardHeld || (mouse != null && mouse.leftButton.isPressed);
        }

        private void ResetCurrent()
        {
            voice?.Stop();
            input?.SetUiPointerActive(false);

            // So desbloqueia quem bloqueou. Um pensamento a acabar nao pode
            // desbloquear a interaccao no meio de uma conversa que esteja a decorrer.
            if (blockedInteraction)
            {
                interactor?.SetInteractionBlocked(false);
                phoneUi?.SetDialogueSuppressed(false);

                // Devolvidos pelo mesmo caminho e pela mesma condicao: quem nao
                // bloqueou tambem nao desbloqueia. Um pensamento de passagem a
                // acabar no meio de uma conversa nao pode devolver o movimento a
                // meio dela.
                input?.SetMoveSuppressed(false);
                input?.SetLookSuppressed(false);

                blockedInteraction = false;
            }
            view.SetContinueHint(false);
            view.Hide();
            state = DialogueState.Hidden;
            fullLine = string.Empty;
            firstChoice = null;
            secondChoice = null;
            choiceTexts = null;
            visibleCharacters = 0;
            holdUntil = 0f;
            holdSecondsAfterTyping = 0f;
            readyAt = float.MaxValue;
            inputArmed = false;
            waitsForInput = false;
            voice = null;
            choiceCallback = null;
            finishedCallback = null;
        }
    }
}
