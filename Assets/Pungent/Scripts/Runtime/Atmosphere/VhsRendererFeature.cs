using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Pungent.Atmosphere
{
    /// <summary>
    /// O interruptor do VHS, do lado do jogo.
    ///
    /// Estatico e nao um componente porque quem le isto e uma
    /// <see cref="ScriptableRendererFeature"/>, que vive num asset do URP e nao numa
    /// cena: nao ha `FindObjectOfType` que a alcance, e nao ha cena nenhuma de onde
    /// ela possa ser arrastada para um campo.
    ///
    /// O §12 do plano pede reducao e desactivacao do VHS, e a §7.1 diz que o filtro
    /// nunca pode esconder informacao de jogo. As duas coisas precisam disto.
    /// </summary>
    public static class VhsSettings
    {
        /// <summary>Desligado, nao ha passe nenhum: a imagem nem chega a ser copiada.</summary>
        public static bool Enabled = true;

        /// <summary>0 = imagem limpa, 1 = como o material do pacote foi afinado.</summary>
        public static float Intensity = 1f;

        /// <summary>
        /// Retangulo do ecra do telemovel em coordenadas de viewport (minX, minY,
        /// maxX, maxY). O VHS continua ligado; dentro desta zona so o bleed e o
        /// ruido sao atenuados para o texto continuar legivel.
        /// </summary>
        public static Vector4 PhoneClarityRect;

        /// <summary>0 = sem compensacao local, 1 = compensacao completa.</summary>
        public static float PhoneClarity;

        /// <summary>
        /// Quantos frames o passe ja desenhou.
        ///
        /// Existe porque um efeito de imagem que nao corre e um efeito de imagem
        /// subtil sao **exactamente a mesma coisa vista de fora**, e distingui-los a
        /// olho num apartamento bege custou-me meia hora de capturas de ecra a
        /// comparar enquadramentos que tinham mudado por outras razoes. Isto responde
        /// a pergunta com um numero.
        /// </summary>
        public static int FramesRendered;

        /// <summary>
        /// Repoe o que o jogo tem por omissao. Existe porque estes valores sao
        /// estaticos e **sobrevivem a sair do Play Mode**: sem isto, baixar a
        /// intensidade uma vez para ver uma coisa deixava o Editor com o VHS
        /// meio-apagado para sempre, sem nada no ecra a dizer porque.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Reset()
        {
            Enabled = true;
            Intensity = 1f;
            PhoneClarityRect = Vector4.zero;
            PhoneClarity = 0f;
            FramesRendered = 0;
        }
    }

    /// <summary>
    /// O VHS, em URP.
    ///
    /// O pacote F2F_VhsFree traz o efeito como `OnRenderImage` num componente da
    /// camara. **Esse metodo nunca e chamado em URP** — nao da erro, nao da aviso, e
    /// nao faz absolutamente nada. Era isto que o backlog chamava "o item mais caro
    /// desta lista e o mais visivel", e o custo dele e todo aqui: o efeito em si sao
    /// vinte linhas de shader que ja estavam escritas.
    ///
    /// **Corre antes do pos-processamento, e isso custou uma hora a descobrir.**
    /// Depois do pos-processamento e o sitio que faz sentido no papel — a fita por
    /// cima da imagem acabada — e e o sitio errado: nessa altura o
    /// `cameraColorTargetHandle` ja nao e a textura que vai para o ecra. O passe
    /// corre, o filtro e calculado, escreve-se numa textura que ninguem volta a ler,
    /// e a imagem chega ao jogador limpa. **Sem erro, sem aviso, e com o contador de
    /// frames a subir.** E indistinguivel, de fora, de um efeito subtil demais — que
    /// e exactamente o que eu andei a concluir a olhar para capturas de ecra de um
    /// apartamento bege.
    ///
    /// O <see cref="VhsSettings.FramesRendered"/> existe por causa disto e nao deve
    /// ser apagado: e a unica maneira barata de separar "nao corre" de "nao se ve".
    ///
    /// **O texto nao e afectado, e isso nao e sorte.** O plano e categorico — os
    /// efeitos VHS nunca podem tornar as legendas dificeis de ler (§7.1, §12.1). O
    /// canvas do dialogo e o HUD sao `ScreenSpaceOverlay`, que o Unity compoe **fora**
    /// do pipeline, depois de tudo isto. Quem mudar esses canvas para
    /// `ScreenSpaceCamera` um dia esta a meter o texto dentro do filtro, e e por isso
    /// que fica escrito aqui e nao so no documento.
    ///
    /// Nao corre na Scene view nem nas camaras de preview: o filtro e para quem
    /// joga, e trabalhar dentro de uma cena com a imagem torta e um imposto.
    /// </summary>
    public sealed class VhsRendererFeature : ScriptableRendererFeature
    {
        [Tooltip("O material do pacote, com a afinacao que veio nele. O shader e "
               + "trocado pelo porte URP pela ferramenta `Wire VHS`.")]
        [SerializeField] private Material material;

        [Tooltip("**Antes** do pos-processamento, e nao depois.\n\n"
               + "Depois parece o sitio certo — a fita por cima da imagem acabada — "
               + "e nao e: nesse ponto o `cameraColorTargetHandle` ja nao e a textura "
               + "que vai para o ecra. O passe corre, o filtro e calculado, e o "
               + "resultado e deitado fora. Sem erro, sem aviso, e com o contador de "
               + "frames a subir alegremente.")]
        [SerializeField] private RenderPassEvent injection = RenderPassEvent.BeforeRenderingPostProcessing;

        private VhsPass pass;
        private Material runtimeMaterial;

        public override void Create()
        {
            // Uma copia, e nao o asset. O passe escreve `_Intensity` e `_TimeSeconds`
            // a cada frame; escrevendo no asset, cada corrida do jogo deixava o
            // ficheiro do material sujo no disco e o git com uma alteracao que
            // ninguem fez.
            if (material != null && runtimeMaterial == null)
                runtimeMaterial = CoreUtils.CreateEngineMaterial(material.shader);

            if (runtimeMaterial != null && material != null)
                runtimeMaterial.CopyPropertiesFromMaterial(material);

            pass = new VhsPass(runtimeMaterial, material) { renderPassEvent = injection };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!VhsSettings.Enabled || VhsSettings.Intensity <= 0f) return;
            if (runtimeMaterial == null) return;

            // So a camara do jogo. `CameraType.SceneView` e `Preview` ficam limpas.
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            renderer.EnqueuePass(pass);
        }

        /// <summary>
        /// O alvo so pode ser pedido aqui.
        ///
        /// Pedir o `cameraColorTargetHandle` dentro do `AddRenderPasses` da um erro
        /// em todos os frames a dizer que ainda nao existe — nessa altura o renderer
        /// ainda nao criou os seus alvos. E um erro barulhento e por isso barato,
        /// mas so na primeira vez que se escreve isto.
        /// </summary>
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (pass == null) return;
            pass.SetSource(renderer.cameraColorTargetHandle);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            CoreUtils.Destroy(runtimeMaterial);
            runtimeMaterial = null;
        }

        // ------------------------------------------------------------------

        private sealed class VhsPass : ScriptableRenderPass
        {
            private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
            private static readonly int TimeId = Shader.PropertyToID("_TimeSeconds");
            private static readonly int PhoneClarityRectId = Shader.PropertyToID("_PhoneClarityRect");
            private static readonly int PhoneClarityId = Shader.PropertyToID("_PhoneClarity");

            private readonly Material material;
            private RTHandle source;
            private RTHandle temp;

            private readonly Material authored;

            internal VhsPass(Material runtime, Material authored)
            {
                this.material = runtime;
                this.authored = authored;
                profilingSampler = new ProfilingSampler("Pungent VHS");
            }

            internal void SetSource(RTHandle handle) => source = handle;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;

                // Sem profundidade e sem MSAA: isto e uma copia de cor. Herdar os dois
                // do alvo da camara aloca um buffer de profundidade por frame que
                // ninguem le, e num alvo com MSAA a copia rebenta.
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;

                RenderingUtils.ReAllocateIfNeeded(ref temp, descriptor, FilterMode.Bilinear,
                    TextureWrapMode.Clamp, name: "_PungentVhsTemp");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (material == null || source == null || temp == null) return;

#if UNITY_EDITOR
                // No Editor, a copia e refeita a cada frame a partir do asset.
                //
                // Sem isto, afinar o efeito era impossivel: o `Create()` so volta a
                // correr quando o renderer e reconstruido, portanto mexer no bleed ou
                // no fisheye do material com o jogo a correr **nao mudava nada no
                // ecra**. Quem estivesse a afinar concluia que o efeito nao estava
                // ligado, e a conclusao era falsa. Num build isto nao existe: la o
                // material nao muda.
                if (authored != null) material.CopyPropertiesFromMaterial(authored);
#endif

                material.SetFloat(IntensityId, Mathf.Clamp01(VhsSettings.Intensity));
                material.SetVector(PhoneClarityRectId, VhsSettings.PhoneClarityRect);
                material.SetFloat(PhoneClarityId, Mathf.Clamp01(VhsSettings.PhoneClarity));

                // `Time.unscaledTime` e nao `Time.time`: uma fita nao para de chiar
                // porque alguem abriu o menu.
                material.SetFloat(TimeId, Time.unscaledTime);

                VhsSettings.FramesRendered++;

                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    // Ida e volta. Nao se pode ler e escrever no mesmo alvo no mesmo
                    // passe: o resultado disso e ruido a acumular sobre si proprio.
                    Blitter.BlitCameraTexture(cmd, source, temp, material, 0);
                    Blitter.BlitCameraTexture(cmd, temp, source);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            internal void Dispose()
            {
                temp?.Release();
                temp = null;
            }
        }
    }
}
