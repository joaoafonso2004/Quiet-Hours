using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pungent.Atmosphere
{
    /// <summary>
    /// Troca o ambiente entre noite e manha em runtime.
    ///
    /// O `NightEnvironmentSetup` faz isto no editor, uma vez, para deixar a cena as
    /// 02:47. Mas a slice passa a ter dois dias, e a passagem de um para o outro
    /// acontece com o jogo a correr — atras de um ecra preto, enquanto o jogador
    /// dorme. Os valores da noite sao os mesmos que la estao, apurados contra o
    /// quarto do Rui com tudo apagado.
    ///
    /// A luz ambiente e a peca critica nos dois casos: multiplicar o albedo por
    /// zero da sempre zero, e sem um minimo em todas as direccoes nao ha afinacao
    /// de candeeiro que safe uma divisao apagada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayCycleController : MonoBehaviour
    {
        [Serializable]
        public sealed class Profile
        {
            public string Name = "Night";
            public Material Skybox;

            [Header("Luz ambiente")]
            public Color AmbientSky = new Color(0.320f, 0.355f, 0.440f);
            public Color AmbientEquator = new Color(0.230f, 0.240f, 0.280f);
            public Color AmbientGround = new Color(0.155f, 0.143f, 0.125f);
            [Min(0f)] public float AmbientIntensity = 1f;
            [Min(0f)] public float ReflectionIntensity = 0.55f;

            [Header("Nevoeiro")]
            public bool Fog = true;
            public Color FogColour = new Color(0.045f, 0.055f, 0.085f);
            [Min(0f)] public float FogDensity = 0.019f;

            [Header("Direccional")]
            public string SunName = "Moonlight";
            public Color SunColour = new Color(0.60f, 0.68f, 0.92f);
            [Min(0f)] public float SunIntensity = 0.35f;
            public Vector3 SunEuler = new Vector3(22f, 25f, 0f);
            [Range(0f, 1f)] public float ShadowStrength = 0.85f;
        }

        [SerializeField] private Light directional;
        [SerializeField] private Profile night = new Profile();

        [SerializeField]
        private Profile morning = new Profile
        {
            Name = "Morning",
            // Muito mais luz de todas as direccoes: de dia o interior le-se sem
            // depender de um unico candeeiro.
            AmbientSky = new Color(0.62f, 0.68f, 0.80f),
            AmbientEquator = new Color(0.52f, 0.52f, 0.52f),
            AmbientGround = new Color(0.34f, 0.31f, 0.28f),
            AmbientIntensity = 1f,
            ReflectionIntensity = 1f,
            Fog = true,
            // Cinzento palido em vez do azul quase preto: a cidade tem de se ver.
            FogColour = new Color(0.62f, 0.66f, 0.72f),
            FogDensity = 0.006f,
            SunName = "Sunlight",
            SunColour = new Color(1f, 0.95f, 0.86f),
            SunIntensity = 1.15f,
            // Rasante do leste: manha cedo, sombras compridas pela sala.
            SunEuler = new Vector3(24f, -110f, 0f),
            ShadowStrength = 0.72f,
        };

        public Profile Night => night;
        public Profile Morning => morning;

        private void Awake()
        {
            if (directional == null) directional = FindDirectional();
        }

        public void ApplyNight() => Apply(night);
        public void ApplyMorning() => Apply(morning);

        public void Apply(Profile profile)
        {
            if (profile == null) return;

            if (profile.Skybox != null) RenderSettings.skybox = profile.Skybox;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = profile.AmbientSky;
            RenderSettings.ambientEquatorColor = profile.AmbientEquator;
            RenderSettings.ambientGroundColor = profile.AmbientGround;
            RenderSettings.ambientIntensity = profile.AmbientIntensity;

            RenderSettings.fog = profile.Fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = profile.FogColour;
            RenderSettings.fogDensity = profile.FogDensity;

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = profile.ReflectionIntensity;

            if (directional == null) directional = FindDirectional();
            if (directional != null)
            {
                directional.gameObject.name = profile.SunName;
                directional.color = profile.SunColour;
                directional.intensity = profile.SunIntensity;
                directional.shadows = LightShadows.Soft;
                directional.shadowStrength = profile.ShadowStrength;
                directional.transform.rotation = Quaternion.Euler(profile.SunEuler);
                RenderSettings.sun = directional;
            }

            // Sem isto a luz ambiente nova so entra no frame seguinte a um bake, e a
            // troca acontece atras de um ecra preto que dura pouco mais do que isso.
            DynamicGI.UpdateEnvironment();
        }

        private static Light FindDirectional()
        {
            foreach (var light in FindObjectsOfType<Light>(true))
                if (light.type == LightType.Directional) return light;
            return null;
        }
    }
}
