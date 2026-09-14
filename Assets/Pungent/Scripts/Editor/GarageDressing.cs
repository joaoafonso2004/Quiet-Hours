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
    /// O que ha para ver na oficina, e a razao de o Dia 4 existir.
    ///
    /// A §5 diz que este capitulo "converte suspeita domestica em perigo concreto".
    /// Ate aqui o Rui era um colega estranho; a partir daqui ha uma prova. Sem este
    /// momento, o Dia 5 nao se pode escrever — o climax inteiro assenta em o jogador
    /// ja saber, e "saber" tem de acontecer nalgum sitio.
    ///
    /// A prova sao tres coisas por ordem de peso, e nenhuma delas e dita por
    /// ninguem:
    ///
    /// 1. **Os numeros raspados.** Diz que as pecas sao roubadas. Mau, mas nao e
    ///    pessoal — comprar pecas roubadas e o risco que ele aceitou correr.
    /// 2. **A lanterna dele.** Esta ali no meio das outras, com fita azul no cabo
    ///    porque foi ele que lha pos. Estava na caixa das ferramentas que ele
    ///    carregou para casa no prologo, com as proprias maos, a frente do jogador.
    /// 3. **O silencio a seguir.** Ele nao diz nada ao Vitor.
    ///
    /// A lanterna nao prova nada sobre o Rui. Prova que alguem entrou no quarto — e
    /// so ha uma pessoa com chave. O jogador faz essa conta sozinho, que e a unica
    /// maneira de ela custar alguma coisa.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class GarageDressing
    {
        private const string Root = "GARAGE_PARTS";
        private const string PropFolder = "Assets/MarioParadiso/Built-In/Prefabs/";
        private const string PartsFolder = "Assets/Junk Car Parts/Prefabs/";
        private const string FlashlightPrefab = "Assets/Flashlight/Model/Flashlight.prefab";
        private const string SedanPrefab = "Assets/Hatchback and Sedan/prefabs/SEDAN.prefab";

        [MenuItem("Pungent/Blockout/Dress Garage (Day 4)", false, 19)]
        internal static void Dress()
        {
            if (BuildGuard.Blocked("DressGarage")) return;

            var scene = EditorSceneManager.GetActiveScene();
            var blockout = GameObject.Find("GARAGE_BLOCKOUT");
            if (blockout == null)
            {
                Debug.LogError("[Garage] Correr 'Rebuild Garage' primeiro.");
                return;
            }

            // Soltar o jogador antes de apagar o que la estava.
            //
            // Ele vai sentado no banco, o banco e filho do carro e o carro vive
            // dentro deste root: apagar o root levava o jogador atras e a cena
            // ficava sem ninguem. Aconteceu — e o aviso "sem jogador na cena" so
            // aparecia **depois**, quando ja nao havia nada a fazer.
            var log = new List<string>();

            var seated = GameObject.Find("PlayerRoot");
            var old = GameObject.Find(Root);
            if (seated != null && old != null && seated.transform.IsChildOf(old.transform))
            {
                seated.transform.SetParent(null, true);
                log.Add("  jogador solto do carro antigo antes de reconstruir");
            }

            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Dress garage");

            Vector3 at = Anchor(blockout, "GARAGE_Parts");

            var tarp = BuildTarp(root.transform, at, log);
            BuildParts(root.transform, at, log);
            BuildSerial(root.transform, at, tarp, log);
            BuildTorch(root.transform, at, tarp, log);
            BuildArrival(root.transform, blockout, log);

            // Depois do carro, porque as pecas carregaveis precisam de saber onde e
            // a bagageira — e a bagageira e filha do carro.
            var pile = root.transform.Find("PART_Pile");
            var boot = Object.FindObjectOfType<BoxDropZone>(true);
            if (pile != null)
                MakeCarryable(pile, new[] { "Radiator", "Wheel", "Wheel" }, boot, log);

            BuildExit(root.transform, log);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Garage] Peças vestidas.\n" + string.Join("\n", log));
        }

        /// <summary>
        /// A lona por cima do monte. E o gesto que abre o capitulo: enquanto ela
        /// estiver la, o que esta por baixo sao "as pecas" e nada mais.
        /// </summary>
        private static FlavourInteractable BuildTarp(Transform parent, Vector3 at, List<string> log)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "PART_Tarp";
            go.transform.SetParent(parent, false);
            go.transform.position = at + new Vector3(0f, 0.62f, 0f);
            go.transform.localScale = new Vector3(2.30f, 0.06f, 1.70f);
            go.transform.rotation = Quaternion.Euler(0f, 8f, 0f);

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = new Color(0.16f, 0.17f, 0.19f);
            material.SetFloat("_Smoothness", 0.18f);
            AssetDatabase.CreateAsset(material, "Assets/Pungent/Materials/M_Garage_Tarp.mat");
            go.GetComponent<Renderer>().sharedMaterial = material;

            var flavour = go.AddComponent<FlavourInteractable>();
            var so = new SerializedObject(flavour);
            so.FindProperty("prompt").stringValue = "Lift the sheet";
            so.FindProperty("thoughtLines").objectReferenceValue = Thoughts("Garage_Tarp", new[]
            {
                "Alternator, discs, a full set of pads. All of it looks right.",
                "Better than right. This is shop stock."
            });
            so.ApplyModifiedPropertiesWithoutUndo();

            log.Add("  lona: tapa as pecas ate alguem a levantar");
            return flavour;
        }

        /// <summary>O monte em si. Nao ha nada para fazer com isto — e cenario.</summary>
        private static void BuildParts(Transform parent, Vector3 at, List<string> log)
        {
            var group = new GameObject("PART_Pile");
            group.transform.SetParent(parent, false);
            group.transform.position = at;

            // Pecas a serio, e as que o SMS do Vitor nomeou: o alternador nao existe
            // no pack, mas o radiador, a correia e as polias leem-se como o mesmo
            // negocio. Um monte de pneus dizia "sucata"; isto diz "stock de oficina",
            // que e o que o pensamento da lona afirma.
            var props = new (string Prefab, Vector3 Offset, float Yaw, float Pitch)[]
            {
                ("Radiator", new Vector3(-0.62f, 0.28f,  0.30f),  14f,  0f),
                ("Wheel",    new Vector3(-0.30f, 0.20f, -0.38f),  74f, 90f),
                ("Wheel",    new Vector3(-0.52f, 0.20f, -0.10f),  30f, 90f),
                ("Muffler",  new Vector3( 0.15f, 0.06f,  0.48f), 108f,  0f),
                ("FanBelt",  new Vector3( 0.05f, 0.02f,  0.05f),  22f,  0f),
                ("Pully",    new Vector3( 0.52f, 0.11f,  0.28f),   0f, 90f),
                ("Piston",   new Vector3( 0.62f, 0.12f, -0.10f), -18f,  0f),
                ("Tube",     new Vector3( 0.30f, 0.05f, -0.42f),  56f,  0f),
                ("FuelTank", new Vector3( 0.95f, 0.11f, -0.55f),  -8f,  0f),
            };

            int placed = 0;
            foreach (var p in props)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PartsFolder + p.Prefab + ".prefab");
                if (asset == null) { Debug.LogWarning("[Garage] Sem peca " + p.Prefab); continue; }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                go.transform.SetParent(group.transform, false);
                go.transform.localPosition = p.Offset;
                go.transform.localRotation = Quaternion.Euler(p.Pitch, p.Yaw, 0f);
                placed++;
            }
            log.Add($"  {placed} pecas no monte");
        }

        /// <summary>
        /// Os numeros raspados. A primeira das tres provas, e a menos pessoal: diz
        /// que as pecas sao roubadas, o que e o risco que ele ja tinha aceitado.
        /// </summary>
        private static void BuildSerial(Transform parent, Vector3 at, FlavourInteractable tarp,
            List<string> log)
        {
            var go = Point(parent, "PART_Serial", at + new Vector3(-0.70f, 0.42f, 0.35f),
                new Vector3(0.5f, 0.4f, 0.5f));

            var flavour = go.AddComponent<FlavourInteractable>();
            var so = new SerializedObject(flavour);
            so.FindProperty("prompt").stringValue = "Look closer";
            so.FindProperty("thoughtLines").objectReferenceValue = Thoughts("Garage_Serial", new[]
            {
                "The serial has been taken off. Filed, not worn.",
                "Every one of them. Same file, same angle.",
                "I knew they were cheap. I did not ask why."
            });
            so.ApplyModifiedPropertiesWithoutUndo();
            log.Add("  numeros raspados: a prova de que sao roubadas");
        }

        /// <summary>
        /// A lanterna dele.
        ///
        /// Esta e a peca que faz o capitulo. Escrita banal de propósito: nao ha
        /// sangue, nao ha bilhete, nao ha o nome do Rui em lado nenhum. Ha uma
        /// lanterna com fita azul, e ele sabe porque e que tem fita azul.
        ///
        /// O jogador carregou aquela caixa de ferramentas para dentro de casa no
        /// prologo, com as proprias maos. E por isso que isto funciona: nao e um
        /// objecto que lhe disseram que era dele.
        /// </summary>
        private static void BuildTorch(Transform parent, Vector3 at, FlavourInteractable tarp,
            List<string> log)
        {
            var go = new GameObject("PART_Torch");
            go.transform.SetParent(parent, false);
            go.transform.position = at + new Vector3(0.34f, 0.06f, 0.08f);
            go.transform.rotation = Quaternion.Euler(0f, 34f, 90f);   // deitada no monte

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightPrefab);
            if (asset != null)
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                body.name = "Torch_Body";
                body.transform.SetParent(go.transform, false);

                // A luz que o prefab traz fica apagada: esta lanterna esta no chao
                // de uma oficina, nao na mao de ninguem.
                foreach (var l in body.GetComponentsInChildren<Light>(true)) l.enabled = false;
            }
            else Debug.LogWarning("[Garage] Sem lanterna em " + FlashlightPrefab);

            var bounds = Bounds(go);
            var pick = go.AddComponent<BoxCollider>();
            pick.center = go.transform.InverseTransformPoint(bounds.center);
            pick.size = bounds.size + Vector3.one * 0.04f;   // margem para se apontar

            // A fita azul, porque o pensamento dela fala nela.
            //
            // Sem isto o jogador le "blue tape on the grip" e ve uma lanterna
            // vulgar. E o unico detalhe que prova que a lanterna e dele, e e a unica
            // frase do capitulo que tem mesmo de ser acreditada — sem a fita a
            // frente dos olhos, a linha soa a invencao do protagonista.
            var tape = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tape.name = "Torch_Tape";
            tape.transform.SetParent(go.transform, false);
            tape.transform.localPosition = new Vector3(0f, 0f, bounds.size.magnitude * 0.22f);
            tape.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tape.transform.localScale = new Vector3(0.078f, 0.016f, 0.078f);
            Object.DestroyImmediate(tape.GetComponent<Collider>());

            var tapeMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            tapeMat.color = new Color(0.16f, 0.29f, 0.62f);
            AssetDatabase.CreateAsset(tapeMat, "Assets/Pungent/Materials/M_Garage_TorchTape.mat");
            tape.GetComponent<Renderer>().sharedMaterial = tapeMat;

            var flavour = go.AddComponent<FlavourInteractable>();
            var so = new SerializedObject(flavour);
            so.FindProperty("prompt").stringValue = "Pick it up";
            so.FindProperty("thoughtLines").objectReferenceValue = Thoughts("Garage_Torch", new[]
            {
                "There is a torch in here, in with the parts.",
                "Blue tape on the grip. I put that tape on because the rubber was splitting.",
                "This was in the tool box. The tool box has been under my bed since Saturday.",
                "I am not going to say anything about it. Not here."
            });
            so.ApplyModifiedPropertiesWithoutUndo();

            // O evento so sai depois de ele ter lido as quatro linhas — nao ao
            // primeiro toque. A conta que ele faz demora, e o passo do capitulo tem
            // de esperar por ela.
            var raiser = go.AddComponent<ChapterEventRaiser>();
            raiser.EditorConfigure("evidence_found", ChapterEventRaiser.Trigger.Interact,
                string.Empty, string.Empty, "seller_met", readThis: flavour);
            EditorUtility.SetDirty(raiser);

            log.Add("  a lanterna dele: levanta 'evidence_found'");
        }

        /// <summary>
        /// O Tomas chega **a conduzir**, e estaciona onde quiser.
        ///
        /// Estava a nascer a pe no meio do patio, o que fazia da chegada um corte de
        /// camara: o objectivo do capitulo diz "conduz ate a unidade 7, a seguir ao
        /// deposito de agua" e o jogador aparecia la sem ter conduzido nada.
        ///
        /// O lugar de estacionamento nao esta marcado em lado nenhum, e e de
        /// proposito (ver <see cref="ParkedCar"/>): quando ele voltar ao carro com as
        /// pecas, o carro esta onde **ele** o deixou. Ficar a vinte metros do portao
        /// e uma decisao dele, e e no fim do capitulo que ela se paga.
        ///
        /// Nao ha codigo novo nisto: e o mesmo carro, banco, ignicao e porta que a
        /// estrada da noite ja usa.
        /// </summary>
        private static void BuildArrival(Transform parent, GameObject blockout, List<string> log)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(SedanPrefab);
            if (asset == null) { Debug.LogWarning("[Garage] Sem o sedan em " + SedanPrefab); return; }

            var car = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            car.name = "CAR";
            car.transform.SetParent(parent, false);

            // A sul do volume de chegada (z -14..-9), virado para o patio. Ele entra
            // a conduzir e o volume conta a chegada quando o carro la passa.
            car.transform.position = new Vector3(-1.20f, 0.60f, -18.00f);
            car.transform.rotation = Quaternion.identity;

            var bounds = Bounds(car);
            Vector3 scale = car.transform.lossyScale;
            Vector3 localSize = new Vector3(
                bounds.size.x / Mathf.Max(0.0001f, scale.x),
                bounds.size.y / Mathf.Max(0.0001f, scale.y),
                bounds.size.z / Mathf.Max(0.0001f, scale.z));

            // Assentar no alcatrao: o pivot do prefab nao esta debaixo das rodas.
            car.transform.position -= new Vector3(0f, bounds.min.y - 0.04f, 0f);
            bounds = Bounds(car);

            foreach (var mesh in car.GetComponentsInChildren<MeshCollider>(true))
                Object.DestroyImmediate(mesh);

            var box = car.AddComponent<BoxCollider>();
            box.center = car.transform.InverseTransformPoint(bounds.center);
            box.size = localSize;

            var body = car.AddComponent<Rigidbody>();
            body.mass = 1100f;

            var driver = car.AddComponent<CarDriver>();
            var seat = car.AddComponent<CarSeat>();
            var ignition = car.AddComponent<CarIgnition>();

            var parked = car.AddComponent<ParkedCar>();
            parked.EditorConfigure(seat, driver, "car_parked");

            SeatAndDoor(car, bounds, seat, ignition, log);
            BuildBoot(car, bounds, log);

            log.Add("  carro a entrada do patio, com o Tomas ao volante");
            log.Add("  o lugar de estacionamento fica onde ele parar (ParkedCar)");
        }

        /// <summary>
        /// Senta o Tomas ao volante e poe a porta a oferecer-se desde o inicio.
        ///
        /// Ao contrario da estrada, aqui a porta nao espera por ninguem: ele pode
        /// sair quando lhe apetecer, porque nao ha nada a chegar por tras. Sair e
        /// uma escolha e nao uma fuga — pelo menos ate ele levantar a lona.
        /// </summary>
        private static void SeatAndDoor(GameObject car, Bounds bounds, CarSeat seat,
            CarIgnition ignition, List<string> log)
        {
            // O banco desfaz a escala do prefab (0,62), senao o jogador nasce a 62%
            // e a camara dele desce com o resto.
            var seatGo = new GameObject("PLAYER_SEAT");
            seatGo.transform.SetParent(car.transform, false);
            Vector3 scale = car.transform.lossyScale;
            seatGo.transform.localScale = new Vector3(
                1f / Mathf.Max(0.0001f, scale.x),
                1f / Mathf.Max(0.0001f, scale.y),
                1f / Mathf.Max(0.0001f, scale.z));
            seatGo.transform.position = bounds.center
                                      + car.transform.right * -0.34f
                                      + car.transform.forward * 0.10f
                                      + Vector3.up * (bounds.min.y + 1.05f - bounds.center.y);
            seatGo.transform.rotation = car.transform.rotation;

            var so = new SerializedObject(seat);
            so.FindProperty("seat").objectReferenceValue = seatGo.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var doorGo = new GameObject("DOOR_Driver");
            doorGo.transform.SetParent(car.transform, false);
            doorGo.transform.position = bounds.center
                                      + car.transform.right * -(bounds.extents.x + 0.15f)
                                      + Vector3.up * (bounds.min.y + 1f - bounds.center.y);
            doorGo.transform.rotation = car.transform.rotation;
            var dbox = doorGo.AddComponent<BoxCollider>();
            dbox.size = new Vector3(0.5f, 1.1f, 1.9f);

            var door = doorGo.AddComponent<CarDoorInteractable>();
            var dso = new SerializedObject(door);
            dso.FindProperty("seat").objectReferenceValue = seat;
            dso.FindProperty("ignition").objectReferenceValue = ignition;
            dso.FindProperty("available").boolValue = true;   // aqui nao ha de quem fugir
            dso.ApplyModifiedPropertiesWithoutUndo();

            // O jogador vai para o banco. O `ScenePlayerSetup` ja o pos na cena; aqui
            // so muda de sitio.
            var player = GameObject.Find("PlayerRoot");
            if (player == null) { log.Add("  AVISO: sem jogador na cena"); return; }

            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            var motor = player.GetComponent<Pungent.Player.PlayerMotor>();
            if (motor != null) motor.enabled = false;

            player.transform.SetParent(seatGo.transform, false);
            player.transform.localRotation = Quaternion.identity;

            // O banco marca a altura dos olhos; quem la vai parar e a base do
            // jogador, com a camara 1,65 m acima.
            var camera = player.GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                float above = camera.transform.position.y - player.transform.position.y;
                player.transform.localPosition = new Vector3(0f, -above, 0f);
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 400f);
            }

            var input = player.GetComponent<Pungent.Player.PlayerInputReader>();
            var drv = new SerializedObject(car.GetComponent<CarDriver>());
            drv.FindProperty("input").objectReferenceValue = input;
            drv.ApplyModifiedPropertiesWithoutUndo();

            log.Add("  Tomas ao volante; a porta oferece-se desde o inicio");
        }

        /// <summary>
        /// A bagageira: onde as pecas contam como carregadas.
        ///
        /// **Filha do carro, e nao um sitio no patio.** O jogador estaciona onde
        /// quiser (ver <see cref="ParkedCar"/>), portanto uma zona fixa no chao
        /// mandava-o largar as pecas num sitio arbitrario que podia nem ser ao pe do
        /// carro. Assim a bagageira anda com o carro, como uma bagageira.
        /// </summary>
        private static void BuildBoot(GameObject car, Bounds bounds, List<string> log)
        {
            var go = new GameObject("CAR_BOOT");
            go.transform.SetParent(car.transform, false);

            // Atras do carro, a altura do tampo. O sedan aponta a +Z, portanto a
            // bagageira e para tras.
            go.transform.position = bounds.center
                                  - car.transform.forward * (bounds.extents.z * 0.72f)
                                  + Vector3.up * (bounds.min.y + 0.65f - bounds.center.y);
            go.transform.rotation = car.transform.rotation;

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.5f, 1.1f, 1.3f);

            var zone = go.AddComponent<BoxDropZone>();
            zone.EditorConfigure("parts_loaded", 3);
            log.Add("  bagageira ligada ao carro: 3 pecas para carregar");
        }

        /// <summary>
        /// Faz de tres pecas do monte coisas que se levam ao colo.
        ///
        /// Tres e nao nove: o §5 pede que ele carregue as pecas, nao que esvazie a
        /// oficina. Tres viagens chegam para o Vitor ter tempo de o observar a
        /// trabalhar — que e o que este bocado do capitulo esta mesmo a fazer.
        /// </summary>
        private static void MakeCarryable(Transform pile, string[] names, BoxDropZone boot,
            List<string> log)
        {
            if (boot == null)
            {
                Debug.LogError("[Garage] Sem bagageira: as pecas ficavam por ligar e " +
                               "dar-se-iam por entregues em qualquer sitio.");
                return;
            }

            int done = 0;
            foreach (var name in names)
            {
                Transform part = null;
                foreach (Transform t in pile)
                    if (t.name == name && t.GetComponent<CarryableBox>() == null) { part = t; break; }
                if (part == null) continue;

                var go = part.gameObject;
                var b = Bounds(go);

                var collider = go.GetComponent<BoxCollider>();
                if (collider == null) collider = go.AddComponent<BoxCollider>();
                collider.center = go.transform.InverseTransformPoint(b.center);
                collider.size = b.size;

                var rb = go.GetComponent<Rigidbody>();
                if (rb == null) rb = go.AddComponent<Rigidbody>();
                rb.mass = 12f;
                rb.isKinematic = true;

                // A bagageira tem de ser dita. Sem ela, o `CarryableBox` da-se por
                // entregue onde quer que seja largado e nunca avisa a zona — as
                // pecas ficavam "entregues" no meio do patio e o `parts_loaded`
                // nunca saia. Silencioso, como sempre.
                var carry = go.AddComponent<CarryableBox>();
                carry.EditorConfigure(boot, null, null);
                done++;
            }
            log.Add($"  {done} pecas carregaveis");
        }

        /// <summary>
        /// A saida do patio: e por aqui que o capitulo acaba.
        ///
        /// O mesmo sitio por onde ele entrou a conduzir, e de propósito — sair e
        /// desfazer a chegada. O volume so conta depois de as pecas estarem
        /// carregadas: antes disso ele pode dar a volta ao patio a vontade sem que o
        /// capitulo salte para a estrada com a bagageira vazia.
        ///
        /// Sem isto o ultimo passo esperava por um `garage_left` que ninguem
        /// levantava — o Dia 4 acabava com o jogador ao volante, parado, e o
        /// objectivo no ecra a dizer-lhe para se ir embora.
        /// </summary>
        private static void BuildExit(Transform parent, List<string> log)
        {
            var go = new GameObject("EVT_GarageLeft");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-1.20f, 0f, -17.60f);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(11f, 3f, 3.2f);
            box.center = new Vector3(0f, 1.5f, 0f);

            var raiser = go.AddComponent<ChapterEventRaiser>();
            raiser.EditorConfigure("garage_left", ChapterEventRaiser.Trigger.EnterArea,
                string.Empty, "Forty minutes of nothing, and then my own bed.", "parts_loaded");
            EditorUtility.SetDirty(raiser);

            log.Add("  saida do patio levanta 'garage_left' (so depois de carregar)");
        }

        private static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.2f);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static GameObject Point(Transform parent, string name, Vector3 at, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            return go;
        }

        private static ThoughtLineSet Thoughts(string name, string[] lines)
        {
            string path = $"Assets/Pungent/Dialogue/Thoughts/THT_{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ThoughtLineSet>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ThoughtLineSet>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.EditorPopulate(lines, ThoughtLineSet.Order.SequentialLastRepeats, 3.4f);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Vector3 Anchor(GameObject blockout, string name)
        {
            foreach (var t in blockout.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t.position;

            Debug.LogWarning($"[Garage] Anchor '{name}' nao encontrado; fica na origem.");
            return Vector3.zero;
        }
    }
}
