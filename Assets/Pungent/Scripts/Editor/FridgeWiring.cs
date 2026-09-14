using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Interaction;
using Pungent.NPC;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Mete o modelo O_R na cozinha e liga-lhe a porta.
    ///
    /// O frigorifico do graybox era uma caixa: o Rui chegava la, ouvia-se o som de
    /// abrir e nada se mexia. O modelo novo tem a folha como objecto proprio, com o
    /// pivo ja na dobradica, portanto da para a rodar sem reparentar nada.
    ///
    /// Re-executavel: nao duplica o modelo.
    /// </summary>
    public static class FridgeWiring
    {
        private const string ModelPath = "Assets/ThirdParty/O_R.fbx";
        private const string ModelName = "Fridge_Model";
        // Ajustado na cena a jogar: a zero o corpo ficava enterrado na parede.
        // Mantido aqui para uma nova corrida do wiring nao desfazer a colocacao.
        private const float ModelForwardOffset = 0.713f;

        [MenuItem("Pungent/Blockout/Wire Fridge", false, 34)]
        public static void Wire()
        {
            var host = GameObject.Find("Kit_Fridge");
            if (host == null) { Debug.LogError("[Fridge] Kit_Fridge nao encontrado."); return; }

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null) { Debug.LogError($"[Fridge] Modelo em falta: {ModelPath}"); return; }

            var stale = host.transform.Find(ModelName);
            if (stale != null) Object.DestroyImmediate(stale.gameObject);

            // O graybox estava rodado 90 graus. O FBX, por sua vez, tem a folha em
            // +Z; deixa-lo a identidade poe a porta **nas costas**, virada para a
            // parede. O corpo fica alinhado com o vao, mas o modelo roda 180 graus
            // para a folha e o interior abrirem para a cozinha.
            //
            // Alem disso, o vao real entre a
            // parede oeste (x = -7.00 por dentro) e o primeiro armario
            // (x = -6.28) tem 72 cm. Um corpo de 83 cm nunca caberia ali sem ja
            // nascer dentro dos dois colliders.
            //
            // Alinha-se com a restante frente da cozinha e usa-se um frigorifico
            // estreito de 68 cm, uma proporcao normal para uma porta unica. O
            // colisor fica com dois centimetros de folga de cada lado.
            host.transform.localPosition = new Vector3(
                -6.64f, host.transform.localPosition.y, host.transform.localPosition.z);
            host.transform.localRotation = Quaternion.identity;

            var hostCollider = host.GetComponent<BoxCollider>();
            if (hostCollider != null)
            {
                var size = hostCollider.size;
                size.x = 0.70f;
                hostCollider.size = size;
                hostCollider.center = new Vector3(
                    0f, hostCollider.center.y, hostCollider.center.z);
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = ModelName;
            Undo.RegisterCreatedObjectUndo(model, "Wire Fridge");
            model.transform.SetParent(host.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // E preciso mudar a dobradica de lado. Um filho de uma instancia de
            // prefab importada nao se deixa reparentar, portanto desempacota-se
            // primeiro — e exactamente a armadilha registada no handoff.
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            // Escala: preserva altura e profundidade, mas estreita a largura para o
            // vao real. Nao e uma deformacao escondida: passa a ler-se como um
            // frigorifico estreito, em vez de um frigorifico normal enterrado na
            // parede e no armario.
            Vector3 slot = hostCollider != null
                ? Vector3.Scale(hostCollider.size, host.transform.lossyScale)
                : new Vector3(0.70f, 2.12f, 0.83f);

            var bounds = MeasureLocal(model.transform);
            float bodyScale = Mathf.Min(
                slot.y / Mathf.Max(0.01f, bounds.size.y),
                slot.z / Mathf.Max(0.01f, bounds.size.z));
            float widthScale = Mathf.Min(
                bodyScale,
                (slot.x - 0.02f) / Mathf.Max(0.01f, bounds.size.x));
            model.transform.localScale = new Vector3(widthScale, bodyScale, bodyScale);

            // Assenta no chao: o pivo do modelo esta na base, mas confirma-se pelas
            // bounds em vez de se assumir. O offset em Z e a colocacao afinada na
            // cena: o frigorifico fica encostado, mas nao dentro da parede.
            model.transform.localPosition = new Vector3(
                0f, -bounds.min.y * bodyScale, ModelForwardOffset);
            Vector3 fittedSize = Vector3.Scale(bounds.size, model.transform.localScale);

            // A caixa do graybox e tambem o alvo de interaccao. Quando o modelo foi
            // puxado para a frente, ela ficou no sitio antigo e era possivel mirar
            // o vazio atras do frigorifico. O centro passa a vir da propria malha:
            // qualquer novo offset do modelo leva a hitbox com ele.
            if (hostCollider != null)
            {
                Vector3 fittedCentre = host.transform.InverseTransformPoint(
                    model.transform.TransformPoint(bounds.center));
                hostCollider.center = new Vector3(
                    fittedCentre.x,
                    hostCollider.center.y,
                    fittedCentre.z);
            }

            // A caixa do graybox deixa de se ver, mas fica como colisor.
            var placeholder = host.transform.Find("Kit_Fridge_Mesh");
            if (placeholder != null)
            {
                var r = placeholder.GetComponent<MeshRenderer>();
                if (r != null) r.enabled = false;
            }

            // --- a luz de dentro ---
            //
            // Estava a meia altura (y = 1.05), **entre** duas prateleiras e a vinte
            // centimetros das duas. Uma luz de ponto cai com o quadrado da
            // distancia: a essa distancia queimava a prateleira, as garrafas e o
            // que la estivesse a branco, e foi isso que se viu a jogar.
            //
            // Sobe para o tejadilho, onde a lampada de um frigorifico esta mesmo, e
            // com metade da intensidade. A prateleira de cima fica a meio metro em
            // vez de a vinte centimetros — a mesma luz, quatro vezes menos forte
            // onde batia — e o interior passa a escurecer para baixo, como um
            // frigorifico escurece.
            var lightObject = new GameObject("Fridge_InteriorLight");
            Undo.RegisterCreatedObjectUndo(lightObject, "Wire Fridge");
            lightObject.transform.SetParent(model.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1.80f, 0.18f);

            var interior = lightObject.AddComponent<Light>();
            interior.type = LightType.Point;
            interior.color = new Color(1f, 0.96f, 0.86f);
            // O alcance nao a torna mais forte — so diz onde deixa de contar. Chega
            // para a prateleira de baixo e para o palmo de cozinha em frente a
            // porta, que as 02:47 e a unica luz que ha.
            interior.range = 2.8f;
            interior.intensity = 0f;
            interior.shadows = LightShadows.None;
            interior.enabled = false;

            // --- o componente da porta ---
            var leaf = model.transform.Find("Door");
            var hinge = BuildOppositeHinge(model.transform, leaf);

            var fridge = model.GetComponent<FridgeDoor>();
            if (fridge == null) fridge = Undo.AddComponent<FridgeDoor>(model);

            var so = new SerializedObject(fridge);
            so.FindProperty("door").objectReferenceValue = hinge;
            so.FindProperty("interiorLight").objectReferenceValue = interior;
            so.ApplyModifiedProperties();

            // --- liga ao circuito do Rui ---
            var effects = Object.FindObjectOfType<RuiRoutineEffects>(true);
            var effectsComponent = effects;
            if (effects != null)
            {
                var eso = new SerializedObject(effects);
                eso.FindProperty("fridgeDoor").objectReferenceValue = fridge;

                // O som passa a sair do frigorifico e nao do Rui.
                var audio = host.GetComponent<AudioSource>();
                if (audio == null) audio = Undo.AddComponent<AudioSource>(host);
                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
                audio.rolloffMode = AudioRolloffMode.Linear;
                audio.minDistance = 0.8f;
                audio.maxDistance = 6f;
                eso.FindProperty("fridgeAudio").objectReferenceValue = audio;
                eso.ApplyModifiedProperties();
            }

            // --- o jogador tambem o abre ---
            // O componente vai para o objecto que tem o colisor: o PlayerInteractor
            // resolve o alvo subindo a hierarquia a partir do colisor atingido, e a
            // caixa do graybox e a unica coisa que ele consegue mirar.
            float openAngle = MeasureOpenAngle(host, model.transform, hinge, out string blockedBy);

            var reachable = host.GetComponent<FridgeDoor>();
            if (reachable == null) reachable = Undo.AddComponent<FridgeDoor>(host);
            var rso = new SerializedObject(reachable);
            rso.FindProperty("door").objectReferenceValue = hinge;
            rso.FindProperty("interiorLight").objectReferenceValue = interior;
            // Escritos e nao deixados ao valor por omissao do componente: mudar o
            // `= 0.9f` no C# nao mexe numa instancia que ja esta na cena — o valor
            // serializado ganha sempre. E das armadilhas mais silenciosas do Unity,
            // e ja custou este projecto varias vezes.
            rso.FindProperty("lightIntensity").floatValue = 0.18f;
            rso.FindProperty("openAngle").floatValue = openAngle;
            rso.ApplyModifiedProperties();

            // O que estava no frigorifico era um pensamento; agora ele abre-se.
            var flavour = host.GetComponent<FlavourInteractable>();
            if (flavour != null) Undo.DestroyObjectImmediate(flavour);

            // O componente no modelo era so para o circuito do Rui; passa a haver um
            // so, no sitio onde o jogador lhe chega.
            if (fridge != null && fridge != reachable) Undo.DestroyObjectImmediate(fridge);
            if (effectsComponent != null)
            {
                var eso2 = new SerializedObject(effectsComponent);
                eso2.FindProperty("fridgeDoor").objectReferenceValue = reachable;
                eso2.ApplyModifiedProperties();
            }

            BuildInterior(model.transform);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Fridge] Modelo colocado. Escala {widthScale:F3} x " +
                      $"{bodyScale:F3} x {bodyScale:F3} " +
                      $"({bounds.size.x:F2}x{bounds.size.y:F2}x{bounds.size.z:F2} -> " +
                      $"{fittedSize.x:F2}x{fittedSize.y:F2}x{fittedSize.z:F2}), " +
                      $"porta a abrir {Mathf.Abs(openAngle):F0} graus" +
                      (string.IsNullOrEmpty(blockedBy) ? " (sem nada no caminho)" : $" — travada por {blockedBy}") +
                      (effects == null ? "  [RuiRoutineEffects NAO encontrado]" : "."));
        }

        /// <summary>
        /// Poe a dobradica no lado do armario, nao no lado da parede.
        ///
        /// O mesh importado traz o pivo no bordo esquerdo. Neste vao esse bordo fica
        /// a dois centimetros da parede oeste: rodar a folha em qualquer sentido
        /// mete-a na parede. Um frigorifico encostado a uma parede abre pelo lado
        /// oposto, para a folha varrer a divisao.
        /// </summary>
        private static Transform BuildOppositeHinge(Transform model, Transform leaf)
        {
            if (leaf == null) return null;

            Bounds local = MeasureLocal(leaf);
            var hingeObject = new GameObject("Door_Hinge");
            Undo.RegisterCreatedObjectUndo(hingeObject, "Wire Fridge");
            var hinge = hingeObject.transform;
            hinge.SetParent(model, false);

            // Com o modelo rodado 180 graus, o bordo local minimo fica do lado
            // direito para quem olha da cozinha: longe da parede oeste e com arco
            // livre para a folha vir para a frente, em vez de rodar para tras.
            Vector3 opposite = leaf.TransformPoint(new Vector3(local.min.x, 0f, 0f));
            hinge.position = opposite;
            hinge.rotation = leaf.rotation;

            leaf.SetParent(hinge, true);
            return hinge;
        }

        /// <summary>
        /// Ate onde a folha abre sem entrar dentro da mobilia.
        ///
        /// A porta abria 105 graus escritos a mao e atravessava a bancada — visto a
        /// jogar. Medir os dois sentidos nao resolvia nada porque **ambos batem**: o
        /// frigorifico esta encostado ao topo do movel da cozinha e a dobradica cai
        /// do lado da bancada.
        ///
        /// Em vez de escolher um numero a olho, o angulo passa a ser medido: varre-se
        /// o arco de fora para dentro com a caixa da folha e fica-se com o primeiro
        /// que nao toca em nada que nao seja o proprio frigorifico. Uma porta de
        /// frigorifico a abrir 80 graus e uma porta de frigorifico; uma a abrir 105
        /// para dentro do balcao e um erro que se ve de toda a cozinha.
        ///
        /// Se o vao mudar — se a bancada andar, se o movel for outro — a ferramenta
        /// volta a medir e o numero acompanha, que e a razao de isto nao ser uma
        /// constante.
        /// </summary>
        private static float MeasureOpenAngle(GameObject host, Transform model, Transform door,
            out string blockedBy)
        {
            blockedBy = null;
            const float wanted = -105f;
            const float least = -35f;

            if (door == null) return wanted;

            Bounds leaf = MeasureLocal(door);
            if (leaf.size.sqrMagnitude < 0.0001f) return wanted;

            // Margem: sem ela a folha rasa a bancada e conta como batida, e o
            // resultado ficava sempre um passo abaixo do que da mesmo.
            Vector3 scale = Vector3.Scale(model.lossyScale, door.localScale);
            Vector3 half = Vector3.Scale(leaf.extents, scale);
            half.x = Mathf.Max(0.01f, half.x - 0.02f);
            half.z = Mathf.Max(0.01f, half.z - 0.02f);
            // Em altura corta-se mais: a folha chega ao chao e o chao e um colisor.
            half.y = Mathf.Max(0.01f, half.y - 0.08f);

            Physics.SyncTransforms();
            var hits = new Collider[16];

            for (float angle = wanted; angle <= least; angle += 5f)
            {
                var local = Quaternion.Euler(0f, angle, 0f);
                Matrix4x4 toWorld = model.localToWorldMatrix *
                    Matrix4x4.TRS(door.localPosition, local, door.localScale);

                int count = Physics.OverlapBoxNonAlloc(
                    toWorld.MultiplyPoint3x4(leaf.center), half, hits,
                    model.rotation * local, ~0, QueryTriggerInteraction.Ignore);

                string blocker = null;
                for (int i = 0; i < count && blocker == null; i++)
                {
                    var hit = hits[i];
                    // O proprio frigorifico nao conta: a caixa do graybox esta
                    // exactamente onde a folha esta fechada.
                    if (hit == null || hit.transform.IsChildOf(host.transform)) continue;
                    blocker = hit.name;
                }

                // Livre. O `blockedBy` que sobrou e quem travou o angulo anterior —
                // e essa a informacao util: nao "abre 80", mas "abre 80 por causa
                // da bancada".
                if (blocker == null) return angle;

                blockedBy = blocker;
            }

            Debug.LogWarning($"[Fridge] A folha nao abre {Mathf.Abs(least):F0} graus sem bater " +
                             $"em {blockedBy}. O frigorifico esta encaixado de mais: " +
                             "isto e para resolver movendo o movel, nao o angulo.");
            return least;
        }

        /// <summary>
        /// Prateleiras e o que la esta em cima.
        ///
        /// O modelo nao traz interior nenhum: sem isto a porta abria para um bloco
        /// macico. Sao tres prateleiras finas e meia duzia de coisas do Mess Maker,
        /// escolhidas pelo que dizem — latas de cerveja que nao sao dele, meio
        /// hamburguer e pouco mais. Um frigorifico cheio contava outra historia.
        /// </summary>
        private static void BuildInterior(Transform model)
        {
            var stale = model.Find("Fridge_Interior");
            if (stale != null) Object.DestroyImmediate(stale.gameObject);

            var root = new GameObject("Fridge_Interior");
            Undo.RegisterCreatedObjectUndo(root, "Wire Fridge");
            root.transform.SetParent(model, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;

            // Medidas em espaco local do modelo: o corpo tem 1.025 x 1.937 x 0.787
            // e a folha fecha a frente em +Z.
            const float innerWidth = 0.86f;
            const float innerDepth = 0.60f;
            const float frontZ = 0.30f;

            var shelfMaterial = GetShelfMaterial();
            float[] heights = { 0.42f, 0.86f, 1.30f };

            for (int i = 0; i < heights.Length; i++)
            {
                var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shelf.name = $"Shelf_{i}";
                shelf.transform.SetParent(root.transform, false);
                shelf.transform.localPosition = new Vector3(0f, heights[i], frontZ - innerDepth * 0.5f);
                shelf.transform.localScale = new Vector3(innerWidth, 0.02f, innerDepth);
                Object.DestroyImmediate(shelf.GetComponent<Collider>());
                shelf.GetComponent<MeshRenderer>().sharedMaterial = shelfMaterial;
            }

            // Keep the fridge sparse and readable. Mess Maker models use Z-up, so
            // every drink must be rotated upright before it is placed on a shelf.
            var contents = new (string Path, int Shelf, float X, float Z, float Scale)[]
            {
                ("Cans/Beer Can Green",                     0, -0.24f, -0.10f, 0.78f),
                ("Cans/Beer Can Blue",                      0,  0.00f, -0.10f, 0.78f),
                ("Cans/Soda Can Orange",                    0,  0.24f, -0.10f, 0.78f),
                ("Glass Bottles/Beer Bottle Green with Label", 1, -0.22f, -0.10f, 0.68f),
                ("Glass Bottles/Wine Bottle Red",           1,  0.22f, -0.10f, 0.46f),
            };

            int placed = 0;
            foreach (var item in contents)
            {
                string path = $"Assets/Mess Maker Free/Prefabs/High Poly/{item.Path}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { Debug.LogWarning($"[Fridge] Em falta: {path}"); continue; }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetParent(root.transform, false);
                instance.transform.localPosition = new Vector3(
                    item.X,
                    heights[item.Shelf] + 0.01f,
                    frontZ - innerDepth * 0.5f + item.Z);
                instance.transform.localRotation = Quaternion.Euler(
                    270f, Random.Range(-12f, 12f), 0f);
                instance.transform.localScale = Vector3.one * item.Scale;

                // Rest the renderer on the shelf instead of trusting imported pivots.
                var itemRenderers = instance.GetComponentsInChildren<Renderer>(true);
                if (itemRenderers.Length > 0)
                {
                    var itemBounds = itemRenderers[0].bounds;
                    for (int i = 1; i < itemRenderers.Length; i++)
                        itemBounds.Encapsulate(itemRenderers[i].bounds);
                    float shelfY = root.transform.TransformPoint(new Vector3(
                        0f, heights[item.Shelf] + 0.02f, 0f)).y;
                    instance.transform.position += Vector3.up * (shelfY - itemBounds.min.y);
                }

                // Nada de fisica nem de colisao la dentro: o jogador nunca lhes toca
                // e um Rigidbody por lata era peso a troco de nada.
                foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true))
                    Object.DestroyImmediate(body);
                foreach (var col in instance.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(col);

                placed++;
            }

            Debug.Log($"[Fridge] Interior: {heights.Length} prateleiras, {placed} objectos.");
        }

        private static Material GetShelfMaterial()
        {
            const string path = "Assets/Pungent/Materials/M_Fridge_Shelf.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", new Color(0.86f, 0.88f, 0.90f, 1f));
            // 0.72 era vidro. Com a lampada a um palmo, cada prateleira devolvia um
            // reflexo especular a branco por cima do branco que ja tinha — metade do
            // "estouro" nao era a luz, era isto. Plastico velho reflecte pouco.
            material.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Bounds do modelo em espaço local do próprio transform.</summary>
        private static Bounds MeasureLocal(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);

            bool first = true;
            Bounds result = default;
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                Bounds mesh = mf.sharedMesh.bounds;
                Matrix4x4 toRoot = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 offset = Vector3.Scale(mesh.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                    Vector3 point = toRoot.MultiplyPoint3x4(mesh.center + offset);
                    if (first) { result = new Bounds(point, Vector3.zero); first = false; }
                    else result.Encapsulate(point);
                }
            }
            return first ? new Bounds(Vector3.zero, Vector3.one) : result;
        }
    }
}
