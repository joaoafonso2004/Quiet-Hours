using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Constrói o exterior visível das janelas e da varanda.
    ///
    /// Princípio: **o apartamento não se mexe**. Fica em y = 0 e a cidade é
    /// construída à volta — o resto do prédio por baixo e por cima, a rua 24 m
    /// abaixo e os prédios vizinhos do outro lado. Assim nenhuma coordenada do
    /// interior, da NavMesh ou das rotinas do Rui precisa de ser tocada.
    ///
    /// A fachada do nosso piso é feita pelas próprias paredes do apartamento, por
    /// isso nada é construído entre y = 0 e y = 2.9: seria geometria a tapar as
    /// nossas janelas.
    /// </summary>
    public static class CityExteriorBuilder
    {
        private const string RootName = "CITY_EXTERIOR";
        private const string PrefabFolder = "Assets/City Builder/Prefabs";

        /// <summary>As peças do pack são miniatura; 7 põe o piso a ~3 m e a rua a ~11 m.</summary>
        private const float CityScale = 7f;

        // --- cotas ---
        private const float StreetY = -24f;          // 8 pisos abaixo
        private const float ApartmentCeiling = 2.9f;
        private const float FloorHeight = 3.0f;
        private const int FloorsAbove = 3;

        // Envolvente do nosso prédio, ligeiramente maior que o apartamento
        // (X [-7.25, 6.05], Z [-5.25, 5.25]).
        private const float ShellWest = -7.6f;
        private const float ShellEast = 6.4f;
        private const float ShellSouth = -5.6f;
        private const float ShellNorth = 5.6f;
        private const float ShellThickness = 0.35f;

        // A varanda avança até Z = 6.70, por isso a rua norte começa depois disso.
        // A 20 m os prédios da frente tapavam o céu todo e não se via a rua lá em
        // baixo. Recuados, a varanda passa a ter altura e profundidade visíveis.
        private const float StreetNorthNear = 9f;
        private const float StreetNorthFar = 30f;
        private const float StreetSouthNear = -8f;
        private const float StreetSouthFar = -28f;

        private const string FacadeMaterial = "Assets/Pungent/Materials/M_City_Facade.mat";
        private const string WindowLitMaterial = "Assets/Pungent/Materials/M_City_WindowLit.mat";
        private const string WindowDarkMaterial = "Assets/Pungent/Materials/M_City_WindowDark.mat";
        private const string RoadMaterial = "Assets/Pungent/Materials/M_City_Road.mat";

        [MenuItem("Pungent/Blockout/Build City Exterior", false, 70)]
        public static void Build()
        {
            if (BuildGuard.Blocked("Cidade")) return;

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            var existing = FindRoot(scene);

            // A cidade da cena deixou de ser gerada: foi trabalhada a mao por cima
            // do que este construtor produziu — edificios e janelas duplicados um a
            // um, mais de mil objectos que nenhuma tabela em codigo reproduz. Isso
            // esta gravado em `Assets/Pungent/Prefabs/CITY_EXTERIOR.prefab`.
            //
            // Correr isto por cima apagava esse trabalho e nao dava erro nenhum: a
            // cidade voltava a ser a de fabrica e ninguem percebia porque. Quem
            // quiser mesmo recomecar do zero apaga a instancia a mao primeiro — e
            // ai sabe o que esta a fazer.
            if (existing != null && PrefabUtility.IsPartOfPrefabInstance(existing))
            {
                Debug.LogError(
                    "[Cidade] A CITY_EXTERIOR desta cena e uma instancia de prefab, ou seja, " +
                    "tem trabalho feito a mao. Reconstruir apagava-o.\n" +
                    "Para voltar a gerar do zero: apagar a instancia na hierarquia e correr outra vez.\n" +
                    "Para repor o trabalho guardado: arrastar " +
                    "Assets/Pungent/Prefabs/CITY_EXTERIOR.prefab para a cena.");
                return;
            }

            if (existing != null) Object.DestroyImmediate(existing);

            EnsureMaterials();
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build city exterior");

            BuildOwnBuilding(Group(root, "OwnBuilding").transform);
            BuildStreet(Group(root, "Street").transform);
            BuildNeighbours(Group(root, "Neighbours").transform);
            BuildSkyline(Group(root, "Skyline").transform);
            BuildNightLights(Group(root, "CityLights").transform);
            ExcludeFromNavMesh(root, scene);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Cidade] Exterior construído: {root.GetComponentsInChildren<Renderer>(true).Length} renderers.");
        }

        /// <summary>
        /// A cidade não pode entrar na NavMesh. Sem isto o bake apanhava o chão da
        /// rua e os telhados, e a NavMesh passava de 264 triângulos dentro do
        /// apartamento para 2428 espalhados por 240 m — desperdício e um risco de o
        /// Rui encontrar caminho para fora de casa.
        /// </summary>
        private static void ExcludeFromNavMesh(GameObject root, UnityEngine.SceneManagement.Scene scene)
        {
            int layer = EnsureLayer("CityExterior");
            SetLayerRecursive(root, layer);

            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name != "NAVIGATION") continue;
                var surface = go.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
                if (surface == null) continue;
                surface.layerMask &= ~(1 << layer);
                surface.BuildNavMesh();
                EditorUtility.SetDirty(surface);
            }
        }

        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return i;
            }

            Debug.LogWarning($"[Cidade] Sem slots livres para a layer '{name}'.");
            return 0;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        public static GameObject FindRoot(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName) return go;
            return null;
        }

        // ------------------------------------------------------------------
        // O nosso prédio
        // ------------------------------------------------------------------

        private static void BuildOwnBuilding(Transform parent)
        {
            var facade = Load(FacadeMaterial);

            // Pisos abaixo, de y = 0 até à rua.
            var below = Group(parent.gameObject, "FloorsBelow").transform;
            for (float top = 0f; top > StreetY; top -= FloorHeight)
            {
                float bottom = Mathf.Max(top - FloorHeight, StreetY);
                BuildFloorShell(below, $"Floor_{Mathf.RoundToInt(top)}", bottom, top, facade, true);
            }

            // Pisos acima, a partir do teto do apartamento.
            var above = Group(parent.gameObject, "FloorsAbove").transform;
            for (int i = 0; i < FloorsAbove; i++)
            {
                float bottom = ApartmentCeiling + i * FloorHeight;
                BuildFloorShell(above, $"Floor_Up_{i}", bottom, bottom + FloorHeight, facade, true);
            }

            // Cobertura.
            float roofY = ApartmentCeiling + FloorsAbove * FloorHeight;
            Box(parent, "Roof", new Vector3((ShellWest + ShellEast) * 0.5f, roofY + 0.2f, (ShellSouth + ShellNorth) * 0.5f),
                new Vector3(ShellEast - ShellWest + 0.6f, 0.4f, ShellNorth - ShellSouth + 0.6f), facade);

            // Laje do nosso piso, vista de baixo pela varanda.
            Box(parent, "OwnFloorSlab", new Vector3((ShellWest + ShellEast) * 0.5f, -0.25f, (ShellSouth + ShellNorth) * 0.5f),
                new Vector3(ShellEast - ShellWest, 0.5f, ShellNorth - ShellSouth), facade);
        }

        /// <summary>Quatro paredes de fachada e uma fila de janelas por piso.</summary>
        private static void BuildFloorShell(Transform parent, string name, float bottom, float top,
                                            Material facade, bool withWindows)
        {
            var group = Group(parent.gameObject, name).transform;
            float height = top - bottom;
            float midY = (bottom + top) * 0.5f;
            float width = ShellEast - ShellWest;
            float depth = ShellNorth - ShellSouth;
            float cx = (ShellWest + ShellEast) * 0.5f;
            float cz = (ShellSouth + ShellNorth) * 0.5f;

            Box(group, "N", new Vector3(cx, midY, ShellNorth), new Vector3(width, height, ShellThickness), facade);
            Box(group, "S", new Vector3(cx, midY, ShellSouth), new Vector3(width, height, ShellThickness), facade);
            Box(group, "W", new Vector3(ShellWest, midY, cz), new Vector3(ShellThickness, height, depth), facade);
            Box(group, "E", new Vector3(ShellEast, midY, cz), new Vector3(ShellThickness, height, depth), facade);

            if (!withWindows || height < 2f) return;

            // Janelas dos outros apartamentos: umas acesas, outras não.
            float windowY = bottom + height * 0.55f;
            for (float x = ShellWest + 2.2f; x < ShellEast - 1f; x += 3.2f)
            {
                PlaceWindow(group, new Vector3(x, windowY, ShellNorth - 0.2f), new Vector3(1.5f, 1.2f, 0.08f));
                PlaceWindow(group, new Vector3(x, windowY, ShellSouth + 0.2f), new Vector3(1.5f, 1.2f, 0.08f));
            }
        }

        private static void PlaceWindow(Transform parent, Vector3 position, Vector3 size)
        {
            // Determinista: a mesma janela acende sempre, para o resultado ser estável
            // entre reconstruções.
            bool lit = Mathf.Abs(Mathf.Sin(position.x * 12.9898f + position.y * 78.233f)) > 0.62f;
            Box(parent, lit ? "Window_Lit" : "Window_Dark", position, size,
                Load(lit ? WindowLitMaterial : WindowDarkMaterial));
        }

        // ------------------------------------------------------------------
        // Rua e vizinhos
        // ------------------------------------------------------------------

        private static void BuildStreet(Transform parent)
        {
            var road = Load(RoadMaterial);
            // Base de asfalto por baixo de tudo, para não haver buracos nas bordas.
            Box(parent, "Ground", new Vector3(0f, StreetY - 0.15f, 0f), new Vector3(240f, 0.2f, 240f), road);

            var prefabs = LoadPrefabs();
            float tile = 1.61f * CityScale;   // 11.27 m por peça

            // As peças de estrada têm o pivô no canto, não no centro.
            BuildRoadRow(parent, prefabs, StreetNorthNear, tile);
            BuildRoadRow(parent, prefabs, StreetSouthFar + 2f, tile);

            if (!prefabs.TryGetValue("Street_Lantern", out GameObject lantern)) return;
            for (float x = -40f; x <= 40f; x += 12f)
            {
                Spawn(parent, lantern, new Vector3(x, StreetY, StreetNorthNear + 1.5f), 180f, CityScale);
                Spawn(parent, lantern, new Vector3(x, StreetY, StreetSouthNear - 1.5f), 0f, CityScale);
            }
        }

        /// <summary>Fila de estrada com passeio de cada lado, ao longo do eixo X.</summary>
        private static void BuildRoadRow(Transform parent, Dictionary<string, GameObject> prefabs,
                                         float zStart, float tile)
        {
            prefabs.TryGetValue("Straight_Road", out GameObject road);
            prefabs.TryGetValue("Sidewalk", out GameObject sidewalk);
            if (road == null) return;

            for (float x = -60f; x <= 60f; x += tile)
            {
                // Straight_Road corre ao longo de Z por omissão; 90° alinha-a com X.
                Spawn(parent, road, new Vector3(x, StreetY + 0.02f, zStart), 90f, CityScale);
                if (sidewalk == null) continue;
                Spawn(parent, sidewalk, new Vector3(x, StreetY + 0.03f, zStart - tile), 0f, CityScale);
                Spawn(parent, sidewalk, new Vector3(x, StreetY + 0.03f, zStart + tile), 0f, CityScale);
            }
        }

        private static void BuildNeighbours(Transform parent)
        {
            var prefabs = LoadPrefabs();
            // (base, corpo, cobertura, nº de pisos)
            var stacks = new (string Base, string Body, string Roof, int Floors)[]
            {
                ("B1_Base", "B1_Windows", "B1_Roof", 9),
                ("B2_Base", "B2_Windows", "B2_Roof", 7),
                ("B1_Base", "B1_Window_2", "B1_Roof", 11),
                ("B2_Base", "B2_Windows", "B2_Roof", 8),
            };

            // Do outro lado da rua, a norte (vista da sala, cozinha e varanda) e a
            // sul (vista dos quartos).
            int index = 0;
            for (float x = -30f; x <= 30f; x += 9f)
            {
                var north = stacks[index % stacks.Length];
                Stack(parent, prefabs, north, new Vector3(x, StreetY, StreetNorthFar), 180f);

                var south = stacks[(index + 2) % stacks.Length];
                Stack(parent, prefabs, south, new Vector3(x, StreetY, StreetSouthFar), 0f);
                index++;
            }
        }

        private static void Stack(Transform parent, Dictionary<string, GameObject> prefabs,
            (string Base, string Body, string Roof, int Floors) def, Vector3 origin, float yaw)
        {
            if (!prefabs.TryGetValue(def.Base, out GameObject basePiece)) return;
            prefabs.TryGetValue(def.Body, out GameObject bodyPiece);
            prefabs.TryGetValue(def.Roof, out GameObject roofPiece);

            float y = origin.y;
            Spawn(parent, basePiece, new Vector3(origin.x, y, origin.z), yaw, CityScale);
            y += FloorHeight;

            for (int i = 0; i < def.Floors && bodyPiece != null; i++)
            {
                Spawn(parent, bodyPiece, new Vector3(origin.x, y, origin.z), yaw, CityScale);
                y += FloorHeight;
            }

            if (roofPiece != null)
                Spawn(parent, roofPiece, new Vector3(origin.x, y, origin.z), yaw, CityScale);
        }

        /// <summary>Blocos distantes: só silhueta, para o horizonte não acabar a meio.</summary>
        private static void BuildSkyline(Transform parent)
        {
            var facade = Load(FacadeMaterial);
            for (int i = 0; i < 26; i++)
            {
                float angle = i / 26f * Mathf.PI * 2f;
                float radius = 70f + Mathf.Abs(Mathf.Sin(i * 3.7f)) * 45f;
                float height = 18f + Mathf.Abs(Mathf.Cos(i * 2.3f)) * 40f;
                float width = 10f + Mathf.Abs(Mathf.Sin(i * 1.9f)) * 12f;

                Box(parent, $"Distant_{i}",
                    new Vector3(Mathf.Sin(angle) * radius, StreetY + height * 0.5f, Mathf.Cos(angle) * radius),
                    new Vector3(width, height, width * 0.85f), facade);
            }
        }

        private static void BuildNightLights(Transform parent)
        {
            // Poucas luzes e de alcance grande: servem para dar volume à rua, não
            // para iluminar o interior do apartamento.
            for (float x = -24f; x <= 24f; x += 16f)
            {
                MakeLight(parent, $"StreetGlow_N_{x}", new Vector3(x, StreetY + 6f, StreetNorthNear + 2f),
                    new Color(1f, 0.82f, 0.55f), 18f, 2.2f);
                MakeLight(parent, $"StreetGlow_S_{x}", new Vector3(x, StreetY + 6f, StreetSouthNear - 2f),
                    new Color(1f, 0.82f, 0.55f), 18f, 2.2f);
            }
        }

        private static void MakeLight(Transform parent, string name, Vector3 position, Color colour,
                                      float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = colour;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------
        // Utilitários
        // ------------------------------------------------------------------

        private static Dictionary<string, GameObject> LoadPrefabs()
        {
            var result = new Dictionary<string, GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!result.ContainsKey(name)) result[name] = go;
            }
            return result;
        }

        private static void Spawn(Transform parent, GameObject prefab, Vector3 position, float yaw, float scale)
        {
            if (prefab == null) return;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * scale;
            StripColliders(instance);
        }

        /// <summary>Nada disto é jogável: colliders só custariam física e queries.</summary>
        private static void StripColliders(GameObject go)
        {
            foreach (var collider in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
        }

        private static GameObject Group(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<BoxCollider>());
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material Load(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

        private static void EnsureMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");

            var facade = GetOrCreate(FacadeMaterial, shader);
            facade.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ThirdParty/PolyHaven/plastered_wall_05/plastered_wall_05_diff_1k.jpg"));
            facade.SetTextureScale("_BaseMap", new Vector2(4f, 4f));
            facade.SetColor("_BaseColor", new Color(0.42f, 0.42f, 0.46f));
            facade.SetFloat("_Smoothness", 0.08f);

            var road = GetOrCreate(RoadMaterial, shader);
            road.SetColor("_BaseColor", new Color(0.11f, 0.11f, 0.13f));
            road.SetFloat("_Smoothness", 0.30f);
            road.SetTexture("_BaseMap", null);

            var lit = GetOrCreate(WindowLitMaterial, shader);
            lit.SetColor("_BaseColor", new Color(0.95f, 0.80f, 0.52f));
            lit.EnableKeyword("_EMISSION");
            lit.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            lit.SetColor("_EmissionColor", new Color(1f, 0.83f, 0.55f) * 2.2f);
            lit.SetTexture("_BaseMap", null);

            var dark = GetOrCreate(WindowDarkMaterial, shader);
            dark.SetColor("_BaseColor", new Color(0.05f, 0.06f, 0.09f));
            dark.SetFloat("_Smoothness", 0.72f);
            dark.SetTexture("_BaseMap", null);

            AssetDatabase.SaveAssets();
        }

        private static Material GetOrCreate(string path, Shader shader)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
