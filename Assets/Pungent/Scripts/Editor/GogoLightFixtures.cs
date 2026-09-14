using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Atmosphere;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Troca os candeeiros de caixote do blockout pelos do Gogo Casual Light Pack e
    /// liga o brilho de cada abajur ao interruptor da divisao.
    ///
    /// Duas armadilhas resolvidas aqui:
    ///
    /// 1. Os materiais do pack vem do HDRP. Num projecto URP o shader nao existe,
    ///    o Unity cai no shader de erro e os candeeiros aparecem a magenta. Como as
    ///    propriedades serializadas ja trazem os nomes do URP (`_BaseMap`,
    ///    `_BaseColor`), basta reapontar o shader.
    ///
    /// 2. Os objectos Light da cena NAO sao tocados. Os interruptores guardam
    ///    referencias directas a esses componentes; destrui-los para pendurar o
    ///    candeeiro novo deixava a casa toda sem interruptores a funcionar.
    /// </summary>
    internal static class GogoLightFixtures
    {
        private const string PrefabFolder =
            "Assets/Gogo Casual Pack/Gogo Casual Free Light Pack/Prefabs";
        private const string FixturesRoot = "LIGHT_FIXTURES";
        private const float CeilingHeight = 2.80f;

        private struct FixtureDef
        {
            public string Name;
            public string Prefab;
            public Vector3 Position;   // x/z da divisao; o y vem do tecto
            public string LightName;   // Light que o candeeiro representa (pode ser vazio)
            public float Scale;
        }

        /// <summary>
        /// Um modelo diferente por divisao: a casa nao foi mobilada de uma vez, e os
        /// candeeiros todos iguais era a leitura errada.
        /// </summary>
        private static readonly FixtureDef[] Fixtures =
        {
            new FixtureDef { Name = "Fixture_Bedroom_Tomas", Prefab = "Decoration_Light_CeilingLamp_03_01",
                Position = new Vector3(-5.20f, 0f, -2.90f), LightName = "Bedroom_Light", Scale = 1.0f },
            new FixtureDef { Name = "Fixture_Bedroom_Rui", Prefab = "Decoration_Light_CeilingLamp_03_02",
                Position = new Vector3(-1.75f, 0f, -3.00f), LightName = "Bedroom_Rui_Light", Scale = 1.0f },
            new FixtureDef { Name = "Fixture_Kitchen", Prefab = "Decoration_Light_CeilingLamp_01_01",
                Position = new Vector3(-5.00f, 0f, 3.00f), LightName = "Kitchen_Light", Scale = 1.0f },
            new FixtureDef { Name = "Fixture_Dining", Prefab = "Decoration_Light_CeilingLamp_02_01",
                Position = new Vector3(-1.50f, 0f, 2.90f), LightName = "Dining_Light", Scale = 1.0f },
            new FixtureDef { Name = "Fixture_Living", Prefab = "Decoration_Light_CeilingLamp_01_02",
                Position = new Vector3(3.20f, 0f, 2.60f), LightName = "Living_Light", Scale = 1.1f },
            new FixtureDef { Name = "Fixture_Hall", Prefab = "Decoration_Light_CeilingLamp_04_01",
                Position = new Vector3(4.10f, 0f, -1.60f), LightName = "Hall_Light", Scale = 0.9f },
            new FixtureDef { Name = "Fixture_Bathroom", Prefab = "Decoration_Light_CeilingLamp_06_01",
                Position = new Vector3(1.15f, 0f, -1.90f), LightName = "Bathroom_Light", Scale = 0.9f },
            new FixtureDef { Name = "Fixture_Corridor_East", Prefab = "Decoration_Light_CeilingLamp_04_02",
                Position = new Vector3(-1.60f, 0f, -0.05f), LightName = "Corridor_Light", Scale = 0.9f },
            new FixtureDef { Name = "Fixture_Corridor_West", Prefab = "Decoration_Light_CeilingLamp_04_02",
                Position = new Vector3(-5.30f, 0f, -0.05f), LightName = "Corridor_Light_West", Scale = 0.9f },
            new FixtureDef { Name = "Fixture_Laundry", Prefab = "Decoration_Light_CeilingLamp_06_02",
                Position = new Vector3(1.15f, 0f, -4.00f), LightName = "", Scale = 0.85f },
            new FixtureDef { Name = "Fixture_Storage", Prefab = "Decoration_Light_CeilingLamp_06_02",
                Position = new Vector3(4.10f, 0f, -4.00f), LightName = "", Scale = 0.85f },
        };

        /// <summary>Os candeeiros de caixote do blockout, que estes substituem.</summary>
        private static readonly string[] OldFixtures =
        {
            "Light_Tomas", "Light_Rui", "Light_Kitchen", "Light_Dining", "Light_Living",
            "Light_Hall", "Light_Bath", "Light_Laundry", "Light_Storage"
        };

        [MenuItem("Tools/Pungent/Swap In Gogo Light Fixtures")]
        internal static void Run()
        {
            RepairPackMaterials();

            var scene = EditorSceneManager.GetActiveScene();
            var existing = ApartmentV2WiringUtil.Find(scene, FixturesRoot);
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject(FixturesRoot);
            Undo.RegisterCreatedObjectUndo(root, "Swap light fixtures");

            // Os candeeiros antigos sao so malha: desaparecem sem levar as Lights
            // atras, que vivem noutro sitio da hierarquia.
            int removed = 0;
            foreach (var name in OldFixtures)
            {
                var old = GameObject.Find(name);
                if (old == null) continue;
                Object.DestroyImmediate(old);
                removed++;
            }

            var lights = new Dictionary<string, Light>();
            foreach (var light in Object.FindObjectsOfType<Light>(true))
                if (!lights.ContainsKey(light.name)) lights[light.name] = light;

            int placed = 0;
            foreach (var def in Fixtures)
                if (Place(def, root.transform, lights)) placed++;

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Gogo] {removed} candeeiros do blockout removidos, {placed} colocados.");
        }

        private static bool Place(FixtureDef def, Transform parent, Dictionary<string, Light> lights)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{def.Prefab}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[Gogo] Prefab em falta: {def.Prefab}");
                return false;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = def.Name;
            instance.transform.SetParent(parent, false);
            // Os candeeiros do pack penduram-se para baixo a partir da origem, por
            // isso a origem vai directamente ao tecto.
            instance.transform.position = new Vector3(def.Position.x, CeilingHeight, def.Position.z);
            instance.transform.localScale = Vector3.one * def.Scale;

            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                renderer.gameObject.isStatic = true;

            if (string.IsNullOrEmpty(def.LightName)) return true;

            if (!lights.TryGetValue(def.LightName, out Light light))
            {
                Debug.LogWarning($"[Gogo] '{def.Name}': luz '{def.LightName}' nao encontrada.");
                return true;
            }

            // A lampada fica dentro do abajur, nao no tecto: o candeeiro tem de
            // projectar a propria sombra em vez de brilhar por cima dela.
            light.transform.position = new Vector3(def.Position.x, CeilingHeight - 0.42f, def.Position.z);

            var glow = instance.AddComponent<LampFixtureGlow>();
            var so = new SerializedObject(glow);
            so.FindProperty("source").objectReferenceValue = light;

            var emissive = new List<Object>();
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material != null && material.name.Contains("Emissive"))
                    { emissive.Add(renderer); break; }

            var array = so.FindProperty("glowing");
            array.arraySize = emissive.Count;
            for (int i = 0; i < emissive.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = emissive[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            return true;
        }

        /// <summary>
        /// Os materiais do pack sao HDRP e ficam sem shader valido no URP. As
        /// propriedades serializadas ja trazem os nomes do URP, por isso chega
        /// reapontar o shader e ligar a emissao onde faz falta.
        /// </summary>
        private static void RepairPackMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) { Debug.LogError("[Gogo] Shader URP/Lit nao encontrado."); return; }

            int fixedCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Gogo Casual Pack" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) continue;
                if (material.shader != null && material.shader.name.StartsWith("Universal Render Pipeline"))
                    continue;

                material.shader = lit;
                material.SetFloat("_Smoothness", 0.25f);
                material.SetFloat("_Metallic", 0f);

                bool emissive = material.name.Contains("Emissive");
                if (emissive)
                {
                    // A cor fica a preto: quem a acende e o LampFixtureGlow, por
                    // candeeiro. O keyword tem de ficar ligado na mesma, senao o
                    // property block nao tem variante de shader onde escrever.
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.black);
                    material.globalIlluminationFlags =
                        MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                EditorUtility.SetDirty(material);
                fixedCount++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Gogo] {fixedCount} materiais HDRP reapontados para URP/Lit.");
        }
    }
}
