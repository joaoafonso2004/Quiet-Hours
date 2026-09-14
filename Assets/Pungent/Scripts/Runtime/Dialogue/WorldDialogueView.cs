using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Pungent.Dialogue
{
    /// <summary>
    /// Apresentacao do dialogo presencial: texto limpo, sem paineis nem molduras.
    /// A fala fica em baixo a esquerda, grande, com contorno para se ler sobre
    /// qualquer fundo; as respostas surgem por baixo, mais pequenas e em maiusculas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldDialogueView : MonoBehaviour
    {
        // O nome muda de versao **de propósito**. O painel e construido por codigo
        // mas fica gravado na cena, e o `TryRebindExistingView` adopta o que la
        // estiver. Enquanto o nome for o mesmo, uma cena gravada com o painel antigo
        // — o do realce por `<mark>` — continuava a ser adoptada, e a correccao nunca
        // chegava a aparecer por muito que o codigo mudasse. Nome novo = reconstroi.
        private const string CanvasName = "DialogueCanvas_v5";
        private static readonly string[] StaleCanvasNames =
            { "DialogueCanvas_v4", "DialogueCanvas_v3", "DialogueCanvas", "WorldDialogueCanvas" };

        private const string DialogueFontResource = "Fonts & Materials/LiberationSans SDF";
        private const string DialogueMaterialResource =
            "Fonts & Materials/LiberationSans SDF - Overlay";

        private static readonly Color LineColour = new Color(0.97f, 0.96f, 0.93f, 1f);
        private static readonly Color SpeakerColour = new Color(0.88f, 0.84f, 0.72f, 1f);

        /// <summary>
        /// Folga entre o topo da fala e a base do nome, em pixeis de referencia.
        ///
        /// Pequena de propósito: o nome pertence a fala e nao ao ecra. Com muito
        /// espaco pelo meio, os dois leem-se como dois elementos de interface; com
        /// pouco, le-se como uma pessoa a falar.
        /// </summary>
        private const float SpeakerGap = 6f;
        private static readonly Color ChoiceColour = new Color(0.84f, 0.80f, 0.66f, 1f);
        private static readonly Color ChoiceHoverColour = new Color(1f, 1f, 1f, 1f);
        private static readonly Color ThoughtColour = new Color(0.92f, 0.91f, 0.88f, 1f);

        private static readonly Color HintColour = new Color(0.62f, 0.60f, 0.54f, 1f);

        /// <summary>
        /// A cor do realce por tras da fala.
        ///
        /// Preto a 62% e nao a 100%: opaco fazia uma tarja de legenda de televisao e
        /// arrancava a fala do sitio onde ela acontece. A esta densidade le-se
        /// sempre, e ainda se ve a cozinha por tras — que e o ponto todo de escrever
        /// no mundo em vez de numa caixa.
        /// </summary>
        private static readonly Color HighlightColour = new Color(0f, 0f, 0f, 0.62f);
        private static readonly Color ThoughtHighlightColour = new Color(0f, 0f, 0f, 0.48f);

        /// <summary>Folga a volta das letras, em unidades do canvas.</summary>
        private static readonly Vector2 HighlightPadding = new Vector2(20f, 10f);

        private CanvasGroup canvasGroup;
        private RectTransform root;
        private Text speakerText;
        /// <summary>
        /// A fala. Em TextMeshPro e nao em `Text` legacy, ao contrario do resto do
        /// painel, por uma razao so: a tag `&lt;mark&gt;` — o realce que faz o fundo
        /// atras das letras — nao existe no rich text do legacy.
        /// </summary>
        private TextMeshProUGUI lineText;

        [Tooltip("Amplitude do tremor de uma fala gritada, em unidades de canvas.")]
        [SerializeField, Range(0f, 8f)] private float shoutShakeAmount = 2.2f;
        [SerializeField, Range(1f, 60f)] private float shoutShakeSpeed = 26f;

        private bool shouting;
        private bool shakeApplied;

        /// <summary>
        /// O realce por tras da fala. **Um `Image`, e nao a tag `&lt;mark&gt;`.**
        ///
        /// Isto ja foi tentado de duas maneiras e falhou nas duas, sempre com o mesmo
        /// sintoma: uma barra preta solida por cima do texto, ilegivel. Vale a pena
        /// ficar escrito, porque parecem dois problemas e sao um so.
        ///
        /// **A causa:** o TMP acrescenta a geometria do `&lt;mark&gt;` ao **fim** da
        /// malha, no mesmo material e sem teste de profundidade. Em UI, quem e
        /// desenhado depois fica por cima — portanto o realce tapa sempre as letras
        /// do proprio objecto onde vive.
        ///
        /// **Primeira tentativa:** duas TMP, o realce atras e a fala a frente. Nao
        /// resolve, porque duas TMP com o mesmo material sao agrupadas num lote e a
        /// ordem de irmaos deixa de mandar — o quadrado do realce de uma sai por cima
        /// das letras da outra.
        ///
        /// **Segunda tentativa:** uma TMP so, com o `&lt;mark&gt;` no proprio texto.
        /// E o uso que a tag foi feita para ter, e falha na mesma pela razao de cima.
        ///
        /// **O que funciona:** um `Image` num objecto proprio, criado antes do texto.
        /// Material diferente do da fonte, portanto nao entra no mesmo lote, portanto
        /// a ordem de irmaos volta a mandar e ele fica mesmo por baixo.
        ///
        /// E continua a crescer letra a letra: a cada `SetLine` o rectangulo e
        /// medido a partir das *bounds* reais do que ja foi escrito. Uma caixa fixa
        /// anunciava o tamanho da fala antes de ela ser dita, e era essa a razao de
        /// haver realce em vez de um painel.
        /// </summary>
        private Image lineBacking;
        private Text hintText;
        private CanvasGroup hintGroup;
        // Tres e nao duas. Duas opcoes leem-se sempre como simpatico/antipatico,
        // que e a caricatura que a seccao 22 avisa. A terceira nao e um meio termo:
        // e o silencio, que num jogo sobre nao saber se estas a exagerar e a
        // resposta mais honesta que ha.
        private const int MaxChoices = 3;

        private readonly RectTransform[] choiceRects = new RectTransform[MaxChoices];
        private readonly Text[] choiceTexts = new Text[MaxChoices];
        private readonly string[] choiceValues = new string[MaxChoices];
        private int activeChoiceCount;
        private float targetAlpha;
        private float choiceTargetAlpha;
        private float hintTargetAlpha;
        private int hoveredChoice = -1;

        public void Build()
        {
            if (ReferencesAreValid() || TryRebindExistingView())
                return;

            Font font = LoadRuntimeFont();
            foreach (var stale in StaleCanvasNames) DestroyChild(stale);
            DestroyChild(CanvasName);

            GameObject canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject rootObject = CreateUiObject("DialogueRoot", canvasObject.transform);
            root = rootObject.GetComponent<RectTransform>();
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.sizeDelta = new Vector2(1240f, 520f);
            root.anchoredPosition = new Vector2(72f, 54f);
            canvasGroup = rootObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            // O nome tem de se ler de relance. A versao anterior tinha 21 px e
            // desaparecia no fundo, sobretudo ao lado da fala a 36 px.
            speakerText = CreateText("Speaker", root, font, 34, FontStyle.Bold, SpeakerColour,
                TextAnchor.LowerLeft);
            SetRect(speakerText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(1180f, 40f), new Vector2(0f, 430f));
            AddOutline(speakerText.gameObject, 1.8f, 0.82f);

            // A camada de tras primeiro: quem e criado antes fica por baixo. Com um
            // `Image` isso e mesmo verdade, porque o material dele nao e o da fonte e
            // os dois nao sao agrupados no mesmo lote — que era o que desfazia esta
            // ordem quando o realce era feito com outra TMP.
            GameObject backing = CreateUiObject("LineBacking", root);
            lineBacking = backing.AddComponent<Image>();
            lineBacking.color = HighlightColour;
            lineBacking.raycastTarget = false;
            lineBacking.enabled = false;
            SetRect(lineBacking.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 200f));

            lineText = CreateTmpText("Line", root, 40, LineColour);
            SetRect(lineText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(1180f, 210f), new Vector2(0f, 200f));

            // Sem contorno nesta: o realce ja separa a letra do mundo, e as duas
            // coisas juntas engrossavam o texto ate parecer borrado.

            for (int i = 0; i < choiceRects.Length; i++)
                CreateChoice(i, root, font);

            // Aviso de "continuar": so aparece quando a fala ja acabou de escrever
            // e o jogador ja pode mesmo avancar. Sem ele, tirar o avanco automatico
            // deixava o jogador sem saber que a vez era dele.
            hintText = CreateText("ContinueHint", root, font, 20, FontStyle.Italic, HintColour,
                TextAnchor.LowerLeft);
            SetRect(hintText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(700f, 26f), new Vector2(0f, 158f));
            AddOutline(hintText.gameObject, 1.6f, 0.75f);
            hintGroup = hintText.gameObject.AddComponent<CanvasGroup>();
            hintGroup.alpha = 0f;
            hintText.text = "click to continue";

            ApplyChoices(null);
        }

        private void Update()
        {
            if (canvasGroup == null)
                return;

            ShakeShout();

            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 7f);

            if (hintGroup != null)
            {
                hintGroup.alpha = Mathf.MoveTowards(hintGroup.alpha, hintTargetAlpha,
                    Time.unscaledDeltaTime * 4f);
                // Pulsar devagar: chama a atencao sem piscar na cara do jogador.
                if (hintTargetAlpha > 0f)
                    hintGroup.alpha *= 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * 2.4f);
            }

            for (int i = 0; i < choiceRects.Length; i++)
            {
                if (choiceRects[i] == null)
                    continue;

                CanvasGroup group = choiceRects[i].GetComponent<CanvasGroup>();
                float delay = i * 0.09f;
                float wanted = Mathf.Clamp01(choiceTargetAlpha * 1.3f - delay);
                group.alpha = Mathf.MoveTowards(group.alpha, wanted, Time.unscaledDeltaTime * 8f);
                // Entrada curta a deslizar da esquerda, em vez de escalar uma caixa.
                float slide = Mathf.Lerp(-14f, 0f, group.alpha);
                choiceRects[i].anchoredPosition = new Vector2(slide, ChoiceY(i));
            }
        }

        public void Show(string speaker)
        {
            Build();
            speakerText.text = string.IsNullOrWhiteSpace(speaker) ? string.Empty : speaker.ToUpperInvariant();
            lineText.text = string.Empty;
            if (lineBacking != null) lineBacking.enabled = false;
            targetAlpha = 1f;
            choiceTargetAlpha = 0f;
            hintTargetAlpha = 0f;
            hoveredChoice = -1;
            RefreshChoiceColours();
        }

        public void Hide()
        {
            targetAlpha = 0f;
            choiceTargetAlpha = 0f;
            hintTargetAlpha = 0f;
            hoveredChoice = -1;
        }

        /// <summary>Mostra "[E] continue" quando o avanco depende mesmo do jogador.</summary>
        public void SetContinueHint(bool visible)
        {
            Build();
            hintTargetAlpha = visible ? 1f : 0f;
            if (!visible && hintGroup != null) hintGroup.alpha = 0f;
        }

        public void SetLine(string value)
        {
            Build();
            // Texto limpo, sem uma unica tag. Ver a nota do `lineBacking`.
            lineText.text = value ?? string.Empty;
            shouting = IsShout(lineText.text);
            FitBacking();
            FollowSpeakerToText();
        }

        /// <summary>
        /// Uma fala escrita toda em maiusculas e uma fala gritada.
        ///
        /// **A intencao ja esta nos dados.** A alternativa era um campo `bool` novo
        /// em cada asset e um parametro a atravessar `ShowThought`, `ShowReaction` e
        /// `BeginChoice` — quatro sitios para esquecer de ligar. Quem escreve
        /// `YOU SCARED ME` ja disse o que queria dizer; isto so o ouve.
        ///
        /// Precisa de letras suficientes para nao apanhar um `OK` ou um `3B`.
        /// </summary>
        private static bool IsShout(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;

            int letters = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsLetter(value[i])) continue;
                if (char.IsLower(value[i])) return false;
                letters++;
            }

            return letters >= 4;
        }

        /// <summary>
        /// O tremor de uma fala gritada, letra a letra.
        ///
        /// Abanar a caixa inteira lia-se como a interface a partir-se; abanar cada
        /// letra por sua conta le-se como uma voz. Cada caractere tem a sua fase,
        /// tirada do indice, para nao andarem todos ao mesmo tempo.
        /// </summary>
        private void ShakeShout()
        {
            if (lineText == null) return;

            if (!shouting)
            {
                if (!shakeApplied) return;
                lineText.ForceMeshUpdate();
                shakeApplied = false;
                return;
            }

            lineText.ForceMeshUpdate();
            var textInfo = lineText.textInfo;
            if (textInfo == null || textInfo.characterCount == 0) return;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                var character = textInfo.characterInfo[i];
                if (!character.isVisible) continue;

                int material = character.materialReferenceIndex;
                int vertex = character.vertexIndex;
                var vertices = textInfo.meshInfo[material].vertices;

                float phase = i * 0.7f + Time.unscaledTime * shoutShakeSpeed;
                var offset = new Vector3(
                    Mathf.Sin(phase) * shoutShakeAmount,
                    Mathf.Cos(phase * 1.3f) * shoutShakeAmount,
                    0f);

                for (int v = 0; v < 4; v++) vertices[vertex + v] += offset;
            }

            for (int m = 0; m < textInfo.meshInfo.Length; m++)
            {
                textInfo.meshInfo[m].mesh.vertices = textInfo.meshInfo[m].vertices;
                lineText.UpdateGeometry(textInfo.meshInfo[m].mesh, m);
            }

            shakeApplied = true;
        }

        /// <summary>
        /// Poe o nome logo por cima da fala, seja ela de uma linha ou de quatro.
        ///
        /// O nome estava parado a uma altura fixa, calculada para o pior caso — uma
        /// fala de quatro linhas. Como o texto e alinhado **em baixo** e cresce para
        /// cima, uma fala de uma linha so deixava cento e oitenta pixeis de vazio
        /// entre "RUI" e o que o Rui diz. E quase todas as falas do jogo tem uma
        /// linha.
        ///
        /// O alinhamento em baixo nao muda e nao devia: e o que mantem a base do
        /// texto sempre no mesmo sitio, para a caixa nao saltar no ecra de fala para
        /// fala. O que passa a mexer-se e o nome, que e o unico que pode.
        ///
        /// `GetPreferredValues` e usado em vez do `preferredHeight` porque este
        /// ultimo depende de a malha ja ter sido reconstruida, e aqui a fala acabou
        /// de ser escrita neste mesmo frame — pela maquina de escrever, caractere a
        /// caractere. Media pela altura do frame anterior, e o nome andava sempre uma
        /// letra atrasado.
        /// </summary>
        private void FollowSpeakerToText()
        {
            if (speakerText == null || lineText == null) return;

            RectTransform lineRect = lineText.rectTransform;
            float width = lineRect.sizeDelta.x;
            if (width <= 1f) return;

            float height = lineText.GetPreferredValues(lineText.text, width, 0f).y;
            if (float.IsNaN(height) || height < 0f) return;

            // O fundo da caixa da fala, mais o que ela ocupa mesmo, mais uma folga.
            float top = lineRect.anchoredPosition.y + Mathf.Min(height, lineRect.sizeDelta.y);

            RectTransform nameRect = speakerText.rectTransform;
            Vector2 position = nameRect.anchoredPosition;
            position.y = top + SpeakerGap;
            nameRect.anchoredPosition = position;
        }

        /// <summary>
        /// Pensamento do protagonista: mesma zona do ecra, mas em italico, mais
        /// pequeno e mais frio, para nunca se confundir com a fala de um NPC.
        /// </summary>
        public void SetThoughtMode(bool thought)
        {
            Build();
            // **Sem italico e sem negrito.**
            //
            // O italico do TMP sem uma fonte italica a serio e uma inclinacao
            // aplicada por cima dos glifos, e a 33 pontos com contorno ficava uma
            // coisa a cair para a direita que parece um titulo de telenovela. O
            // negrito da fala fazia o mesmo do outro lado.
            //
            // A diferenca entre pensar e falar passa a ser o que ja era suficiente
            // e nunca chamou a atencao a si proprio: corpo mais pequeno, fundo mais
            // leve e nenhum nome por cima.
            lineText.fontStyle = FontStyles.Normal;
            lineText.fontSize = thought ? 29f : 36f;
            lineText.color = thought ? ThoughtColour : LineColour;
            lineBacking.color = thought ? ThoughtHighlightColour : HighlightColour;

            // O contorno vinha do preset da fonte e era grosso e azulado — a jogar
            // le-se como WordArt. Com o realce por tras, nao faz falta nenhum.
            lineText.outlineWidth = 0f;
            lineText.characterSpacing = 0f;

            FitBacking();

        }

        /// <summary>
        /// Encosta o realce ao que ja foi escrito.
        ///
        /// As `bounds` do TMP so estao certas depois de a malha ser gerada, e ela e
        /// gerada no fim do frame — por isso o `ForceMeshUpdate`. Sem ele, o
        /// rectangulo ficava sempre uma letra atras, o que a 34 caracteres por
        /// segundo se ve.
        /// </summary>
        private void FitBacking()
        {
            if (lineBacking == null || lineText == null) return;

            if (string.IsNullOrEmpty(lineText.text))
            {
                lineBacking.enabled = false;
                return;
            }

            lineText.ForceMeshUpdate();
            float width = lineText.preferredWidth;
            float height = lineText.preferredHeight;

            if (width <= 0.01f || height <= 0.01f)
            {
                lineBacking.enabled = false;
                return;
            }

            lineBacking.enabled = true;

            // Os dois rects partilham ancora e pivo no canto inferior esquerdo, e o
            // texto e alinhado por baixo a esquerda. Com isso, o canto de um e o
            // canto do outro e a conta e uma subtraccao — nada de `bounds.center`,
            // que vem em espaco local do texto e foi o que atirou o rectangulo para
            // cima e para a direita das letras.
            var line = lineText.rectTransform;
            var rect = lineBacking.rectTransform;
            rect.sizeDelta = new Vector2(width, height) + HighlightPadding * 2f;
            rect.anchoredPosition = line.anchoredPosition - HighlightPadding;
        }

        public void SetChoices(string first, string second) => SetChoices(new[] { first, second });

        public void SetChoices(string[] values)
        {
            Build();
            ApplyChoices(values);
        }

        private void ApplyChoices(string[] values)
        {
            int count = 0;
            for (int i = 0; i < choiceRects.Length; i++)
            {
                string value = values != null && i < values.Length ? values[i] : null;
                bool visible = !string.IsNullOrWhiteSpace(value);
                choiceValues[i] = visible ? value.ToUpperInvariant() : string.Empty;
                if (visible) count++;
            }

            // As visiveis contam-se antes de as posicionar: a pilha cresce para
            // cima a partir da mesma base, para a linha de baixo nao mudar de sitio
            // conforme a fala tenha duas respostas ou tres.
            activeChoiceCount = count;

            for (int i = 0; i < choiceRects.Length; i++)
            {
                bool visible = !string.IsNullOrEmpty(choiceValues[i]);
                choiceRects[i].gameObject.SetActive(visible);
                if (visible)
                    choiceRects[i].anchoredPosition =
                        new Vector2(choiceRects[i].anchoredPosition.x, ChoiceY(i));
            }

            choiceTargetAlpha = count > 0 ? 1f : 0f;
            RefreshChoiceColours();
        }

        public int GetHoveredChoice(Vector2 screenPosition)
        {
            for (int i = 0; i < choiceRects.Length; i++)
            {
                if (choiceRects[i] != null && choiceRects[i].gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(choiceRects[i], screenPosition))
                    return i;
            }

            return -1;
        }

        public void SetHoveredChoice(int index)
        {
            if (hoveredChoice == index)
                return;

            hoveredChoice = index;
            RefreshChoiceColours();
        }

        /// <summary>
        /// A pilha cresce para cima: a ultima opcao fica sempre na mesma linha,
        /// independentemente de haver duas ou tres. Com a base a mexer, o olho
        /// perdia a referencia entre falas.
        /// </summary>
        private float ChoiceY(int index)
        {
            int count = Mathf.Max(1, activeChoiceCount);
            return (count - 1 - index) * 54f;
        }

        private void CreateChoice(int index, Transform parent, Font font)
        {
            Text label = CreateText($"Choice_{index + 1}", parent, font, 27, FontStyle.Normal,
                ChoiceColour, TextAnchor.MiddleLeft);
            choiceTexts[index] = label;
            choiceRects[index] = label.rectTransform;
            SetRect(label.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(1100f, 46f), new Vector2(0f, ChoiceY(index)));
            AddOutline(label.gameObject, 1.8f, 0.9f);
            label.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        }

        private void RefreshChoiceColours()
        {
            for (int i = 0; i < choiceTexts.Length; i++)
            {
                if (choiceTexts[i] == null)
                    continue;

                bool highlighted = i == hoveredChoice;
                choiceTexts[i].color = highlighted ? ChoiceHoverColour : ChoiceColour;
                // O marcador substitui o realce por caixa.
                string marker = highlighted ? "›" : " ";
                string key = highlighted ? "#FFFFFF" : "#7C7768";
                choiceTexts[i].text = string.IsNullOrEmpty(choiceValues[i])
                    ? string.Empty
                    : $"{marker} <color={key}>{i + 1}</color>   {choiceValues[i]}";
            }
        }

        private bool ReferencesAreValid()
        {
            return root != null && speakerText != null && lineText != null &&
                   lineBacking != null && hintText != null && hintGroup != null &&
                   AllChoiceReferencesBound();
        }

        private bool AllChoiceReferencesBound()
        {
            for (int i = 0; i < choiceRects.Length; i++)
                if (choiceRects[i] == null || choiceTexts[i] == null) return false;
            return true;
        }

        private bool TryRebindExistingView()
        {
            Transform canvas = transform.Find(CanvasName);
            Transform existingRoot = canvas != null ? canvas.Find("DialogueRoot") : null;
            if (existingRoot == null)
                return false;

            root = existingRoot as RectTransform;
            canvasGroup = existingRoot.GetComponent<CanvasGroup>();
            Transform speaker = existingRoot.Find("Speaker");
            Transform line = existingRoot.Find("Line");
            speakerText = speaker != null ? speaker.GetComponent<Text>() : null;
            // Um canvas antigo tem aqui um `Text` legacy e devolve null: o
            // `ReferencesAreValid` recusa, e o painel e reconstruido de raiz. E o
            // que se quer — a camada de realce nao existia nesses.
            lineText = line != null ? line.GetComponent<TextMeshProUGUI>() : null;
            Transform backing = existingRoot.Find("LineBacking");
            lineBacking = backing != null ? backing.GetComponent<Image>() : null;
            Transform hint = existingRoot.Find("ContinueHint");
            hintText = hint != null ? hint.GetComponent<Text>() : null;
            hintGroup = hint != null ? hint.GetComponent<CanvasGroup>() : null;

            for (int i = 0; i < choiceRects.Length; i++)
            {
                Transform choice = existingRoot.Find($"Choice_{i + 1}");
                choiceRects[i] = choice as RectTransform;
                choiceTexts[i] = choice != null ? choice.GetComponent<Text>() : null;
            }

            return canvasGroup != null && ReferencesAreValid();
        }

        private void DestroyChild(string name)
        {
            Transform stale = transform.Find(name);
            if (stale == null) return;
            stale.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(stale.gameObject);
            else DestroyImmediate(stale.gameObject);
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject result = new GameObject(name, typeof(RectTransform));
            result.transform.SetParent(parent, false);
            return result;
        }

        /// <summary>
        /// Um texto TMP com as mesmas medidas do legacy ao lado.
        ///
        /// A fonte e o material sao atribuidos em conjunto. O `defaultFontAsset`
        /// deste projecto usa o shader TMP de superficie, que recebe iluminacao e
        /// deixa as letras de UI negras/azuladas. O preset Overlay partilha o mesmo
        /// atlas e desenha a cor pedida sem luz nem contorno escondido.
        /// </summary>
        private static TextMeshProUGUI CreateTmpText(string name, Transform parent,
            float size, Color colour)
        {
            GameObject result = CreateUiObject(name, parent);
            var text = result.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(DialogueFontResource);
            Material material = Resources.Load<Material>(DialogueMaterialResource);
            if (font != null) text.font = font;
            if (material != null) text.fontSharedMaterial = material;
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAlignmentOptions.BottomLeft;
            text.enableWordWrapping = true;   // TMP 3.0.7: o `textWrappingMode` so chega no 3.2
            text.overflowMode = TextOverflowModes.Overflow;
            text.lineSpacing = 6f;
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }

        private static Text CreateText(string name, Transform parent, Font font, int size,
            FontStyle style, Color colour, TextAnchor alignment)
        {
            GameObject result = CreateUiObject(name, parent);
            Text text = result.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = colour;
            text.alignment = alignment;
            text.supportRichText = true;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Contorno preto: substitui a caixa de fundo como garantia de leitura.</summary>
        private static void AddOutline(GameObject target, float distance, float alpha)
        {
            Outline outline = target.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, alpha);
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;

            Shadow shadow = target.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, alpha * 0.6f);
            shadow.effectDistance = new Vector2(distance * 1.6f, -distance * 1.6f);
            shadow.useGraphicAlpha = true;
        }

        private static Font LoadRuntimeFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
                return font;

            return Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 24);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
