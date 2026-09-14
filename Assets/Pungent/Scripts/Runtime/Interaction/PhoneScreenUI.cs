using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Pungent.Interaction
{
    /// <summary>
    /// Ecrã do telemóvel diegético: um Canvas em world-space colado à face do
    /// modelo, em vez de OnGUI desenhado por cima do jogo.
    ///
    /// Os cantos arredondados são gerados em memória como sprite 9-slice, o que
    /// evita depender de assets de UI e permite qualquer tamanho de balão.
    /// A interface é original: cabeçalho fino, balões alinhados por remetente e
    /// respostas rápidas em baixo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PhoneScreenUI : MonoBehaviour
    {
        private const int CanvasWidth = 360;
        private const int CanvasHeight = 720;

        private static readonly Color ScreenColour = new Color(0.055f, 0.062f, 0.075f, 1f);
        private static readonly Color BubbleThem = new Color(0.16f, 0.18f, 0.21f, 1f);
        private static readonly Color BubbleMine = new Color(0.16f, 0.34f, 0.44f, 1f);
        private static readonly Color TextColour = new Color(0.93f, 0.94f, 0.95f, 1f);
        private static readonly Color MutedColour = new Color(0.55f, 0.58f, 0.62f, 1f);
        private static readonly Color ReplyIdle = new Color(0.13f, 0.15f, 0.18f, 1f);
        private static readonly Color ReplyHot = new Color(0.24f, 0.42f, 0.50f, 1f);

        private RectTransform root;
        private RectTransform thread;
        private Text clockText;
        private Text contactText;
        private Text typingText;
        private readonly List<RectTransform> bubbles = new List<RectTransform>();
        private readonly RectTransform[] replyRects = new RectTransform[2];
        private readonly Text[] replyTexts = new Text[2];
        private readonly Image[] replyImages = new Image[2];

        private Sprite roundedSprite;
        private Font font;
        private float threadHeight;
        private int hovered = -1;

        // --- lista de conversas ---
        private RectTransform inbox;
        private RectTransform backButton;
        private Image backImage;
        private readonly List<RectTransform> rowRects = new List<RectTransform>();
        private readonly List<Image> rowImages = new List<Image>();
        private int hoveredRow = -1;

        private static readonly Color UnreadDot = new Color(0.36f, 0.72f, 0.86f, 1f);

        // Mais claro que os baloes: contra um ecra escuro o botao de voltar tem de
        // se ver de relance, senao o jogador julga que nao ha maneira de sair.
        private static readonly Color BackIdle = new Color(0.22f, 0.25f, 0.30f, 1f);

        /// <summary>Uma linha da lista de conversas.</summary>
        public struct InboxRow
        {
            public string Contact;
            public string Preview;
            public bool Unread;
        }

        public RectTransform Root => root;

        public void Build(Transform parent, float worldWidth)
        {
            if (root != null) return;

            font = LoadFont();
            roundedSprite = CreateRoundedSprite(48, 14);

            var canvasObject = new GameObject("PhoneScreenCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(parent, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            root = canvasObject.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            // O canvas é medido em pixéis; a escala converte-o para metros.
            float scale = worldWidth / CanvasWidth;
            root.localScale = Vector3.one * scale;
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;

            var background = Panel(root, "Screen", ScreenColour);
            Stretch(background.rectTransform, 0f, 0f, 0f, 0f);

            // --- barra de estado ---
            clockText = Label(root, "Clock", 22, FontStyle.Bold, MutedColour, TextAnchor.MiddleCenter);
            Anchor(clockText.rectTransform, new Vector2(0.5f, 1f), new Vector2(300f, 30f), new Vector2(0f, -26f));

            // --- cabeçalho do contacto ---
            contactText = Label(root, "Contact", 30, FontStyle.Bold, TextColour, TextAnchor.MiddleLeft);
            Anchor(contactText.rectTransform, new Vector2(0.5f, 1f), new Vector2(300f, 40f), new Vector2(0f, -66f));

            var divider = Panel(root, "Divider", new Color(1f, 1f, 1f, 0.09f));
            Anchor(divider.rectTransform, new Vector2(0.5f, 1f), new Vector2(320f, 2f), new Vector2(0f, -94f));

            // --- área de mensagens ---
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(root, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            Anchor(viewportRect, new Vector2(0.5f, 1f), new Vector2(330f, 470f), new Vector2(0f, -340f));

            thread = new GameObject("Thread", typeof(RectTransform)).GetComponent<RectTransform>();
            thread.SetParent(viewport.transform, false);
            thread.anchorMin = new Vector2(0f, 1f);
            thread.anchorMax = new Vector2(1f, 1f);
            thread.pivot = new Vector2(0.5f, 1f);
            thread.sizeDelta = new Vector2(0f, 0f);
            thread.anchoredPosition = Vector2.zero;

            typingText = Label(root, "Typing", 20, FontStyle.Italic, MutedColour, TextAnchor.MiddleLeft);
            Anchor(typingText.rectTransform, new Vector2(0.5f, 1f), new Vector2(320f, 26f), new Vector2(0f, -590f));
            typingText.text = string.Empty;

            // --- respostas rápidas ---
            for (int i = 0; i < 2; i++)
                BuildReply(i);

            SetReplies(null, null);

            // --- lista de conversas ---
            // Ocupa a mesma area das mensagens: so uma das duas esta visivel.
            inbox = new GameObject("Inbox", typeof(RectTransform)).GetComponent<RectTransform>();
            inbox.SetParent(root, false);
            Anchor(inbox, new Vector2(0.5f, 1f), new Vector2(330f, 500f), new Vector2(0f, -355f));

            // Voltar: no cabecalho, do lado esquerdo do nome do contacto.
            //
            // Generoso de proposito. O canvas tem 360 px de largura mas o aparelho
            // ocupa uma fatia pequena do ecra: a escala ronda os 0.5 px de ecra por
            // px de canvas, portanto um botao de 44x34 no canvas sai a 23x18 reais.
            // Isso e um alvo que ninguem acerta.
            backImage = Panel(root, "Back", BackIdle);
            backImage.sprite = roundedSprite;
            backImage.type = Image.Type.Sliced;
            backButton = backImage.rectTransform;
            Anchor(backButton, new Vector2(0f, 1f), new Vector2(124f, 44f), new Vector2(70f, -66f));

            var backLabel = Label(backButton, "Label", 21, FontStyle.Bold, TextColour, TextAnchor.MiddleCenter);
            backLabel.text = "‹  Back";
            Stretch(backLabel.rectTransform, 0f, 0f, 0f, 0f);
        }

        private void BuildReply(int index)
        {
            var image = Panel(root, $"Reply_{index + 1}", ReplyIdle);
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            replyImages[index] = image;
            replyRects[index] = image.rectTransform;
            Anchor(replyRects[index], new Vector2(0.5f, 0f), new Vector2(320f, 56f),
                new Vector2(0f, 84f - index * 64f));

            var label = Label(replyRects[index], "Label", 21, FontStyle.Normal, TextColour, TextAnchor.MiddleLeft);
            replyTexts[index] = label;
            Stretch(label.rectTransform, 16f, 42f, 4f, 4f);

            var key = Label(replyRects[index], "Key", 18, FontStyle.Bold, MutedColour, TextAnchor.MiddleRight);
            key.text = (index + 1).ToString();
            Anchor(key.rectTransform, new Vector2(1f, 0.5f), new Vector2(26f, 26f), new Vector2(-14f, 0f));
        }

        // ------------------------------------------------------------------

        public void SetHeader(string contact, string clock)
        {
            if (contactText != null) contactText.text = contact;
            if (clockText != null) clockText.text = clock;
        }

        /// <summary>
        /// So as horas. O cabecalho e reescrito quando o ecra muda de conversa, e o
        /// relogio tem de andar sem esperar por isso.
        /// </summary>
        public void SetClock(string clock)
        {
            if (clockText != null) clockText.text = clock;
        }

        // ------------------------------------------------------------------
        // Lista de conversas
        //
        // Sem ela o telemovel mostrava sempre o fio de actividade mais recente, e
        // uma mensagem do Pai roubava o ecra a conversa do Rui a meio. Com dois
        // contactos isso deixou de ser aceitavel: quem escolhe o que ler e o jogador.
        // ------------------------------------------------------------------

        /// <summary>Mostra a lista de contactos e esconde a conversa.</summary>
        public void ShowInbox(IReadOnlyList<InboxRow> rows)
        {
            if (thread != null) thread.gameObject.SetActive(false);
            if (backButton != null) backButton.gameObject.SetActive(false);
            SetTyping(string.Empty);
            SetReplies(null, null);

            if (contactText != null)
            {
                contactText.text = "Messages";
                contactText.rectTransform.sizeDelta = new Vector2(300f, 40f);
                contactText.rectTransform.anchoredPosition = new Vector2(0f, -66f);
            }

            if (inbox != null) inbox.gameObject.SetActive(true);
            BuildRows(rows);
        }

        /// <summary>Mostra a conversa de um contacto e esconde a lista.</summary>
        public void ShowThread(string contact)
        {
            ClearRows();
            if (inbox != null) inbox.gameObject.SetActive(false);
            if (thread != null) thread.gameObject.SetActive(true);
            if (backButton != null) backButton.gameObject.SetActive(true);

            if (contactText != null)
            {
                contactText.text = contact;
                // Estreitado e empurrado para a direita: o botao de voltar ocupa
                // agora a esquerda do cabecalho ate x = 132.
                contactText.rectTransform.sizeDelta = new Vector2(200f, 40f);
                contactText.rectTransform.anchoredPosition = new Vector2(80f, -66f);
            }
        }

        private void BuildRows(IReadOnlyList<InboxRow> rows)
        {
            ClearRows();
            if (rows == null || inbox == null) return;

            const float height = 78f;
            const float gap = 8f;

            for (int i = 0; i < rows.Count; i++)
            {
                InboxRow row = rows[i];

                var image = Panel(inbox, $"Row_{i}", ReplyIdle);
                image.sprite = roundedSprite;
                image.type = Image.Type.Sliced;
                Anchor(image.rectTransform, new Vector2(0.5f, 1f), new Vector2(320f, height),
                    new Vector2(0f, -(height * 0.5f + i * (height + gap))));

                var name = Label(image.rectTransform, "Contact", 24, FontStyle.Bold,
                    TextColour, TextAnchor.MiddleLeft);
                name.text = row.Contact;
                Anchor(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(250f, 28f),
                    new Vector2(-14f, -22f));

                var preview = Label(image.rectTransform, "Preview", 18, FontStyle.Normal,
                    MutedColour, TextAnchor.MiddleLeft);
                preview.text = Shorten(row.Preview, 38);
                preview.horizontalOverflow = HorizontalWrapMode.Overflow;
                Anchor(preview.rectTransform, new Vector2(0.5f, 1f), new Vector2(250f, 24f),
                    new Vector2(-14f, -52f));

                if (row.Unread)
                {
                    var dot = Panel(image.rectTransform, "Unread", UnreadDot);
                    dot.sprite = roundedSprite;
                    dot.type = Image.Type.Sliced;
                    Anchor(dot.rectTransform, new Vector2(1f, 1f), new Vector2(14f, 14f),
                        new Vector2(-20f, -24f));
                }

                var key = Label(image.rectTransform, "Key", 18, FontStyle.Bold,
                    MutedColour, TextAnchor.MiddleRight);
                key.text = (i + 1).ToString();
                Anchor(key.rectTransform, new Vector2(1f, 1f), new Vector2(26f, 24f),
                    new Vector2(-18f, -52f));

                rowRects.Add(image.rectTransform);
                rowImages.Add(image);
            }
        }

        private void ClearRows()
        {
            foreach (var r in rowRects)
            {
                if (r == null) continue;
                // Mesmo cuidado que em ClearThread: Destroy so age no fim do frame.
                r.gameObject.SetActive(false);
                r.SetParent(null, false);
                Object.Destroy(r.gameObject);
            }
            rowRects.Clear();
            rowImages.Clear();
            hoveredRow = -1;
        }

        /// <summary>Que linha da lista esta sob o cursor, ou -1.</summary>
        public int GetRowUnderPointer(Vector2 screenPosition, Camera camera)
        {
            for (int i = 0; i < rowRects.Count; i++)
            {
                if (rowRects[i] == null || !rowRects[i].gameObject.activeInHierarchy) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(rowRects[i], screenPosition, camera))
                    return i;
            }
            return -1;
        }

        public void SetRowHovered(int index)
        {
            hoveredRow = index;
            for (int i = 0; i < rowImages.Count; i++)
                if (rowImages[i] != null)
                    rowImages[i].color = i == hoveredRow ? ReplyHot : ReplyIdle;
        }

        /// <summary>O cursor esta sobre o botao de voltar?</summary>
        public bool IsBackUnderPointer(Vector2 screenPosition, Camera camera)
        {
            if (backButton == null || !backButton.gameObject.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(backButton, screenPosition, camera);
        }

        public void SetBackHovered(bool value)
        {
            if (backImage != null) backImage.color = value ? ReplyHot : BackIdle;
        }

        private static string Shorten(string value, int limit)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            value = value.Replace("\n", " ");
            return value.Length <= limit ? value : value.Substring(0, limit - 1).TrimEnd() + "…";
        }

        public void ClearThread()
        {
            foreach (var b in bubbles)
            {
                if (b == null) continue;

                // Object.Destroy so remove o objeto no fim do frame. Como o
                // threadHeight volta a zero aqui e os baloes novos sao criados
                // logo a seguir, os antigos ficavam a desenhar por baixo deles
                // durante esse frame — baloes sobrepostos, e a mensagem do fio
                // anterior visivel debaixo do cabecalho do fio novo.
                //
                // Desligar e tirar do pai tem efeito imediato; o Destroy fica a
                // tratar da limpeza a seguir.
                b.gameObject.SetActive(false);
                b.SetParent(null, false);
                Object.Destroy(b.gameObject);
            }
            bubbles.Clear();
            threadHeight = 0f;
        }

        /// <summary>Acrescenta um balão. <paramref name="mine"/> alinha-o à direita.</summary>
        public void AddBubble(string text, bool mine)
        {
            const float maxWidth = 250f;
            const float padding = 18f;

            var image = Panel(thread, mine ? "Bubble_Mine" : "Bubble_Them", mine ? BubbleMine : BubbleThem);
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;

            var label = Label(image.rectTransform, "Text", 21, FontStyle.Normal, TextColour,
                mine ? TextAnchor.UpperRight : TextAnchor.UpperLeft);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = text;

            // Mede o texto para o balão acompanhar o conteúdo em vez de ter altura fixa.
            var settings = label.GetGenerationSettings(new Vector2(maxWidth - padding * 2f, 0f));
            float textHeight = label.cachedTextGeneratorForLayout.GetPreferredHeight(text, settings)
                               / label.pixelsPerUnit;
            float textWidth = Mathf.Min(maxWidth - padding * 2f,
                label.cachedTextGeneratorForLayout.GetPreferredWidth(text, settings) / label.pixelsPerUnit);

            float bubbleWidth = textWidth + padding * 2f;
            float bubbleHeight = textHeight + padding * 1.4f;

            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(mine ? 1f : 0f, 1f);
            rect.anchorMax = new Vector2(mine ? 1f : 0f, 1f);
            rect.pivot = new Vector2(mine ? 1f : 0f, 1f);
            rect.sizeDelta = new Vector2(bubbleWidth, bubbleHeight);
            rect.anchoredPosition = new Vector2(mine ? -6f : 6f, -threadHeight);

            Stretch(label.rectTransform, padding, padding, padding * 0.7f, padding * 0.7f);

            threadHeight += bubbleHeight + 12f;
            bubbles.Add(rect);
            ScrollToBottom();
        }

        public void SetTyping(string value)
        {
            if (typingText != null) typingText.text = value ?? string.Empty;
        }

        public void SetReplies(string first, string second)
        {
            string[] values = { first, second };
            for (int i = 0; i < replyRects.Length; i++)
            {
                bool visible = !string.IsNullOrWhiteSpace(values[i]);
                if (replyRects[i] != null) replyRects[i].gameObject.SetActive(visible);
                // replyTexts tinha de ser verificado como o replyRects acima: um
                // domain reload a meio do Play esvazia estes arrays e o widget
                // rebentava aqui em vez de simplesmente nao desenhar.
                if (visible && replyTexts[i] != null) replyTexts[i].text = values[i];
            }
            SetHovered(-1);
        }

        public void SetHovered(int index)
        {
            hovered = index;
            for (int i = 0; i < replyImages.Length; i++)
            {
                if (replyImages[i] == null) continue;
                replyImages[i].color = i == hovered ? ReplyHot : ReplyIdle;
                if (replyTexts[i] != null)
                    replyTexts[i].color = i == hovered ? Color.white : TextColour;
            }
        }

        /// <summary>Qual resposta está sob o cursor, ou -1.</summary>
        public int GetReplyUnderPointer(Vector2 screenPosition, Camera camera)
        {
            for (int i = 0; i < replyRects.Length; i++)
            {
                if (replyRects[i] == null || !replyRects[i].gameObject.activeInHierarchy) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(replyRects[i], screenPosition, camera))
                    return i;
            }
            return -1;
        }

        private void ScrollToBottom()
        {
            if (thread == null) return;
            const float viewportHeight = 470f;
            float overflow = Mathf.Max(0f, threadHeight - viewportHeight);
            thread.anchoredPosition = new Vector2(0f, overflow);
        }

        // ------------------------------------------------------------------
        // Construção de widgets
        // ------------------------------------------------------------------

        private Image Panel(Transform parent, string name, Color colour)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        private Text Label(Transform parent, string name, int size, FontStyle style, Color colour, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = colour;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>
        /// Retângulo de cantos arredondados gerado em memória, usado como 9-slice.
        /// Evita depender de sprites externos e serve qualquer tamanho de balão.
        /// </summary>
        private static Sprite CreateRoundedSprite(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, 0f, x - (size - 1 - radius));
                    float dy = Mathf.Max(radius - y, 0f, y - (size - 1 - radius));
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static Font LoadFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return f != null ? f : Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 22);
        }
    }
}
