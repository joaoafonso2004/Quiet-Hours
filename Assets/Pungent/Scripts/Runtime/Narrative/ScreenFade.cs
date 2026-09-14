using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Pungent.Narrative
{
    /// <summary>
    /// Corte a preto reutilizável.
    ///
    /// A introdução já tinha o seu próprio blackout, mas fechado dentro dela. Os
    /// momentos em que o jogador perde o controlo do corpo — ser posto fora de um
    /// quarto, desmaiar, um salto no tempo — precisam todos do mesmo gesto, e não
    /// vale a pena cada um construir a sua tela.
    ///
    /// Constrói o canvas por código para não depender de nada montado à mão na
    /// cena, e fica por cima de tudo o resto.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenFade : MonoBehaviour
    {
        private const string CanvasName = "ScreenFadeCanvas";

        private Image blackout;
        private Coroutine running;

        public bool IsFading => running != null;

        private void Awake() => Build();

        private void Build()
        {
            if (blackout != null) return;

            Transform existing = transform.Find(CanvasName);
            if (existing != null)
            {
                blackout = existing.GetComponentInChildren<Image>(true);
                if (blackout != null) return;
                DestroyImmediate(existing.gameObject);
            }

            var canvasObject = new GameObject(CanvasName, typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Acima do diálogo (80) e do HUD: quando isto fecha, fecha sobre tudo.
            canvas.sortingOrder = 200;

            var imageObject = new GameObject("Blackout", typeof(Image));
            imageObject.transform.SetParent(canvasObject.transform, false);
            blackout = imageObject.GetComponent<Image>();
            blackout.color = new Color(0f, 0f, 0f, 0f);
            blackout.raycastTarget = false;

            var rect = blackout.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Fecha (ou abre) e fica assim.
        ///
        /// O <see cref="Blink"/> resolve o corte que volta atrás, que é a maioria dos
        /// casos deste jogo. O epílogo é o caso que ele não resolve: o ecrã fecha e
        /// **não reabre** — o que vem a seguir são três frases e o fim. Fazê-lo com um
        /// `Blink` de espera muito longa funcionava por acidente e deixava uma
        /// corrotina viva a contar até um número que ninguém tencionava atingir.
        /// </summary>
        public Coroutine FadeTo(float alpha, float seconds)
        {
            Build();
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(HoldRoutine(Mathf.Clamp01(alpha), seconds));
            return running;
        }

        private IEnumerator HoldRoutine(float alpha, float seconds)
        {
            yield return Ramp(blackout.color.a, alpha, seconds);
            running = null;
        }

        /// <summary>
        /// Fecha a preto, espera, e reabre. O <paramref name="whileBlack"/> corre
        /// com o ecrã totalmente tapado — é aí que se teleporta o jogador, se
        /// fecham portas ou se muda o que ele não pode ver a mudar.
        /// </summary>
        public Coroutine Blink(float fadeOutSeconds, float holdSeconds, float fadeInSeconds,
            System.Action whileBlack = null)
        {
            Build();
            if (running != null) StopCoroutine(running);
            running = StartCoroutine(BlinkRoutine(fadeOutSeconds, holdSeconds, fadeInSeconds, whileBlack));
            return running;
        }

        private IEnumerator BlinkRoutine(float fadeOutSeconds, float holdSeconds, float fadeInSeconds,
            System.Action whileBlack)
        {
            yield return Ramp(0f, 1f, fadeOutSeconds);

            blackout.color = Color.black;
            whileBlack?.Invoke();

            float held = 0f;
            while (held < holdSeconds) { held += Time.unscaledDeltaTime; yield return null; }

            yield return Ramp(1f, 0f, fadeInSeconds);
            blackout.color = new Color(0f, 0f, 0f, 0f);
            running = null;
        }

        private IEnumerator Ramp(float from, float to, float seconds)
        {
            if (seconds <= 0f) { blackout.color = new Color(0f, 0f, 0f, to); yield break; }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                blackout.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t));
                yield return null;
            }

            blackout.color = new Color(0f, 0f, 0f, to);
        }
    }
}
