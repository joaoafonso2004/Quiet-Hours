using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O cartao que abre cada capitulo: ecra preto com a data e a hora, e so
    /// depois o jogo.
    ///
    /// Nao e so arrumacao. Num jogo em que o apartamento e sempre o mesmo, a unica
    /// coisa que diz ao jogador que passou uma noite e o cartao — sem ele, acordar
    /// no mesmo quarto le-se como se nada tivesse acontecido. E a hora faz
    /// trabalho sozinha: "02:47" na terceira vez ja nao precisa de explicacao.
    ///
    /// Em OnGUI como o resto do HUD, para nao ficar mais uma peca a migrar quando
    /// o HUD sair do OnGUI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChapterCard : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.8f;
        [SerializeField, Min(0f)] private float holdSeconds = 2.6f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 1.2f;

        [Header("Aspecto")]
        [SerializeField] private int dateSize = 34;
        [SerializeField] private int timeSize = 58;
        [SerializeField] private Color textColour = new Color(0.88f, 0.88f, 0.84f);

        private string dateLine;
        private string timeLine;
        private float alpha;
        private float textAlpha;
        private Texture2D black;
        private GUIStyle dateStyle;
        private GUIStyle timeStyle;

        public bool IsShowing { get; private set; }

        /// <summary>Mostra o cartao e devolve o controlo quando ele sair de cena.</summary>
        public System.Collections.IEnumerator Show(string date, string time)
        {
            dateLine = date;
            timeLine = time;
            IsShowing = true;

            // Entra a preto primeiro e so depois aparece o texto: as duas coisas ao
            // mesmo tempo leem-se como um ecra de carregamento.
            yield return Ramp(0f, 1f, fadeInSeconds, textOnly: false);
            yield return Ramp(0f, 1f, 0.5f, textOnly: true);

            float held = 0f;
            while (held < holdSeconds) { held += Time.unscaledDeltaTime; yield return null; }

            yield return Ramp(1f, 0f, 0.4f, textOnly: true);
            yield return Ramp(1f, 0f, fadeOutSeconds, textOnly: false);

            IsShowing = false;
        }

        private System.Collections.IEnumerator Ramp(float from, float to, float seconds, bool textOnly)
        {
            if (seconds <= 0f)
            {
                if (textOnly) textAlpha = to; else alpha = to;
                yield break;
            }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float v = Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds));
                if (textOnly) textAlpha = v; else alpha = v;
                yield return null;
            }
            if (textOnly) textAlpha = to; else alpha = to;
        }

        private void OnGUI()
        {
            if (alpha <= 0f) return;

            if (black == null)
            {
                black = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                black.SetPixel(0, 0, Color.black);
                black.Apply();
            }

            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), black);

            if (textAlpha > 0f)
            {
                EnsureStyles();
                GUI.color = new Color(textColour.r, textColour.g, textColour.b, textAlpha * alpha);

                float centreY = Screen.height * 0.5f;
                if (!string.IsNullOrEmpty(dateLine))
                    GUI.Label(new Rect(0f, centreY - 64f, Screen.width, 44f), dateLine, dateStyle);
                if (!string.IsNullOrEmpty(timeLine))
                    GUI.Label(new Rect(0f, centreY - 16f, Screen.width, 80f), timeLine, timeStyle);
            }

            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (dateStyle != null) return;

            dateStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = dateSize,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal,
            };
            timeStyle = new GUIStyle(dateStyle)
            {
                fontSize = timeSize,
                fontStyle = FontStyle.Bold,
            };
        }

        private void OnDestroy()
        {
            if (black != null) Destroy(black);
        }
    }
}
