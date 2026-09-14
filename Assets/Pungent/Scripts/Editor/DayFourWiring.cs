using System.Collections.Generic;
using Pungent.Audio;
using Pungent.Interaction;
using Pungent.Narrative;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// O Dia 4 dentro de casa — a caixa e a chamada.
    ///
    /// ---
    ///
    /// **Substitui o `GarageDressing` e o `GarageCallWiring`.** A garagem e a estrada
    /// sairam (`COMO_TRABALHAR.md` §2.4 e §2.5): cortou-se o *sitio*, e nao o que la
    /// acontecia. As duas coisas que faziam o Dia 4 existir mudaram-se para dentro do
    /// apartamento, e nenhuma delas perdeu nada pelo caminho.
    ///
    /// **1. A prova.** A lanterna com fita azul deixa de estar num monte de pecas a
    /// dez quilometros e vem **dentro da caixa**, desempacotada na bancada da cozinha,
    /// com o Rui no quarto ao lado e a caixa de ferramentas dela debaixo da cama, a
    /// seis metros. Mesma informacao, e reaproveita a mecanica de carregar caixas que
    /// o prologo ja ensinou — o jogador carregou aquela caixa de ferramentas para
    /// dentro de casa com as proprias maos, e e por isso que isto funciona.
    ///
    /// **2. A meia conversa.** O vendedor esta **no patamar, do lado de fora da porta
    /// de casa**, ao telefone, e nunca entra.
    ///
    /// A primeira versao punha-o na rua, ouvido da varanda. Nao podia ser: o
    /// `CityExteriorBuilder` poe a rua a `StreetY = -24`, oito pisos abaixo, e a
    /// mecanica inteira do <see cref="OverheardCall"/> e uma distancia. A porta
    /// devolve-lhe o plano horizontal — encostas-te para ouvir, e se ele te ouvir a
    /// ti, cala-se — e ainda resolve outra coisa: **atras de uma porta fechada ele nao
    /// tem cara.** Era isso que o §2.4 queria dizer quando escreveu que o Vitor deixa
    /// de existir como pessoa. Ele deixa de ser alguem com quem se fala e passa a ser
    /// uma voz do outro lado de uma coisa que nao se abre.
    ///
    /// ---
    ///
    /// **Nada aqui tem prompt a competir com nada.** A lanterna leva um
    /// <see cref="FlavourInteractable"/> — que e quem fala — e o
    /// <see cref="ChapterEventRaiser"/> ao lado tem o prompt vazio e escuta-a pelo
    /// `afterReading`. E o padrao que a garagem ja usava, e existe porque dois
    /// interagiveis no mesmo objecto disputam o clique e quem ganha e a ordem em que
    /// os componentes foram arrastados.
    ///
    /// **Quem levanta o que:**
    /// <list type="bullet">
    /// <item>`parts_carried` — a <see cref="BoxDropZone"/> da bancada, quando a caixa la chega;</item>
    /// <item>`evidence_found` — o raiser da lanterna, depois de a fita ser lida;</item>
    /// <item>`heard_call` — o <see cref="OverheardCall"/>, e e ele que levanta o `climax`
    ///       pelo ultimo passo do capitulo.</item>
    /// </list>
    ///
    /// ---
    ///
    /// Re-executavel: destroi e reconstroi so as suas duas raizes. Correr com a
    /// `Apartment_Blockout_V2` aberta, **depois** do dressing.
    /// </summary>
    internal static class DayFourWiring
    {
        private const string BoxRoot = "DAY4_BOX";
        private const string CallRoot = "DAY4_CALL";

        private const string BoxSetPath = "Assets/ThirdParty/cardboard_boxes_fbx.fbx";
        private const string CardboardMaterial = "Assets/Pungent/Materials/M_Cardboard.mat";
        private const string FlashlightPrefab = "Assets/Flashlight/Model/Flashlight.prefab";
        private const string TapeMaterial = "Assets/Pungent/Materials/M_Day4_TorchTape.mat";

        private const string WorktopName = "Kit_Worktop";
        private const string FrontDoorName = "Door_Front_3B";

        /// <summary>
        /// O centro aproximado do apartamento, usado so para saber de que lado da
        /// porta de entrada fica o **fora**. Melhor do que confiar no `forward` da
        /// porta, que depende de como ela foi construida.
        /// </summary>
        private static readonly Vector3 InteriorCentre = new Vector3(-0.6f, 0f, 0f);

        [MenuItem("Pungent/Blockout/Wire Day Four (Box + Call)", false, 21)]
        internal static void Wire()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Apartment"))
            {
                Debug.LogError("[Dia4] Abrir a `Apartment_Blockout_V2` primeiro. "
                             + "Esta ferramenta mexe na cena do apartamento.");
                return;
            }

            var log = new List<string>();

            // **`FindObjectOfType` nao serve para procurar o director.** Havia dois na
            // cena e este apanhava o errado — cinco campos ficaram a apontar para um
            // director que nunca recebe acontecimentos. O runtime usa o
            // `ChapterDirector.Resolve`, que procura primeiro dentro do
            // `GameSystemsRoot`; as ferramentas tem de procurar da mesma maneira, ou
            // escrevem para um sitio que o jogo nao le.
            var systems = Object.FindObjectOfType<GameSystemsRoot>(true);
            var director = systems != null
                ? systems.GetComponentInChildren<ChapterDirector>(true)
                : Object.FindObjectOfType<ChapterDirector>(true);
            var thoughts = Object.FindObjectOfType<PlayerThoughtDirector>(true);
            if (director == null) log.Add("  aviso: sem ChapterDirector na cena; os campos ficam por resolver no arranque");

            BuildBox(director, thoughts, log);
            BuildCall(director, thoughts, log);

            // **Nada disto existe antes do Dia 4.**
            //
            // A primeira versao deixou as duas raizes activas desde o arranque, e o
            // prologo tem um passo que diz "Bring your boxes in": o jogador encontrava
            // uma caixa a mais ao pe da porta, podia leva-la a bancada, e levantava o
            // `parts_carried` — que destranca a lanterna. A prova do jogo inteiro, no
            // primeiro dia.
            //
            // Os prompts ja estavam guardados por `requiresEvent`. **Uma condicao no
            // prompt nao e uma condicao na coisa.**
            Gate(BoxRoot, director, log);
            Gate(CallRoot, director, log);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Dia4] A caixa e a chamada ligadas.\n" + string.Join("\n", log));
        }

        /// <summary>
        /// Poe a raiz a espera do `day_four`. O <see cref="ChapterContent"/> desliga os
        /// filhos no `Awake` e volta a liga-los quando o capitulo abrir.
        /// </summary>
        private static void Gate(string rootName, ChapterDirector director, List<string> log)
        {
            var root = GameObject.Find(rootName);
            if (root == null) return;

            var gate = root.AddComponent<ChapterContent>();
            gate.EditorConfigure("day_four");
            Bind(gate, ("director", director));

            log.Add("  " + rootName + " so existe a partir de 'day_four'");
        }

        // ------------------------------------------------------------------
        // A caixa, a bancada e a lanterna.
        // ------------------------------------------------------------------

        private static void BuildBox(ChapterDirector director, PlayerThoughtDirector thoughts, List<string> log)
        {
            var root = Reset(BoxRoot);

            var worktop = Find(WorktopName);
            var frontDoor = Find(FrontDoorName);
            if (worktop == null) { log.Add("  ERRO: sem `" + WorktopName + "` — corre o dressing primeiro."); return; }
            if (frontDoor == null) { log.Add("  ERRO: sem `" + FrontDoorName + "`."); return; }

            // A zona de destino: em cima da bancada da cozinha, onde ele desempacota.
            var zoneGo = new GameObject("ZONE_Worktop");
            zoneGo.transform.SetParent(root, false);
            zoneGo.transform.position = TopOf(worktop) + Vector3.up * 0.12f;

            var zoneBox = zoneGo.AddComponent<BoxCollider>();
            zoneBox.isTrigger = true;
            zoneBox.size = new Vector3(1.30f, 0.90f, 0.90f);

            var zone = zoneGo.AddComponent<BoxDropZone>();
            zone.EditorConfigure("parts_carried", 1);
            Bind(zone, ("director", director), ("thoughts", thoughts));
            Set(zone, so =>
            {
                var last = so.FindProperty("lastBoxThought");
                if (last != null) last.stringValue = "Ninety for both. Nobody sells a pair of these for ninety.";
            });
            log.Add("  ZONE_Worktop na bancada, levanta 'parts_carried'");

            // A caixa comeca ao pe da porta de entrada, do lado de dentro: ele acabou
            // de a pousar para fechar a porta.
            // **Era um cubo primitivo, e isso deu tres bugs de uma vez:** textura que
            // nao batia certo com as caixas do prologo, nenhum corpo rigido — pousada
            // em qualquer sitio ficava a flutuar — e sem colisor decente empurrava o
            // jogador para fora do mundo.
            //
            // Passa a ser a mesma caixa de cartao do prologo, montada como as outras:
            // corpo rigido cinematico que so acorda ao ser pousada, colisor tirado das
            // bounds da malha. O jogador ja carregou tres destas; esta tem de pesar
            // igual.
            var boxGo = LoadProp("cardboard_box_b", "BOX_Parts");
            if (boxGo == null) { log.Add("  ERRO: sem prefab `cardboard_box_b`."); return; }

            boxGo.transform.SetParent(root, false);
            boxGo.transform.position = Inward(frontDoor.position, 0.85f);
            boxGo.transform.rotation = Quaternion.Euler(0f, 21f, 0f);

            var bounds = MeshBounds(boxGo);
            var collider = boxGo.GetComponent<BoxCollider>();
            if (collider == null) collider = boxGo.AddComponent<BoxCollider>();
            collider.center = boxGo.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;

            var body = boxGo.AddComponent<Rigidbody>();
            body.mass = 6f;
            body.isKinematic = true;   // so acorda quando for pousada, como no prologo

            var carry = boxGo.AddComponent<CarryableBox>();
            Bind(carry, ("director", director), ("thoughts", thoughts), ("dropZone", zone));
            Set(carry, so =>
            {
                SetString(so, "pickUpPrompt", "Pick up the box");
                SetString(so, "putDownPrompt", "Put it down");
                SetString(so, "firstLiftThought", "Heavier than ninety euros of anything should be.");
                SetString(so, "wrongPlaceThought", "Not here. The counter.");
            });
            log.Add("  BOX_Parts junto a porta, entregue na bancada");

            BuildTorch(boxGo.transform, director, log);
        }

        /// <summary>
        /// A lanterna dele, dentro da caixa.
        ///
        /// Fica **filha da caixa** de proposito: viaja com ela ao colo do jogador, e
        /// so se deixa ler depois de a caixa chegar ao destino
        /// (<c>requiresEvent: parts_carried</c>). Sem essa condicao, o jogador podia
        /// ler a fita azul com a caixa ainda no chao da entrada e o capitulo inteiro
        /// acontecia ao contrario.
        ///
        /// Escrita banal de proposito: nao ha sangue, nao ha bilhete, nao ha o nome do
        /// Rui em lado nenhum. Ha uma lanterna com fita azul, e ele sabe porque e que
        /// tem fita azul.
        /// </summary>
        private static void BuildTorch(Transform box, ChapterDirector director, List<string> log)
        {
            var go = new GameObject("PART_Torch");
            go.transform.SetParent(box, false);
            go.transform.localPosition = new Vector3(0f, 0.62f, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, 34f, 90f);
            go.transform.localScale = Divide(Vector3.one, box.localScale);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(FlashlightPrefab);
            if (asset != null)
            {
                var body = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                body.name = "Torch_Body";
                body.transform.SetParent(go.transform, false);
                foreach (var l in body.GetComponentsInChildren<Light>(true)) l.enabled = false;
            }
            else log.Add("  aviso: sem lanterna em " + FlashlightPrefab);

            var pick = go.AddComponent<BoxCollider>();
            pick.size = new Vector3(0.09f, 0.09f, 0.26f);

            // A fita azul, porque o pensamento dela fala nela. Sem isto o jogador le
            // "blue tape on the grip" e ve uma lanterna vulgar — e a linha passa a
            // soar a invencao do protagonista.
            var tape = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tape.name = "Torch_Tape";
            tape.transform.SetParent(go.transform, false);
            tape.transform.localPosition = new Vector3(0f, 0f, 0.06f);
            tape.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tape.transform.localScale = new Vector3(0.078f, 0.016f, 0.078f);
            Object.DestroyImmediate(tape.GetComponent<Collider>());
            tape.GetComponent<Renderer>().sharedMaterial = TapeMaterialAsset();

            // Quem fala e o Flavour. O raiser ao lado tem prompt vazio e escuta-o.
            var flavour = go.AddComponent<FlavourInteractable>();
            Bind(flavour, ("director", director));
            Set(flavour, so =>
            {
                SetString(so, "prompt", "Look at it");
                SetString(so, "requiresEvent", "parts_carried");
                var lines = so.FindProperty("thoughts");
                if (lines != null)
                {
                    lines.arraySize = 3;
                    lines.GetArrayElementAtIndex(0).stringValue = "Blue tape on the grip.";
                    lines.GetArrayElementAtIndex(1).stringValue = "I put that there. The switch sticks otherwise.";
                    lines.GetArrayElementAtIndex(2).stringValue = "My toolbox is under my bed. Six metres from here.";
                }
            });

            var raiser = go.AddComponent<ChapterEventRaiser>();
            Bind(raiser, ("director", director), ("afterReading", flavour));
            Set(raiser, so =>
            {
                SetString(so, "eventId", "evidence_found");
                SetString(so, "prompt", string.Empty);
                SetString(so, "requiresEvent", "parts_carried");
                var trigger = so.FindProperty("raiseOn");
                if (trigger != null) trigger.enumValueIndex = IndexOf(trigger, "Interact");
            });

            log.Add("  PART_Torch dentro da caixa, fita azul, levanta 'evidence_found' depois de lida");
        }

        // ------------------------------------------------------------------
        // O vendedor ao telefone, do lado de fora da porta.
        // ------------------------------------------------------------------

        private static void BuildCall(ChapterDirector director, PlayerThoughtDirector thoughts, List<string> log)
        {
            var root = Reset(CallRoot);

            var frontDoor = Find(FrontDoorName);
            if (frontDoor == null) { log.Add("  ERRO: sem `" + FrontDoorName + "`."); return; }

            Vector3 outside = Outward(frontDoor.position, 1.15f);

            var seller = new GameObject("SELLER_Landing");
            seller.transform.SetParent(root, false);
            seller.transform.position = outside;
            seller.transform.rotation = Quaternion.LookRotation(
                Flat(frontDoor.position - outside), Vector3.up);

            // Para onde ele se afasta a atender: mais um passo para longe da porta, o
            // que obriga o jogador a encostar-se mesmo a ela para ouvir.
            var spot = new GameObject("SELLER_CallSpot");
            spot.transform.SetParent(root, false);
            spot.transform.position = Outward(frontDoor.position, 2.05f);

            var phoneGo = new GameObject("Seller_Phone");
            phoneGo.transform.SetParent(seller.transform, false);

            var phone = phoneGo.AddComponent<AudioSource>();
            phone.playOnAwake = false;
            phone.spatialBlend = 1f;
            phone.minDistance = 0.9f;
            phone.maxDistance = 9f;
            phone.rolloffMode = AudioRolloffMode.Linear;

            // A porta esta pelo meio, e tem de se ouvir que esta.
            phoneGo.AddComponent<MuffledThroughWalls>();

            var voice = BuildVoice(seller.transform, log);

            var call = seller.AddComponent<OverheardCall>();
            call.EditorConfigure(seller, spot.transform, phone);
            Bind(call, ("director", director), ("thoughts", thoughts), ("voice", voice));
            Set(call, so =>
            {
                var ring = so.FindProperty("ringClip");
                if (ring != null && ring.objectReferenceValue == null)
                    ring.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(
                        "Assets/ThirdParty/Audio/phone_ring.mp3");

                log.Add(ring != null && ring.objectReferenceValue != null
                    ? "  toque: `phone_ring.mp3` ligado"
                    : "  POR PREENCHER: falta `Assets/ThirdParty/Audio/phone_ring.mp3` — sem ele "
                    + "nada puxa o jogador ate a porta e a cena passa-lhe ao lado");
            });
            Set(call, so =>
            {
                SetString(so, "armOnEvent", "evidence_found");
                SetString(so, "heardEvent", "heard_call");

                // Perto o suficiente para ser preciso ir a porta, e nao tanto que se
                // ouca do sofa. O dono do projecto julga isto melhor do que eu: sao
                // metros, e ele mede-os a jogar.
                SetFloat(so, "earshot", 4.2f);
                SetFloat(so, "noticeRange", 1.1f);
                SetFloat(so, "delaySeconds", 7f);

                var lines = so.FindProperty("lines");
                if (lines != null)
                {
                    string[] said =
                    {
                        // Tudo banal. O que nao e banal e ele saber isto.
                        "Yeah, I am outside it now. Rua das Flores, the old block.",
                        "Third floor, he said. Blue Astra downstairs, the one where the passenger door only opens from the inside.",
                        "No, I am not knocking. That was not the arrangement.",
                        "Well, somebody told him. It was not me.",
                        "Fine. Tomorrow, then.",
                    };
                    lines.arraySize = said.Length;
                    for (int i = 0; i < said.Length; i++)
                        lines.GetArrayElementAtIndex(i).stringValue = said[i];
                }

                SetString(so, "noticedLine", "...hold on.");
                SetString(so, "afterThought",
                    "He knows which car is mine. He has never seen my car.");
            });

            log.Add("  SELLER_Landing do lado de fora da " + FrontDoorName + ", voz abafada pela porta");
            log.Add("  chamada armada em 'evidence_found', levanta 'heard_call' -> 'climax'");
            log.Add("  earshot 4,2 m / notice 1,1 m — numeros por julgar a jogar");
        }

        /// <summary>
        /// A voz dele. **Nao falta ficheiro nenhum** — o `NpcMumbleVoice` gera as
        /// silabas por codigo, e e de propósito: o que o jogador tem de perceber e o
        /// *sentido* das frases, que aparecem escritas, e nao as palavras.
        ///
        /// Grave e lenta, diferente da do Rui, porque sao duas pessoas.
        ///
        /// **Leva `MuffledThroughWalls` como o toque**, e esta e a peca que faz a cena
        /// existir: e a voz que o jogador se esforca por ouvir, e e a porta que a
        /// torna dificil. Sem isto ouvia-se tudo do sofa e nao havia razao nenhuma
        /// para ir ate a porta.
        /// </summary>
        private static Pungent.Dialogue.NpcMumbleVoice BuildVoice(Transform seller, List<string> log)
        {
            var go = new GameObject("Seller_Voice");
            go.transform.SetParent(seller, false);
            go.transform.localPosition = new Vector3(0f, 1.62f, 0f);   // altura da boca
            Undo.RegisterCreatedObjectUndo(go, "Wire Day Four");

            // O `RequireComponent` so acrescenta o `AudioSource` ao adicionar pelo
            // inspector, e nao por codigo.
            if (go.GetComponent<AudioSource>() == null) go.AddComponent<AudioSource>();

            var voice = go.AddComponent<Pungent.Dialogue.NpcMumbleVoice>();
            voice.EditorConfigure(isSpatial: true,
                pitch: new Vector2(0.82f, 0.92f),
                loudness: 0.30f, minDistance: 1.0f, maxDistance: 9f);

            go.AddComponent<MuffledThroughWalls>();

            log.Add("  voz do vendedor: procedural, grave (0,82-0,92), abafada pela porta");
            return voice;
        }

        // ------------------------------------------------------------------
        // Utilitarios.
        // ------------------------------------------------------------------

        private static Transform Reset(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Wire Day Four");
            return go.transform;
        }

        private static Transform Find(string name)
        {
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        /// <summary>
        /// Instancia um prefab do projecto pelo nome, sem caminho escrito a mao — os
        /// pacotes de terceiros mudam de pasta entre reimportacoes e um caminho fixo
        /// falha em silencio.
        /// </summary>
        private static GameObject LoadProp(string childName, string newName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(BoxSetPath);
            if (asset == null) return null;

            // **As tres caixas sao filhas do mesmo FBX**, e um filho de uma instancia
            // de prefab nao se deixa reparentar. Mesmo padrao do `PrologueWiring`:
            // instancia-se o conjunto, desembrulha-se, fica-se com a que interessa e
            // deita-se fora o resto.
            //
            // A primeira versao disto procurava um asset chamado `cardboard_box_b` e
            // nunca encontrava nada — nao existe ficheiro nenhum com esse nome. A
            // caixa do Dia 4 deixou de nascer e ninguem soube porque, ate se ir ver
            // como o prologo fazia. **Quando ha duas maneiras de carregar a mesma
            // coisa, uma delas esta errada.**
            var set = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(set, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            var wanted = set.transform.Find(childName);
            if (wanted == null) { Object.DestroyImmediate(set); return null; }

            var go = wanted.gameObject;
            go.name = newName;
            go.transform.SetParent(null, true);

            // Sem isto o modelo fica com o branco por omissao do Unity em vez do
            // cartao — foi exactamente o que se viu a jogar nas caixas do prologo.
            var cardboard = AssetDatabase.LoadAssetAtPath<Material>(CardboardMaterial);
            if (cardboard != null)
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = cardboard;

            // Desactivar antes de destruir: o `DestroyImmediate` de um pai leva atras
            // o que ainda estiver pendurado.
            set.SetActive(false);
            Object.DestroyImmediate(set);
            return go;
        }

        /// <summary>As bounds das malhas, em espaco de mundo. Os props deste projecto
        /// sao "holder + malha com offset local": o transform nao serve.</summary>
        private static Bounds MeshBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.4f);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static Vector3 TopOf(Transform t)
        {
            var renderer = t.GetComponentInChildren<Renderer>();
            return renderer != null
                ? new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, renderer.bounds.center.z)
                : t.position;
        }

        private static Vector3 Outward(Vector3 door, float metres)
            => door + Flat(door - InteriorCentre).normalized * metres;

        private static Vector3 Inward(Vector3 door, float metres)
            => door - Flat(door - InteriorCentre).normalized * metres;

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(
            b.x == 0f ? a.x : a.x / b.x,
            b.y == 0f ? a.y : a.y / b.y,
            b.z == 0f ? a.z : a.z / b.z);

        private static int IndexOf(SerializedProperty enumProperty, string name)
        {
            var names = enumProperty.enumNames;
            for (int i = 0; i < names.Length; i++)
                if (names[i] == name) return i;
            return enumProperty.enumValueIndex;
        }

        private static void Set(Object target, System.Action<SerializedObject> body)
        {
            var so = new SerializedObject(target);
            body(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Bind(Object target, params (string field, Object value)[] pairs)
        {
            var so = new SerializedObject(target);
            foreach (var pair in pairs)
            {
                if (pair.value == null) continue;
                var property = so.FindProperty(pair.field);
                if (property != null) property.objectReferenceValue = pair.value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(SerializedObject so, string field, string value)
        {
            var property = so.FindProperty(field);
            if (property != null) property.stringValue = value;
        }

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            var property = so.FindProperty(field);
            if (property != null) property.floatValue = value;
        }

        private static Material TapeMaterialAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(TapeMaterial);
            if (existing != null) return existing;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.16f, 0.29f, 0.62f)
            };
            AssetDatabase.CreateAsset(material, TapeMaterial);
            return material;
        }
    }
}
