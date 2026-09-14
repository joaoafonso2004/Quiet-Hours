using UnityEngine;
using UnityEngine.UI;

namespace Pungent.Narrative
{
    /// <summary>
    /// Enquadramento escrito antes do jogo comecar: ecra preto, texto serifado
    /// centrado, linhas a acumular uma a uma. Sem caixas, sem molduras.
    ///
    /// Uma linha vazia no array vale como pausa/paragrafo.
    /// O jogador pode saltar; o controlo so volta no fim do fade.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IntroTextSequence : MonoBehaviour
    {
        private const string CanvasName = "IntroCanvas";

        [Header("Texto")]
        // Um relato e nao uma sinopse. A versao anterior anunciava-se — abria em
        // "My name is Tomas" e fechava em "This is the week I stopped feeling
        // safe at home", que e a logline do jogo dita antes de o jogo comecar.
        //
        // Nada aqui pode prometer o que vem a seguir. Quem esta a ler ja sabe que
        // isto e um jogo de terror; se o texto tambem lho disser, gasta a duvida
        // que e a unica coisa que ele tinha para dar. E "He knew things about me
        // I had never told him" era pior ainda: e a revelacao do Dia 2, jogada
        // antes de o jogador ter visto o Rui uma vez.
        //
        // Por isso: factos aborrecidos, especificos, e sem adjectivos de emocao.
        // The rent establishes the premise; Tuesdays establish the routine Rui
        // later knows; the father's repeated warning about the lock pays into one
        // ending. Rui enters without threat, carrying a box.
        //
        // Acaba na caixa e nao num remate. Havia aqui um "The first week was
        // fine", que e uma linha honesta mas ainda aponta para a frente — e um
        // "primeiro" implica um segundo. Sem ela nao ha gesto nenhum: o relato
        // simplesmente para, e o apartamento aparece. E o corte que assusta,
        // nao o aviso.
        //
        // "Next to mine" e literal: `Door_Bedroom_Tomas` e `Door_Bedroom_Rui`
        // estao os dois em z = -0,80, a 2,65 m um do outro. Sao portas da mesma
        // parede do corredor e nao lados opostos dele.
        [SerializeField, TextArea]
        private string[] lines =
        {
            "Four hundred a month, bills included.",
            "That was the whole reason I took it.",
            "",
            "New city, new school. On Tuesdays, thirty",
            "fifteen-year-olds who could tell I was nervous.",
            "",
            "My father paid half the deposit and called it a loan.",
            "He told me three times to lock the door.",
            "I told him I had heard him the first time.",
            "",
            "Rui had the room next to mine.",
            "He was polite. He carried a box in for me."
        };

        [Header("Ritmo")]
        [SerializeField, Min(0.1f)] private float lineFadeSeconds = 0.9f;
        [SerializeField, Min(0f)] private float lineHoldSeconds = 1.15f;
        [SerializeField, Min(0f)] private float paragraphPauseSeconds = 0.7f;
        [SerializeField, Min(0f)] private float finalHoldSeconds = 2.2f;
        [SerializeField, Min(0.1f)] private float fadeOutSeconds = 1.6f;
        [SerializeField, Min(0f)] private float startDelaySeconds = 0.8f;

        [Header("Aspeto")]
        [SerializeField] private int fontSize = 34;
        [SerializeField] private Color textColour = new Color(0.93f, 0.90f, 0.82f, 1f);
        [SerializeField, Min(1f)] private float lineSpacing = 46f;

        [Header("Controlo")]
        [Tooltip("Componentes desligados enquanto a introducao corre (motor, interactor, HUD, telemovel).")]
        [SerializeField] private MonoBehaviour[] suppressedDuringIntro;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool allowSkip = true;

        [Tooltip("No fim, parar e esperar que o jogador carregue.\n\n"
               + "Desligado, isto le-se como o dialogo do jogo: corre sozinho, e "
               + "carregar acaba de escrever as linhas que faltam; carregar outra "
               + "vez avanca. Ligado, fica parado com 'Press any button to "
               + "continue...' ate alguem responder — o que serve um ecra final, "
               + "onde nao ha jogo para onde voltar, e nao uma passagem de tempo "
               + "no meio do prologo.")]
        [SerializeField] private bool waitForInputAtEnd = true;

        [Tooltip("Tempo, desde o arranque, em que nenhuma tecla salta nada.\n\n"
               + "O menu deixa um clique pendurado mesmo antes do apartamento "
               + "carregar. Sem isto, esse clique — ou o seguinte, de quem clica "
               + "outra vez enquanto a cena carrega — revelava as onze linhas de "
               + "golpe e mandava-as embora, e a abertura toda passava a piscar "
               + "uma vez e desaparecer.\n\n"
               + "Cobre a espera inicial e a primeira linha a aparecer: saltar "
               + "tem de ser uma decisao tomada com o texto ja no ecra, e nao "
               + "input herdado da cena anterior.")]
        [SerializeField, Min(0f)] private float deafSeconds = 1.6f;

        [Tooltip("No fim, fica no preto em vez de revelar a cena.\n\n"
               + "E o que separa uma abertura de um fecho. A abertura desvanece "
               + "para o jogo comecar; um fim que desvanecesse punha o jogador "
               + "outra vez de pe no apartamento depois da ultima linha, com o "
               + "jogo acabado e sem nada para fazer — que e exactamente o que "
               + "acontecia por nao haver fim nenhum.")]
        [SerializeField] private bool holdOnBlack;

        private Canvas canvas;
        private Image blackout;
        private Text[] lineTexts;
        private float[] lineAlpha;
        private int revealedCount;
        private float phaseTimer;
        private bool running;
        private bool fadingOut;
        private float fadeOutTimer;
        private bool skipRequested;
        private float deafUntil;
        private bool waitingForInputToFinish;
        private Text continueText;

        /// <summary>
        /// Verdadeiro enquanto o texto de abertura esta no ecra. A cena esta viva
        /// por tras do preto, por isso quem tem uma encenacao propria a seguir
        /// precisa de saber que ainda nao e a sua vez.
        /// </summary>
        public bool IsRunning => running;

        private void Start()
        {
            if (playOnStart)
                Play();
        }

        public void Play()
        {
            BuildCanvas();
            SetSuppressed(true);
            running = true;
            fadingOut = false;
            revealedCount = 0;
            phaseTimer = -startDelaySeconds;
            skipRequested = false;
            // Tempo real e nao `phaseTimer`: o `phaseTimer` reinicia a cada
            // paragrafo, e o silencio e contado uma vez, do inicio.
            deafUntil = Time.unscaledTime + deafSeconds;

            for (int i = 0; i < lineAlpha.Length; i++)
            {
                lineAlpha[i] = 0f;
                lineTexts[i].color = WithAlpha(textColour, 0f);
            }

            blackout.color = new Color(0f, 0f, 0f, 1f);
            if (continueText != null) continueText.color = WithAlpha(textColour, 0f);
            canvas.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!running) return;

            if (allowSkip && !fadingOut && AnySkipInput())
                skipRequested = true;

            if (fadingOut)
            {
                UpdateFadeOut();
                return;
            }

            if (waitingForInputToFinish)
            {
                if (continueText != null)
                {
                    // Pulse alpha
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                    continueText.color = WithAlpha(textColour, pulse * 0.7f);
                }

                if (allowSkip && AnySkipInput())
                {
                    BeginFadeOut();
                }
                return;
            }

            if (skipRequested)
            {
                skipRequested = false;

                // Segunda pressao, com tudo ja escrito: sai. E o gesto do dialogo
                // — a primeira acaba de escrever, a segunda avanca — e nao um
                // botao que engole o texto todo de uma vez.
                if (revealedCount >= lineTexts.Length && !waitForInputAtEnd)
                {
                    BeginFadeOut();
                    return;
                }

                for (int i = 0; i < lineAlpha.Length; i++)
                {
                    lineAlpha[i] = 1f;
                    lineTexts[i].color = WithAlpha(textColour, 1f);
                }

                revealedCount = lineTexts.Length;
                phaseTimer = 0f;
                if (waitForInputAtEnd) waitingForInputToFinish = true;
                return;
            }

            float dt = Time.unscaledDeltaTime;
            phaseTimer += dt;

            // Sobe a opacidade das linhas ja reveladas.
            for (int i = 0; i < revealedCount; i++)
            {
                if (lineAlpha[i] >= 1f) continue;
                lineAlpha[i] = Mathf.Min(1f, lineAlpha[i] + dt / lineFadeSeconds);
                lineTexts[i].color = WithAlpha(textColour, lineAlpha[i]);
            }

            if (revealedCount >= lineTexts.Length)
            {
                if (phaseTimer >= finalHoldSeconds)
                {
                    if (waitForInputAtEnd) waitingForInputToFinish = true;
                    else BeginFadeOut();
                }
                return;
            }

            bool blank = string.IsNullOrWhiteSpace(lines[revealedCount]);
            float wait = revealedCount == 0
                ? 0f
                : (blank ? paragraphPauseSeconds : lineHoldSeconds);

            if (phaseTimer >= wait)
            {
                revealedCount++;
                phaseTimer = 0f;
            }
        }

        private void BeginFadeOut()
        {
            fadingOut = true;
            fadeOutTimer = 0f;
        }

        private void UpdateFadeOut()
        {
            fadeOutTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(fadeOutTimer / fadeOutSeconds);

            // A fechar, o preto fica. So o texto e que sai.
            blackout.color = new Color(0f, 0f, 0f, holdOnBlack ? 1f : 1f - t);
            float textAlpha = Mathf.Clamp01(1f - t * 1.8f);
            for (int i = 0; i < lineTexts.Length; i++)
                lineTexts[i].color = WithAlpha(textColour, lineAlpha[i] * textAlpha);
                
            if (continueText != null)
                continueText.color = WithAlpha(textColour, continueText.color.a * textAlpha);

            if (t < 1f) return;

            running = false;

            // Num fecho, a tela fica de pe e o jogador continua sem maos: nao ha
            // jogo para onde voltar. Quem devolve o controlo — ou nao devolve — e
            // quem mandou tocar isto.
            if (holdOnBlack) return;

            canvas.gameObject.SetActive(false);
            SetSuppressed(false);
        }

        private bool AnySkipInput()
        {
            if (Time.unscaledTime < deafUntil) return false;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        private void SetSuppressed(bool suppressed)
        {
            if (suppressedDuringIntro == null) return;
            foreach (var component in suppressedDuringIntro)
                if (component != null)
                    component.enabled = !suppressed;
        }

        /// <summary>
        /// A rede: se isto for desligado a meio, devolve o jogador antes de sair.
        ///
        /// ---
        ///
        /// **Enquanto corre, este componente tem o jogador inteiro na mao** — o
        /// motor, o HUD, o interactor e o telemovel — e no fim fica a espera de uma
        /// tecla. Se alguma coisa acabar o capitulo com a sequencia ainda a correr,
        /// os quatro ficam desligados para o resto do jogo e **nao ha erro nenhum**:
        /// o jogador nao anda, nao ve objectivos, nao interage e nao abre o
        /// telemovel, e nada na consola diz porque.
        ///
        /// Apanhado tres vezes numa so sessao de testes, sempre a custar meia hora a
        /// perceber. A jogar a serio so acontece se a sequencia for saltada por
        /// codigo — mas e exactamente isso que uma ferramenta de teste faz, e o
        /// preco de nao ter a rede e alto de mais para o que ela custa.
        ///
        /// O `holdOnBlack` fica de fora de proposito: nesse modo a tela fica de pe e
        /// o jogador **deve** continuar sem maos, porque nao ha jogo para onde
        /// voltar.
        /// </summary>
        private void OnDisable()
        {
            if (!running || holdOnBlack) return;

            Debug.LogWarning("[Abertura] Desligada a meio, com o jogador suprimido. " +
                             "Devolvido o controlo — mas alguem acabou o capitulo por cima " +
                             "desta sequencia, e isso e que ha para corrigir.", this);

            running = false;
            waitingForInputToFinish = false;
            SetSuppressed(false);
        }

        private void BuildCanvas()
        {
            if (canvas != null && lineTexts != null && lineTexts.Length == lines.Length)
                return;

            Transform stale = transform.Find(CanvasName);
            if (stale != null)
            {
                if (Application.isPlaying) Destroy(stale.gameObject);
                else DestroyImmediate(stale.gameObject);
            }

            GameObject canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Acima do dialogo, do telemovel e do ScreenFade.
            //
            // O ScreenFade tambem esta em 200, e um empate deixa a ordem ao acaso
            // da hierarquia: metade das vezes a abertura ficava escrita por baixo
            // do preto e nao se lia nada. O prologo do reboot põe os dois no ar
            // ao mesmo tempo de proposito — o fade-out desta tela revela o preto
            // do ScreenFade por baixo, sem um unico frame de cena a espreitar.
            canvas.sortingOrder = 210;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject blackoutObject = new GameObject("Blackout", typeof(RectTransform));
            blackoutObject.transform.SetParent(canvasObject.transform, false);
            blackout = blackoutObject.AddComponent<Image>();
            blackout.color = Color.black;
            blackout.raycastTarget = false;
            RectTransform blackoutRect = blackout.rectTransform;
            blackoutRect.anchorMin = Vector2.zero;
            blackoutRect.anchorMax = Vector2.one;
            blackoutRect.offsetMin = Vector2.zero;
            blackoutRect.offsetMax = Vector2.zero;

            Font font = LoadSerifFont();
            lineTexts = new Text[lines.Length];
            lineAlpha = new float[lines.Length];

            float totalHeight = (lines.Length - 1) * lineSpacing;
            for (int i = 0; i < lines.Length; i++)
            {
                GameObject lineObject = new GameObject($"Line_{i}", typeof(RectTransform));
                lineObject.transform.SetParent(canvasObject.transform, false);

                Text text = lineObject.AddComponent<Text>();
                text.font = font;
                text.fontSize = fontSize;
                text.color = WithAlpha(textColour, 0f);
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                text.text = lines[i];

                RectTransform rect = text.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(1500f, lineSpacing);
                rect.anchoredPosition = new Vector2(0f, totalHeight * 0.5f - i * lineSpacing);

                lineTexts[i] = text;
            }
            
            // Add Continue Text
            GameObject continueObject = new GameObject("ContinueText", typeof(RectTransform));
            continueObject.transform.SetParent(canvasObject.transform, false);
            continueText = continueObject.AddComponent<Text>();
            continueText.font = font;
            continueText.fontSize = Mathf.RoundToInt(fontSize * 0.65f); // slightly smaller
            continueText.color = WithAlpha(textColour, 0f);
            continueText.alignment = TextAnchor.MiddleCenter;
            continueText.raycastTarget = false;
            continueText.text = "Press any button to continue...";
            
            RectTransform contRect = continueText.rectTransform;
            contRect.anchorMin = new Vector2(0.5f, 0f);
            contRect.anchorMax = new Vector2(0.5f, 0f);
            contRect.pivot = new Vector2(0.5f, 0f);
            contRect.sizeDelta = new Vector2(1000f, lineSpacing);
            contRect.anchoredPosition = new Vector2(0f, 60f); // near the bottom
        }

        private static Color WithAlpha(Color colour, float alpha)
            => new Color(colour.r, colour.g, colour.b, alpha);

        /// <summary>Serifa do sistema; cai para a fonte interna se nenhuma existir.</summary>
        private static Font LoadSerifFont()
        {
            Font font = Font.CreateDynamicFontFromOSFont(
                new[] { "Georgia", "Times New Roman", "Palatino Linotype", "Garamond", "Cambria" }, 34);
            if (font != null)
                return font;

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
