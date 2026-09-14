using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Blockout da oficina da zona industrial (§10.3), onde se passa o Dia 4.
    ///
    /// O pack de arte da garagem ainda nao esta no projecto, e esperar por ele
    /// deixava o Dia 4 sem sitio onde acontecer — o capitulo ja existe como asset e
    /// os passos dele esperam por `arrived_garage`, `seller_met` e `parts_loaded`.
    /// Primeiro o espaco em caixas, como se fez com o apartamento; a arte entra por
    /// cima quando houver, sem mexer nas medidas.
    ///
    /// O que a §10.3 pede e o que esta aqui: oficina, patio e portao, contentores e
    /// prateleiras, escritorio pequeno, luz de sodio, e **duas rotas** — a principal
    /// pelo portao e uma de fuga pelas traseiras. As duas rotas nao sao enfeite: o
    /// capitulo acaba com uma perseguicao curta, e um espaco com uma unica saida
    /// transforma isso num corredor sem escolha.
    ///
    /// Convencao igual a do apartamento: X para leste, Z para norte, chao a y = 0.
    /// </summary>
    public static class GarageBlockoutBuilder
    {
        public const string RootName = "GARAGE_BLOCKOUT";

        // --- Cotas mestras (metros) ---
        // A nave e alta de proposito: um pe-direito de casa fazia a oficina parecer
        // uma garagem particular, e isto e um sitio onde se levantam carros.
        private const float ShopHeight = 5.2f;
        private const float OfficeHeight = 2.6f;
        private const float WallThickness = 0.25f;

        // Nave principal.
        private const float ShopWest = -9f;
        private const float ShopEast = 5f;
        private const float ShopSouth = -7f;
        private const float ShopNorth = 6f;

        // Escritorio, encostado ao canto nordeste da nave.
        private const float OfficeWest = 1.2f;
        private const float OfficeSouth = 2.6f;

        // Portao grande, na parede sul: e por aqui que o carro entra.
        private const float GateWest = -3.2f;
        private const float GateEast = 0.8f;
        private const float GateHead = 4.2f;

        // Porta de servico nas traseiras (parede norte): a rota de fuga.
        private const float BackDoorWest = -7.6f;
        private const float BackDoorEast = -6.6f;
        private const float DoorHead = 2.1f;

        [MenuItem("Pungent/Blockout/Rebuild Garage", false, 12)]
        public static void Rebuild()
        {
            if (BuildGuard.Blocked("RebuildGarage")) return;

            var scene = EditorSceneManager.GetActiveScene();
            var existing = GameObject.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Rebuild garage blockout");

            var concrete = MakeMaterial("M_Garage_Concrete", new Color(0.34f, 0.34f, 0.33f), 0.10f);
            var wall = MakeMaterial("M_Garage_Wall", new Color(0.46f, 0.45f, 0.42f), 0.08f);
            var metal = MakeMaterial("M_Garage_Metal", new Color(0.30f, 0.31f, 0.34f), 0.45f);

            BuildShell(root.transform, concrete, wall);
            BuildOffice(root.transform, wall);
            BuildYard(root.transform, concrete, metal);
            BuildClutter(root.transform, metal);
            BuildLighting(root.transform);
            BuildAnchors(root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Garage] Blockout reconstruido. Rotas: portao a sul, saida de servico a norte.");
        }

        // ------------------------------------------------------------------

        private static void BuildShell(Transform parent, Material floor, Material wall)
        {
            var group = Group("SHELL", parent);

            Box(group, "Garage_Floor",
                new Vector3((ShopWest + ShopEast) * 0.5f, -0.1f, (ShopSouth + ShopNorth) * 0.5f),
                new Vector3(ShopEast - ShopWest, 0.2f, ShopNorth - ShopSouth), floor);

            // Parede sul partida em dois, para deixar o vao do portao.
            WallRun(group, "Wall_South_W", true, ShopSouth, ShopWest, GateWest, ShopHeight, wall);
            WallRun(group, "Wall_South_E", true, ShopSouth, GateEast, ShopEast, ShopHeight, wall);
            // Lintel por cima do portao.
            Box(group, "Wall_South_Head",
                new Vector3((GateWest + GateEast) * 0.5f, (GateHead + ShopHeight) * 0.5f, ShopSouth),
                new Vector3(GateEast - GateWest, ShopHeight - GateHead, WallThickness), wall);

            // Parede norte partida para a porta de servico.
            WallRun(group, "Wall_North_W", true, ShopNorth, ShopWest, BackDoorWest, ShopHeight, wall);
            WallRun(group, "Wall_North_E", true, ShopNorth, BackDoorEast, ShopEast, ShopHeight, wall);
            Box(group, "Wall_North_Head",
                new Vector3((BackDoorWest + BackDoorEast) * 0.5f, (DoorHead + ShopHeight) * 0.5f, ShopNorth),
                new Vector3(BackDoorEast - BackDoorWest, ShopHeight - DoorHead, WallThickness), wall);

            WallRun(group, "Wall_West", false, ShopWest, ShopSouth, ShopNorth, ShopHeight, wall);
            WallRun(group, "Wall_East", false, ShopEast, ShopSouth, ShopNorth, ShopHeight, wall);

            Box(group, "Garage_Roof",
                new Vector3((ShopWest + ShopEast) * 0.5f, ShopHeight, (ShopSouth + ShopNorth) * 0.5f),
                new Vector3(ShopEast - ShopWest, 0.2f, ShopNorth - ShopSouth), wall);
        }

        /// <summary>
        /// Escritorio envidracado no canto: e dali que o vendedor te ve chegar, e a
        /// janela interior serve a linha de visao que o slow-burn precisa.
        /// </summary>
        private static void BuildOffice(Transform parent, Material wall)
        {
            var group = Group("OFFICE", parent);

            WallRun(group, "Office_South", true, OfficeSouth, OfficeWest, ShopEast, OfficeHeight, wall);
            WallRun(group, "Office_West", false, OfficeWest, OfficeSouth, ShopNorth, OfficeHeight, wall);

            Box(group, "Office_Ceiling",
                new Vector3((OfficeWest + ShopEast) * 0.5f, OfficeHeight,
                            (OfficeSouth + ShopNorth) * 0.5f),
                new Vector3(ShopEast - OfficeWest, 0.15f, ShopNorth - OfficeSouth), wall);

            Box(group, "Office_Desk", new Vector3(3.4f, 0.38f, 4.6f),
                new Vector3(1.6f, 0.75f, 0.7f), wall);
        }

        private static void BuildYard(Transform parent, Material ground, Material metal)
        {
            var group = Group("YARD", parent);

            // Patio a sul do portao: onde o carro fica parado.
            Box(group, "Yard_Ground", new Vector3(-1.2f, -0.1f, ShopSouth - 6f),
                new Vector3(18f, 0.2f, 12f), ground);

            // Vedacao do patio, com a abertura do portao alinhada com a nave.
            Box(group, "Fence_West", new Vector3(-10.2f, 1.1f, ShopSouth - 6f),
                new Vector3(0.15f, 2.2f, 12f), metal);
            Box(group, "Fence_East", new Vector3(7.8f, 1.1f, ShopSouth - 6f),
                new Vector3(0.15f, 2.2f, 12f), metal);
            Box(group, "Fence_South", new Vector3(-1.2f, 1.1f, ShopSouth - 12f),
                new Vector3(18f, 2.2f, 0.15f), metal);
        }

        private static void BuildClutter(Transform parent, Material metal)
        {
            var group = Group("CLUTTER", parent);

            // Prateleiras encostadas a nascente, contentores a poente. O corredor
            // livre pelo meio e a rota principal; a folga por tras das prateleiras
            // e a de fuga.
            for (int i = 0; i < 4; i++)
                Box(group, $"Shelf_{i}", new Vector3(4.1f, 1.1f, -5.2f + i * 2.4f),
                    new Vector3(0.8f, 2.2f, 1.9f), metal);

            Box(group, "Container_A", new Vector3(-7.6f, 1.3f, -3.4f),
                new Vector3(2.4f, 2.6f, 5.8f), metal);
            Box(group, "Container_B", new Vector3(-7.6f, 1.3f, 2.6f),
                new Vector3(2.4f, 2.6f, 4.2f), metal);

            // Bancada e elevador: o motivo pelo qual o sitio existe.
            Box(group, "Workbench", new Vector3(-1.4f, 0.45f, 5.2f),
                new Vector3(5.2f, 0.9f, 0.8f), metal);
            Box(group, "Lift_Pad", new Vector3(-1.4f, 0.06f, -1.4f),
                new Vector3(3.2f, 0.12f, 5.4f), metal);

            // As pecas que se vem buscar. E este monte que o passo `parts_loaded`
            // fecha, por isso tem de estar num sitio que se veja da entrada.
            Box(group, "Parts_Pallet", new Vector3(1.8f, 0.3f, -4.2f),
                new Vector3(1.2f, 0.6f, 1.2f), metal);
        }

        /// <summary>
        /// Luz de sodio, como a §10.3 pede: pouca, alta e alaranjada. O contraste
        /// entre as ilhas de luz e o escuro entre elas e o que torna o espaco
        /// legivel para uma perseguicao curta.
        /// </summary>
        private static void BuildLighting(Transform parent)
        {
            var group = Group("LIGHTING", parent);

            Lamp(group, "Sodium_Shop_W", new Vector3(-5.5f, 4.6f, -1f), 11f, 1.5f);
            Lamp(group, "Sodium_Shop_E", new Vector3(1.5f, 4.6f, 1.5f), 11f, 1.5f);
            Lamp(group, "Sodium_Gate", new Vector3(-1.2f, 4.0f, ShopSouth + 1f), 8f, 1.1f);
            Lamp(group, "Sodium_Yard", new Vector3(-1.2f, 5.0f, ShopSouth - 5f), 14f, 1.3f);

            // O escritorio tem fluorescente, nao sodio: e outro sitio, com outra luz.
            var office = Lamp(group, "Office_Fluorescent", new Vector3(3.2f, 2.4f, 4.4f), 6f, 1.0f);
            office.GetComponent<Light>().color = new Color(0.82f, 0.86f, 0.80f);
        }

        private static GameObject Lamp(Transform parent, string name, Vector3 position,
            float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.26f);   // sodio
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.Soft;
            return go;
        }

        /// <summary>
        /// Marcadores para a ferramenta de ligacao. Ficam aqui e nao la porque sao
        /// geometria: quem mexer nas medidas da nave mexe nestes ao mesmo tempo.
        /// </summary>
        private static void BuildAnchors(Transform parent)
        {
            var group = Group("ANCHORS", parent);

            Anchor(group, "GARAGE_Arrival", new Vector3(-1.2f, 0f, ShopSouth - 4.5f));
            Anchor(group, "GARAGE_Seller", new Vector3(2.6f, 0f, 3.8f));
            Anchor(group, "GARAGE_Parts", new Vector3(1.8f, 0f, -4.2f));
            Anchor(group, "GARAGE_BackExit", new Vector3(-7.1f, 0f, ShopNorth + 1.2f));
        }

        // ------------------------------------------------------------------

        private static void WallRun(Transform parent, string name, bool alongX, float line,
            float start, float end, float height, Material material)
        {
            if (Mathf.Abs(end - start) < 0.01f) return;

            float mid = (start + end) * 0.5f;
            Vector3 position = alongX
                ? new Vector3(mid, height * 0.5f, line)
                : new Vector3(line, height * 0.5f, mid);
            Vector3 size = alongX
                ? new Vector3(Mathf.Abs(end - start), height, WallThickness)
                : new Vector3(WallThickness, height, Mathf.Abs(end - start));

            Box(parent, name, position, size, material);
        }

        private static GameObject Box(Transform parent, string name, Vector3 position,
            Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;
            go.isStatic = true;
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static Transform Group(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Anchor(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
        }

        private static Material MakeMaterial(string name, Color colour, float smoothness)
        {
            string path = $"Assets/Pungent/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
