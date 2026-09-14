using UnityEngine;

namespace Pungent.Atmosphere
{
    /// <summary>
    /// Da imagem ao ecra da televisao.
    ///
    /// O `TelevisionScreen` faz a luz: pisca, corta, e derrama azul pela sala. Isso
    /// resolve a televisao **vista de lado** — o que se quer da sala e a luz. Visto
    /// de frente, porem, o ecra era um rectangulo de cor lisa a mudar de brilho, e
    /// e ai que ninguem acredita nele.
    ///
    /// Nao ha nenhum video no projecto e nao vale a pena arranjar um: uma emissao
    /// reconhecivel obrigava a decidir o que esta a dar, e a televisao passava a ser
    /// conteudo em vez de mobiliario. Estatica resolve tudo — e de graca, e e a
    /// unica coisa que uma televisao pode estar a dar as tres da manha sem levantar
    /// perguntas.
    ///
    /// A textura e gerada aqui, pequena e a cores de propósito: 64 pixeis chegam
    /// para o ecra desta escala, e a granulacao grossa e mais parecida com um
    /// aparelho velho do que ruido fino.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TelevisionScreen))]
    public sealed class TelevisionStatic : MonoBehaviour
    {
        [SerializeField] private Renderer screen;

        [Tooltip("Lado da textura, em pixeis. Grosso e melhor: fino le-se como "
               + "ruido digital, grosso le-se como antena.")]
        [SerializeField, Range(16, 128)] private int resolution = 64;

        [Tooltip("Quantas vezes por segundo a imagem muda. Uma televisao analogica "
               + "com chuva anda pelos 12-15; mais do que isso e so brilho.")]
        [SerializeField, Range(4f, 30f)] private float framesPerSecond = 14f;

        [Tooltip("Quanto da estatica e cinzenta e quanto puxa ao azul do tubo.")]
        [SerializeField] private Color coolTint = new Color(0.72f, 0.82f, 1f);

        [Tooltip("Bandas horizontais que atravessam a imagem, como um sinal fraco.")]
        [SerializeField, Range(0f, 1f)] private float rollingBand = 0.35f;

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

        private TelevisionScreen tv;
        private Texture2D noise;
        private Color32[] pixels;
        private MaterialPropertyBlock block;
        private float nextFrameAt;
        private float bandOffset;

        private void Awake()
        {
            tv = GetComponent<TelevisionScreen>();
            if (screen == null && tv != null) screen = tv.GlowRenderer;
            EnsureResources();
        }

        /// <summary>
        /// A textura, o buffer e o bloco de propriedades, criados a cabeca do uso.
        ///
        /// Estavam so no `Awake`, e o `Awake` **nao volta a correr** quando o Unity
        /// recompila com o jogo a andar: o domain reload repoe os campos nao
        /// serializados a nulo e o componente continua vivo, agora sem nada nas
        /// maos. O resultado era um `ArgumentNullException` por frame vindo do
        /// `GetPropertyBlock` — a consola cheia, e os erros que interessavam
        /// enterrados por baixo, que numa fase de teste a mao e o que custa caro.
        ///
        /// O `TelevisionScreen` ja tinha aprendido isto para o bloco dele; aqui sao
        /// tres campos em vez de um. Tambem serve de guarda a resolucao ser mudada
        /// no inspector com o jogo a correr.
        /// </summary>
        private void EnsureResources()
        {
            if (block == null) block = new MaterialPropertyBlock();

            if (noise == null || noise.width != resolution)
            {
                if (noise != null) Destroy(noise);

                // Point + Repeat: interpolar estatica transforma-a numa nevoa cinzenta.
                noise = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Repeat
                };
            }

            if (pixels == null || pixels.Length != resolution * resolution)
                pixels = new Color32[resolution * resolution];
        }

        private void OnDestroy()
        {
            if (noise != null) Destroy(noise);
        }

        private void Update()
        {
            // O `tv` tambem se perde num domain reload, e sem ele a televisao
            // desligada voltava a escrever ruido por cima do ecra preto.
            if (tv == null) tv = GetComponent<TelevisionScreen>();
            if (screen == null && tv != null) screen = tv.GlowRenderer;
            if (screen == null) return;

            // Desligada nao tem imagem nenhuma. O `TelevisionScreen` ja poe o ecra
            // preto; escrever ruido por cima acendia uma televisao desligada.
            if (tv != null && !tv.IsOn) return;

            if (Time.time < nextFrameAt) return;
            nextFrameAt = Time.time + 1f / framesPerSecond;

            Redraw();
        }

        private void Redraw()
        {
            EnsureResources();

            bandOffset = Mathf.Repeat(bandOffset + 0.07f, 1f);
            int bandRow = Mathf.RoundToInt(bandOffset * resolution);

            for (int y = 0; y < resolution; y++)
            {
                // A banda que rola: uma faixa mais clara e achatada, que e o que um
                // sinal fraco faz a subir pelo ecra.
                int distance = Mathf.Abs(y - bandRow);
                float band = distance < 3 ? rollingBand * (1f - distance / 3f) : 0f;

                for (int x = 0; x < resolution; x++)
                {
                    float v = Random.value;
                    v = Mathf.Clamp01(v * 0.85f + band);

                    byte r = (byte)(v * coolTint.r * 255f);
                    byte g = (byte)(v * coolTint.g * 255f);
                    byte b = (byte)(v * coolTint.b * 255f);
                    pixels[y * resolution + x] = new Color32(r, g, b, 255);
                }
            }

            noise.SetPixels32(pixels);
            noise.Apply(false);

            // Bloco de propriedades e nao o material: partilhado, mudava o ecra de
            // todas as televisoes que usassem o mesmo material.
            screen.GetPropertyBlock(block);
            block.SetTexture(BaseMap, noise);
            screen.SetPropertyBlock(block);
        }
    }
}
