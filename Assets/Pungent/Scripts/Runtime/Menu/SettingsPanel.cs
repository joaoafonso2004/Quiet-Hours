using UnityEngine;

namespace Pungent.Menu
{
    /// <summary>
    /// O painel de opcoes, desenhado uma vez e usado nos dois sitios.
    ///
    /// ---
    ///
    /// **Partilhado entre o menu inicial e o de pausa de proposito.** Sao o mesmo
    /// painel para o jogador, e duas copias divergem sempre: acrescenta-se uma
    /// linha num, esquece-se no outro, e passa a haver definicoes que so existem
    /// se o jogo ja tiver comecado. Aqui uma linha nova aparece nos dois.
    ///
    /// ---
    ///
    /// **Em `OnGUI` como o resto da interface deste jogo.** Um Canvas so para
    /// isto punha o projecto com dois sistemas de interface — ver a nota no
    /// `MainMenu`. Quando o HUD sair do `OnGUI`, isto sai com ele.
    ///
    /// ---
    ///
    /// **Aplica enquanto se arrasta, grava quando se sai.** Quem esta a afinar o
    /// brilho precisa de o ver a mudar com o cursor; escrever no disco a cada
    /// frame de arrasto e desperdicio. O `Commit` fecha a conta.
    /// </summary>
    public static class SettingsPanel
    {
        private enum Kind { Slider, Toggle }

        private readonly struct Row
        {
            public readonly string Label;
            public readonly Kind Kind;
            public Row(string label, Kind kind) { Label = label; Kind = kind; }
        }

        // A ordem e a da imagem de referencia: som primeiro, ecra a seguir,
        // rato e camara no fim. Agrupado por aquilo em que se mexe, e nao por
        // tipo de controlo.
        private static readonly Row[] Rows =
        {
            new Row("Volume", Kind.Slider),
            new Row("Fullscreen", Kind.Toggle),
            new Row("VSync", Kind.Toggle),
            new Row("Brightness", Kind.Slider),
            new Row("Texture Filtering", Kind.Toggle),
            new Row("RAW input", Kind.Toggle),
            new Row("Invert Y", Kind.Toggle),
            new Row("Sensitivity", Kind.Slider),
            new Row("Head Bobbing", Kind.Toggle)
        };

        /// <summary>Qual slider esta agarrado ao rato. -1 = nenhum.</summary>
        private static int dragging = -1;

        private static Texture2D pixel;

        public static int RowCount => Rows.Length;

        /// <summary>Altura total que o painel ocupa com o espacamento dado.</summary>
        public static float HeightFor(float rowHeight) => Rows.Length * rowHeight;

        /// <summary>
        /// Grava e larga o cursor. Chamado por quem fecha o painel.
        /// </summary>
        public static void Commit()
        {
            dragging = -1;
            GameSettings.Save();
        }

        /// <summary>
        /// Desenha o painel dentro da area dada.
        ///
        /// O rato manda em tudo. Nao ha navegacao por teclado aqui de propósito:
        /// o menu de pausa ja tem a sua, e duas coisas a mexer no mesmo cursor
        /// era a maneira mais rapida de o painel deixar de responder.
        /// </summary>
        public static void Draw(Rect area, GUIStyle labelStyle, float rowHeight)
        {
            EnsurePixel();

            // Metade esquerda para o nome, metade direita para o controlo.
            float labelWidth = area.width * 0.42f;
            float controlLeft = area.x + area.width * 0.46f;
            float controlWidth = area.width * 0.54f;

            var name = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleLeft };

            Event e = Event.current;
            if (e.type == EventType.MouseUp) dragging = -1;

            for (int i = 0; i < Rows.Length; i++)
            {
                var row = Rows[i];
                float y = area.y + i * rowHeight;

                Shadowed(new Rect(area.x, y, labelWidth, rowHeight), row.Label, name);

                var slot = new Rect(controlLeft, y, controlWidth, rowHeight);
                if (row.Kind == Kind.Slider) DrawSlider(i, slot, e);
                else DrawToggle(i, slot, e);
            }
        }

        // ------------------------------------------------------------------

        private static void DrawSlider(int index, Rect slot, Event e)
        {
            const float Track = 3f, Knob = 13f;

            float value = Get(index);
            var track = new Rect(slot.x, slot.y + slot.height * 0.5f - Track * 0.5f,
                                 slot.width * 0.86f, Track);

            GUI.color = new Color(0.88f, 0.88f, 0.84f, 0.85f);
            GUI.DrawTexture(track, pixel);

            float knobX = track.x + track.width * Mathf.Clamp01(value);
            var knob = new Rect(knobX - Knob * 0.5f, slot.y + slot.height * 0.5f - Knob * 0.75f,
                                Knob, Knob * 1.5f);

            // A zona de agarrar e a barra inteira e nao so o cursor: um alvo de
            // treze pixeis obriga a mira antes de obrigar a decisao.
            var grab = new Rect(track.x - Knob, slot.y, track.width + Knob * 2f, slot.height);
            bool over = grab.Contains(e.mousePosition);

            if (over && e.type == EventType.MouseDown) { dragging = index; e.Use(); }
            if (dragging == index && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown))
            {
                Set(index, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                GameSettings.Apply();
                e.Use();
            }

            GUI.color = dragging == index || over
                ? new Color(1f, 1f, 0.98f)
                : new Color(0.90f, 0.89f, 0.85f, 0.95f);
            GUI.DrawTexture(knob, pixel);
            GUI.color = Color.white;
        }

        private static void DrawToggle(int index, Rect slot, Event e)
        {
            const float Box = 20f;
            var box = new Rect(slot.x + 2f, slot.y + slot.height * 0.5f - Box * 0.5f, Box, Box);
            bool over = box.Contains(e.mousePosition);
            bool on = Get(index) > 0.5f;

            if (over && e.type == EventType.MouseDown)
            {
                Set(index, on ? 0f : 1f);
                GameSettings.Apply();
                e.Use();
                on = !on;
            }

            // Moldura, e o preenchimento so quando esta ligado. Uma caixa vazia e
            // uma caixa cheia leem-se de relance; uma marca de visto pequena, nao.
            GUI.color = over ? new Color(1f, 1f, 0.98f) : new Color(0.90f, 0.89f, 0.85f, 0.95f);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 2f), pixel);
            GUI.DrawTexture(new Rect(box.x, box.yMax - 2f, box.width, 2f), pixel);
            GUI.DrawTexture(new Rect(box.x, box.y, 2f, box.height), pixel);
            GUI.DrawTexture(new Rect(box.xMax - 2f, box.y, 2f, box.height), pixel);

            if (on)
                GUI.DrawTexture(new Rect(box.x + 5f, box.y + 5f, box.width - 10f, box.height - 10f), pixel);

            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// O valor de cada linha, em 0..1. Os interruptores usam 0 e 1 para o
        /// desenho nao precisar de saber de que tipo e cada coisa.
        /// </summary>
        private static float Get(int index)
        {
            switch (index)
            {
                case 0: return GameSettings.Volume;
                case 1: return GameSettings.Fullscreen ? 1f : 0f;
                case 2: return GameSettings.VSync ? 1f : 0f;
                case 3: return GameSettings.Brightness;
                case 4: return GameSettings.TextureFiltering ? 1f : 0f;
                case 5: return GameSettings.RawInput ? 1f : 0f;
                case 6: return GameSettings.InvertY ? 1f : 0f;
                case 7: return GameSettings.Sensitivity;
                case 8: return GameSettings.HeadBobbing ? 1f : 0f;
                default: return 0f;
            }
        }

        private static void Set(int index, float value)
        {
            switch (index)
            {
                case 0: GameSettings.Volume = value; break;
                case 1: GameSettings.Fullscreen = value > 0.5f; break;
                case 2: GameSettings.VSync = value > 0.5f; break;
                case 3: GameSettings.Brightness = value; break;
                case 4: GameSettings.TextureFiltering = value > 0.5f; break;
                case 5: GameSettings.RawInput = value > 0.5f; break;
                case 6: GameSettings.InvertY = value > 0.5f; break;
                case 7: GameSettings.Sensitivity = value; break;
                case 8: GameSettings.HeadBobbing = value > 0.5f; break;
            }
        }

        private static void EnsurePixel()
        {
            if (pixel != null) return;
            pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
        }

        private static void Shadowed(Rect rect, string text, GUIStyle style)
        {
            Color original = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            style.normal.textColor = original;
            GUI.Label(rect, text, style);
        }
    }
}
