using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.Driving;
using Pungent.Interaction;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Monta a estrada da noite: alcatrao, arvores, carro, radio e quem vem atras.
    ///
    /// A estrada do pack sao duas camadas da mesma rota — o `MeshV00` e o alcatrao
    /// com relva, banking e milho, e o `_Alley` e a fila de arvores. O handoff
    /// recomendava o `_Alley` como sendo a estrada; nao e, o material dele e folha
    /// e nao ha por onde conduzir. Entram as duas, que e o que faz o corredor.
    ///
    /// A viagem dura o que as duas musicas durarem (4m21). Ver
    /// <see cref="NightRoadDirector"/> para o porque de o relogio ser a musica.
    ///
    /// Re-executavel: apaga o que montou antes e volta a montar.
    /// </summary>
    internal static class NightRoadBuilder
    {
        private const string Root = "NIGHT_ROAD";
        private const string RoadPrefab =
            "Assets/KajamansRoads/Free/Prefabs/l10km_cc4_sl20_t(12123025)_rw10_wh3_n3_RsBTW_MeshV00.prefab";
        private const string AlleyPrefab =
            "Assets/KajamansRoads/Free/Prefabs/l10km_cc4_sl20_t(12123025)_A87113010_Alley.prefab";
        // O SEDAN e nao o hatchback: e o carro da historia, e a pintura dele esta
        // preta no proprio material (`SEDAN_PAINT.mat`) — mudar a cor do carro e
        // mudar la o `_BaseColor`, sem tocar em cena nenhuma.
        private const string CarPrefab = "Assets/Hatchback and Sedan/prefabs/SEDAN.prefab";

        /// <summary>
        /// Comprimento do percurso, em metros. A ~72 km/h sao uns 3m30 de estrada,
        /// e o `endMargin` do director tira os ultimos 420 para a avaria e a fuga —
        /// sobram cerca de 2m50 a conduzir. Estava em 5,7 km e era entediante.
        /// </summary>
        private const float RouteMetres = 4200f;

        /// <summary>O carro do estranho: o outro da pasta, para nao ser o mesmo.</summary>
        private const string FollowerCarPrefab =
            "Assets/Hatchback and Sedan/prefabs/HATCHBACK_1988.prefab";
        private const string Track1 = "Assets/ThirdParty/Audio/radio_music1.mp3";
        private const string Track2 = "Assets/ThirdParty/Audio/radio_music2.mp3";
        private const string Track3 = "Assets/ThirdParty/Audio/radio_music3.mp3";

        // Midnight e nao Night: o Night ainda tem resto de crepusculo no horizonte,
        // e esta estrada e noite fechada — o que se ve ao longe tem de ser o
        // nevoeiro a comer o alcatrao, nao um por-do-sol.
        private const string NightSkybox = "Assets/Day-Night Skyboxes/Materials/SkyMidnight.mat";

        private const string GrassMeshFolder = "Assets/grass/source/Grass/Grass/another folder/";
        private const string GrassTexFolder = "Assets/grass/textures/";
        private const string GrassPrefabFolder = "Assets/Pungent/Prefabs/Grass";
        private const string PolePrefab =
            "Assets/Sat Productions/G-01/Street Lights Pack 01/Prefabs/Street_Light_A_01.prefab";

        [MenuItem("Pungent/Blockout/Build Night Road", false, 17)]
        internal static void Build()
        {
            if (BuildGuard.Blocked("BuildNightRoad")) return;

            var scene = EditorSceneManager.GetActiveScene();

            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Build night road");

            var path = BuildRoad(root.transform);
            if (path == null) return;

            var car = BuildCar(root.transform);
            var follower = BuildFollower(root.transform);
            var walker = BuildWalker(root.transform);
            BuildNight(root.transform);

            WireDirector(root.transform, path, car, follower, walker);

            // Os pensamentos da viagem: tres minutos de estrada sem uma palavra
            // deixavam o jogador a perguntar para onde ia.
            root.AddComponent<RoadThoughts>();
            BuildRoadside(root.transform, path, car);
            BuildRoadEnd(root.transform, path);

            // Jogador e sistemas, e depois sentado ao volante. Feito aqui e nao a
            // mao porque a estrada e o unico capitulo onde o jogador nao anda a pe:
            // deixar isto por fazer dava uma cena com estrada e sem ninguem nela.
            EnsureSpawnAtSeat(car);
            ScenePlayerSetup.Setup();
            SeatPlayer(car);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Estrada] Montada, com o Tomas ao volante. A viagem dura o que " +
                      "o radio durar (~4m21) e o motor morre quando a segunda musica acabar.");
        }

        /// <summary>
        /// Poe as duas camadas da estrada e assa a linha central a partir do alcatrao.
        /// </summary>
        private static RoadPath BuildRoad(Transform parent)
        {
            var roadAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RoadPrefab);
            if (roadAsset == null)
            {
                Debug.LogError("[Estrada] Nao encontrei o alcatrao em " + RoadPrefab);
                return null;
            }

            var road = (GameObject)PrefabUtility.InstantiatePrefab(roadAsset);
            road.name = "ROAD_Surface";
            road.transform.SetParent(parent, false);

            // A malha nao traz collider em cima do prefab instanciado em todos os
            // casos; sem ele o carro cai para sempre.
            var filter = road.GetComponentInChildren<MeshFilter>(true);
            var collider = road.GetComponentInChildren<MeshCollider>(true);
            if (collider == null && filter != null)
            {
                collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }

            // A layer que os raios do CarDriver procuram. Na "Default", o raio de
            // assentar na rampa apanhava o proprio carro.
            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0 && collider != null) collider.gameObject.layer = ground;
            else if (ground < 0) Debug.LogWarning("[Estrada] Sem layer 'Ground' no projecto; " +
                                                  "o carro alinha com o que o raio apanhar.");

            var alleyAsset = AssetDatabase.LoadAssetAtPath<GameObject>(AlleyPrefab);
            if (alleyAsset != null)
            {
                var alley = (GameObject)PrefabUtility.InstantiatePrefab(alleyAsset);
                alley.name = "ROAD_Trees";
                alley.transform.SetParent(parent, false);
            }
            else Debug.LogWarning("[Estrada] Sem a fila de arvores; o corredor fica mais pobre.");

            var path = road.AddComponent<RoadPath>();
            var centre = Centreline(filter.sharedMesh);

            // Cortada aos metros e nao a contar seccoes: elas nao sao todas do mesmo
            // tamanho — nas curvas ha muito mais por metro — e cortar "dois tercos
            // das seccoes" dava 5,7 km enquanto "43%" dava 3,4 km. Em metros
            // pede-se o que se quer.
            //
            // Estava em 5,7 km porque a viagem tinha de durar as duas musicas.
            // Jogada, era comprida de mais: estrada nacional a direito de noite nao
            // tem o que ver, e a curiosidade acaba antes do alcatrao.
            centre = TrimTo(centre, RouteMetres);
            path.EditorSetPoints(centre);
            Debug.Log($"[Estrada] Linha central assada: {path.Count} seccoes, {path.Length:F0} m.");
            return path;
        }

        /// <summary>
        /// Os vertices do alcatrao vem aos pares, bordo a bordo, pela ordem do
        /// gerador. O centro de cada par e a linha central.
        /// </summary>
        /// <summary>
        /// Corta a linha central aos tantos metros.
        ///
        /// O `endMargin` do <see cref="NightRoadDirector"/> guarda os ultimos metros
        /// para a avaria e a fuga, portanto o que sobra para conduzir e menos do que
        /// isto — a conta esta la, nao aqui.
        /// </summary>
        private static Vector3[] TrimTo(Vector3[] centre, float metres)
        {
            float total = 0f;
            for (int i = 1; i < centre.Length; i++)
            {
                total += Vector3.Distance(centre[i - 1], centre[i]);
                if (total < metres) continue;

                var cut = new Vector3[i + 1];
                System.Array.Copy(centre, cut, i + 1);
                return cut;
            }
            return centre;
        }

        private static Vector3[] Centreline(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var indices = mesh.GetIndices(0);

            var seen = new System.Collections.Generic.HashSet<int>();
            var ordered = new System.Collections.Generic.List<int>();
            foreach (var i in indices) if (seen.Add(i)) ordered.Add(i);
            ordered.Sort();

            var centre = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i + 1 < ordered.Count; i += 2)
                centre.Add((vertices[ordered[i]] + vertices[ordered[i + 1]]) * 0.5f);

            return centre.ToArray();
        }

        private static CarDriver BuildCar(Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CarPrefab);
            var car = asset != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(asset)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

            car.name = "CAR";
            car.transform.SetParent(parent, false);
            car.transform.position = new Vector3(0f, 0.6f, 12f);
            car.transform.rotation = Quaternion.identity;   // a estrada arranca virada a +Z

            var bounds = Bounds(car);

            // O prefab vem com escala 0,6. As `bounds` de um `Renderer` sao em
            // metros do mundo, mas o `size` de um `BoxCollider` e em espaco local —
            // passar-lhe metros do mundo dava um collider 40% mais pequeno que o
            // carro, e o carro atravessava o muro pelos cantos.
            Vector3 scale = car.transform.lossyScale;
            Vector3 localSize = new Vector3(
                bounds.size.x / Mathf.Max(0.0001f, scale.x),
                bounds.size.y / Mathf.Max(0.0001f, scale.y),
                bounds.size.z / Mathf.Max(0.0001f, scale.z));

            // Assentar no alcatrao. O pivot do prefab nao esta debaixo das rodas, e
            // sem isto o carro nascia a meio metro do chao e caia no primeiro frame.
            float drop = bounds.min.y - 0.04f;
            car.transform.position -= new Vector3(0f, drop, 0f);
            bounds = Bounds(car);

            // O prefab traz um MeshCollider concavo, que um Rigidbody a mexer nao
            // aceita. Uma caixa chega e e previsivel — a §22 pede fisica simples.
            foreach (var mesh in car.GetComponentsInChildren<MeshCollider>(true))
                Object.DestroyImmediate(mesh);

            var box = car.AddComponent<BoxCollider>();
            box.center = car.transform.InverseTransformPoint(bounds.center);
            box.size = localSize;

            var body = car.AddComponent<Rigidbody>();
            body.mass = 1100f;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var driver = car.AddComponent<CarDriver>();
            BuildHeadlights(car.transform, bounds);
            BuildCabinLight(car.transform, bounds);
            BuildWheelVisuals(car, driver);
            BuildBodyFeel(car, driver, bounds);
            BuildRadio(car.transform, driver, bounds);
            BuildSeat(car.transform, bounds);
            BuildBreakdown(car, driver, bounds);
            return driver;
        }

        /// <summary>
        /// O que existe so depois de o carro morrer: o capo, o que se ve la dentro,
        /// a porta para voltar a entrar e a chave que nao pega.
        ///
        /// Tudo montado ja, mas nada disto se oferece durante a viagem — a porta so
        /// aparece quando o estranho chega, e o capo esta nas costas de um carro que
        /// esta a andar a oitenta. Montar agora e mais simples do que instanciar a
        /// meio do capitulo, e nao custa nada: sao colliders parados.
        /// </summary>
        private static void BuildBreakdown(GameObject car, CarDriver driver, Bounds bounds)
        {
            var doorAudio = car.AddComponent<AudioSource>();
            doorAudio.playOnAwake = false;
            doorAudio.spatialBlend = 1f;
            doorAudio.minDistance = 1.5f;
            doorAudio.maxDistance = 25f;

            var seat = car.GetComponent<CarSeat>() ?? car.AddComponent<CarSeat>();
            var sso = new SerializedObject(seat);
            sso.FindProperty("doorAudio").objectReferenceValue = doorAudio;
            sso.FindProperty("lockClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/Audio/door_lock.mp3");
            sso.FindProperty("unlockClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ThirdParty/Audio/door_unlock.mp3");
            sso.ApplyModifiedPropertiesWithoutUndo();

            // A chave. Sem clips por agora: nao ha som de motor de arranque no
            // projecto, e a alternativa era inventar um com o que ha, que soaria pior
            // do que o silencio.
            var ignition = car.AddComponent<CarIgnition>();
            var iso = new SerializedObject(ignition);
            iso.FindProperty("car").objectReferenceValue = driver;
            iso.FindProperty("ignitionAudio").objectReferenceValue = doorAudio;
            iso.ApplyModifiedPropertiesWithoutUndo();

            BuildDoor(car.transform, bounds, seat, ignition);
            BuildEngineBay(car.transform, bounds);
        }

        /// <summary>A porta do condutor, do lado de fora.</summary>
        private static void BuildDoor(Transform car, Bounds bounds, CarSeat seat, CarIgnition ignition)
        {
            var go = new GameObject("DOOR_Driver");
            go.transform.SetParent(car, false);
            go.transform.position = bounds.center
                                  + car.right * -(bounds.extents.x + 0.15f)
                                  + Vector3.up * (bounds.min.y + 1f - bounds.center.y);
            go.transform.rotation = car.rotation;

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.5f, 1.1f, 1.9f);

            var door = go.AddComponent<CarDoorInteractable>();
            var so = new SerializedObject(door);
            so.FindProperty("seat").objectReferenceValue = seat;
            so.FindProperty("ignition").objectReferenceValue = ignition;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// O capo e as tres coisas la dentro. Duas estao bem; a terceira e um cabo
        /// fora do sitio, e e o que vira o capitulo (§8.13).
        /// </summary>
        private static void BuildEngineBay(Transform car, Bounds bounds)
        {
            var hood = new GameObject("HOOD");
            hood.transform.SetParent(car, false);
            hood.transform.position = bounds.center
                                    + car.forward * (bounds.extents.z * 0.72f)
                                    + Vector3.up * (bounds.min.y + 1f - bounds.center.y);
            hood.transform.rotation = car.rotation;

            var hoodBox = hood.AddComponent<BoxCollider>();
            hoodBox.size = new Vector3(1.6f, 0.7f, 1f);

            var parts = new GameObject("ENGINE_PARTS");
            parts.transform.SetParent(hood.transform, false);

            var coolant = EnginePart(parts.transform, "PART_Coolant", new Vector3(-0.45f, 0.05f, 0.1f),
                "Coolant", "THT_Engine_Coolant", new[]
                {
                    "Coolant is where it should be. Nearly full.",
                    "Not this, then."
                });

            var belt = EnginePart(parts.transform, "PART_Belt", new Vector3(0.45f, 0.05f, 0.1f),
                "Belt", "THT_Engine_Belt", new[]
                {
                    "Belt is tight. No shine on it, no cracks.",
                    "Fine. That is fine too."
                });

            // A culpada. Banal de proposito: nao ha nada cortado nem partido, so um
            // cabo solto — e a ponta limpa, que e o unico detalhe que nao encaixa.
            var cable = EnginePart(parts.transform, "PART_Cable", new Vector3(0f, 0.08f, -0.2f),
                "Loose cable", "THT_Engine_Cable", new[]
                {
                    "A lead is off its post.",
                    "The end of it is clean. No burn, no green, nothing.",
                    "It has not been off long."
                });

            var inspection = hood.AddComponent<EngineInspection>();
            var so = new SerializedObject(inspection);
            so.FindProperty("parts").objectReferenceValue = parts;
            so.FindProperty("culprit").objectReferenceValue = cable;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (coolant == null || belt == null)
                Debug.LogWarning("[Estrada] Faltou uma peca do motor.");
        }

        private static FlavourInteractable EnginePart(Transform parent, string name,
            Vector3 localOffset, string prompt, string assetName, string[] lines)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localOffset;

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.45f, 0.3f, 0.45f);

            string path = "Assets/Pungent/Dialogue/Thoughts/" + assetName + ".asset";
            var set = AssetDatabase.LoadAssetAtPath<ThoughtLineSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<ThoughtLineSet>();
                AssetDatabase.CreateAsset(set, path);
            }
            set.EditorPopulate(lines, ThoughtLineSet.Order.SequentialLastRepeats, 3.2f);
            EditorUtility.SetDirty(set);

            var flavour = go.AddComponent<FlavourInteractable>();
            var so = new SerializedObject(flavour);
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("thoughtLines").objectReferenceValue = set;
            so.ApplyModifiedPropertiesWithoutUndo();
            return flavour;
        }

        /// <summary>
        /// A sensacao: o corpo que inclina e o motor que se ouve. O rig visual
        /// separado da fisica e o padrao dos controladores arcade.
        /// </summary>
        private static void BuildBodyFeel(GameObject car, CarDriver driver, Bounds bounds)
        {
            var tilt = car.AddComponent<CarBodyTilt>();
            var so = new SerializedObject(tilt);
            so.FindProperty("body").objectReferenceValue = car.transform.Find("SEDAN");
            so.FindProperty("car").objectReferenceValue = driver;
            so.ApplyModifiedPropertiesWithoutUndo();

            var engine = new GameObject("ENGINE");
            engine.transform.SetParent(car.transform, false);
            engine.transform.position = bounds.center
                                      + car.transform.forward * (bounds.size.z * 0.5f - 0.4f)
                                      + Vector3.up * (bounds.min.y + 0.55f - bounds.center.y);
            engine.AddComponent<AudioSource>();
            var audio = engine.AddComponent<CarEngineAudio>();
            var audioSo = new SerializedObject(audio);
            audioSo.FindProperty("car").objectReferenceValue = driver;
            audioSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// O brilho dos instrumentos: uma luz quente, fraca, ao pe do tablier. Nao
        /// e para se ver — e para as maos e o volante nao serem uma silhueta contra
        /// os farois. Fica acesa com o motor morto, como o radio: e da bateria.
        /// </summary>
        private static void BuildCabinLight(Transform car, Bounds bounds)
        {
            var go = new GameObject("CABIN_LIGHT");
            go.transform.SetParent(car, false);
            go.transform.position = bounds.center
                                  + car.forward * (bounds.extents.z * 0.28f)
                                  + Vector3.up * (bounds.min.y + 0.76f - bounds.center.y);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            // Curta e fraca de proposito, e mais baixa que o tablier: sem sombras,
            // uma luz maior atravessava o chao do carro e pintava o alcatrao de
            // ambar (neon de tuning), e mais forte lavava o habitaculo inteiro.
            light.range = 1.0f;
            light.intensity = 0.22f;
            light.color = new Color(1f, 0.62f, 0.32f);   // ambar de instrumentos velhos
            light.shadows = LightShadows.None;
        }

        /// <summary>
        /// Liga o <see cref="CarWheelVisuals"/> aos pivots do modelo. Os nomes sao
        /// os do prefab do SEDAN; se um dia o carro trocar, isto avisa em vez de
        /// deixar as rodas rigidas sem ninguem reparar.
        /// </summary>
        private static void BuildWheelVisuals(GameObject car, CarDriver driver)
        {
            var steering = new[] { Find(car, "FL_PARENT"), Find(car, "FR_PARENT") };
            var rolling = new[]
            {
                Find(car, "FL_WHEEL"), Find(car, "FR_WHEEL"),
                Find(car, "RL_WHEEL"), Find(car, "RR_WHEEL")
            };

            var visuals = car.AddComponent<CarWheelVisuals>();
            var so = new SerializedObject(visuals);
            so.FindProperty("car").objectReferenceValue = driver;
            Fill(so.FindProperty("steering"), steering);
            Fill(so.FindProperty("rolling"), rolling);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// A vida da berma: relva pelo <see cref="RoadsideScatter"/> (por blocos —
        /// dez quilometros nao cabem na cena) e os postes de luz, com as lampadas
        /// entregues ao <see cref="PoleLights"/> para so os proximos arderem.
        /// </summary>
        private static void BuildRoadside(Transform parent, RoadPath path, CarDriver car)
        {
            FixTexture(GrassTexFolder + "T_Grass_Base_D.png", false);
            FixTexture(GrassTexFolder + "T_Grass_Base_N.png", true);
            FixTexture(GrassTexFolder + "T_Grass_Reeds_D.png", false);
            FixTexture(GrassTexFolder + "T_Grass_Reeds_N.png", true);

            var baseMat = EnsureGrassMaterial("Assets/Pungent/Materials/M_Grass_Roadside.mat",
                GrassTexFolder + "T_Grass_Base_D.png", GrassTexFolder + "T_Grass_Base_N.png");
            var reedMat = EnsureGrassMaterial("Assets/Pungent/Materials/M_Grass_Reeds.mat",
                GrassTexFolder + "T_Grass_Reeds_D.png", GrassTexFolder + "T_Grass_Reeds_N.png");

            var dry = new[]
            {
                GrassPrefab("SM_Grass_Dry01", baseMat), GrassPrefab("SM_Grass_Dry02", baseMat)
            };
            var verge = new[]
            {
                GrassPrefab("SM_Grass01", baseMat), GrassPrefab("SM_Grass02", baseMat),
                GrassPrefab("SM_Grass03", baseMat), GrassPrefab("SM_Grass_Flowers01", baseMat),
                GrassPrefab("SM_Grass_Flowers02", baseMat), GrassPrefab("SM_Grass_Flowers03", baseMat)
            };
            var reeds = new[] { GrassPrefab("SM_Grass_Reeds01", reedMat) };

            int ground = LayerMask.NameToLayer("Ground");
            var scatter = parent.gameObject.AddComponent<RoadsideScatter>();
            var so = new SerializedObject(scatter);
            so.FindProperty("path").objectReferenceValue = path;
            so.FindProperty("focus").objectReferenceValue = car.transform;
            Fill(so.FindProperty("nearRoad"), dry);
            Fill(so.FindProperty("verge"), verge);
            Fill(so.FindProperty("ditch"), reeds);
            if (ground >= 0) so.FindProperty("groundMask").intValue = 1 << ground;
            so.ApplyModifiedPropertiesWithoutUndo();

            BuildPoles(parent, path, car, ground);
        }

        private static void BuildPoles(Transform parent, RoadPath path, CarDriver car, int ground)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PolePrefab);
            if (asset == null)
            {
                Debug.LogWarning("[Estrada] Sem poste em " + PolePrefab + "; a estrada fica sem iluminacao publica.");
                return;
            }

            var root = new GameObject("POLES");
            root.transform.SetParent(parent, false);

            var lamps = new List<Light>();
            int mask = ground >= 0 ? 1 << ground : ~0;

            // De 65 em 65 m e nao de 170: com o nevoeiro a comer aos 250, e o que
            // poe tres ou quatro pocas de luz a perder-se na noite a frente do
            // carro — um poste sozinho de cada vez lia-se como estrada abandonada.
            for (float d = 60f; d < path.Length; d += 65f)
            {
                Vector3 forward = path.ForwardAt(d);
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                // Um de cada lado, frente a frente, cada um com o braco por cima
                // da sua faixa — e o que faz o corredor de luz em vez da fila coxa.
                for (int side = -1; side <= 1; side += 2)
                    PlacePole(root.transform, asset, path, d, right, side, mask, lamps);
            }

            var keeper = parent.gameObject.AddComponent<PoleLights>();
            keeper.EditorSetLamps(lamps.ToArray(), car.transform);
            Debug.Log($"[Estrada] {lamps.Count} postes na berma (dois a dois, frente a frente), " +
                      "lampadas ao cuidado do PoleLights.");
        }

        private static void PlacePole(Transform root, GameObject asset, RoadPath path,
            float d, Vector3 right, int side, int mask, List<Light> lamps)
        {
            // Colados ao limite do alcatrao (meia estrada sao 5 m): mais para fora
            // e a fila de arvores, e o talude sobe — um poste la em cima ficava
            // escondido na copa e a lampada a oito metros do chao.
            Vector3 probe = path.PointAt(d) + right * (side * 5.6f) + Vector3.up * 3f;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 10f, mask,
                    QueryTriggerInteraction.Ignore)) return;

            var pole = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            pole.name = $"POLE_{(side < 0 ? 'L' : 'R')}_{(int)d:D5}";
            pole.transform.SetParent(root, false);

            // O braco da lampada vira-se para o alcatrao, como deve ser. No modelo
            // o braco estende contra o +Z local — verificado a olho: de costas para
            // a estrada, a poca de luz caia na relva. Cada lado olha para fora para
            // o braco cair por cima da sua faixa.
            pole.transform.SetPositionAndRotation(hit.point,
                Quaternion.LookRotation(right * side, Vector3.up));

            // O modelo mede 3,95 m — escala de rua pedonal. Um candeeiro de estrada
            // nacional anda pelos 7 a 8 m, e a altura e o que espalha a poca em vez
            // de a concentrar num circulo aos pes do poste.
            pole.transform.localScale = Vector3.one * 1.9f;

            // O prefab e so geometria: o carro passava por dentro dos postes como se
            // nao existissem, e nada tira mais o peso a uma estrada do que atravessar
            // o que esta na berma. Uma capsula fina no mastro chega — bater no braco
            // da lampada, a sete metros de altura, nao acontece a ninguem.
            var trunk = new GameObject("POLE_Collider");
            trunk.transform.SetParent(pole.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 1.9f, 0f);
            var capsule = trunk.AddComponent<CapsuleCollider>();
            capsule.radius = 0.11f;
            capsule.height = 3.8f;
            capsule.direction = 1;   // ao longo do Y local

            // O modelo tem tres cabecas, cada uma com o seu spot. Uma luz real por
            // poste chega; as outras cabecas ficam so com a geometria emissiva. O
            // objecto inteiro, e nao so o componente: a luz URP traz um
            // UniversalAdditionalLightData agarrado que recusa ficar orfao.
            var all = pole.GetComponentsInChildren<Light>(true);
            if (all.Length == 0) return;
            for (int i = 1; i < all.Length; i++) Object.DestroyImmediate(all[i].gameObject);

            // O pack traz a luz com 5 m de alcance, 25 de forca e cone fechado —
            // num poste nem chegava ao chao, quanto mais desenhar poca. Com a
            // lampada aos 7,5 m, alcance 34 e cone interior 25: e a queda suave
            // entre 25 e 110 graus que desfaz o circulo duro. Sem sombras: trezentas
            // sombras dinamicas era o fim da GPU.
            var lamp = all[0];
            lamp.range = 34f;
            lamp.intensity = 40f;
            lamp.spotAngle = 110f;
            lamp.innerSpotAngle = 25f;
            lamp.shadows = LightShadows.None;
            lamp.enabled = false;   // o PoleLights acende os que estao perto
            lamps.Add(lamp);
        }

        /// <summary>
        /// Fecha a estrada.
        ///
        /// O alcatrao do pack tem 10 km e o percurso usa 5,7: sem isto o carro
        /// seguia para os quilometros orfaos, sem postes nem relva. Invisivel e nao
        /// uma cancela: uma barreira fisica no meio de uma estrada nacional lia-se
        /// como erro, e o nevoeiro ja faz o resto do trabalho.
        /// </summary>
        private static void BuildRoadEnd(Transform parent, RoadPath path)
        {
            var stop = new GameObject("ROAD_END_STOP");
            stop.transform.SetParent(parent, false);

            float d = Mathf.Max(0f, path.Length - 2f);
            stop.transform.SetPositionAndRotation(path.PointAt(d) + Vector3.up * 2f,
                Quaternion.LookRotation(path.ForwardAt(d), Vector3.up));

            var barrier = stop.AddComponent<BoxCollider>();
            barrier.size = new Vector3(24f, 6f, 0.5f);
        }

        /// <summary>
        /// A casa do vendedor: garagem aberta com luz quente, ele a espera la dentro,
        /// as pecas no chao.
        ///
        /// **Nao e chamada nesta estrada, e de proposito.** A §5 diz que esta viagem
        /// e "a estrada de regresso": o Tomas ja tem as pecas e vem para casa. O
        /// vendedor esta na zona industrial do Dia 4 (§10.3), que e a
        /// `Garage_Blockout` — po-lo tambem no fim desta estrada fazia a viagem ler
        /// como a ida, e a ida, segundo a §5, e "pequena conducao urbana ou
        /// transicao controlada", nao estes 5,7 km.
        ///
        /// Fica aqui porque o que ela monta — a boca da garagem acesa no escuro, as
        /// pecas no chao — e exactamente o que falta a oficina do Dia 4, que hoje so
        /// tem caixas e dois pontos de interaccao.
        /// </summary>
        private static void BuildSellerHouse(Transform parent, RoadPath path)
        {
            float d = Mathf.Max(0f, path.Length - 18f);
            Vector3 centre = path.PointAt(d);
            Vector3 forward = path.ForwardAt(d);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            var root = new GameObject("SELLER_HOUSE");
            root.transform.SetParent(parent, false);

            // O +Z local e a parede do fundo; a olhar PARA FORA da estrada, a boca
            // aberta da garagem fica virada ao alcatrao — que e o que se ve a
            // chegar: um rectangulo quente aceso no escuro, com ele la dentro.
            root.transform.SetPositionAndRotation(centre + right * 13f,
                Quaternion.LookRotation(right, Vector3.up));

            var concrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pungent/Materials/M_Garage_Concrete.mat");
            var wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pungent/Materials/M_Garage_Wall.mat");

            // A laje, e a garagem aberta para a estrada: tres paredes e tecto.
            Box(root.transform, "Slab", new Vector3(0f, 0.08f, 0f), new Vector3(9f, 0.16f, 8f), concrete);
            Box(root.transform, "Wall_Back", new Vector3(0f, 1.6f, 3.9f), new Vector3(9f, 3.2f, 0.2f), wall);
            Box(root.transform, "Wall_Left", new Vector3(-4.4f, 1.6f, 0f), new Vector3(0.2f, 3.2f, 8f), wall);
            Box(root.transform, "Wall_Right", new Vector3(4.4f, 1.6f, 0f), new Vector3(0.2f, 3.2f, 8f), wall);
            Box(root.transform, "Roof", new Vector3(0f, 3.3f, 0f), new Vector3(9.4f, 0.2f, 8.4f), wall);

            // A casa agarrada a garagem: só volume — ninguem entra nela neste passo.
            Box(root.transform, "House", new Vector3(-7.5f, 2.1f, 1f), new Vector3(6f, 4.2f, 6f), wall);
            Box(root.transform, "House_Roof", new Vector3(-7.5f, 4.35f, 1f), new Vector3(6.4f, 0.3f, 6.4f), concrete);

            // A luz quente da garagem: acesa a espera. Ve-se do fundo da estrada, e
            // e para isso que serve — e o unico sitio aceso que nao e um candeeiro.
            var glow = new GameObject("GarageGlow");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 2.6f, 0.5f);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 14f;
            light.intensity = 2.6f;
            light.color = new Color(1f, 0.78f, 0.5f);
            light.shadows = LightShadows.None;

            // Ele, a espera. Uma capsula, como o walker: quem e vem no capitulo.
            var guy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            guy.name = "SELLER";
            guy.transform.SetParent(root.transform, false);
            guy.transform.localPosition = new Vector3(0.8f, 0.9f, -2.6f);
            guy.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
            Object.DestroyImmediate(guy.GetComponent<Collider>());

            // As pecas no chao: pneus e um barril do pack da oficina.
            PlaceProp(root.transform, "Assets/MarioParadiso/Built-In/Prefabs/Tire.prefab",
                new Vector3(-1.6f, 0.16f, -1.2f), 15f);
            PlaceProp(root.transform, "Assets/MarioParadiso/Built-In/Prefabs/Tire.prefab",
                new Vector3(-2.1f, 0.16f, -0.4f), 80f);
            PlaceProp(root.transform, "Assets/MarioParadiso/Built-In/Prefabs/Tire.prefab",
                new Vector3(1.9f, 0.16f, 0.6f), 140f);
            PlaceProp(root.transform, "Assets/MarioParadiso/Built-In/Prefabs/Barell.prefab",
                new Vector3(3.1f, 0.16f, 2.2f), 0f);
            PlaceProp(root.transform, "Assets/MarioParadiso/Built-In/Prefabs/Cart.prefab",
                new Vector3(-3.2f, 0.16f, 1.6f), 200f);

            // A estrada acaba aqui, mas o alcatrao do pack continua — uma parede
            // invisivel um pouco depois da casa impede o carro de seguir para os
            // quilometros orfaos. Invisivel e nao cancela: uma barreira fisica no
            // meio de uma estrada publica lia-se como erro, e o nevoeiro ja faz o
            // resto do trabalho.
            var stop = new GameObject("ROAD_END_STOP");
            stop.transform.SetParent(parent, false);
            Vector3 stopPos = path.PointAt(Mathf.Max(0f, path.Length - 2f)) + Vector3.up * 2f;
            stop.transform.SetPositionAndRotation(stopPos, Quaternion.LookRotation(forward, Vector3.up));
            var barrier = stop.AddComponent<BoxCollider>();
            barrier.size = new Vector3(24f, 6f, 0.5f);
        }

        private static void Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPos;
            box.transform.localScale = size;
            if (material != null) box.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void PlaceProp(Transform parent, string prefabPath, Vector3 localPos, float yaw)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null)
            {
                Debug.LogWarning("[Estrada] Sem prop em " + prefabPath);
                return;
            }
            var prop = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = localPos;
            prop.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        /// <summary>Um prefab de relva: a malha do pack com o material URP em cima.</summary>
        private static GameObject GrassPrefab(string meshName, Material material)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(GrassMeshFolder + meshName + ".fbx");
            if (model == null)
            {
                Debug.LogWarning("[Estrada] Sem malha de relva '" + meshName + "'.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(GrassPrefabFolder))
                AssetDatabase.CreateFolder("Assets/Pungent/Prefabs", "Grass");

            var temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(temp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var renderer in temp.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;

                // Relva nao projecta sombra: de noite ninguem da pela falta, e sao
                // centenas de manchas dentro do alcance dos farois.
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, GrassPrefabFolder + "/" + meshName + ".prefab");
            Object.DestroyImmediate(temp);
            return prefab;
        }

        private static Material EnsureGrassMaterial(string assetPath, string diffusePath, string normalPath)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath));
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            material.SetTexture("_BumpMap", normal);
            if (normal != null) material.EnableKeyword("_NORMALMAP");
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_AlphaClip", 1f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cutoff", 0.45f);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_Cull", 0f);   // folhas de dupla face
            material.renderQueue = 2450;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void FixTexture(string path, bool isNormal)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (isNormal && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                dirty = true;
            }
            if (!isNormal && !importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }
            if (dirty) importer.SaveAndReimport();
        }

        private static Transform Find(GameObject car, string name)
        {
            foreach (var t in car.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;

            Debug.LogWarning($"[Estrada] O carro nao tem '{name}'; essa roda fica rigida.");
            return null;
        }

        private static void Fill(SerializedProperty array, Object[] values)
        {
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void BuildRadio(Transform car, CarDriver driver, Bounds bounds)
        {
            var go = new GameObject("RADIO");
            go.transform.SetParent(car, false);

            // No tablier, e nao no pivot do carro: e dali que as colunas tocam. De
            // tras do volante a musica vem da frente, do banco de tras vem de mais
            // longe, e de fora do carro morto vem de dentro dele — a direccao conta
            // a mesma historia nos tres sitios.
            go.transform.position = bounds.center
                                  + car.forward * (bounds.extents.z * 0.35f)
                                  + Vector3.up * (bounds.min.y + 0.72f - bounds.center.y);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 0.55f;

            // 3D, e nao 2D: sentado, a cabeca dele esta dentro do `minDistance` e a
            // musica vem de todo o lado na mesma; a diferenca aparece quando ele sai
            // do carro morto e o radio fica atras dele, a tocar de um sitio. E essa
            // a imersao toda do fim do capitulo — em 2D a musica seguia-o pela
            // estrada fora como banda sonora, e o carro deixava de ser um sitio.
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.2f;  // o habitaculo a volume cheio
            source.maxDistance = 20f;   // ao motor ainda se ouve; a vinte metros a noite engole-o
            source.dopplerLevel = 0f;   // radio nao faz doppler com a cabeca de quem conduz

            // O que faz isto soar a radio de carro e nao a banda sonora: sem
            // sub-graves que aquelas colunas nunca dariam, sem brilho digital, e um
            // resto de vibracao de plastico nos picos.
            go.AddComponent<AudioHighPassFilter>().cutoffFrequency = 150f;
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = 7500f;
            go.AddComponent<AudioDistortionFilter>().distortionLevel = 0.05f;

            var radio = go.AddComponent<CarRadio>();
            radio.EditorSetTracks(
                new[]
                {
                    AssetDatabase.LoadAssetAtPath<AudioClip>(Track1),
                    AssetDatabase.LoadAssetAtPath<AudioClip>(Track2)
                },
                AssetDatabase.LoadAssetAtPath<AudioClip>(Track3));

            var so = new SerializedObject(radio);
            so.FindProperty("car").objectReferenceValue = driver;
            so.ApplyModifiedPropertiesWithoutUndo();

            // O botao: apontar ao tablier e calar a musica. Quem quiser conduzir os
            // quatro minutos em silencio tem o direito.
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.34f, 0.16f, 0.14f);

            var toggle = go.AddComponent<RadioToggle>();
            var tso = new SerializedObject(toggle);
            tso.FindProperty("source").objectReferenceValue = source;
            tso.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Onde o Tomas se senta.
        ///
        /// Posicionado em metros do mundo e so depois convertido para local: com a
        /// escala 0,6 do prefab, escrever offsets locais a olho punha os olhos dele
        /// a trinta centimetros do chao do carro.
        /// </summary>
        private static void BuildSeat(Transform car, Bounds bounds)
        {
            var seat = new GameObject("PLAYER_SEAT");
            seat.transform.SetParent(car, false);

            // Desfazer a escala do carro. Um filho de um objecto a 0,62 nasce a
            // 0,62, e isso incluia o jogador inteiro: a camara dele descia com o
            // resto e o mundo ficava um terco maior visto de dentro do carro.
            Vector3 scale = car.lossyScale;
            seat.transform.localScale = new Vector3(
                1f / Mathf.Max(0.0001f, scale.x),
                1f / Mathf.Max(0.0001f, scale.y),
                1f / Mathf.Max(0.0001f, scale.z));

            // Volante a esquerda, ligeiramente a frente do centro. Isto marca a
            // altura dos OLHOS — e nao os pes; quem senta o jogador trata da
            // diferenca. 1,24 m acima da base do carro: o tejadilho do SEDAN esta a
            // 1,47 m e o tablier do modelo e alto — a 1,05 m via-se o volante e
            // pouco alcatrao.
            seat.transform.position = bounds.center
                                    + car.right * -0.34f
                                    + car.forward * 0.10f
                                    + Vector3.up * (bounds.min.y + 1.24f - bounds.center.y);
            seat.transform.rotation = car.rotation;

            // Onde ele poe os pes ao sair: junto a porta do condutor, no alcatrao.
            // Filho do carro, porque o carro morre onde a musica mandar e o sitio
            // de sair tem de ir com ele.
            var exit = new GameObject("EXIT_POINT");
            exit.transform.SetParent(car, false);
            exit.transform.localScale = seat.transform.localScale;
            exit.transform.position = bounds.center
                                    + car.right * -(bounds.extents.x + 0.75f)
                                    + Vector3.up * (bounds.min.y + 0.05f - bounds.center.y);
            exit.transform.rotation = car.rotation;

            var carSeat = car.gameObject.AddComponent<CarSeat>();
            var so = new SerializedObject(carSeat);
            so.FindProperty("seat").objectReferenceValue = seat.transform;
            so.FindProperty("exitPoint").objectReferenceValue = exit.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var feel = car.gameObject.AddComponent<CarCameraFeel>();
            var feelSo = new SerializedObject(feel);
            feelSo.FindProperty("car").objectReferenceValue = car.GetComponent<CarDriver>();
            feelSo.FindProperty("seat").objectReferenceValue = carSeat;
            feelSo.ApplyModifiedPropertiesWithoutUndo();

            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0)
            {
                var driverSo = new SerializedObject(car.GetComponent<CarDriver>());
                driverSo.FindProperty("groundMask").intValue = 1 << ground;
                driverSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Os farois. Tambem em metros do mundo, pela mesma razao do banco: a escala
        /// do prefab comia qualquer offset local escrito a olho.
        /// </summary>
        private static void BuildHeadlights(Transform car, Bounds bounds)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var go = new GameObject(side < 0 ? "Headlight_L" : "Headlight_R");
                go.transform.SetParent(car, false);

                go.transform.position = bounds.center
                                      + car.right * (side * 0.62f)
                                      + car.forward * (bounds.size.z * 0.5f - 0.1f)
                                      + Vector3.up * (bounds.min.y + 0.62f - bounds.center.y);
                go.transform.rotation = car.rotation * Quaternion.Euler(3f, 0f, 0f);

                var light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.range = 80f;
                light.spotAngle = 62f;
                light.innerSpotAngle = 30f;
                // 35 e nao um numero timido: no URP a atenuacao por distancia come
                // o spot, e a 15 m de alcatrao um farol a 12 ja nao desenhava poca
                // nenhuma. A 35 a estrada le-se ate a curva.
                light.intensity = 35f;
                light.color = new Color(1f, 0.96f, 0.88f);
                light.shadows = LightShadows.Soft;
            }
        }

        /// <summary>
        /// O carro que nao larga.
        ///
        /// Era so um par de luzes soltas, com a ideia de que nao se devia saber o
        /// que era. Mas ele acaba por encostar e o estranho tem de sair de algum
        /// lado — um homem a materializar-se ao pe de duas lampadas a flutuar dava
        /// pior do que mostrar o carro. Fica o hatchback, o outro carro da pasta:
        /// a distancia e de noite, contraluz dos proprios farois, o que se ve e uma
        /// silhueta e nada mais.
        /// </summary>
        private static Transform BuildFollower(Transform parent)
        {
            var go = new GameObject("FOLLOWER_Car");
            go.transform.SetParent(parent, false);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(FollowerCarPrefab);
            if (asset != null)
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                body.name = "FOLLOWER_Body";
                body.transform.SetParent(go.transform, false);

                // So geometria: este carro nunca e conduzido e um collider a mexer
                // atras do jogador so podia dar encontroes que ninguem pediu.
                foreach (var mesh in body.GetComponentsInChildren<MeshCollider>(true))
                    Object.DestroyImmediate(mesh);

                // Assentar no alcatrao, como o do jogador: o pivot nao esta debaixo
                // das rodas.
                var rends = body.GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    body.transform.position -= new Vector3(0f, b.min.y - go.transform.position.y, 0f);
                }
            }
            else Debug.LogWarning("[Estrada] Sem o carro do estranho em " + FollowerCarPrefab);

            for (int side = -1; side <= 1; side += 2)
            {
                var lamp = new GameObject(side < 0 ? "Lamp_L" : "Lamp_R");
                lamp.transform.SetParent(go.transform, false);
                lamp.transform.localPosition = new Vector3(side * 0.62f, 0f, 0f);

                var light = lamp.AddComponent<Light>();
                light.type = LightType.Spot;
                light.range = 55f;
                light.spotAngle = 52f;
                light.intensity = 3.4f;
                light.color = new Color(0.94f, 0.95f, 1f);
            }
            return go.transform;
        }

        /// <summary>
        /// O estranho que vem a pe pela estrada acima.
        ///
        /// Uma pessoa do mesmo pack do Rui, mas outra cara — sao vizinhos de pack e
        /// nao podem ser irmaos gemeos. Ainda nao tem animacao: desliza. Nesta
        /// distancia e neste escuro, contraluz dos farois do proprio carro, o que se
        /// ve e uma silhueta a aproximar-se, e isso ja e infinitamente mais do que a
        /// capsula que aqui estava.
        /// </summary>
        private static Transform BuildWalker(Transform parent)
        {
            var go = new GameObject("WALKER");
            go.transform.SetParent(parent, false);

            var body = PsxCharacter.Place(go.transform, PsxCharacter.Stranger, "Stranger_Body");
            if (body == null)
            {
                // Sem o pack, uma capsula. Melhor isso do que um capitulo sem ninguem.
                var fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fallback.name = "Stranger_Fallback";
                fallback.transform.SetParent(go.transform, false);
                fallback.transform.localPosition = Vector3.up * 0.9f;
                fallback.transform.localScale = new Vector3(0.55f, 0.9f, 0.55f);
                Object.DestroyImmediate(fallback.GetComponent<Collider>());
            }

            go.AddComponent<StrangerEncounter>();
            return go.transform;
        }

        private static void BuildNight(Transform parent)
        {
            var go = new GameObject("MOONLIGHT");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(42f, 205f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.12f;
            light.color = new Color(0.62f, 0.70f, 0.92f);
            light.shadows = LightShadows.Soft;

            var sky = AssetDatabase.LoadAssetAtPath<Material>(NightSkybox);
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                RenderSettings.sun = light;
            }
            else Debug.LogWarning("[Estrada] Sem skybox em " + NightSkybox + "; o ceu fica na cor da camara.");

            // O ambiente continua Flat e escuro apesar do skybox: tirar ambiente do
            // ceu de meia-noite dava um azul uniforme que lavava os farois.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.05f, 0.06f, 0.09f);

            // O nevoeiro nao e atmosfera: e o que faz a estrada acabar no escuro em
            // vez de se ver dez quilometros de alcatrao vazio a frente.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.014f;
            RenderSettings.fogColor = new Color(0.03f, 0.035f, 0.05f);
        }

        private static void WireDirector(Transform parent, RoadPath path, CarDriver car,
            Transform follower, Transform walker)
        {
            var go = new GameObject("ROAD_DIRECTOR");
            go.transform.SetParent(parent, false);

            var director = go.AddComponent<NightRoadDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("path").objectReferenceValue = path;
            so.FindProperty("car").objectReferenceValue = car;
            so.FindProperty("radio").objectReferenceValue = car.GetComponentInChildren<CarRadio>(true);
            so.FindProperty("seat").objectReferenceValue = car.GetComponent<CarSeat>();
            so.FindProperty("follower").objectReferenceValue = follower;
            so.FindProperty("walker").objectReferenceValue = walker;
            so.FindProperty("director").objectReferenceValue = Object.FindObjectOfType<ChapterDirector>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// O `ScenePlayerSetup` avisa quando nao ha `PLAYER_SPAWN`. Aqui ha sitio
        /// certo — o banco do condutor — so que ainda nao ha carro quando aquela
        /// ferramenta e generica. Marcar o sitio evita o aviso e deixa a cena
        /// coerente para quem a abrir a mao.
        /// </summary>
        private static void EnsureSpawnAtSeat(CarDriver car)
        {
            var seat = car.transform.Find("PLAYER_SEAT");
            if (seat == null) return;

            var existing = GameObject.Find("PLAYER_SPAWN");
            if (existing != null) Object.DestroyImmediate(existing);

            var spawn = new GameObject("PLAYER_SPAWN");
            spawn.transform.SetPositionAndRotation(seat.position, seat.rotation);
        }

        /// <summary>
        /// Senta o jogador ao volante.
        ///
        /// O `PlayerMotor` e o `CharacterController` ficam desligados: um corpo com
        /// controlador proprio agarrado a um carro a mexer briga com ele e sai a
        /// tremer. Desligados, o jogador e so um filho do carro — mas continua a
        /// poder olhar a volta, que e o que faz o retrovisor valer alguma coisa.
        ///
        /// O input **nao** e suprimido: e o mesmo `Move` que dava passos que passa a
        /// dar acelerador e volante. Suprimi-lo punha o `Move` a zero e deixava o
        /// carro sem quem o guiasse.
        /// </summary>
        private static void SeatPlayer(CarDriver car)
        {
            var player = GameObject.Find("PlayerRoot");
            if (player == null)
            {
                Debug.LogWarning("[Estrada] Sem jogador na cena; o carro fica sem condutor.");
                return;
            }

            var seat = car.transform.Find("PLAYER_SEAT");
            if (seat == null) { Debug.LogWarning("[Estrada] Sem PLAYER_SEAT no carro."); return; }

            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;

            var motor = player.GetComponent<Pungent.Player.PlayerMotor>();
            if (motor != null) motor.enabled = false;

            player.transform.SetParent(seat, false);
            player.transform.localPosition = Vector3.zero;
            player.transform.localRotation = Quaternion.identity;

            var camera = player.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                // O banco marca a altura dos olhos, mas quem la foi parar foi a base
                // do jogador — e a camara do prefab esta 1,65 m acima dela, que e
                // altura de quem esta de pe. Sem descontar isso, o Tomas conduzia
                // com a cabeca fora do tejadilho.
                float above = camera.transform.position.y - player.transform.position.y;
                player.transform.localPosition = new Vector3(0f, -above, 0f);

                // Dez quilometros de estrada nao cabem no plano de corte de uma sala.
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 600f);
            }

            var driverField = new SerializedObject(car);
            var inputProperty = driverField.FindProperty("input");
            inputProperty.objectReferenceValue = player.GetComponent<Pungent.Player.PlayerInputReader>();
            driverField.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 2f);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
