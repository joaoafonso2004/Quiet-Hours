using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// O jogo passa-se as 02:47. A cena vinha do template com o ceu de dia, o
    /// nevoeiro azul claro e o ambiente praticamente a preto — a combinacao que
    /// fazia com que qualquer superficie sem luz directa perdesse a textura e
    /// ficasse uma mancha chapada.
    ///
    /// Este passo poe a hora certa e, sobretudo, poe um minimo de luz ambiente por
    /// todas as direccoes. Sem ele nao ha afinacao de candeeiros que safe as
    /// divisoes apagadas: multiplicar o albedo por zero da sempre zero.
    /// </summary>
    internal static class NightEnvironmentSetup
    {
        private const string NightSkybox = "Assets/Day-Night Skyboxes/Materials/SkyMidnight.mat";

        // A luz ambiente e a peca critica. Os valores foram apurados a olho contra
        // o quarto do Rui com tudo apagado: abaixo disto a mobilia vira silhueta,
        // acima disto deixa de ser noite. O gradiente vem de cima (luar frio) para
        // baixo (bounce morno do soalho), para as divisoes nao ficarem chapadas.
        private static readonly Color AmbientSky     = new Color(0.320f, 0.355f, 0.440f);
        private static readonly Color AmbientEquator = new Color(0.230f, 0.240f, 0.280f);
        private static readonly Color AmbientGround  = new Color(0.155f, 0.143f, 0.125f);

        [MenuItem("Tools/Pungent/Apply Night Environment (02:47)")]
        internal static void Apply()
        {
            ApplyEnvironment();
            ApplyMoonlight();

            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Night] Ambiente nocturno aplicado (ceu, ambiente, nevoeiro, luar).");
        }

        internal static void ApplyEnvironment()
        {
            var skybox = AssetDatabase.LoadAssetAtPath<Material>(NightSkybox);
            if (skybox != null) RenderSettings.skybox = skybox;
            else Debug.LogWarning($"[Night] Skybox em falta: {NightSkybox}");

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.ambientIntensity = 1f;

            // O nevoeiro estava azul claro de dia e linear a 15-65 m, o que punha
            // um veu diurno em cima da cidade vista das janelas.
            //
            // A densidade também tapa o fim do mapa: a cidade acaba a ±120 m e o
            // chão da rua acaba com ela. A 0.019 sobra ~3% de visibilidade a 100 m,
            // portanto o limite dissolve-se antes de se ver a aresta. Aos 40 m, onde
            // estão os prédios vizinhos, ainda só leva 43% — continua a ver-se a
            // cidade, só com ar entre as coisas.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            // Perto do azul do horizonte do skybox: se o nevoeiro fosse preto, os
            // prédios desapareciam contra um céu mais claro e via-se o recorte.
            RenderSettings.fogColor = new Color(0.045f, 0.055f, 0.085f);
            RenderSettings.fogDensity = 0.019f;

            // O cubemap de noite e quase preto: a 1.0 os materiais lisos ficavam
            // com um reflexo preto por cima do albedo.
            RenderSettings.defaultReflectionMode =
                UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = 256;
            RenderSettings.reflectionIntensity = 0.55f;
        }

        /// <summary>
        /// A direccional deixa de ser sol e passa a ser lua: rasante, vinda de sul,
        /// para entrar pelas janelas dos quartos em vez de cair a pique.
        /// </summary>
        internal static void ApplyMoonlight()
        {
            Light moon = FindDirectional();
            if (moon == null) { Debug.LogWarning("[Night] Sem luz direccional na cena."); return; }

            moon.gameObject.name = "Moonlight";
            moon.color = new Color(0.60f, 0.68f, 0.92f);
            moon.intensity = 0.35f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.85f;
            moon.transform.rotation = Quaternion.Euler(22f, 25f, 0f);
            RenderSettings.sun = moon;
        }

        private static Light FindDirectional()
        {
            foreach (var light in Object.FindObjectsOfType<Light>(true))
                if (light.type == LightType.Directional) return light;
            return null;
        }
    }
}
