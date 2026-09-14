using Pungent.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Pungent.Menu
{
    /// <summary>
    /// O menu de pausa. O jogo tem noventa minutos e ate hoje nao se podia
    /// interromper.
    ///
    /// **Sem `Button` e sem `EventSystem`.** E teclado: setas para cima e para baixo
    /// escolhem, esquerda e direita mexem no valor, Enter confirma, Esc volta ao
    /// jogo. Nao e nostalgia — e que os `Button` do Unity nao respondem sem um
    /// `EventSystem` na cena, e um menu de pausa que **abre e nao reage** e pior do
    /// que nao haver menu nenhum: o jogador fica preso num ecra que parece estar a
    /// funcionar. O teclado nao depende de nada que possa faltar numa das tres
    /// cenas.
    ///
    /// **O tempo para, o filtro nao.** `Time.timeScale = 0` congela tudo o que conta
    /// segundos — a caca, os temporizadores dos capitulos, as tarefas. O VHS
    /// continua a andar porque corre em `Time.unscaledTime`: uma imagem
    /// completamente parada por tras do menu denuncia que aquilo nao e video, e uma
    /// fita em pausa continua a chiar.
    ///
    /// Vive no `GAME_SYSTEMS` e atravessa as cenas com o resto. Volta a procurar o
    /// jogador sozinho, porque o apartamento e recarregado para o climax e o
    /// exemplar antigo morre nesse frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseMenu : MonoBehaviour
    {
        private const string CanvasName = "PAUSE_MENU";

        // O volume era uma linha daqui, com setas para a esquerda e para a direita.
        // Passou para dentro das opcoes com as outras oito: um menu de pausa que so
        // deixa mexer no som obriga a sair do jogo para mudar a sensibilidade do
        // rato, que e a definicao que mais gente quer mudar nos primeiros dois
        // minutos e a unica que nao se pode adivinhar por alguem.
        private enum Row { Resume, Settings, Quit }

        [Tooltip("O telemovel. Com ele aberto, o Escape fecha-o em vez de abrir "
               + "isto — ver a nota no `Update`.")]
        [SerializeField] private Pungent.Interaction.PrototypePhoneUI phone;

        /// <summary>O painel de opcoes esta por cima do menu.</summary>
        private bool inSettings;

        [Tooltip("Impede que o menu abra durante o epilogo: a partir do `game_end` "
               + "nao ha jogo para retomar, e um 'Resume' nesse ponto so confunde.")]
        [SerializeField] private bool closedAfterEnd = true;

        private Canvas canvas;
        private CanvasGroup group;
        private Text titleText;
        private Text[] rowTexts;
        private Text hintText;

        private PlayerInputReader input;
        private Row selected = Row.Resume;
        private bool open;
        private float previousTimeScale = 1f;
        private bool ended;

        /// <summary>Aberto agora. Lido por quem nao deve correr por baixo dele.</summary>
        public bool IsOpen => open;

        private void Awake()
        {
            Build();
            Show(false);
        }

        private void OnDestroy()
        {
            // Sair de Play com o menu aberto deixava o Editor com o `timeScale` a
            // zero e o jogo seguinte comecava congelado, sem nada a dizer porque.
            if (open) Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (input == null) input = FindObjectOfType<PlayerInputReader>();
            if (phone == null) phone = FindObjectOfType<Pungent.Interaction.PrototypePhoneUI>();

            if (!open)
            {
                // **O telemovel tem precedencia sobre a pausa.**
                //
                // Uma tecla que fecha coisas fecha-as uma de cada vez, da mais
                // recente para a mais antiga: com o telemovel na mao, o Escape
                // fecha o telemovel; so no toque seguinte e que pausa o jogo.
                //
                // Ate aqui os dois liam a mesma tecla no mesmo frame e faziam as
                // duas coisas de uma vez — o telemovel fechava-se **e** o jogo
                // parava, e o jogador levava com um menu de pausa que nao pediu.
                //
                // O `ClosedByEscapeThisFrame` existe por causa da ordem de
                // execucao: se o telemovel correr primeiro ja se fechou, e um
                // `IsOpen` sozinho voltava a deixar passar o Escape para aqui.
                // **Pergunta ao tipo e nao a um exemplar.** A cena tem dois
                // `PrototypePhoneUI` — `PLAYER` e `PlayerRoot` — e este campo
                // guardava o primeiro que o `FindObjectOfType` desse. Com o outro
                // aberto, o `IsOpen` lido aqui era falso e o Escape abria a pausa
                // por cima das mensagens: o defeito exacto que esta guarda existe
                // para impedir. Ver a nota do `AnyOpen`.
                if (Pungent.Interaction.PrototypePhoneUI.AnyOpen
                    || Pungent.Interaction.PrototypePhoneUI.AnyClosedByEscapeThisFrame) return;

                if (phone != null && (phone.IsOpen || phone.ClosedByEscapeThisFrame)) return;

                if (!ended && keyboard.escapeKey.wasPressedThisFrame) Open();
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                // Mesma regra por dentro: as opcoes fecham-se antes do menu.
                if (inSettings) { SettingsPanel.Commit(); SetSettings(false); }
                else Close();
                return;
            }

            // Com as opcoes abertas manda o rato. As setas mexiam na linha
            // escolhida por tras do painel, e ao fechar aparecia outra coisa
            // seleccionada sem ninguem lhe ter tocado.
            if (inSettings) return;

            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                Move(1);
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                Move(-1);

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame)
                Activate();
        }

        /// <summary>
        /// O painel de opcoes, por cima do menu.
        ///
        /// Em `OnGUI` enquanto o menu e Canvas: o IMGUI desenha sempre por cima de
        /// um Canvas em Screen Space Overlay, portanto o painel fica onde tem de
        /// ficar sem lhe mexer na ordem. E assim e o **mesmo** painel do menu
        /// inicial — nove linhas escritas uma vez.
        /// </summary>
        private void OnGUI()
        {
            if (!open || !inSettings) return;

            float w = Screen.width, h = Screen.height;

            if (blackPixel == null)
            {
                blackPixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                blackPixel.SetPixel(0, 0, Color.white);
                blackPixel.Apply();
            }

            GUI.color = new Color(0f, 0f, 0f, 0.86f);
            GUI.DrawTexture(new Rect(0f, 0f, w, h), blackPixel);
            GUI.color = Color.white;

            float rowHeight = Mathf.Clamp(h * 0.062f, 34f, 58f);
            float panelHeight = SettingsPanel.HeightFor(rowHeight);
            float panelWidth = Mathf.Min(w * 0.62f, 760f);
            var area = new Rect((w - panelWidth) * 0.5f,
                                h * 0.5f - panelHeight * 0.5f - 20f, panelWidth, panelHeight);

            var style = new GUIStyle
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),
                fontSize = Mathf.RoundToInt(rowHeight * 0.46f),
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.90f, 0.89f, 0.85f, 0.95f) }
            };

            SettingsPanel.Draw(area, style, rowHeight);

            var back = new GUIStyle(style)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(rowHeight * 0.42f)
            };
            GUI.Label(new Rect(0f, area.yMax + 30f, w, 30f), "Escape to go back", back);
        }

        private Texture2D blackPixel;

        /// <summary>
        /// A partir do fim do jogo o menu deixa de abrir. Chamado por quem souber que
        /// o `game_end` passou.
        /// </summary>
        public void MarkEnded()
        {
            if (!closedAfterEnd) return;
            ended = true;
            if (open) Close();
        }

        private void Move(int delta)
        {
            int count = System.Enum.GetValues(typeof(Row)).Length;
            selected = (Row)(((int)selected + delta + count) % count);
            Paint();
        }

        private void Activate()
        {
            switch (selected)
            {
                case Row.Resume:
                    Close();
                    break;

                case Row.Settings:
                    SetSettings(true);
                    break;

                case Row.Quit:
                    Quit();
                    break;
            }
        }

        private void Open()
        {
            open = true;
            selected = Row.Resume;

            // O menu abre sempre no menu, e nunca dentro das opcoes onde ficou da
            // ultima vez — sair por outro caminho que nao o Escape deixava a
            // bandeira levantada e a pausa seguinte comecava com o painel na cara.
            inSettings = false;

            previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;

            // O rato volta a existir e o corpo dele fica quieto. O `SetUiPointerActive`
            // e o mesmo caminho que as escolhas de dialogo ja usam.
            input?.SetUiPointerActive(true);
            input?.SetMoveSuppressed(true);
            input?.SetLookSuppressed(true);

            Show(true);
            Paint();
        }

        private void Close()
        {
            open = false;

            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            AudioListener.pause = false;

            input?.SetUiPointerActive(false);
            input?.SetMoveSuppressed(false);
            input?.SetLookSuppressed(false);

            Show(false);
        }

        private void Quit()
        {
            // Fechar com o tempo a zero deixava um build a sair a meio de um frame
            // congelado, e no Editor deixava o `timeScale` assim para a proxima.
            Time.timeScale = 1f;
            AudioListener.pause = false;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Entra e sai das opcoes, apagando o menu enquanto elas estao a vista.
        ///
        /// O painel e IMGUI e o menu e Canvas, portanto o painel ja fica por cima
        /// — mas "por cima" nao e "em vez de": o `PAUSA` e as tres linhas
        /// continuavam a ler-se por tras das nove, e o que se via eram duas
        /// interfaces sobrepostas. Apagar o canvas deixa o painel sozinho no ecra.
        /// </summary>
        private void SetSettings(bool value)
        {
            inSettings = value;
            Show(!value);
        }

        private void Show(bool visible)
        {
            if (group == null) return;
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            if (canvas != null) canvas.enabled = visible;
        }

        private void Paint()
        {
            if (rowTexts == null) return;

            for (int i = 0; i < rowTexts.Length; i++)
            {
                if (rowTexts[i] == null) continue;

                bool here = i == (int)selected;
                string label = Label((Row)i);

                // O cursor e um traco a esquerda e nao uma cor diferente: com o VHS
                // por cima, duas tonalidades de branco a distancia de um metro nao se
                // distinguem, e o jogador deixa de saber em que linha esta.
                rowTexts[i].text = (here ? "—  " : "     ") + label;
                rowTexts[i].color = here ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }

            if (hintText != null)
                hintText.text = "setas para escolher    enter para confirmar    esc para voltar";
        }

        private string Label(Row row)
        {
            switch (row)
            {
                case Row.Resume: return "CONTINUAR";
                case Row.Settings: return "OPCOES";
                case Row.Quit: return "SAIR DO JOGO";
            }
            return string.Empty;
        }

        // ------------------------------------------------------------------

        private void Build()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var stale = transform.Find(CanvasName);
            if (stale != null) DestroyImmediate(stale.gameObject);

            var host = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            host.transform.SetParent(transform, false);

            canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Acima do dialogo, que esta em 80. O menu tapa tudo o resto.
            canvas.sortingOrder = 200;

            var scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            group = host.GetComponent<CanvasGroup>();

            // O fundo. Nao e preto opaco: o apartamento continua la atras, desfocado
            // pela propria escuridao. Um preto cheio faz o menu parecer outro ecra;
            // assim continua a ser o mesmo sitio, com o jogo parado.
            var veil = new GameObject("Veil", typeof(Image));
            veil.transform.SetParent(host.transform, false);
            Stretch(veil.GetComponent<RectTransform>());
            veil.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);

            titleText = MakeText(host.transform, font, "PAUSA", 46, new Vector2(0f, 210f), 620f);
            titleText.color = new Color(1f, 1f, 1f, 0.75f);

            int rows = System.Enum.GetValues(typeof(Row)).Length;
            rowTexts = new Text[rows];
            for (int i = 0; i < rows; i++)
                rowTexts[i] = MakeText(host.transform, font, string.Empty, 30,
                    new Vector2(0f, 70f - i * 62f), 620f);

            hintText = MakeText(host.transform, font, string.Empty, 18, new Vector2(0f, -190f), 900f);
            hintText.color = new Color(1f, 1f, 1f, 0.35f);
        }

        private static Text MakeText(Transform parent, Font font, string content,
            int size, Vector2 position, float width)
        {
            var go = new GameObject("Row", typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, size + 18f);

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
