using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// As palpebras do jogador: duas barras pretas que fecham de cima e de baixo.
    ///
    /// Vive a parte porque tanto o adormecer como o acordar precisam dela, um a
    /// fechar e outro a abrir. Um fade preto simples servia para adormecer, mas
    /// acordar com um fade e uma transicao de filme; acordar com palpebras e um
    /// corpo a abrir os olhos.
    ///
    /// Em OnGUI de proposito: o resto do HUD tambem la vive, e abrir um Canvas so
    /// para isto era mais uma peca a migrar quando o HUD sair do OnGUI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenEyelid : MonoBehaviour
    {
        [Tooltip("0 = olhos abertos, 1 = fechados.")]
        [SerializeField, Range(0f, 1f)] private float amount;

        private Texture2D black;

        /// <summary>0 = aberto, 1 = fechado.</summary>
        public float Amount
        {
            get => amount;
            set => amount = Mathf.Clamp01(value);
        }

        public bool IsClosed => amount >= 0.999f;

        /// <summary>
        /// Um piscar de olhos, com a mudanca escondida no escuro do meio.
        ///
        /// **Para que serve.** Entrar e sair do carro, sentar-se, ser posto num sitio
        /// — momentos em que o corpo do jogador salta de um transform para outro sem
        /// animacao nenhuma. Um salto desses le-se como um erro do jogo. O mesmo salto
        /// feito com os olhos fechados le-se como nao ter reparado.
        ///
        /// **Rapido, como um olho a serio.** Um piscar humano dura cerca de um decimo
        /// de segundo a fechar e outro tanto a abrir. Mais lento do que isso deixa de
        /// ser um piscar e passa a ser um desmaio, e um desmaio ao entrar no carro
        /// levanta perguntas que nao existem.
        ///
        /// A troca corre no instante em que o ecra esta mesmo preto — nao antes, para
        /// nao se ver o salto; nao depois, porque a essa altura ja se ve outra vez.
        /// </summary>
        public Coroutine Blink(System.Action whileClosed,
            float closeSeconds = 0.09f, float shutSeconds = 0.05f, float openSeconds = 0.13f)
        {
            if (blinking != null) StopCoroutine(blinking);
            blinking = StartCoroutine(BlinkRoutine(whileClosed, closeSeconds, shutSeconds, openSeconds));
            return blinking;
        }

        private Coroutine blinking;

        private System.Collections.IEnumerator BlinkRoutine(System.Action whileClosed,
            float closeSeconds, float shutSeconds, float openSeconds)
        {
            yield return Ramp(amount, 1f, closeSeconds);

            amount = 1f;
            whileClosed?.Invoke();

            float held = 0f;
            while (held < shutSeconds) { held += Time.unscaledDeltaTime; yield return null; }

            yield return Ramp(1f, 0f, openSeconds);
            amount = 0f;
            blinking = null;
        }

        private System.Collections.IEnumerator Ramp(float from, float to, float seconds)
        {
            if (seconds <= 0f) { amount = to; yield break; }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                amount = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }
            amount = to;
        }

        private void OnGUI()
        {
            if (amount <= 0f) return;

            if (black == null)
            {
                black = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                black.SetPixel(0, 0, Color.black);
                black.Apply();
            }

            float half = Screen.height * 0.5f * amount;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, half), black);
            GUI.DrawTexture(new Rect(0f, Screen.height - half, Screen.width, half), black);
        }

        private void OnDestroy()
        {
            if (black != null) Destroy(black);
        }
    }
}
