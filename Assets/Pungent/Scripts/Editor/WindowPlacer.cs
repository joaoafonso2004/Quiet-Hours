using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Mete vidros nos buracos das paredes exteriores.
    ///
    /// O blockout cria cada vao como um par de pecas com o mesmo nome e o mesmo
    /// indice — `Ext_South_sill3` por baixo e `Ext_South_head3` por cima — e o
    /// buraco e o espaco entre as duas. E dai que sai a medida da janela, e nao de
    /// uma tabela a parte: se alguem mexer na parede, a janela acompanha.
    ///
    /// Os vaos sem peitoril (`Ext_North_head3`, `Ext_East_head1`) sao portas — a
    /// varanda e a porta da entrada — e ficam de fora exactamente por isso.
    /// </summary>
    internal static class WindowPlacer
    {
        private const string WindowsRoot = "WINDOWS_V2";
        private const string ModelPath = "Assets/ThirdParty/window.fbx";
        private const string FrameMaterialPath = "Assets/Pungent/Materials/M_Window_Frame.mat";
        private const string GlassMaterialPath = "Assets/Pungent/Materials/M_Window_Glass.mat";

        private const string ColourTexture = "Assets/ThirdParty/Textures/co.png";
        private const string NormalTexture = "Assets/ThirdParty/Textures/no.png";
        private const string RoughnessTexture = "Assets/ThirdParty/Textures/r.png";
        private const string GlassTexture = "Assets/ThirdParty/Textures/glass.png";

        // Medidas do FBX a escala 1: 3.44 de largo, 2.56 de alto, 0.31 de fundo,
        // com o centro dos renderers 0.02 a frente da origem do objecto.
        private static readonly Vector3 ModelSize = new Vector3(3.44f, 2.56f, 0.31f);
        private static readonly Vector3 ModelCentre = new Vector3(0f, 0f, 0.02f);

        private struct Opening
        {
            public string Name;
            public Vector3 Centre;
            public float Width;      // ao longo da parede
            public float Height;
            public float Thickness;  // espessura da parede
            public float Yaw;        // rotacao para a janela ficar de frente para o vao
        }

        [MenuItem("Tools/Pungent/Place Windows")]
        internal static void Run()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            var structure = GameObject.Find("BLOCKOUT_V2/STRUCTURE");
            if (structure == null) { Debug.LogError("[Windows] BLOCKOUT_V2/STRUCTURE nao encontrado."); return; }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) { Debug.LogError($"[Windows] Modelo em falta: {ModelPath}"); return; }

            Material frame = BuildFrameMaterial();
            Material glass = BuildGlassMaterial();

            var old = ApartmentV2WiringUtil.Find(scene, WindowsRoot);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(WindowsRoot);
            Undo.RegisterCreatedObjectUndo(root, "Place windows");

            List<Opening> openings = CollectOpenings(structure.transform);
            foreach (var opening in openings)
                Place(model, root.transform, opening, frame, glass);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Windows] {openings.Count} janelas colocadas em {WindowsRoot}.");
        }

        /// <summary>
        /// Empareilha cada `*_sillN` com o `*_headN` do mesmo grupo. So paredes
        /// exteriores: os vaos interiores sao passagens, nao levam vidro.
        /// </summary>
        private static List<Opening> CollectOpenings(Transform structure)
        {
            var result = new List<Opening>();

            foreach (Transform group in structure)
            {
                if (!group.name.StartsWith("Ext_")) continue;

                var sills = new Dictionary<string, Renderer>();
                var heads = new Dictionary<string, Renderer>();

                foreach (Transform piece in group)
                {
                    var renderer = piece.GetComponent<Renderer>();
                    if (renderer == null) continue;

                    int sill = piece.name.IndexOf("_sill", System.StringComparison.Ordinal);
                    int head = piece.name.IndexOf("_head", System.StringComparison.Ordinal);
                    if (sill >= 0) sills[piece.name.Substring(sill + 5)] = renderer;
                    else if (head >= 0) heads[piece.name.Substring(head + 5)] = renderer;
                }

                foreach (var pair in sills)
                {
                    if (!heads.TryGetValue(pair.Key, out Renderer head)) continue;   // vao de porta

                    Bounds sillBounds = pair.Value.bounds;
                    Bounds headBounds = head.bounds;

                    // O eixo fino da peca e a espessura da parede; o outro e o
                    // comprimento do vao.
                    bool thinInZ = sillBounds.size.z <= sillBounds.size.x;
                    float width = thinInZ ? sillBounds.size.x : sillBounds.size.z;
                    float thickness = thinInZ ? sillBounds.size.z : sillBounds.size.x;

                    float bottom = sillBounds.max.y;
                    float top = headBounds.min.y;
                    if (top - bottom <= 0.05f) continue;

                    result.Add(new Opening
                    {
                        Name = $"Window_{group.name.Substring(4)}_{pair.Key}",
                        Centre = new Vector3(sillBounds.center.x, (bottom + top) * 0.5f, sillBounds.center.z),
                        Width = width,
                        Height = top - bottom,
                        Thickness = thickness,
                        // Parede fina em Z => a janela olha para Z (yaw 0);
                        // parede fina em X => olha para X (yaw 90).
                        Yaw = thinInZ ? 0f : 90f
                    });
                }
            }

            return result;
        }

        private static void Place(GameObject model, Transform parent, Opening opening,
            Material frame, Material glass)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = opening.Name;
            instance.transform.SetParent(parent, false);

            Quaternion rotation = Quaternion.Euler(0f, opening.Yaw, 0f);
            // Um pelo abaixo do vao, para o aro nao pontar com as faces da parede.
            var scale = new Vector3(
                (opening.Width - 0.01f) / ModelSize.x,
                (opening.Height - 0.01f) / ModelSize.y,
                (opening.Thickness * 0.85f) / ModelSize.z);

            instance.transform.rotation = rotation;
            instance.transform.localScale = scale;
            // O centro dos renderers nao coincide com a origem do FBX: sem isto a
            // janela assentava desviada meio centimetro para fora da parede.
            instance.transform.position = opening.Centre -
                rotation * Vector3.Scale(ModelCentre, scale);

            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    // O FBX vem com o slot de vidro em segundo e o aro no primeiro.
                    bool isGlass = materials[i] != null &&
                                   materials[i].name.ToLowerInvariant().Contains("glass");
                    materials[i] = isGlass ? glass : frame;
                }
                renderer.sharedMaterials = materials;
                renderer.gameObject.isStatic = true;
            }

            // Sem colisor o jogador podia encostar a cabeca ao vao e espreitar para
            // dentro da parede. Uma placa fina no vao chega.
            var blocker = new GameObject(opening.Name + "_Blocker");
            blocker.transform.SetParent(instance.transform.parent, false);
            blocker.transform.position = opening.Centre;
            blocker.transform.rotation = rotation;
            var box = blocker.AddComponent<BoxCollider>();
            box.size = new Vector3(opening.Width, opening.Height, opening.Thickness * 0.5f);
            blocker.isStatic = true;
        }

        // ------------------------------------------------------------------

        private static Material BuildFrameMaterial()
        {
            Material material = LoadOrCreate(FrameMaterialPath);

            var colour = ImportTexture(ColourTexture, normalMap: false, sRgb: true);
            var normal = ImportTexture(NormalTexture, normalMap: true, sRgb: false);
            // A `r.png` e rugosidade e o URP quer suavidade (o inverso) no canal
            // alfa de um metallic map. Em vez de a alimentar ao contrario, fica um
            // valor fixo: sao aros pequenos e vistos de noite.
            ImportTexture(RoughnessTexture, normalMap: false, sRgb: false);

            if (colour != null) material.SetTexture("_BaseMap", colour);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_BumpScale", 1f);
            }

            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.35f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material BuildGlassMaterial()
        {
            Material material = LoadOrCreate(GlassMaterialPath);
            var glass = ImportTexture(GlassTexture, normalMap: false, sRgb: true);
            if (glass != null) material.SetTexture("_BaseMap", glass);

            // Transparente e quase limpo: a cidade la fora e o que da a noite,
            // seria pena tapa-la com o proprio vidro.
            material.SetFloat("_Surface", 1f);                 // Transparent
            material.SetFloat("_Blend", 0f);                   // Alpha
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            material.SetColor("_BaseColor", new Color(0.72f, 0.79f, 0.88f, 0.16f));
            material.SetFloat("_Smoothness", 0.92f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreate(string path)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// As texturas vieram importadas todas como cor sRGB. A normal tem de ser
        /// marcada como normal map e a rugosidade tem de ser linear, senao o Unity
        /// aplica-lhes uma curva de gama que nao faz sentido nenhum.
        /// </summary>
        private static Texture2D ImportTexture(string path, bool normalMap, bool sRgb)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Windows] Textura em falta: {path}");
                return null;
            }

            TextureImporterType wanted = normalMap
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;

            if (importer.textureType != wanted || importer.sRGBTexture != sRgb)
            {
                importer.textureType = wanted;
                importer.sRGBTexture = sRgb;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
