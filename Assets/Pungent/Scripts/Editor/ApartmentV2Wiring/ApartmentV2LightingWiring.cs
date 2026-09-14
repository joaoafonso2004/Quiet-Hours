using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Pungent.Interaction;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Luzes por divisao e os interruptores que as comandam. Andam juntos de
    /// proposito: um interruptor sem Light para ligar nao faz nada, e os dois
    /// conjuntos partilham os mesmos nomes de divisao.
    /// </summary>
    internal static class ApartmentV2LightingWiring
    {
        /// <summary>
        /// Altura das lampadas: tecto (2.80) menos o comprimento do candeeiro.
        ///
        /// Tem de bater certo com o que o `GogoLightFixtures` usa, senao voltar a
        /// correr o Wire depois do passe de candeeiros baixa as luzes 3 cm e elas
        /// deixam de estar dentro do abajur — passam a iluminar por fora dele.
        /// </summary>
        private const float BulbHeight = 2.38f;

        internal static void PlaceLights()
        {
            ApartmentV2WiringUtil.Move("Bedroom_Light", new Vector3(-5.20f, BulbHeight, -2.90f), null);
            ApartmentV2WiringUtil.Move("Corridor_Light", new Vector3(-1.60f, BulbHeight, -0.05f), null);
            ApartmentV2WiringUtil.Move("Kitchen_Light", new Vector3(-5.00f, BulbHeight, 3.00f), null);
            ApartmentV2WiringUtil.Move("Living_Light", new Vector3(3.20f, BulbHeight, 2.60f), null);

            // O corredor e longo: uma segunda luz instavel a oeste evita um tunel opaco.
            var corridor = GameObject.Find("Corridor_Light");
            if (corridor != null && GameObject.Find("Corridor_Light_West") == null)
            {
                var extra = Object.Instantiate(corridor, corridor.transform.parent);
                extra.name = "Corridor_Light_West";
                extra.transform.position = new Vector3(-5.30f, BulbHeight, -0.05f);
            }

            // Luz da entrada, para o router ser encontravel.
            var kitchen = GameObject.Find("Kitchen_Light");
            if (kitchen == null) return;

            CloneLight(kitchen, "Hall_Light", new Vector3(4.10f, BulbHeight, -1.60f), true);
            // A casa de banho e a zona de jantar tinham candeeiro mas nenhuma Light:
            // sem isto os interruptores dessas divisoes nao teriam nada para ligar.
            CloneLight(kitchen, "Bathroom_Light", new Vector3(1.15f, BulbHeight, -1.90f), false);
            CloneLight(kitchen, "Dining_Light", new Vector3(-1.50f, BulbHeight, 2.90f), false);
            // O quarto do Rui nao tinha luz nenhuma: o interruptor dele acendia a
            // luz do quarto do Tomas, do outro lado do corredor.
            CloneLight(kitchen, "Bedroom_Rui_Light", new Vector3(-1.75f, BulbHeight, -3.00f), false);
        }

        private static void CloneLight(GameObject template, string name, Vector3 position, bool startsOn)
        {
            if (GameObject.Find(name) != null) return;
            var clone = Object.Instantiate(template, template.transform.parent);
            clone.name = name;
            clone.transform.position = position;
            var light = clone.GetComponent<Light>();
            if (light != null) light.enabled = startsOn;
            // As luzes clonadas do corredor trariam a oscilacao junto.
            var unstable = clone.GetComponent<Pungent.Atmosphere.UnstableLight>();
            if (unstable != null) Object.DestroyImmediate(unstable);
        }

        // ------------------------------------------------------------------

        private struct SwitchDef
        {
            public string Name;
            public string RoomLabel;
            public Vector3 Position;
            public float Yaw;          // rotacao da placa; a tecla fica virada para a divisao
            public string[] Lights;
        }

        /// <summary>
        /// Interruptores a 1.15 m do chao, junto ao vao de cada divisao.
        ///
        /// A posicao tem de assentar na FACE da parede, nao na sua linha central:
        /// as paredes interiores tem 0.12 de espessura, pelo que a face fica a 0.06
        /// da linha. Com yaw 0 a tecla sai para +Z, com 90 para +X, com 180 para -Z.
        /// </summary>
        private static readonly SwitchDef[] Switches =
        {
            // Parede sul do corredor (z = -0.80): face do quarto a -0.86.
            new SwitchDef { Name = "Switch_Bedroom_Tomas", RoomLabel = "bedroom light",
                Position = new Vector3(-4.45f, 1.15f, -0.86f), Yaw = 180f,
                Lights = new[] { "Bedroom_Light" } },
            new SwitchDef { Name = "Switch_Bathroom", RoomLabel = "bathroom light",
                Position = new Vector3(1.55f, 1.15f, -0.86f), Yaw = 180f,
                Lights = new[] { "Bathroom_Light" } },

            // Tem de ficar a leste da ombreira (x = -1.55) e nao dentro do vao:
            // o vao vai de -2.45 a -1.55 e a folha da porta varre-o todo. Com o
            // interruptor a meio do vao a porta batia nele e parava aos 55 graus.
            new SwitchDef { Name = "Switch_Bedroom_Rui", RoomLabel = "Rui's light",
                Position = new Vector3(-1.42f, 1.15f, -0.86f), Yaw = 180f,
                Lights = new[] { "Bedroom_Rui_Light" } },

            // Topo oeste do corredor, na parede exterior (x = -7.00).
            new SwitchDef { Name = "Switch_Corridor", RoomLabel = "hall light",
                Position = new Vector3(-7.00f, 1.15f, -0.05f), Yaw = 90f,
                Lights = new[] { "Corridor_Light", "Corridor_Light_West" } },

            // Parede norte do corredor (z = 0.70): face da cozinha a 0.76.
            new SwitchDef { Name = "Switch_Kitchen", RoomLabel = "kitchen light",
                Position = new Vector3(-4.90f, 1.15f, 0.76f), Yaw = 0f,
                Lights = new[] { "Kitchen_Light" } },

            // Parede sul da sala (z = 0.70): face da sala a 0.76.
            // Nao pode ir para o arco sala/jantar: ali nao ha parede nenhuma entre
            // z = 0.95 e 4.40, e o interruptor ficava suspenso no vao.
            new SwitchDef { Name = "Switch_Living", RoomLabel = "living room light",
                Position = new Vector3(0.55f, 1.15f, 0.76f), Yaw = 0f,
                Lights = new[] { "Living_Light" } },

            // Parede casa de banho/hall (x = 2.40): face do hall a 2.46.
            new SwitchDef { Name = "Switch_Hall", RoomLabel = "entrance light",
                Position = new Vector3(2.46f, 1.15f, -1.60f), Yaw = 90f,
                Lights = new[] { "Hall_Light" } },

            // Parede cozinha/jantar (x = -3.00): face do jantar a -2.94.
            new SwitchDef { Name = "Switch_Dining", RoomLabel = "dining light",
                Position = new Vector3(-2.94f, 1.15f, 2.20f), Yaw = 90f,
                Lights = new[] { "Dining_Light" } },
        };

        internal static void BuildSwitches(Scene scene)
        {
            var existing = ApartmentV2WiringUtil.Find(scene, "LIGHT_SWITCHES");
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject("LIGHT_SWITCHES");
            Undo.RegisterCreatedObjectUndo(root, "Build light switches");

            // Material proprio: com o bege da parede o interruptor era detetado mas
            // ficava literalmente invisivel contra o reboco.
            var plate = GetSwitchMaterial();
            var lightsInScene = new Dictionary<string, Light>();
            foreach (var light in Object.FindObjectsOfType<Light>(true))
                if (!lightsInScene.ContainsKey(light.name))
                    lightsInScene[light.name] = light;

            foreach (var def in Switches)
            {
                var holder = new GameObject(def.Name);
                holder.transform.SetParent(root.transform, false);
                holder.transform.position = def.Position;
                holder.transform.rotation = Quaternion.Euler(0f, def.Yaw, 0f);

                // Placa e tecla um pouco maiores e mais salientes, para se lerem
                // de relance numa parede lisa e em pouca luz.
                var plateBox = MakeBox(holder.transform, "Plate", new Vector3(0f, 0f, 0.010f),
                    new Vector3(0.095f, 0.145f, 0.020f), plate);
                var toggle = MakeBox(holder.transform, "Toggle", new Vector3(0f, 0f, 0.026f),
                    new Vector3(0.052f, 0.082f, 0.020f), plate);

                // Um so collider, generoso, no holder: a tecla e pequena de mais para mirar.
                var box = holder.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0f, 0.02f);
                Vector3 inherited = holder.transform.lossyScale;
                box.size = new Vector3(
                    0.22f / Mathf.Max(0.0001f, Mathf.Abs(inherited.x)),
                    0.28f / Mathf.Max(0.0001f, Mathf.Abs(inherited.y)),
                    0.12f / Mathf.Max(0.0001f, Mathf.Abs(inherited.z)));

                var lights = new List<Light>();
                foreach (var name in def.Lights)
                    if (lightsInScene.TryGetValue(name, out Light light)) lights.Add(light);
                    else Debug.LogWarning($"[WireV2] Interruptor '{def.Name}': luz '{name}' nao encontrada.");

                var component = holder.AddComponent<LightSwitchInteractable>();
                var so = new SerializedObject(component);
                var array = so.FindProperty("lights");
                array.arraySize = lights.Count;
                for (int i = 0; i < lights.Count; i++)
                    array.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
                so.FindProperty("toggleTransform").objectReferenceValue = toggle.transform;
                so.FindProperty("roomName").stringValue = def.RoomLabel;
                so.ApplyModifiedPropertiesWithoutUndo();

                plateBox.isStatic = true;
            }
        }

        private static Material GetSwitchMaterial()
        {
            const string path = "Assets/Pungent/Materials/M_Switch_Plate.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", new Color(0.94f, 0.94f, 0.92f));
            material.SetFloat("_Smoothness", 0.55f);   // plastico: apanha um brilho e destaca-se
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject MakeBox(Transform parent, string name, Vector3 localPosition,
            Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<BoxCollider>()); // o collider vive no holder
            if (material != null) go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
    }
}
