using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Pungent.Interaction;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Portas fisicas da planta V2: instancia a porta modelo do graybox em cada vao,
    /// veste-a com o FBX de madeira e poe tudo na layer "Door".
    /// </summary>
    internal static class ApartmentV2DoorWiring
    {
        private const string DoorsRoot = "DOORS_V2";
        private const string DoorTemplate = "Door_Bedroom_Hinge";
        private const string FrontDoor = "Door_Front_3B";
        private const string FrontDoorSafety = "Door_Front_3B_Safety";
        private const string DoorModelPath = "Assets/ThirdParty/door/woodenDoor_01_v1_ANIM.fbx";

        // Medidas reais do FBX da porta: aro 1.00 x 2.10, folha 0.836 com a
        // dobradica em +X e os puxadores em -X.
        private const float ModelFrameWidth = 1.00f;
        private const float ModelLeafHingeEdge = 0.413f;
        private const float ModelReferenceOpening = 0.90f;

        private struct DoorDef
        {
            public string Name;
            public Vector3 HingePosition;
            public float Yaw;
            public float Width;
            public bool Locked;
        }

        /// <summary>
        /// A folha estende-se de +Z local a partir da dobradica e abre para +X local.
        /// yaw 90  => folha para este, abre para sul (entra no quarto/arrumo)
        /// yaw 180 => folha para sul,  abre para oeste (entra no hall)
        /// </summary>
        private static readonly DoorDef[] Doors =
        {
            new DoorDef { Name = "Door_Bedroom_Tomas", HingePosition = new Vector3(-5.55f, 0f, -0.80f), Yaw = 90f,  Width = 0.90f },
            new DoorDef { Name = "Door_Bedroom_Rui",   HingePosition = new Vector3(-2.45f, 0f, -0.80f), Yaw = 90f,  Width = 0.90f },
            new DoorDef { Name = "Door_Bathroom",      HingePosition = new Vector3( 0.45f, 0f, -0.80f), Yaw = 90f,  Width = 0.90f },
            new DoorDef { Name = "Door_Laundry",       HingePosition = new Vector3( 0.60f, 0f, -3.00f), Yaw = 90f,  Width = 0.90f },
            new DoorDef { Name = "Door_Storage",       HingePosition = new Vector3( 3.50f, 0f, -3.00f), Yaw = 90f,  Width = 0.90f },
            new DoorDef { Name = FrontDoor,            HingePosition = new Vector3( 5.925f, 0f, -1.45f), Yaw = 180f, Width = 1.00f, Locked = true },
            new DoorDef { Name = "Door_Balcony",       HingePosition = new Vector3( 0.30f, 0f,  5.125f), Yaw = 90f,  Width = 1.20f },
        };

        internal static void Build(Scene scene, int doorLayer)
        {
            var template = ApartmentV2WiringUtil.Find(scene, DoorTemplate);
            if (template == null)
            {
                Debug.LogError($"[WireV2] Porta modelo '{DoorTemplate}' nao encontrada. Portas nao criadas.");
                return;
            }

            var old = ApartmentV2WiringUtil.Find(scene, DoorsRoot);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(DoorsRoot);
            Undo.RegisterCreatedObjectUndo(root, "Wire Apartment V2 doors");

            foreach (var def in Doors)
            {
                var door = Object.Instantiate(template, root.transform);
                door.name = def.Name;
                door.SetActive(true);
                door.transform.position = def.HingePosition;
                door.transform.rotation = Quaternion.Euler(0f, def.Yaw, 0f);

                // A entrada abre para dentro. O limite negativo deixava a fisica
                // tentar empurra-la contra o batente exterior, que era precisamente
                // o contacto que a fazia aparecer atravessada.
                if (def.Name == FrontDoor)
                {
                    var hinge = door.GetComponent<HingeJoint>();
                    if (hinge != null)
                    {
                        var limits = hinge.limits;
                        limits.min = 0f;
                        limits.max = 105f;
                        hinge.limits = limits;
                        hinge.useLimits = true;
                    }
                }

                var leaf = door.transform.Find(template.transform.GetChild(0).name);
                var handle = door.transform.Find(template.transform.GetChild(1).name);
                if (leaf != null)
                {
                    leaf.name = def.Name + "_Leaf";
                    leaf.localPosition = new Vector3(0f, 1.05f, def.Width * 0.5f);
                    leaf.localScale = new Vector3(0.08f, 2.10f, def.Width);
                }
                if (handle != null)
                {
                    handle.name = def.Name + "_Handle";
                    handle.localPosition = new Vector3(-0.08f, 1.05f, def.Width - 0.16f);
                }

                var interactable = door.GetComponent<DoorDragInteractable>();
                if (interactable != null)
                {
                    interactable.SetLocked(def.Locked);
                    AttachNavObstacle(door, interactable, def.Width);

                    // A entrada nao usa fisica: fica fechada e imovel durante o jogo.
                    // No climax, o ClimaxExit e que trata a saida e corta para preto.
                    if (def.Name == FrontDoor)
                    {
                        var body = door.GetComponent<Rigidbody>();
                        if (body != null)
                        {
                            body.angularVelocity = Vector3.zero;
                            body.velocity = Vector3.zero;
                            body.interpolation = RigidbodyInterpolation.None;
                            body.isKinematic = true;
                        }

                        var so = new SerializedObject(interactable);
                        so.FindProperty("staticDoor").boolValue = true;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                AttachModel(door, root.transform, def.Width);
                if (def.Name == FrontDoor)
                    AttachFrontDoorSafety(door, root.transform, def.Width, doorLayer);
                SetLayerRecursive(door, doorLayer);
            }

            // O modelo original do graybox deixa de ser usado na cena V2.
            template.SetActive(false);
        }

        /// <summary>
        /// Tapa o vao na NavMesh enquanto a porta esta fechada.
        ///
        /// A NavMesh e feita uma vez, com os vaos livres, e nao sabe que ali ha uma
        /// folha: sem isto o Rui atravessava portas fechadas como se nao existissem.
        /// O obstaculo fica no vao (estatico) e nao na folha, para nao andar a
        /// carvar a NavMesh enquanto a porta roda.
        /// </summary>
        private static void AttachNavObstacle(GameObject door, DoorDragInteractable interactable,
            float width)
        {
            var holder = new GameObject(door.name + "_NavBlock");
            holder.transform.SetParent(door.transform.parent, false);
            holder.transform.position = door.transform.position;
            holder.transform.rotation = door.transform.rotation;
            // O vao estende-se de +Z local a partir da dobradica.
            holder.transform.position += holder.transform.TransformDirection(
                new Vector3(0f, 0f, width * 0.5f));

            var obstacle = holder.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, 1.05f, 0f);
            obstacle.size = new Vector3(0.16f, 2.10f, width);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            var so = new SerializedObject(interactable);
            so.FindProperty("navObstacle").objectReferenceValue = obstacle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        /// <summary>
        /// Backstop invisivel no lado exterior da porta 3B.
        ///
        /// O chao do hall termina exactamente no plano da parede. Se a fisica da
        /// folha falhar por um frame, o passo seguinte ja e vazio. Esta caixa comeca
        /// onde o chao acaba e fecha apenas o vao exterior; nao interfere com o corte
        /// final, que suspende o jogador antes de abrir a porta.
        /// </summary>
        private static void AttachFrontDoorSafety(GameObject door, Transform parent,
            float width, int layer)
        {
            var holder = new GameObject(FrontDoorSafety);
            holder.transform.SetParent(parent, false);
            holder.transform.position = door.transform.position
                                      + door.transform.forward * (width * 0.5f)
                                      - door.transform.right * 0.22f;
            holder.transform.rotation = door.transform.rotation;
            holder.layer = layer;

            var box = holder.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.05f, 0f);
            box.size = new Vector3(0.18f, 2.10f, width);
        }

        /// <summary>
        /// Substitui a caixa da folha pelo modelo de madeira, mantendo a caixa como
        /// colisor de fisica (mais barato e estavel que um MeshCollider num Rigidbody).
        /// O aro e reparentado para o root estatico: se rodasse com a folha, o
        /// caixilho girava com a porta.
        /// </summary>
        private static void AttachModel(GameObject door, Transform staticParent, float width)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(DoorModelPath);
            if (source == null)
            {
                Debug.LogWarning($"[WireV2] Modelo de porta em falta: {DoorModelPath}");
                return;
            }

            // A caixa passa a ser so colisor.
            var leaf = door.transform.Find(door.name + "_Leaf");
            if (leaf != null)
            {
                var renderer = leaf.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = false;
            }
            var handle = door.transform.Find(door.name + "_Handle");
            if (handle != null) handle.gameObject.SetActive(false);

            float widthScale = width / ModelReferenceOpening;
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pungent/Materials/M_Door_Wood.mat");
            var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pungent/Materials/M_Door_Handle.mat");

            // O aro nao pode rodar com a folha, mas o Unity nao permite reparentar
            // filhos de uma instancia de prefab. Instancia-se o modelo duas vezes e
            // em cada copia desliga-se o que nao interessa.
            var leafModel = SpawnPiece(source, door.transform, door.name + "_Model", widthScale, wood, metal,
                keepFrame: false);
            var frameHolder = new GameObject(door.name + "_Frame");
            frameHolder.transform.SetParent(staticParent, false);
            frameHolder.transform.position = door.transform.position;
            frameHolder.transform.rotation = door.transform.rotation;
            SpawnPiece(source, frameHolder.transform, door.name + "_FrameModel", widthScale, wood, metal,
                keepFrame: true);

            if (leafModel == null)
                Debug.LogWarning($"[WireV2] Falhou a colocar o modelo em {door.name}.");
        }

        private static GameObject SpawnPiece(GameObject source, Transform parent, string name,
            float widthScale, Material wood, Material metal, bool keepFrame)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = name;
            model.transform.SetParent(parent, false);

            // Rodar 90 em Y faz o -X do modelo (dobradica -> aresta livre) coincidir
            // com o +Z local, que e a direcao em que a folha se estende.
            model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            model.transform.localScale = new Vector3(widthScale, 1f, 1f);
            model.transform.localPosition = new Vector3(0f, 0f, ModelLeafHingeEdge * widthScale);

            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool isFrame = r.name == "DoorFrame";
                r.enabled = keepFrame == isFrame;
                if (!r.enabled) continue;
                var material = r.name.StartsWith("Handle") ? metal : wood;
                if (material != null) r.sharedMaterial = material;
            }

            return model;
        }
    }
}
