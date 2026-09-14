using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>Texturas da comida, comida do prato e xadrez da mesa.</summary>
    internal static class RemainingArtWiring
    {
        private const string FoodAsset = "Assets/ThirdParty/laying_food.fbx";
        private const string FoodMaterialFolder = "Assets/Pungent/Materials/Food";

        private struct FoodMaterial
        {
            public string Material;
            public string TextureStem;
        }

        private static readonly FoodMaterial[] FoodMaterials =
        {
            new FoodMaterial { Material = "Bowl of Cereal PBR", TextureStem = "Bowl_of_Cereal" },
            new FoodMaterial { Material = "Cheese Wedge PBR", TextureStem = "Cheese_Wedge" },
            new FoodMaterial { Material = "Chocolate Brioche PBR", TextureStem = "Chocolate_Brioche" },
            new FoodMaterial { Material = "Decorative Gourd PBR", TextureStem = "Decorative_Gourd" },
            new FoodMaterial { Material = "Cake PBR", TextureStem = "Frosted_Cake" },
            new FoodMaterial { Material = "Half Cheese Wheel PBR", TextureStem = "Half_Cheese_Wheel" },
            new FoodMaterial { Material = "Bread PBR.001", TextureStem = "Load_of_Bread" },
            new FoodMaterial { Material = "Mandarin PBR", TextureStem = "Mandarin_Orange" },
            new FoodMaterial { Material = "Potato PBR", TextureStem = "Potato" },
            new FoodMaterial { Material = "Red Pepper PBR", TextureStem = "Red_Pepper" },
            new FoodMaterial { Material = "Russet Potato PBR", TextureStem = "Russet_Potato" },
            new FoodMaterial { Material = "Spice Cupcake PBR", TextureStem = "Spice_Cupcake" },
            new FoodMaterial { Material = "Sub Sandwich PBR", TextureStem = "Sub_Sandwich" },
            new FoodMaterial { Material = "Sweet Bread Roll PBR", TextureStem = "Sweet_Bread_Roll" },
        };

        [MenuItem("Pungent/Blockout/Fix Remaining Art", false, 58)]
        internal static void Run()
        {
            WireFoodMaterials();
            ReplacePlateContents();
            PlaceChess();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[RemainingArt] 14 comidas texturadas; prato com sandes; xadrez colocado.");
        }

        private static void WireFoodMaterials()
        {
            EnsureFolder("Assets/Pungent/Materials", "Food");
            var importer = AssetImporter.GetAtPath(FoodAsset) as ModelImporter;
            if (importer == null) { Debug.LogError("[RemainingArt] laying_food.fbx em falta."); return; }

            foreach (var definition in FoodMaterials)
            {
                string safeName = definition.Material.Replace(" ", "_").Replace(".", "_");
                string materialPath = FoodMaterialFolder + "/M_" + safeName + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(material, materialPath);
                }

                Texture2D albedo = FindTexture(definition.TextureStem, "Albedo");
                Texture2D normal = FindTexture(definition.TextureStem, "Normal");
                Texture2D occlusion = FindTexture(definition.TextureStem, "Occlusion");
                if (occlusion == null) occlusion = FindTexture(definition.TextureStem, "Ambient_Occlusion");

                material.SetColor("_BaseColor", Color.white);
                if (albedo != null) material.SetTexture("_BaseMap", albedo);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0.24f);
                if (normal != null)
                {
                    ConfigureNormalMap(normal);
                    material.SetTexture("_BumpMap", normal);
                    material.EnableKeyword("_NORMALMAP");
                }
                if (occlusion != null) material.SetTexture("_OcclusionMap", occlusion);
                EditorUtility.SetDirty(material);

                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(
                    typeof(Material), definition.Material), material);
            }

            importer.SaveAndReimport();
        }

        private static Texture2D FindTexture(string stem, string suffix)
        {
            string[] extensions = { ".jpg", ".jpeg", ".png" };
            foreach (string extension in extensions)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    "Assets/ThirdParty/Textures/" + stem + "_" + suffix + extension);
                if (texture != null) return texture;
            }
            return null;
        }

        private static void ConfigureNormalMap(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.NormalMap) return;
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }

        private static void ReplacePlateContents()
        {
            const string platePath = "Assets/Pungent/Meshes/HeldProps/plate.prefab";
            var food = AssetDatabase.LoadAssetAtPath<GameObject>(FoodAsset);
            Transform sandwich = food != null ? food.transform.Find("Sub Sandwich") : null;
            if (sandwich == null) { Debug.LogWarning("[RemainingArt] Sub Sandwich em falta."); return; }

            GameObject root = PrefabUtility.LoadPrefabContents(platePath);
            try
            {
                Transform anchor = root.transform.Find("ContentsAnchor");
                if (anchor == null) return;
                Transform old = anchor.Find("Contents");
                if (old != null) Object.DestroyImmediate(old.gameObject);

                var contents = new GameObject("Contents");
                contents.transform.SetParent(anchor, false);
                var model = Object.Instantiate(sandwich.gameObject, contents.transform);
                model.name = "Sub Sandwich";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = sandwich.localRotation;
                model.transform.localScale = sandwich.localScale;
                foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

                Bounds bounds = BoundsOf(model);
                float largest = Mathf.Max(bounds.size.x, bounds.size.z);
                if (largest > 0.001f) contents.transform.localScale = Vector3.one * (0.13f / largest);
                PrefabUtility.SaveAsPrefabAsset(root, platePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void PlaceChess()
        {
            var old = GameObject.Find("TABLE_CHESS");
            if (old != null) Object.DestroyImmediate(old);
            // `Kit_Table` e apenas um hotspot sem malha. O tabuleiro acabava a
            // flutuar no chao da cozinha. A mesa fisica e a Dining_Table.
            var table = GameObject.Find("Dining_Table");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/table_decor.FBX");
            if (table == null || source == null) return;

            var chess = (GameObject)PrefabUtility.InstantiatePrefab(source);
            chess.name = "TABLE_CHESS";
            // O FBX traz o tampo no plano XY. Deita-o primeiro; medir/posicionar
            // antes desta rotacao foi o que deixou o tabuleiro de pe.
            // -90 (e nao +90): as peças estão no lado +Z do FBX; a outra direcção
            // deixava-as penduradas por baixo do tampo.
            chess.transform.rotation = Quaternion.Euler(-90f, 8f, 0f);

            Material white = SimpleMaterial("M_Chess_White", new Color(0.78f, 0.72f, 0.60f));
            Material black = SimpleMaterial("M_Chess_Black", new Color(0.10f, 0.075f, 0.055f));
            Material board = SimpleMaterial("M_Chess_Board", Color.white);
            var boardTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ThirdParty/Textures/02-shahmatnaya-doska-A3-900x636.jpg");
            if (boardTexture != null) board.SetTexture("_BaseMap", boardTexture);

            int kept = 0;
            foreach (var renderer in chess.GetComponentsInChildren<Renderer>(true))
            {
                bool isChess = false;
                var replacements = new List<Material>();
                foreach (var material in renderer.sharedMaterials)
                {
                    string name = material != null ? material.name : string.Empty;
                    if (name == "Белые") { replacements.Add(white); isChess = true; }
                    else if (name == "Черные") { replacements.Add(black); isChess = true; }
                    else if (name == "Доска" || name == "Доска2")
                    { replacements.Add(board); isChess = true; }
                    else replacements.Add(material);
                }
                renderer.enabled = isChess;
                if (isChess) { renderer.sharedMaterials = replacements.ToArray(); kept++; }
            }
            foreach (var collider in chess.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            if (kept == 0) { Object.DestroyImmediate(chess); return; }

            Bounds chessBounds = BoundsOf(chess);
            float width = Mathf.Max(chessBounds.size.x, chessBounds.size.z);
            if (width > 0.001f) chess.transform.localScale *= 0.46f / width;
            chessBounds = BoundsOf(chess);
            Bounds tableBounds = BoundsOf(table);
            // As duas chávenas ocupam os cantos opostos; este canto fica livre.
            Vector3 target = tableBounds.center +
                new Vector3(0.22f, tableBounds.extents.y + 0.012f, -0.20f);
            chess.transform.position += new Vector3(target.x - chessBounds.center.x,
                target.y - chessBounds.min.y, target.z - chessBounds.center.z);
            chess.transform.SetParent(table.transform, true);
        }

        private static Material SimpleMaterial(string name, Color colour)
        {
            string path = "Assets/Pungent/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.32f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            Bounds result = new Bounds(go.transform.position, Vector3.zero);
            bool first = true;
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled) continue;
                if (first) { result = renderer.bounds; first = false; }
                else result.Encapsulate(renderer.bounds);
            }
            return first ? new Bounds(go.transform.position, Vector3.one) : result;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
