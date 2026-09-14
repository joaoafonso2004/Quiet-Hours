using UnityEngine;

namespace Pungent.Atmosphere
{
    /// <summary>
    /// Poeira suspensa no ar, configurada por codigo.
    ///
    /// O efeito nao vem das particulas serem visiveis — a maior parte do tempo nao
    /// sao. Vem de existirem para a luz as apanhar: quando a lanterna do telemovel
    /// varre uma divisao, o feixe deixa de ser um cone chapado e passa a ter
    /// materia dentro. E por isso que o material tem de ser **Lit**: com um
    /// material Unlit o po fica igual as escuras e ao sol, e o efeito desaparece.
    ///
    /// Tudo e configurado no Start() em vez de ficar guardado no asset do
    /// ParticleSystem: assim os valores estao a vista e comentados aqui, e nao
    /// escondidos em vinte separadores de um Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class VolumetricDustURP : MonoBehaviour
    {
        [Header("Volume")]
        [Tooltip("Tamanho da caixa onde o po existe, em metros. Deve cobrir a divisao "
               + "com folga: as particulas sao criadas dentro dela e ficam paradas no "
               + "mundo, portanto uma caixa curta faz o po acabar a meio do ar.")]
        [SerializeField] private Vector3 volumeSize = new Vector3(6f, 3f, 6f);

        [Header("Densidade")]
        [Tooltip("Particulas por segundo. Baixo de proposito: po a mais le-se como "
               + "neve ou como sujidade na lente.")]
        [SerializeField, Range(1f, 60f)] private float rateOverTime = 14f;

        [Header("Ritmo")]
        [Tooltip("Quanto tempo cada grao vive. Longo, para nao haver renovacao visivel.")]
        [SerializeField] private Vector2 lifetime = new Vector2(5f, 10f);

        [Tooltip("Velocidade inicial. Quase nula: o po nao cai, fica suspenso.")]
        [SerializeField] private Vector2 speed = new Vector2(0.02f, 0.05f);

        [Tooltip("Tamanho de cada grao, em metros. A variacao e o que impede a "
               + "leitura de 'todas iguais'.")]
        [SerializeField] private Vector2 size = new Vector2(0.01f, 0.03f);

        [Header("Corrente de ar")]
        [Tooltip("Forca do ruido. Muito baixa: o que se quer e deriva, nao turbulencia.")]
        [SerializeField, Range(0f, 0.5f)] private float noiseStrength = 0.05f;

        [Tooltip("Escala do ruido. Baixa = graos vizinhos derivam juntos, como ar a "
               + "mexer-se em bloco; alta = cada um por si, que le como insectos.")]
        [SerializeField, Range(0.05f, 2f)] private float noiseFrequency = 0.18f;

        [Header("Material")]
        [Tooltip("Deixa vazio para criar um em runtime a partir de "
               + "'Universal Render Pipeline/Particles/Lit'. Preencher e mais seguro "
               + "para builds — ver a nota no fim deste ficheiro.")]
        [SerializeField] private Material dustMaterial;

        [Tooltip("Cor do po. Quase branco e translucido; a luz e que lhe da a cor.")]
        [SerializeField] private Color tint = new Color(0.86f, 0.86f, 0.82f, 0.35f);

        private const string LitParticleShader = "Universal Render Pipeline/Particles/Lit";

        private ParticleSystem system;

        private void Start()
        {
            system = GetComponent<ParticleSystem>();

            // As modificacoes tem de ser feitas com o sistema parado, senao o Unity
            // ignora parte delas e o prewarm nao acontece.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ConfigureMain();
            ConfigureEmission();
            ConfigureShape();
            ConfigureColourOverLifetime();
            ConfigureNoise();
            ConfigureRenderer();

            system.Play();
        }

        private void ConfigureMain()
        {
            var main = system.main;

            main.loop = true;
            // O prewarm so funciona com loop ligado e sem start delay. E ele que
            // evita a divisao aparecer limpa e ir enchendo de po a vista.
            main.prewarm = true;
            main.startDelay = 0f;

            main.duration = 10f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = tint;

            // Sem rotacao: um grao de po nao tem orientacao legivel, e roda-lo so
            // faz cintilar o billboard.
            main.startRotation = 0f;

            // World e a peca critica. Em Local, o po acompanhava o objecto a que
            // este script esta agarrado — andar pela divisao arrastava a poeira
            // toda atras do jogador.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Gravidade zero: isto e po suspenso, nao po a assentar.
            main.gravityModifier = 0f;

            main.maxParticles = Mathf.CeilToInt(rateOverTime * lifetime.y * 1.2f);
            main.playOnAwake = false;
        }

        private void ConfigureEmission()
        {
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = rateOverTime;
            // Nada de bursts: o po nao chega em rajadas.
            emission.rateOverDistance = 0f;
        }

        private void ConfigureShape()
        {
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = volumeSize;
            shape.position = Vector3.zero;
            shape.rotation = Vector3.zero;
            // Emitir do volume inteiro e nao so da casca, senao o po formava uma
            // caixa oca e via-se a aresta.
            shape.boxThickness = Vector3.one;
        }

        /// <summary>
        /// Alpha em gradiente: entra e sai devagar.
        ///
        /// Sem isto os graos aparecem e desaparecem de repente no meio do ar, e o
        /// olho apanha-o como cintilacao — sobretudo com a lanterna parada, que e
        /// exactamente quando o efeito devia estar a ajudar.
        /// </summary>
        private void ConfigureColourOverLifetime()
        {
            var colour = system.colorOverLifetime;
            colour.enabled = true;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.20f),
                    new GradientAlphaKey(1f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                });

            colour.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private void ConfigureNoise()
        {
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = noiseStrength;
            noise.frequency = noiseFrequency;

            // Devagar: o ar de uma sala fechada nao muda de direccao ao segundo.
            noise.scrollSpeed = 0.06f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            noise.octaveCount = 1;
        }

        private void ConfigureRenderer()
        {
            var renderer = GetComponent<ParticleSystemRenderer>();

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.material = ResolveMaterial();

            // O po nao lanca sombra — cada grao tem 2 cm e o custo nao se paga —
            // mas recebe luz, que e a razao de existir.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            // Nao contribui para a iluminacao global.
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            renderer.sortMode = ParticleSystemSortMode.Distance;
        }

        /// <summary>
        /// O material iluminado do URP.
        ///
        /// Preencher o campo no Inspector e o caminho seguro. O Shader.Find so
        /// encontra shaders incluidos no build, e os shaders de particulas do URP
        /// sao dos primeiros a ser removidos por stripping quando nada no projecto
        /// os referencia — funciona no editor e depois falha no build, que e o pior
        /// sitio para descobrir. Ter um material com este shader guardado num asset
        /// e o que garante que ele e incluido.
        /// </summary>
        private Material ResolveMaterial()
        {
            if (dustMaterial != null) return dustMaterial;

            Shader shader = Shader.Find(LitParticleShader);
            if (shader == null)
            {
                Debug.LogWarning(
                    $"[VolumetricDust] '{LitParticleShader}' nao encontrado. O po vai " +
                    "ficar com o material por omissao e deixa de reagir a luz. " +
                    "Cria um material com esse shader e arrasta-o para o campo " +
                    "'dustMaterial'.", this);
                return null;
            }

            var material = new Material(shader)
            {
                name = "M_Dust_Runtime",
                hideFlags = HideFlags.HideAndDontSave
            };

            // Transparente e aditivo-suave: o po clareia o que esta atras dele em
            // vez de o tapar.
            material.SetFloat("_Surface", 1f);            // 0 opaco, 1 transparente
            material.SetFloat("_Blend", 0f);              // alpha
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", 0f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            return material;
        }

        /// <summary>Mostra o volume no editor, para se poder enquadrar a divisao.</summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.8f, 0.8f, 0.7f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, volumeSize);
        }
    }
}
