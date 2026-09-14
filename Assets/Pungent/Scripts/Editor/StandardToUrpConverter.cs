using UnityEditor;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Converte materiais do shader Built-in `Standard` para `Universal Render
    /// Pipeline/Lit`.
    ///
    /// Um material Standard num projeto URP não tem shader válido e é desenhado a
    /// magenta. A conversão não é só trocar o shader: os nomes das propriedades
    /// mudam (`_MainTex` -> `_BaseMap`, `_Color` -> `_BaseColor`,
    /// `_Glossiness` -> `_Smoothness`), e trocar o shader primeiro apaga os valores
    /// antigos. Por isso lê-se tudo antes e reaplica-se depois.
    /// </summary>
    public static class StandardToUrpConverter
    {
        [MenuItem("Pungent/Blockout/Convert Standard Materials to URP", false, 60)]
        public static void ConvertAll()
        {
            Convert("Assets");
        }

        public static void Convert(string folder)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[URP] Shader 'Universal Render Pipeline/Lit' nao encontrado.");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
            int converted = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                if (material.shader == null) continue;
                if (material.shader.name != "Standard" && material.shader.name != "Standard (Specular setup)")
                    continue;

                // --- ler tudo antes de trocar o shader ---
                Texture main = Get(material, "_MainTex");
                Vector2 mainScale = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
                Vector2 mainOffset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;
                Color colour = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                Texture bump = Get(material, "_BumpMap");
                float bumpScale = material.HasProperty("_BumpScale") ? material.GetFloat("_BumpScale") : 1f;
                Texture metallicMap = Get(material, "_MetallicGlossMap");
                float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
                float glossiness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f;
                Texture occlusion = Get(material, "_OcclusionMap");
                Texture emissionMap = Get(material, "_EmissionMap");
                Color emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                bool emissive = material.IsKeywordEnabled("_EMISSION");
                float cutoff = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : 0.5f;
                int mode = material.HasProperty("_Mode") ? (int)material.GetFloat("_Mode") : 0;

                // --- trocar e reaplicar ---
                material.shader = urpLit;

                if (main != null) material.SetTexture("_BaseMap", main);
                material.SetTextureScale("_BaseMap", mainScale);
                material.SetTextureOffset("_BaseMap", mainOffset);
                material.SetColor("_BaseColor", colour);

                if (bump != null)
                {
                    material.SetTexture("_BumpMap", bump);
                    material.SetFloat("_BumpScale", bumpScale);
                    material.EnableKeyword("_NORMALMAP");
                }

                if (metallicMap != null)
                {
                    material.SetTexture("_MetallicGlossMap", metallicMap);
                    material.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                material.SetFloat("_Metallic", metallic);
                material.SetFloat("_Smoothness", glossiness);

                if (occlusion != null)
                {
                    material.SetTexture("_OcclusionMap", occlusion);
                    material.EnableKeyword("_OCCLUSIONMAP");
                }

                if (emissive && (emissionMap != null || emission.maxColorComponent > 0f))
                {
                    if (emissionMap != null) material.SetTexture("_EmissionMap", emissionMap);
                    material.SetColor("_EmissionColor", emission);
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                ApplySurfaceMode(material, mode, cutoff);
                EditorUtility.SetDirty(material);
                converted++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[URP] {converted} materiais Standard convertidos para URP/Lit em '{folder}'.");
        }

        private static Texture Get(Material material, string property)
            => material.HasProperty(property) ? material.GetTexture(property) : null;

        /// <summary>Traduz o _Mode do Standard para as flags de superficie do URP.</summary>
        private static void ApplySurfaceMode(Material material, int mode, float cutoff)
        {
            // 0 Opaque, 1 Cutout, 2 Fade, 3 Transparent
            bool transparent = mode == 2 || mode == 3;
            material.SetFloat("_Surface", transparent ? 1f : 0f);
            material.SetFloat("_AlphaClip", mode == 1 ? 1f : 0f);
            material.SetFloat("_Cutoff", cutoff);

            if (mode == 1) material.EnableKeyword("_ALPHATEST_ON");
            else material.DisableKeyword("_ALPHATEST_ON");

            if (transparent)
            {
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                material.SetFloat("_ZWrite", 1f);
                material.renderQueue = mode == 1
                    ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest
                    : (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }
        }
    }
}
