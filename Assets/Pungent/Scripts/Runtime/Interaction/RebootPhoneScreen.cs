using System.Collections.Generic;
using Pungent.Atmosphere;
using Pungent.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Pungent.Interaction
{
    /// <summary>
    /// What is on the phone: a list of conversations, and one conversation open.
    ///
    /// Two screens and nothing else. The old phone had a home screen, apps, a
    /// gallery and a settings page, and the player only ever used it to read what
    /// the father wrote. A phone with two screens is a phone nobody has to learn.
    ///
    /// **Drawn on the model, not over it.** The panel is a child of the phone mesh,
    /// sitting a fraction proud of the glass, so the text tilts and travels with
    /// the object. That is the point of holding it: reading costs you the corner of
    /// the room the phone covers.
    ///
    /// **It has to look like a phone.** A single block of left-aligned text reads
    /// as a debug overlay, and a debug overlay in the player's hand is not a phone
    /// however correctly it is positioned. Hence a header bar, and messages in
    /// bubbles that take sides — his father's on the left, his own on the right.
    /// The side is the only thing telling the player who said what without a line
    /// of text spending screen width to say it.
    ///
    /// Content comes from <see cref="PhoneMessageService"/>, which already owns
    /// threads, delays, typing and choices. This component only draws and takes
    /// key presses.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RebootPhoneScreen : MonoBehaviour
    {
        private enum View { List, Thread }

        [Header("Scene references")]
        [SerializeField] private RebootPhone phone;
        [SerializeField] private PhoneMessageService service;
        [SerializeField] private PlayerInputReader input;

        [Tooltip("The phone mesh. The panel is built as a child of this, so the "
               + "measurements below are in the model's own space.")]
        [SerializeField] private Transform screenAnchor;

        [Header("Screen face")]
        [SerializeField] private Vector3 screenLocalPosition = new Vector3(0f, 0.0732f, 0.0060f);

        [Tooltip("Canvas units, one tenth of a millimetre each. Slightly smaller "
               + "than the glass: the panel has square corners and the phone does "
               + "not, so at full size the corners poke past the shell.")]
        [SerializeField] private Vector2 screenSize = new Vector2(627f, 1360f);
        [SerializeField, Min(0.00001f)] private float screenScale = 0.0001f;

        [Header("Look")]
        [SerializeField] private Color glassColour = new Color(0.055f, 0.062f, 0.075f, 1f);
        [SerializeField] private Color barColour = new Color(0.105f, 0.115f, 0.135f, 1f);
        [SerializeField] private Color theirBubble = new Color(0.165f, 0.180f, 0.205f, 1f);
        [SerializeField] private Color myBubble = new Color(0.145f, 0.310f, 0.420f, 1f);
        [SerializeField] private Color textColour = new Color(0.88f, 0.90f, 0.92f, 1f);
        [SerializeField] private Color dimColour = new Color(0.46f, 0.50f, 0.55f, 1f);
        [SerializeField] private Color accentColour = new Color(0.35f, 0.62f, 0.92f, 1f);
        [SerializeField, Min(8)] private int fontSize = 52;

        [Tooltip("How many messages a thread shows. Older ones scroll off the top; "
               + "the phone is not an archive.")]
        [SerializeField, Min(2)] private int historyLines = 7;

        [Header("VHS")]
        [Tooltip("The full-screen VHS pass eats small text. It can hold a clean "
               + "window open, and this is what asks for one over the glass.")]
        [SerializeField] private Camera playerCamera;
        [SerializeField, Range(0f, 0.05f)] private float clarityPadding = 0.008f;

        [Header("Aviso no HUD")]
        [Tooltip("Onde aparece o aviso de mensagem por ler, por baixo dos "
               + "objectivos.\n\nExiste porque este telemóvel não toca: uma "
               + "mensagem que chega enquanto o jogador está de costas para ele "
               + "não existe até alguém lhe dizer que existe.")]
        [SerializeField] private PrototypeHUD hud;

        [SerializeField] private string noticeText = "New message  [TAB]";

        [Header("Chegada de mensagem")]
        [Tooltip("2D de propósito: o telemóvel está no bolso dele, não do outro "
               + "lado da sala.")]
        [SerializeField] private AudioSource notificationSource;
        [SerializeField] private AudioClip notificationClip;
        [SerializeField, Range(0f, 1f)] private float notificationVolume = 0.7f;

        private Canvas canvas;
        private RectTransform canvasRect;
        private RectTransform content;
        private Text headerLabel;
        private Text footerLabel;
        private Font font;
        private Sprite roundedSprite;

        private View view = View.List;
        private PhoneMessageService.Thread openThread;
        private string drawnSignature;
        private readonly Vector3[] corners = new Vector3[4];
        private readonly List<GameObject> rows = new List<GameObject>();

        private void Awake()
        {
            if (phone == null) phone = GetComponentInParent<RebootPhone>();
            if (input == null) input = FindObjectOfType<PlayerInputReader>();
            if (service == null) service = FindObjectOfType<PhoneMessageService>();
            if (playerCamera == null) playerCamera = Camera.main;
            if (hud == null) hud = FindObjectOfType<PrototypeHUD>();

            if (phone == null || input == null || service == null || screenAnchor == null)
            {
                Debug.LogError("[RebootPhoneScreen] Missing required references.", this);
                enabled = false;
                return;
            }

            BuildCanvas();
            phone.BackRequest = TryGoBack;
            service.MessageArrived += OnMessageArrived;
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (phone != null && phone.BackRequest == TryGoBack) phone.BackRequest = null;
            if (service != null) service.MessageArrived -= OnMessageArrived;
        }

        /// <summary>
        /// O aviso sonoro de uma mensagem nova.
        ///
        /// **Cala-se com o telemóvel já levantado.** Nessa altura o jogador tem a
        /// mensagem à frente dos olhos e o alerta soa a erro — e gasta num momento
        /// banal um som cujo trabalho é apanhá-lo desprevenido. É a mesma regra do
        /// aviso no HUD, e pela mesma razão.
        /// </summary>
        private void OnMessageArrived(PhoneMessageService.Thread thread)
        {
            if (phone == null || phone.IsOpen) return;
            if (notificationSource == null || notificationClip == null) return;
            notificationSource.PlayOneShot(notificationClip, notificationVolume);
        }

        private void OnDisable()
        {
            VhsSettings.PhoneClarity = 0f;
            if (hud != null) hud.SetNotice(false);
        }

        private void Update()
        {
            UpdateNotice();

            if (!phone.IsOpen)
            {
                if (canvas != null && canvas.enabled)
                {
                    SetVisible(false);
                    view = View.List;
                    openThread = null;
                    drawnSignature = null;
                }
                VhsSettings.PhoneClarity = 0f;
                return;
            }

            if (!canvas.enabled) SetVisible(true);

            FaceCamera();
            ReadInput();
            RedrawIfChanged();
            ApplyVhsClarity();
        }

        /// <summary>
        /// Diz ao HUD se há alguma coisa por ler.
        ///
        /// **Cala-se com o telemóvel na mão.** Com ele levantado o jogador tem a
        /// mensagem à frente dos olhos; continuar a avisá-lo soa a erro e gasta um
        /// aviso que só devia servir para quem está de costas.
        /// </summary>
        private void UpdateNotice()
        {
            if (hud == null || service == null) return;
            hud.SetNotice(!phone.IsOpen && service.AnyUnread, noticeText);
        }

        /// <summary>
        /// Turns the panel round when it ends up showing the player its back.
        ///
        /// Which side is the back depends on how the phone model happens to be
        /// rotated, and that is an authored number the owner keeps tuning. Rather
        /// than hard code a flip a later tweak would silently undo, the panel
        /// checks and corrects itself.
        /// </summary>
        private void FaceCamera()
        {
            if (playerCamera == null || canvasRect == null) return;

            // A canvas is read from the side its forward points AWAY from. Getting
            // this sign the wrong way round is exactly what put mirrored text on
            // the screen, so it is spelled out rather than inlined.
            Vector3 toCamera = playerCamera.transform.position - canvasRect.position;
            bool readable = Vector3.Dot(canvasRect.forward, toCamera) < 0f;
            if (readable) return;

            canvasRect.localRotation *= Quaternion.Euler(0f, 180f, 0f);
        }

        private void ReadInput()
        {
            int pressed = input.NumberPressed;
            if (pressed <= 0) return;

            if (view == View.List)
            {
                var list = service.Visible;
                int index = pressed - 1;
                if (index < 0 || index >= list.Count) return;
                openThread = list[index];
                service.MarkRead(openThread);
                view = View.Thread;
                return;
            }

            if (openThread == null) return;

            // Only a step that is actually waiting takes a number. Otherwise the
            // key that opened a contact would also fire the first reply, and the
            // player would answer his father without reading him.
            var step = openThread.Current;
            if (openThread.PendingChoiceStep < 0 || step == null || !step.HasChoices) return;

            int choice = pressed - 1;
            if (choice < 0 || choice >= step.Choices.Length) return;
            service.Choose(openThread, choice);
        }

        /// <summary>
        /// Tab inside a thread goes back to the list; Tab in the list puts the
        /// phone away. One key, and it never traps the player one level down.
        /// </summary>
        private bool TryGoBack()
        {
            if (view != View.Thread) return false;
            view = View.List;
            openThread = null;
            return true;
        }

        // ---------------------------------------------------------------- drawing

        /// <summary>
        /// Rebuilding a dozen UI objects every frame would be wasteful and would
        /// also fight the layout system, so the screen only redraws when something
        /// it shows has actually changed.
        /// </summary>
        private void RedrawIfChanged()
        {
            string signature = Signature();
            if (signature == drawnSignature) return;
            drawnSignature = signature;
            Redraw();
        }

        private string Signature()
        {
            if (view == View.List)
            {
                var list = service.Visible;
                var s = "L" + list.Count;
                for (int i = 0; i < list.Count; i++)
                    s += "|" + list[i].ContactName + list[i].Entries.Count + (list[i].Unread ? "*" : "");
                return s;
            }

            if (openThread == null) return "T?";
            return "T" + openThread.ContactName + openThread.Entries.Count
                 + "|" + openThread.PendingChoiceStep
                 + "|" + (string.IsNullOrEmpty(openThread.PendingText) ? "" : "typing");
        }

        private void Redraw()
        {
            for (int i = 0; i < rows.Count; i++)
                if (rows[i] != null) Destroy(rows[i]);
            rows.Clear();

            // An inbox fills from the top; a conversation piles up from the bottom,
            // so the newest message sits where the thumb is and the oldest scrolls
            // away. Same container, opposite gravity.
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = view == View.List
                ? TextAnchor.UpperCenter : TextAnchor.LowerCenter;

            if (view == View.List) DrawList();
            else DrawThread();
        }

        private void DrawList()
        {
            headerLabel.text = "Messages";
            footerLabel.text = "[TAB] put away";

            var list = service.Visible;
            if (list.Count == 0)
            {
                AddNotice("No messages.");
                return;
            }

            for (int i = 0; i < list.Count; i++) AddListRow(i + 1, list[i]);
        }

        private void DrawThread()
        {
            if (openThread == null) { view = View.List; return; }

            headerLabel.text = openThread.ContactName;
            footerLabel.text = "[TAB] back";

            var entries = openThread.Entries;
            int from = Mathf.Max(0, entries.Count - historyLines);
            for (int i = from; i < entries.Count; i++)
                AddBubble(entries[i].Text, entries[i].Mine);

            if (!string.IsNullOrEmpty(openThread.PendingText))
                AddBubble("...", false);

            var step = openThread.Current;
            if (openThread.PendingChoiceStep >= 0 && step != null && step.HasChoices)
                for (int i = 0; i < step.Choices.Length; i++)
                    AddChoice(i + 1, step.Choices[i].Text);
        }

        // ------------------------------------------------------------------ rows

        private void AddNotice(string message)
        {
            var row = NewRow("Notice", TextAnchor.UpperCenter);
            var plate = NewPanel(row.transform, glassColour, screenSize.x - 60f, 0);
            var label = NewText(plate, message, dimColour, fontSize);
            label.alignment = TextAnchor.UpperCenter;
        }

        /// <summary>One contact: its number key, its name, and the last thing said.</summary>
        private void AddListRow(int number, PhoneMessageService.Thread thread)
        {
            var row = NewRow("Contact", TextAnchor.UpperCenter);
            var plate = NewPanel(row.transform, barColour, screenSize.x - 60f, 6);

            var title = NewText(plate, number + "   " + thread.ContactName,
                thread.Unread ? accentColour : textColour, fontSize);
            title.fontStyle = thread.Unread ? FontStyle.Bold : FontStyle.Normal;

            var last = thread.Entries.Count == 0 ? "" : thread.Entries[thread.Entries.Count - 1].Text;
            NewText(plate, Shorten(last, 38), dimColour, Mathf.RoundToInt(fontSize * 0.8f));
        }

        /// <summary>
        /// One message. Side is the whole design: theirs left, yours right, the way
        /// every phone the player has ever held does it.
        /// </summary>
        private void AddBubble(string message, bool mine)
        {
            // Never the full width: a bubble that reaches both edges stops reading
            // as a bubble and goes back to being a paragraph.
            var row = NewRow("Bubble", mine ? TextAnchor.UpperRight : TextAnchor.UpperLeft);
            var bubble = NewPanel(row.transform, mine ? myBubble : theirBubble,
                (screenSize.x - 60f) * 0.88f, 0);
            NewText(bubble, message, textColour, fontSize);
        }

        private void AddChoice(int number, string message)
        {
            var row = NewRow("Choice", TextAnchor.UpperRight);
            var bubble = NewPanel(row.transform, glassColour, (screenSize.x - 60f) * 0.88f, 0);

            var outline = bubble.gameObject.AddComponent<Outline>();
            outline.effectColor = accentColour;
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            NewText(bubble, "[" + number + "]  " + message, accentColour,
                Mathf.RoundToInt(fontSize * 0.92f));
        }

        /// <summary>
        /// A fixed-width plate that grows downwards to fit whatever text goes in it.
        ///
        /// The width is set on the rect and the parent is told not to touch it,
        /// because wrapping needs a width decided BEFORE the text is measured. An
        /// earlier version left the width to the layout system and to a
        /// <c>LayoutElement</c> at the same time; the two disagreed and every
        /// message came out two words wide.
        /// </summary>
        private RectTransform NewPanel(Transform parent, Color colour, float width, int spacing)
        {
            var go = new GameObject("Panel", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(width, 0f);

            var image = go.AddComponent<Image>();
            image.color = colour;
            if (roundedSprite != null)
            {
                image.sprite = roundedSprite;
                image.type = Image.Type.Sliced;
            }

            var vertical = go.AddComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(24, 24, 16, 18);
            vertical.spacing = spacing;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        // -------------------------------------------------------------- plumbing

        private GameObject NewRow(string name, TextAnchor alignment)
        {
            var row = new GameObject(name, typeof(RectTransform));
            var rect = row.GetComponent<RectTransform>();
            rect.SetParent(content, false);

            // Controls height only. The panel inside sets its own width, which is
            // what puts a bubble on the left or the right without stretching it.
            var horizontal = row.AddComponent<HorizontalLayoutGroup>();
            horizontal.childAlignment = alignment;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;
            horizontal.childControlWidth = false;
            horizontal.childControlHeight = true;

            row.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            rows.Add(row);
            return row;
        }

        private Text NewText(Transform parent, string value, Color colour, int size)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = colour;
            text.text = value;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            return text;
        }

        private static string Shorten(string value, int limit)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            value = value.Replace('\n', ' ');
            return value.Length <= limit ? value : value.Substring(0, limit - 1) + "…";
        }

        private void SetVisible(bool visible)
        {
            if (canvas != null) canvas.enabled = visible;
        }

        private void ApplyVhsClarity()
        {
            if (playerCamera == null || canvasRect == null)
            {
                VhsSettings.PhoneClarity = 0f;
                return;
            }

            canvasRect.GetWorldCorners(corners);

            float minX = 1f, minY = 1f, maxX = 0f, maxY = 0f;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 viewport = playerCamera.WorldToViewportPoint(corners[i]);

                // Behind the camera the viewport point mirrors, and a mirrored
                // rectangle clears the wrong half of the screen.
                if (viewport.z <= 0f) { VhsSettings.PhoneClarity = 0f; return; }

                minX = Mathf.Min(minX, viewport.x);
                minY = Mathf.Min(minY, viewport.y);
                maxX = Mathf.Max(maxX, viewport.x);
                maxY = Mathf.Max(maxY, viewport.y);
            }

            VhsSettings.PhoneClarityRect = new Vector4(
                Mathf.Clamp01(minX - clarityPadding), Mathf.Clamp01(minY - clarityPadding),
                Mathf.Clamp01(maxX + clarityPadding), Mathf.Clamp01(maxY + clarityPadding));
            VhsSettings.PhoneClarity = phone.IsSettled ? 1f : 0.35f;
        }

        private void BuildCanvas()
        {
            var stale = screenAnchor.Find("PhoneCanvas");
            if (stale != null) DestroyImmediate(stale.gameObject);

            font = LoadFont();
            roundedSprite = BuildRoundedSprite();

            var canvasObject = new GameObject("PhoneCanvas", typeof(RectTransform), typeof(Canvas));
            canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.SetParent(screenAnchor, false);
            canvasRect.localPosition = screenLocalPosition;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * screenScale;
            canvasRect.sizeDelta = screenSize;

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var glass = NewChild("Glass", canvasRect);
            Stretch(glass);
            glass.gameObject.AddComponent<Image>().color = glassColour;

            // Status bar. Pure decoration, and it earns its pixels: without it the
            // panel reads as a text box, with it the same text reads as a device.
            var status = NewChild("StatusBar", canvasRect);
            status.anchorMin = new Vector2(0f, 1f);
            status.anchorMax = new Vector2(1f, 1f);
            status.pivot = new Vector2(0.5f, 1f);
            status.anchoredPosition = Vector2.zero;
            status.sizeDelta = new Vector2(0f, 62f);
            var statusText = NewText(status, "02:47", dimColour, Mathf.RoundToInt(fontSize * 0.66f));
            statusText.alignment = TextAnchor.MiddleCenter;
            Stretch(statusText.rectTransform);

            var header = NewChild("Header", canvasRect);
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = new Vector2(0f, -62f);
            header.sizeDelta = new Vector2(0f, 104f);
            header.gameObject.AddComponent<Image>().color = barColour;
            headerLabel = NewText(header, "Messages", textColour, Mathf.RoundToInt(fontSize * 1.05f));
            headerLabel.alignment = TextAnchor.MiddleCenter;
            headerLabel.fontStyle = FontStyle.Bold;
            Stretch(headerLabel.rectTransform);

            content = NewChild("Content", canvasRect);
            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(1f, 1f);
            content.offsetMin = new Vector2(30f, 78f);
            content.offsetMax = new Vector2(-30f, -178f);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            // A long message stacked from the bottom grows upward past the header
            // and writes over it. Clipping is the honest fix: the phone shows the
            // last few messages and the rest is simply off the top of the screen.
            content.gameObject.AddComponent<RectMask2D>();

            var footer = NewChild("Footer", canvasRect);
            footer.anchorMin = new Vector2(0f, 0f);
            footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.anchoredPosition = Vector2.zero;
            footer.sizeDelta = new Vector2(0f, 70f);
            footerLabel = NewText(footer, "[TAB] put away", dimColour,
                Mathf.RoundToInt(fontSize * 0.7f));
            footerLabel.alignment = TextAnchor.MiddleCenter;
            Stretch(footerLabel.rectTransform);
        }

        private static RectTransform NewChild(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            var rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// A nine-sliced rounded rectangle, drawn here rather than loaded.
        ///
        /// Unity's built-in <c>UI/Skin/UISprite.psd</c> is editor-only: asking for
        /// it at runtime prints two errors and hands back null, which is how the
        /// bubbles ended up with square corners and the console with noise. Thirty
        /// two pixels and a corner radius cost nothing and always exist.
        /// </summary>
        private static Sprite BuildRoundedSprite()
        {
            const int size = 32;
            const float radius = 9f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Distance from the nearest corner centre, so the alpha rolls off
                // only inside the corner squares and stays solid everywhere else.
                float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius), 0f);
                float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius), 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>
        /// A phone screen is the one place in this game that should not use the
        /// serif of the title cards. It is a device, not the game's voice.
        /// </summary>
        private static Font LoadFont()
        {
            var loaded = Font.CreateDynamicFontFromOSFont(
                new[] { "Segoe UI", "Arial", "Helvetica" }, 52);
            if (loaded != null) return loaded;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
    }
}
