using UnityEngine;

namespace Pungent.Atmosphere
{
    /// <summary>
    /// A imagem que esta no ecra do portatil na abertura.
    ///
    /// O anuncio das pecas do carro e a primeira coisa que o jogo mostra, antes de
    /// qualquer texto: e ele que explica porque e que o Tomas esta acordado as duas
    /// e quarenta e sete. Quando a ligacao cai, desaparece — e a perda ve-se em vez
    /// de ser anunciada.
    ///
    /// O ecra e emissivo e nao iluminado: um portatil aceso e uma fonte de luz, e
    /// com um material Lit ficava escuro num quarto sem candeeiro aceso, que e
    /// exactamente a situacao da abertura.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LaptopScreenImage : MonoBehaviour
    {
        [SerializeField] private Renderer image;
        [SerializeField] private bool on = true;

        [Tooltip("Brilho do ecra. Acima de 1 apanha o Bloom do Global Volume.")]
        [SerializeField, Min(0f)] private float intensity = 1.35f;
        [SerializeField] private Color tint = Color.white;

        [Header("Cintilacao")]
        [Tooltip("Um ecra de portatil nao e perfeitamente estavel. Muito subtil.")]
        [SerializeField, Range(0f, 0.2f)] private float flicker = 0.04f;

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;

        public bool IsOn => on;

        private void OnEnable() => Apply(1f);

        public void SetOn(bool value)
        {
            if (on == value) return;
            on = value;
            Apply(1f);
        }

        private void Update()
        {
            if (!on || flicker <= 0f) return;
            // Ruido lento em vez de aleatorio por frame: por frame le-se como erro
            // de renderizacao, nao como um ecra.
            float n = Mathf.PerlinNoise(Time.time * 3.1f, 0f);
            Apply(1f + (n - 0.5f) * 2f * flicker);
        }

        private void Apply(float level)
        {
            if (image == null) return;

            if (block == null) block = new MaterialPropertyBlock();
            image.GetPropertyBlock(block);
            Color c = tint * intensity * (on ? level : 0f);
            c.a = 1f;
            block.SetColor(BaseColour, c);
            image.SetPropertyBlock(block);

            image.enabled = on;
        }
    }
}
