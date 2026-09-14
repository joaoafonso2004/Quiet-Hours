using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Textura o mobiliário Quaternius.
    ///
    /// O pack é de cor plana e as malhas **não têm UVs nenhumas**, por isso nenhum
    /// mapa pega nelas: qualquer textura amostraria sempre o mesmo texel. A solução
    /// aqui é gerar UVs por projeção em caixa (a face de cada triângulo é projetada
    /// no plano do eixo dominante da sua normal) e guardar malhas novas como assets,
    /// sem tocar nos FBX originais.
    ///
    /// As cores originais do pack são preservadas em `_BaseColor`: a textura entra
    /// como grão de superfície, não substitui a direção de arte.
    /// </summary>
    public static class FurnitureTexturePass
    {
        private const string MeshFolder = "Assets/Pungent/Meshes";
        private const string TextureRoot = "Assets/ThirdParty/PolyHaven/";

        /// <summary>Metros de mundo por repetição, já a contar com a escala típica dos props.</summary>
        private const float UvScale = 1.6f;

        private enum Surface { Wood, Plywood, Fabric, Metal, Tile }

        /// <summary>Material do Quaternius -> tipo de superfície.</summary>
        private static readonly Dictionary<string, Surface> MaterialSurfaces = new Dictionary<string, Surface>
        {
            { "Wood", Surface.Wood },
            { "Wood_Dark", Surface.Wood },
            { "Wood_Light", Surface.Wood },
            { "Brown", Surface.Wood },
            { "DarkRed", Surface.Wood },
            { "Kitchen", Surface.Plywood },
            { "KitchenTop", Surface.Plywood },
            { "Couch_Beige", Surface.Fabric },
            { "Couch_BeigeDark", Surface.Fabric },
            { "Couch_Blue", Surface.Fabric },
            { "Cushin", Surface.Fabric },
            { "Grey", Surface.Metal },
            { "White", Surface.Metal },
            { "Light", Surface.Metal },
            { "LightMetal", Surface.Metal },
            { "DarkMetal", Surface.Metal },
            { "Metal", Surface.Metal },
            { "Black", Surface.Metal },
            { "LightOrange", Surface.Wood },
            { "Red", Surface.Metal },
            { "DarkGreen", Surface.Fabric },
            { "Plant_Green", Surface.Fabric },
            { "Mirror", Surface.Metal },
            { "Glass", Surface.Metal },
        };

        private static readonly Dictionary<Surface, (string Texture, float Smoothness)> SurfaceTextures =
            new Dictionary<Surface, (string, float)>
            {
                { Surface.Wood, ("wood_table_001", 0.24f) },
                { Surface.Plywood, ("plywood", 0.20f) },
                { Surface.Fabric, ("cotton_jersey", 0.06f) },
                { Surface.Metal, ("metal_plate", 0.42f) },
                { Surface.Tile, ("interior_tiles", 0.45f) },
            };

        [MenuItem("Pungent/Blockout/Texture Furniture", false, 50)]
        public static void Run()
        {
            if (BuildGuard.Blocked("TexturaMobiliario")) return;

            EnsureFolder();
            var materials = BuildSurfaceMaterials();

            int meshesBuilt = 0, renderersTouched = 0;
            var cache = new Dictionary<Mesh, Mesh>();

            foreach (var filter in Object.FindObjectsOfType<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;
                if (mesh.uv != null && mesh.uv.Length > 0) continue; // já tem UVs

                if (!cache.TryGetValue(mesh, out Mesh replacement))
                {
                    replacement = BuildProjectedMesh(mesh);
                    if (replacement != null) meshesBuilt++;
                    cache[mesh] = replacement;
                }

                if (replacement != null)
                    filter.sharedMesh = replacement;
            }

            foreach (var renderer in Object.FindObjectsOfType<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < slots.Length; i++)
                {
                    var material = slots[i];
                    if (material == null) continue;
                    if (!MaterialSurfaces.TryGetValue(material.name, out Surface surface)) continue;
                    if (!materials.TryGetValue(surface, out Material textured)) continue;

                    // Uma variante por cor original, para não perder a paleta do pack.
                    slots[i] = GetTintedVariant(textured, material, surface);
                    changed = true;
                }

                if (!changed) continue;
                renderer.sharedMaterials = slots;
                renderersTouched++;
            }

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log($"[TexturaMobiliario] {meshesBuilt} malhas com UVs geradas, {renderersTouched} renderers retexturados.");
        }

        // ------------------------------------------------------------------

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder))
                AssetDatabase.CreateFolder("Assets/Pungent", "Meshes");
        }

        /// <summary>
        /// Copia a malha e acrescenta UVs projetadas na caixa. O eixo de projeção é
        /// escolhido pela normal de cada vértice, o que evita esticões nas laterais.
        /// </summary>
        private static Mesh BuildProjectedMesh(Mesh source)
        {
            var vertices = source.vertices;
            if (vertices.Length == 0) return null;

            var normals = source.normals;
            bool hasNormals = normals != null && normals.Length == vertices.Length;

            var uvs = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                Vector3 n = hasNormals ? normals[i] : Vector3.up;
                float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);

                if (ax >= ay && ax >= az) uvs[i] = new Vector2(v.z, v.y) * UvScale;
                else if (ay >= ax && ay >= az) uvs[i] = new Vector2(v.x, v.z) * UvScale;
                else uvs[i] = new Vector2(v.x, v.y) * UvScale;
            }

            var mesh = Object.Instantiate(source);
            mesh.name = source.name + "_UV";
            mesh.uv = uvs;
            if (!hasNormals) mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            string path = AssetDatabase.GenerateUniqueAssetPath($"{MeshFolder}/{mesh.name}.asset");
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Dictionary<Surface, Material> BuildSurfaceMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var result = new Dictionary<Surface, Material>();

            foreach (var kv in SurfaceTextures)
            {
                string name = "M_Surf_" + kv.Key;
                string path = "Assets/Pungent/Materials/" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }

                material.shader = shader;
                string texture = kv.Value.Texture;
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                    $"{TextureRoot}{texture}/{texture}_diff_1k.jpg"));
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    $"{TextureRoot}{texture}/{texture}_nor_gl_1k.jpg");
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                    material.SetFloat("_BumpScale", 0.6f);
                }
                material.SetFloat("_Smoothness", kv.Value.Smoothness);
                material.SetFloat("_Metallic", 0f);
                EditorUtility.SetDirty(material);
                result[kv.Key] = material;
            }

            return result;
        }

        /// <summary>
        /// Variante da superfície tingida com a cor original do Quaternius, para o
        /// sofá continuar vermelho e a bancada continuar clara depois de texturados.
        /// </summary>
        private static Material GetTintedVariant(Material baseMaterial, Material original, Surface surface)
        {
            Color tint = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor")
                       : original.HasProperty("_Color") ? original.GetColor("_Color")
                       : Color.white;

            string name = $"M_Surf_{surface}_{original.name}";
            string path = "Assets/Pungent/Materials/" + name + ".mat";
            var variant = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (variant == null)
            {
                variant = new Material(baseMaterial);
                AssetDatabase.CreateAsset(variant, path);
            }

            variant.shader = baseMaterial.shader;
            variant.CopyPropertiesFromMaterial(baseMaterial);
            // A textura clareia a cor: compensa-se um pouco para não desbotar.
            variant.SetColor("_BaseColor", tint * 1.25f);
            EditorUtility.SetDirty(variant);
            return variant;
        }
    }
}
