using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.NPC;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Poe de pe o prologo: a noite em que o Tomas se mudou.
    ///
    /// Tres coisas, todas em cima de sistemas que ja existem:
    ///
    /// 1. As caixas sao o tutorial. Desfaze-las ensina olhar, aproximar e
    ///    interagir sem uma unica instrucao no ecra, e o que sai delas caracteriza
    ///    o Tomas melhor do que qualquer texto.
    /// 2. A pergunta do Rui e util para ele. Nao e "de onde es" — e a que horas
    ///    sais de manha. No Dia 2 ele fala da aula de terca, e a fala deixa de ser
    ///    um susto e passa a ser uma coisa que o jogador lhe disse. Ou nao disse,
    ///    e ele sabe na mesma, que e pior.
    /// 3. Ele fica ao vao da porta e o jogo nao diz nada. Sem prompt, sem
    ///    objectivo. Esperar ou fechar a porta e uma decisao sem sinalizacao.
    ///
    /// Re-executavel.
    /// </summary>
    public static class PrologueWiring
    {
        private const string ChapterPath = "Assets/Pungent/Narrative/Chapters/CH_Prologue.asset";
        private const string DialoguePath = "Assets/Pungent/Dialogue/Sequences/DLG_Rui_Prologue.asset";
        private const string NightDialoguePath =
            "Assets/Pungent/Dialogue/Sequences/DLG_NPC_Rui_Opening.asset";

        /// <summary>
        /// O set de cartao. Duas fechadas e uma aberta — a `c` e a que tem medidas
        /// diferentes das outras duas, portanto e a aberta. Se for ao contrario,
        /// trocar estas tres linhas chega.
        /// </summary>
        private const string BoxSetPath = "Assets/ThirdParty/cardboard_boxes_fbx.fbx";
        private const string ClosedBoxA = "cardboard_box_a";
        private const string ClosedBoxB = "cardboard_box_b";
        private const string OpenBox = "cardboard_box_c";
        private const string BoxThoughtsPath = "Assets/Pungent/Dialogue/Thoughts/THT_Prologue_Boxes.asset";
        private const string KeyModelPath = "Assets/ThirdParty/key.fbx";
        private const string KeyMaterialPath =
            "Assets/Pungent/Materials/M_Surf_Metal_LightMetal.mat";

        /// <summary>
        /// Comprimento da chave no mundo, em metros.
        ///
        /// O modelo vem a medida certa — 6,5 cm, uma chave de porta a serio — e a
        /// medida certa nao e a que se quer aqui. O suporte esta num corredor as
        /// escuras a 1,56 m, e o que interessa e que se veja que **ha** ali uma
        /// chave antes de se apontar. Onze centimetros e o que o modelo antigo
        /// ocupava no ecra, e essa leitura ja estava afinada; so muda o objecto.
        /// </summary>
        private const float KeyLength = 0.11f;

        [MenuItem("Pungent/Blockout/Wire Prologue", false, 37)]
        public static void Wire()
        {
            var player = GameObject.Find("PlayerRoot");
            if (player == null) { Debug.LogError("[Prologue] PlayerRoot nao encontrado."); return; }

            var log = new List<string>();

            var boxes = BuildBoxes(log);
            var keys = BuildKeys(player, log);
            var dialogue = BuildRuiConversation();
            var chapter = BuildChapter();
            var stage = BuildStage(player, boxes, keys, log);
            WireRuiContact(log);

            // O capitulo entra em primeiro lugar na lista do director.
            var director = player.GetComponent<ChapterDirector>();
            if (director != null)
            {
                var so = new SerializedObject(director);
                var list = so.FindProperty("chapters");
                bool alreadyThere = false;
                for (int i = 0; i < list.arraySize; i++)
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == chapter) alreadyThere = true;

                if (!alreadyThere)
                {
                    list.InsertArrayElementAtIndex(0);
                    list.GetArrayElementAtIndex(0).objectReferenceValue = chapter;
                    so.ApplyModifiedProperties();
                    log.Add("  capitulo posto em primeiro na lista do ChapterDirector");
                }
            }

            // A conversa do prologo vive num interactavel proprio no Rui, para nao
            // colidir com a RuiStoryConversation da noite das 02:47.
            var rui = GameObject.Find("NPC_Rui");
            if (rui != null && dialogue != null)
            {
                var convo = rui.GetComponent<RuiStoryConversation>();
                if (convo != null)
                    log.Add($"  NOTA: o Rui ja tem RuiStoryConversation (Dia 2). A do prologo " +
                            $"esta em {System.IO.Path.GetFileName(DialoguePath)} e tem de ser " +
                            $"trocada por codigo quando o prologo correr.");
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Prologue] Ligado. {chapter.StepCount} passos, " +
                      $"{dialogue.BeatCount} beats de conversa.\n" + string.Join("\n", log));
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Caixas por abrir no quarto. Blockout: cubos com o material do cartao que
        /// o passe de arte ja usa, cada um com um pensamento proprio.
        /// </summary>
        private static GameObject BuildBoxes(List<string> log)
        {
            var stale = GameObject.Find("PROLOGUE_BOXES");
            if (stale != null) Object.DestroyImmediate(stale);

            // Restos do set de cartao. Uma corrida que rebente a meio da extraccao
            // deixa o conjunto inteiro na cena com as caixas ja renomeadas la
            // dentro — e a seguir ha seis caixas no chao e ninguem percebe porque.
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
                if (t != null && t.name.StartsWith("cardboard_boxes"))
                {
                    t.gameObject.SetActive(false);
                    Object.DestroyImmediate(t.gameObject);
                }

            var root = new GameObject("PROLOGUE_BOXES");
            Undo.RegisterCreatedObjectUndo(root, "Wire Prologue");

            var material = GetCardboardMaterial();

            // As caixas ficam **a entrada**, e nao no quarto.
            //
            // Estavam ja arrumadas ao pe da cama, o que saltava a unica coisa que o
            // prologo tem para dar: chegar. Uma pessoa que se muda entra com as
            // caixas a porta e leva-as. Sao seis viagens pelo mesmo corredor — e no
            // meio desse corredor ha uma porta entreaberta que nao e a dele.
            //
            // Ninguem tem de dizer nada sobre essa porta. O jogador passa-lhe ao
            // lado seis vezes com as maos ocupadas.
            var boxes = new (string Name, string Model, Vector3 Pos, float Yaw, string Thought)[]
            {
                // Afastadas do ponto de partida: com elas a trinta centimetros, o
                // jogador nascia dentro da primeira e era empurrado pelo collider.
                // Espalhadas, e **fora do arco da porta dos arrumos**.
                //
                // Estavam a menos de meio metro umas das outras e a 0,75 m da
                // dobradica da `Door_Storage`, em (3,50 / -3,00). Duas consequencias,
                // ambas vistas a jogar: encavalitavam-se, e a folha da porta batia
                // sem parar contra a caixa que lhe estava no caminho — era isso o
                // "flick" de abrir e fechar sozinha.
                //
                // A mola da porta varre um raio de 0,90 m a partir da dobradica.
                // Nenhuma destas esta a menos de 0,95 m dela, nenhuma esta a menos de
                // 0,70 m de outra, e nenhuma esta a menos de 0,70 m do sitio onde o
                // jogador nasce (5,10 / -2,20).
                // **O pensamento tem de bater certo com o que sai da caixa.** Dizia
                // "Textbooks", a etiqueta pintada de lado diz DESK, e o que la esta
                // dentro e o portatil. Tres versoes da mesma caixa, e o jogador via as
                // tres. O nome do objecto fica — mudar-lhe o nome partia o
                // `BuildContents`, que o procura por `Box_Books`.
                ("Box_Books",   ClosedBoxA, new Vector3(4.30f, 0f, -1.50f), -14f,
                    "The laptop, and everything that plugs into it. I paid for this "
                  + "before I paid for the room."),
                ("Box_Kitchen", OpenBox,    new Vector3(3.85f, 0f, -2.10f),  -6f,
                    "Two plates and a pan. Mum packed this one."),
                ("Box_Tools",   ClosedBoxB, new Vector3(4.55f, 0f, -2.70f),  22f,
                    "Sockets, a torch, spare cable. Everything I keep and never use."),

                // **A quarta caixa**, acrescentada a 11-08.
                //
                // A roupa aparecia do nada em cima da cama no Dia 1, e a tarefa de a
                // arrumar estava partida. Passa a entrar em casa como tudo o resto: as
                // costas do jogador, numa caixa que ele carrega.
                //
                // **Nao substitui a das ferramentas**, e isso importa. A lanterna com
                // fita azul do Dia 4 so funciona porque o jogador carregou a caixa de
                // ferramentas com as proprias maos — o pensamento dela diz "my toolbox
                // is under my bed". Trocar as ferramentas por roupa tirava o chao ao
                // momento-charneira da historia.
                //
                // Custa uma quarta viagem no passo mais longo do prologo. Paga-se com
                // a lavagem do Dia 1 e com o roupeiro a existir por alguma razao.
                // This point was checked against the live scene colliders. The old
                // position was beyond the storage wall, so the box appeared inside
                // the room to the player's left.
                ("Box_Clothes", OpenBox,    new Vector3(3.30f, 0f, -1.40f),  -9f,
                    "Clothes. Half of these have not fitted me since I was nineteen."),
            };

            var zone = BuildDropZone(root.transform);

            foreach (var box in boxes)
            {
                var go = BoxModel(box.Model, box.Name, root.transform, material);
                go.transform.position = box.Pos;
                go.transform.rotation = Quaternion.Euler(0f, box.Yaw, 0f);

                // Colisor pela malha e nao a olho: as tres caixas do set tem tamanhos
                // diferentes, e uma caixa que se pega tem de ter o volume certo ou
                // atravessa o chao ao ser pousada.
                var bounds = VisualBounds(go);
                var collider = go.AddComponent<BoxCollider>();
                collider.center = go.transform.InverseTransformPoint(bounds.center);
                collider.size = bounds.size;

                // **Uma caixa nao pode ter exactamente a altura de um degrau.**
                //
                // O `CharacterController` do jogador tem `stepOffset` de 25 cm e
                // estas caixas medem 23 a 25. Um obstaculo mesmo em cima desse
                // limiar poe o controlador a tentar subi-lo e a gravidade a puxa-lo
                // para baixo no frame seguinte, sem parar. E esse vaivem que a jogar
                // se sente como ficar preso na caixa — nao e uma colisao a mais, e
                // uma decisao que o controlador nao consegue tomar.
                //
                // O `stepOffset` fica como esta: serve as soleiras e os degraus da
                // casa toda. Sobe-se o **topo** do colisor para bem acima dele. A
                // pegada em x e z nao muda, portanto a caixa continua a ocupar
                // exactamente o que se ve; so deixa de ser escalavel. O fundo fica
                // onde estava, e por isso ela continua a assentar no chao quando e
                // pousada.
                float step = 0.25f;
                var controller = Object.FindObjectOfType<CharacterController>();
                if (controller != null) step = controller.stepOffset;

                float tall = Mathf.Max(bounds.size.y, step + 0.12f);
                collider.center += Vector3.up * ((tall - bounds.size.y) * 0.5f);
                collider.size = new Vector3(bounds.size.x, tall, bounds.size.z);

                // O volume de ajuda de mira: uma coluna a subir do chao.
                //
                // ---
                //
                // **Tinha 1,87 m e era compensacao de um defeito que ja nao existe.**
                // Foi esticado ate a altura do peito porque a mira nao acertava nas
                // caixas — e nao acertava porque o `PlayerInteractor` tratava o corpo
                // do proprio jogador como uma parede a zero metros e descartava tudo
                // o resto. Corrigido isso, a tolerancia vem do varrimento em esfera e
                // esta coluna deixa de ter trabalho a fazer.
                //
                // **E estava a custar caro.** Uma coluna que chega a altura dos olhos
                // fica sempre mais perto da camara do que o que estiver atras dela, e
                // ganha por isso. Medido no metodo real, com o jogador a olhar direito
                // para a porta dos arrumos: as caixas roubavam-lhe o clique em **tres
                // das oito direccoes** — a 0, 45 e 90 graus, que sao precisamente as
                // de quem vem da entrada.
                //
                // A 1,05 m a coluna cobre a caixa de quem esta a olhar para baixo — o
                // gesto natural de quem quer pegar numa caixa — e passa por baixo da
                // linha de vista de quem esta a olhar em frente. As duas leituras
                // deixam de disputar.
                const float PickupTop = 1.05f;

                var target = new GameObject("PickupTarget");
                target.transform.SetParent(go.transform, false);
                var pickup = target.AddComponent<BoxCollider>();
                pickup.isTrigger = true;

                // Um palmo mais larga do que a caixa: perdoa a mira pelo contorno sem
                // se sobrepor a caixa do lado, que esta a 60 cm de distancia.
                pickup.size = new Vector3(
                    bounds.size.x + 0.10f,
                    PickupTop,
                    bounds.size.z + 0.10f);

                // Assente no chao: o `center` e em espaco local e a caixa esta a y=0.
                pickup.center = new Vector3(collider.center.x, PickupTop * 0.5f, collider.center.z);

                var rb = go.AddComponent<Rigidbody>();
                rb.mass = 6f;
                rb.isKinematic = true;   // so acorda quando for pousada

                var carry = go.AddComponent<CarryableBox>();
                carry.EditorConfigure(zone, null, box.Thought);

                LabelTheBox(go, bounds, box.Name);
            }

            BuildDesk(root.transform, log);

            log.Add($"  {boxes.Length} caixas a entrada, para levar ao quarto");
            return root;
        }

        /// <summary>
        /// Escreve nas caixas o que vai dentro delas.
        ///
        /// ---
        ///
        /// **Tres caixas iguais e um objectivo que nao diz qual.** O jogo mandava
        /// pousar o portatil na secretaria e as caixas eram tres cubos identicos: a
        /// unica maneira de saber qual era qual era encostar-se a cada uma e ler o
        /// prompt. Tres viagens pelo corredor a adivinhar.
        ///
        /// **A solucao e a que qualquer pessoa usa numa mudanca:** escreve-se por
        /// fora. Nao e HUD, nao e um marcador flutuante, nao e uma seta — e a
        /// caixa a dizer o que tem, lida do outro lado da sala, como se le uma caixa
        /// de verdade. E de graca: quem embala escreve nas caixas, portanto isto nao
        /// acrescenta uma affordance de jogo, tira uma omissao.
        ///
        /// Nos quatro lados, porque elas ficam encostadas e viradas ao acaso, e uma
        /// caixa que so tem letra num lado e uma caixa sem letra metade das vezes.
        /// </summary>
        private static void LabelTheBox(GameObject box, Bounds bounds, string boxName)
        {
            string text = boxName == "Box_Books" ? "DESK"
                        : boxName == "Box_Tools" ? "TOOLS"
                        : boxName == "Box_Kitchen" ? "KITCHEN"
                        : boxName == "Box_Clothes" ? "CLOTHES"
                        : null;
            if (text == null) return;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font == null)
            {
                Debug.LogWarning("[Prologue] Sem tipo de letra interno — as caixas ficam sem rotulo.");
                return;
            }

            var holder = new GameObject("Label");
            holder.transform.SetParent(box.transform, false);

            // A meia altura da caixa e um pouco a frente da face, para a letra nao
            // ficar dentro do cartao.
            float half = bounds.size.x * 0.5f;
            float depth = bounds.size.z * 0.5f;
            float y = bounds.size.y * 0.55f;

            // A largura util de cada face. As duas dimensoes sao diferentes — estas
            // caixas sao mais largas do que fundas — e uma letra com o tamanho da
            // face maior transborda da menor.
            float wide = bounds.size.x * 0.78f;
            float narrow = bounds.size.z * 0.78f;
            float tall = bounds.size.y * 0.55f;

            var labelMaterial = GetBoxLabelMaterial(font);
            if (labelMaterial == null)
            {
                Object.DestroyImmediate(holder);
                return;
            }

            AddFace(holder.transform, font, labelMaterial, text, new Vector3(0f, y, -depth - 0.005f), 0f, wide, tall);
            AddFace(holder.transform, font, labelMaterial, text, new Vector3(0f, y, depth + 0.005f), 180f, wide, tall);
            AddFace(holder.transform, font, labelMaterial, text, new Vector3(-half - 0.005f, y, 0f), 90f, narrow, tall);
            AddFace(holder.transform, font, labelMaterial, text, new Vector3(half + 0.005f, y, 0f), 270f, narrow, tall);
        }

        private static Material GetBoxLabelMaterial(Font font)
        {
            const string MaterialPath = "Assets/Pungent/Materials/M_Box_Label.mat";
            var shader = Shader.Find("Pungent/Box Label");
            if (shader == null)
            {
                Debug.LogError("[Prologue] Pungent/Box Label shader is missing.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_Box_Label" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            // TextMesh gets glyph alpha from the built-in font atlas. This material
            // keeps that atlas but, unlike the GUI text shader, obeys scene depth.
            material.SetTexture("_MainTex", font.material.mainTexture);
            material.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Uma face escrita, encolhida ate caber.
        ///
        /// O `TextMesh` nao se ajusta a nada: com um `characterSize` escrito a mao, a
        /// palavra "KITCHEN" saia com um metro e meio de largura numa caixa de meio
        /// metro. Mede-se a malha depois de a gerar e escala-se o transform pelo
        /// factor que falta — que e a unica maneira de o mesmo codigo servir tres
        /// palavras de comprimentos diferentes em caixas de tamanhos diferentes.
        /// </summary>
        private static void AddFace(Transform parent, Font font, Material labelMaterial, string text, Vector3 local, float yaw,
            float maxWidth, float maxHeight)
        {
            var go = new GameObject("Face");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localRotation = Quaternion.Euler(0f, yaw + 180f, 0f);

            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = font;
            mesh.fontSize = 64;
            mesh.characterSize = 0.035f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;

            // Marcador preto em cartao castanho, e nao branco: branco a luz da
            // entrada le-se como interface e nao como uma coisa escrita a mao.
            mesh.color = new Color(0.13f, 0.12f, 0.11f, 1f);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = labelMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Medida depois de o texto e o tipo de letra estarem postos: e ai que a
            // malha existe. Escala pelo eixo que estiver mais apertado.
            var size = renderer.bounds.size;
            if (size.x > 0.0001f && size.y > 0.0001f)
            {
                float fit = Mathf.Min(maxWidth / Mathf.Max(size.x, size.z), maxHeight / size.y);
                if (fit > 0f && fit < 10f) go.transform.localScale = Vector3.one * fit;
            }
        }

        /// <summary>
        /// A chave existe no mundo, em cima do suporte da entrada. O volume e maior
        /// do que a malha porque a chave tem onze centimetros e nao deve exigir
        /// pixel hunting.
        /// </summary>
        private static PrologueKeyPickup BuildKeys(GameObject player, List<string> log)
        {
            var stale = GameObject.Find("PROLOGUE_KEYS");
            if (stale != null) Object.DestroyImmediate(stale);

            var root = new GameObject("PROLOGUE_KEYS");
            Undo.RegisterCreatedObjectUndo(root, "Wire Prologue");

            // Onde a chave fica, perguntado a prateleira e nao escrito a mao.
            //
            // Estava em (5,63 / 1,56 / -2,55), tres numeros fixos de uma prateleira
            // que entretanto e outra: o corpo do movel ocupa z de -2,833 a -2,594,
            // portanto -2,55 esta **quatro centimetros a frente dele**, no ar, com o
            // chao a 1,56 m por baixo. Nada a segurava e nada ia segura-la: a
            // `KeyShelf_Hall_Mesh` nem colisor tem, e por isso nem o teste de queda
            // dava sinal — a chave nao estava a atravessar nada, estava a flutuar.
            //
            // Agora pergunta-se a malha onde estao as tabuas viradas para cima e
            // usa-se a mais alta abaixo da linha dos olhos. Se a prateleira mudar de
            // sitio, de modelo ou de escala, a chave vai atras dela.
            Vector3 spot = new Vector3(5.63f, 1.56f, -2.55f);
            Vector3 board;
            if (ShelfBoard("KeyShelf_Hall_Mesh", 1.75f, out board))
            {
                spot = board;
                log.Add($"  chave assente na tabua da prateleira a y={board.y:F3}");
            }
            else
            {
                log.Add("  AVISO: sem tabuas em KeyShelf_Hall_Mesh; a chave fica onde estava");
            }
            root.transform.position = spot;

            var volume = root.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            // O compartimento tem 27 cm de altura: um volume de 48 atravessava as
            // tabuas de cima e de baixo. Empurrado para a boca da prateleira, que e
            // de onde se aponta — o corredor esta do lado do +z.
            volume.size = new Vector3(0.40f, 0.26f, 0.34f);
            volume.center = new Vector3(0f, 0.07f, 0.12f);

            GameObject visual = null;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(KeyModelPath);
            if (model != null)
            {
                visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.name = "Keys_Visual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;

                // A malha e chata no plano XY — a espessura e o Z, com 4 mm. Rodar
                // 90 graus em X poe essa espessura na vertical e deita a chave no
                // suporte; o guinada e so para ela nao ficar alinhada com o movel,
                // que e como fica uma chave que alguem largou ali.
                visual.transform.localRotation = Quaternion.Euler(90f, 24f, 0f);

                // Escala e centro tirados da malha, e nao a olho.
                //
                // O pivo do FBX esta **fora** da chave: o centro dela cai a 1,5 cm
                // do pivo, portanto poe-la em `localPosition = zero` deixava-a ao
                // lado do volume de interaccao em vez de dentro dele. E a escala
                // fixa que aqui estava valia para o modelo anterior e para mais
                // nenhum — bastava reimportar isto com outras unidades para nascer
                // uma chave do tamanho da porta sem nada a avisar.
                Bounds raw = VisualBounds(visual);
                float longest = Mathf.Max(raw.size.x, Mathf.Max(raw.size.y, raw.size.z));
                if (longest > 0.0001f)
                    visual.transform.localScale = Vector3.one * (KeyLength / longest);

                // Assente na tabua, e nao centrada no ponto: centrar enterrava
                // metade da chave na madeira. Em x e z vai ao meio da tabua, em y
                // pousa. O pivo do FBX esta fora da chave, por isso a correccao e
                // sempre pela malha e nunca pelo transform.
                Bounds placed = VisualBounds(visual);
                visual.transform.position += new Vector3(
                    root.transform.position.x - placed.center.x,
                    root.transform.position.y - placed.min.y,
                    root.transform.position.z - placed.center.z);

                // A unica colisao que interessa e o volume confortavel do root.
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                    collider.enabled = false;

                // O material que vem dentro do FBX e um `lambert2` sem textura no
                // projecto, e um material importado assim nao tem garantia nenhuma
                // de vir no shader do URP. O metal neutro da casa vem.
                var keyMaterial = AssetDatabase.LoadAssetAtPath<Material>(KeyMaterialPath);
                if (keyMaterial != null)
                    foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                        renderer.sharedMaterial = keyMaterial;
            }
            else
            {
                log.Add("  AVISO: key.fbx nao encontrado; o pickup fica invisivel");
            }

            var pickup = root.AddComponent<PrologueKeyPickup>();
            pickup.EditorConfigure(
                player.GetComponent<ChapterDirector>(),
                GameObject.Find("Door_Bedroom_Tomas")?.GetComponent<DoorDragInteractable>(),
                visual);
            log.Add("  chaves no suporte da entrada; o quarto so abre depois de as apanhar");
            return pickup;
        }

        /// <summary>
        /// O ponto de interaccao em cima da secretaria, e o portatil que ja la esta
        /// posto a dormir ate ser pousado.
        /// </summary>
        private static void BuildDesk(Transform parent, List<string> log)
        {
            CarryableBox books = null, tools = null, kitchen = null, clothes = null;
            foreach (Transform child in parent)
            {
                if (child.name == "Box_Books") books = child.GetComponent<CarryableBox>();
                if (child.name == "Box_Tools") tools = child.GetComponent<CarryableBox>();
                if (child.name == "Box_Kitchen") kitchen = child.GetComponent<CarryableBox>();
                if (child.name == "Box_Clothes") clothes = child.GetComponent<CarryableBox>();
            }

            // Estes dois objetos nascem desligados. GameObject.Find ignora objetos
            // inativos e deixava as referencias nulas precisamente quando o passe
            // era repetido; a caixa dizia que estava arrumada mas o portatil nao
            // aparecia.
            GameObject laptop = FindSceneObject("Laptop_Screen");
            GameObject glow = FindSceneObject("Laptop_Glow");

            // As superficies sao perguntadas aos moveis, e nao escritas a mao.
            //
            // Era assim que o destino das ferramentas tinha ido parar ao ar. Usava
            // (-3,75 / 0,88 / -3,78), que e onde esta o **transform vazio**
            // `Dresser_Tomas` — e o corpo da comoda, `Dresser_Tomas_Mesh`, esta em
            // z = -1,71. Dois metros e nove ao lado do movel que o prompt nomeia,
            // com nada nenhum por baixo: medida a sondagem para baixo a partir do
            // destino, nao havia superficie em 1,2 m. O porta-chaves de parede
            // ficava pendurado no meio do quarto e o jogador apontava a um ponto do
            // ar a ouvir "put the tools on the dresser".
            //
            // Um transform vazio nao e o sitio de um movel. O sitio de um movel e
            // onde esta a malha dele.
            Vector3 deskTop, dresserTop, worktop;
            bool hasDesk = SurfaceTop("Desk_Top", out deskTop);
            bool hasDresser = SurfaceTop("Dresser_Tomas_Mesh", out dresserTop);
            bool hasWorktop = SurfaceTop("Kit_Worktop", out worktop);

            if (!hasDesk) { deskTop = new Vector3(-4.50f, 0.84f, -4.65f); log.Add("  AVISO: Desk_Top nao encontrado"); }
            if (!hasDresser) { dresserTop = new Vector3(-3.75f, 0.81f, -1.71f); log.Add("  AVISO: Dresser_Tomas_Mesh nao encontrado"); }
            if (!hasWorktop) { worktop = new Vector3(-3.99f, 1.05f, 4.61f); log.Add("  AVISO: Kit_Worktop nao encontrado"); }

            // O tampo da cozinha corre por cima do forno. O meio dele e a placa, e
            // "put them on the worktop" a apontar para os bicos e uma frase que o
            // sitio desmente. Sessenta centimetros para oeste ficam as gavetas, e
            // ai o tampo esta livre.
            worktop += new Vector3(-0.62f, 0f, 0f);

            // **E entretanto deixou de estar livre.** O `DRESS_CounterFood` veio
            // depois e pousou coisas em cima da bancada; o destino ficou dentro
            // delas, e o relatorio de teste diz que o sitio de pousar a comida
            // "fica dentro de outros assets". Um deslocamento escrito a mao resolve
            // no dia em que se escreve e volta a partir-se na proxima vez que
            // alguem vestir a cozinha.
            //
            // Passa a procurar: anda ao longo do tampo, para leste e para oeste, e
            // fica no primeiro sitio onde nao ha nada. Se nao houver nenhum, avisa
            // e fica onde estava — melhor um destino mau e uma linha na consola do
            // que um destino mau e silencio.
            worktop = FindClearSpot(worktop, new Vector3(1f, 0f, 0f), 0.34f, log, "cozinha");

            GameObject toolVisual = PlaceVisual(
                "Assets/MarioParadiso/Built-In/Prefabs/WrenchHolder.prefab",
                "Unpacked_Tools", dresserTop, 0.38f, 12f);
            GameObject kitchenVisual = PlaceVisual(
                "Assets/Pungent/Meshes/HeldProps/plate.prefab",
                "Unpacked_Kitchenware", worktop, 0.24f, -8f);

            BuildContents(parent, "DESK_SETUP", books,
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Laptop.fbx"),
                laptop, glow, deskTop, new Vector2(0.95f, 0.62f),
                "Take the laptop from the box", "Put the laptop on the desk", "desk_ready",
                "Careful. The screen is the expensive part.",
                "There. That is the room working, at least.");

            BuildLaptopSwitch(laptop, glow, log);

            BuildContents(parent, "TOOLS_SETUP", tools,
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/MarioParadiso/Built-In/Prefabs/WrenchHolder.prefab"),
                toolVisual, null, dresserTop, new Vector2(0.65f, 0.75f),
                "Take the tools from the box", "Put the tools on the dresser", "tools_ready",
                "Sockets, a torch, jump leads. Heavy for things I barely use.",
                "Out of the box. I might even find them again.");

            BuildContents(parent, "KITCHEN_SETUP", kitchen,
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Pungent/Meshes/HeldProps/plate.prefab"),
                kitchenVisual, null, worktop, new Vector2(0.60f, 0.60f),
                "Take the kitchen things from the box", "Put them on the worktop", "kitchen_ready",
                "Two plates and a pan. Mum packed this one.",
                "Kitchen things in the kitchen. A promising start.");

            // **A roupa, e o sitio dela e o mesmo movel das ferramentas.**
            //
            // Nao ha roupeiro do Tomas nesta casa — ha a comoda, e o `Wardrobe_Rui` e
            // dele. Duas coisas a irem para a mesma superficie nao chocam: o
            // `FindClearSpot` afasta a segunda ate haver espaco, e e para isso que ele
            // existe.
            //
            // Isto e a metade do prologo de uma coisa que continua no Dia 1: a roupa
            // entra em casa aqui, e e daqui que sai para a maquina de lavar no
            // "Settle in". Antes aparecia do nada em cima da cama, e a tarefa de a
            // arrumar estava partida.
            // **O `FindClearSpot` nao serve aqui, e a razao e uma armadilha.**
            //
            // As coisas arrumadas ficam **inactivas** ate serem colocadas — e o
            // `BuildContents` que as desliga no fim — e o `FindClearSpot` salta tudo o
            // que esta inactivo. Procurar sitio livre ao pe da comoda devolvia sempre
            // o mesmo ponto das ferramentas, porque as ferramentas ainda nao existiam
            // aos olhos da busca. As duas pilhas ficavam uma dentro da outra.
            //
            // Desvio escrito a mao, ao longo do movel. E um numero, e numeros sao para
            // o dono do projecto julgar a jogar — por isso vai para a consola.
            Vector3 clothesSpot = dresserTop + AlongSurface("Dresser_Tomas_Mesh", 0.34f);

            GameObject clothesVisual = PlaceVisual(
                "Assets/Pungent/Meshes/HeldProps/cloth.prefab",
                "Unpacked_Clothes", clothesSpot, 0.30f, -6f);

            BuildContents(parent, "CLOTHES_SETUP", clothes,
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Pungent/Meshes/HeldProps/cloth.prefab"),
                clothesVisual, null, clothesSpot, new Vector2(0.55f, 0.55f),
                "Take the clothes from the box", "Put the clothes in the drawers", "clothes_ready",
                "Half of these have not fitted me since I was nineteen.",
                "Two drawers is plenty. I own almost nothing.");

            log.Add($"  portatil na secretaria y={deskTop.y:F2}, ferramentas na comoda " +
                    $"{dresserTop.ToString("F2")}, cozinha no tampo {worktop.ToString("F2")}, " +
                    $"roupa em {clothesSpot.ToString("F2")}");
        }

        /// <summary>
        /// O interruptor do portatil — a saida que faltava a um portao que ja existia.
        ///
        /// ---
        ///
        /// **O `PlayerSleep` recusa-se a dormir com qualquer luz acesa**, e nomeia-a:
        /// com o `Laptop_Glow` aceso diz *"Not with the laptop still on."* Essa frase
        /// foi escrita de proposito — alguem quis que o jogador fechasse o portatil
        /// antes de se deitar. **Ninguem construiu a maneira de o fazer.**
        ///
        /// Resultado: pousavas o portatil na secretaria, o brilho acendia, e o prologo
        /// deixava de poder acabar. Sem erro nenhum, com uma frase a explicar
        /// exactamente o que estava mal e nenhuma forma de lhe obedecer.
        ///
        /// **Um portao sem saida e pior do que um bug mudo**, porque o jogador confia
        /// na frase e passa dez minutos a procurar um interruptor que nao existe.
        ///
        /// ---
        ///
        /// **Reaproveita o `LightSwitchInteractable`**, que ja acende e apaga as luzes
        /// da casa toda. Nao ha componente novo: a unica diferenca e que o "quarto"
        /// que ele nomeia e o portatil, e por isso o prompt le-se "Close the laptop"
        /// em vez de "Turn on the light".
        /// </summary>
        private static void BuildLaptopSwitch(GameObject laptop, GameObject glow, List<string> log)
        {
            if (laptop == null || glow == null)
            {
                log.Add("  AVISO: sem Laptop_Screen ou Laptop_Glow — o portatil fica sem interruptor, "
                      + "e o `PlayerSleep` nao deixa dormir depois de ele estar na secretaria.");
                return;
            }

            var light = glow.GetComponent<Light>();
            if (light == null) light = glow.GetComponentInChildren<Light>(true);
            if (light == null)
            {
                log.Add("  AVISO: `Laptop_Glow` sem componente Light.");
                return;
            }

            var existing = laptop.GetComponent<LightSwitchInteractable>();
            if (existing != null) Object.DestroyImmediate(existing, true);

            var toggle = laptop.AddComponent<LightSwitchInteractable>();
            var so = new SerializedObject(toggle);

            var lights = so.FindProperty("lights");
            lights.arraySize = 1;
            lights.GetArrayElementAtIndex(0).objectReferenceValue = light;

            // O prompt sai daqui: o componente escreve "Turn off the <roomName>".
            so.FindProperty("roomName").stringValue = "laptop";

            // Sem parte movel: um portatil nao tem patilha para bascular. Deixar o
            // `toggleTransform` a nulo faz o componente saltar a animacao em vez de
            // rodar o ecra inteiro onze graus.
            so.FindProperty("toggleTransform").objectReferenceValue = null;

            so.ApplyModifiedPropertiesWithoutUndo();

            log.Add("  interruptor do portatil ligado ao `Laptop_Glow` — sem ele o prologo "
                  + "nao acabava");
        }

        /// <summary>
        /// Um desvio ao longo do lado mais comprido de um movel, em metros.
        ///
        /// Duas coisas arrumadas no mesmo tampo tem de ficar lado a lado, e "lado a
        /// lado" depende de como o movel esta virado — a comoda esta rodada 270 graus,
        /// e um desvio escrito em X ou em Z acertava numa metade das vezes. Le-se das
        /// bounds: o eixo horizontal mais comprido e o comprimento do movel.
        /// </summary>
        private static Vector3 AlongSurface(string meshName, float metres)
        {
            var go = GameObject.Find(meshName);
            var renderer = go != null ? go.GetComponentInChildren<Renderer>(true) : null;
            if (renderer == null) return new Vector3(metres, 0f, 0f);

            var size = renderer.bounds.size;
            return size.x >= size.z ? new Vector3(metres, 0f, 0f) : new Vector3(0f, 0f, metres);
        }

        /// <summary>Altura do volume de entrega, em metros.</summary>
        private const float DestinationHeight = 0.44f;

        /// <summary>
        /// O primeiro sitio livre em cima de uma superficie, a partir de um ponto.
        ///
        /// Anda para os dois lados alternadamente — 0, +passo, -passo, +2 passos… —
        /// e devolve o primeiro onde uma caixa do tamanho da entrega nao toca em
        /// nada solido. Alternar importa: procurar so para um lado empurra sempre a
        /// entrega para a ponta do movel, e a ponta de um tampo e onde as coisas
        /// caem.
        ///
        /// **Mede por malhas e nao por colisores**, e essa e a parte que interessa.
        /// A primeira versao usava `OverlapBox` e dava o tampo por livre — enquanto
        /// quatro coisas do `DRESS_CounterFood` estavam pousadas exactamente ali. Um
        /// batatas, uma tangerina, uma cunha de queijo e uma taca de cereais, e
        /// **nenhuma delas tem colisor**, porque sao decoracao. O que o jogador ve e
        /// a malha; a fisica nao sabe que elas existem.
        ///
        /// So conta o que esta **pousado** na superficie — a base entre o tampo e
        /// trinta centimetros acima dele. Sem essa faixa, os armarios de parede que
        /// passam por cima contavam como obstaculo e nao ha tampo nenhum livre em
        /// lado nenhum.
        /// </summary>
        private static Vector3 FindClearSpot(Vector3 start, Vector3 along, float footprint,
            List<string> log, string label)
        {
            along = along.normalized;
            Vector3 half = new Vector3(footprint * 0.5f, DestinationHeight * 0.45f, footprint * 0.5f);

            for (int i = 0; i < 8; i++)
            {
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    if (i == 0 && sign < 0) continue;

                    Vector3 candidate = start + along * (i * footprint * 0.9f * sign);
                    Vector3 centre = candidate + Vector3.up * (DestinationHeight * 0.5f);
                    var space = new Bounds(centre, half * 2f);

                    bool blocked = false;
                    foreach (var renderer in Object.FindObjectsOfType<Renderer>(true))
                    {
                        if (!renderer.gameObject.activeInHierarchy) continue;

                        var b = renderer.bounds;

                        // O que ocupa este sitio, de duas maneiras.
                        //
                        // **Pousado por cima:** a base assenta a altura do tampo. E o
                        // teste que ja existia, e chega para chavenas e tachos.
                        //
                        // **Enfiado no tampo:** um lava-loica nao esta pousado — a
                        // bacia atravessa a bancada e o fundo dela fica vinte
                        // centimetros abaixo. Com o teste antigo, `b.min.y` ficava
                        // longe da altura do tampo e o lava-loica **nao contava como
                        // obstaculo nenhum**: o destino da cozinha ia parar em cima
                        // dele e o prato ficava a flutuar sobre o buraco. Reportado a
                        // jogar, no prologo e no Settle in.
                        //
                        // O proprio tampo continua de fora — acaba a altura do
                        // candidato e nao passa acima dele — e os armarios de parede
                        // tambem, por comecarem acima.
                        bool restsOnTop = b.min.y >= candidate.y - 0.05f
                                       && b.min.y <= candidate.y + 0.30f;
                        bool straddles = b.min.y < candidate.y - 0.05f
                                      && b.max.y > candidate.y + 0.02f;
                        if (!restsOnTop && !straddles) continue;

                        if (!b.Intersects(space)) continue;

                        blocked = true;
                        break;
                    }

                    if (blocked) continue;

                    if (i > 0)
                        log.Add($"  destino da {label} afastado {(i * footprint * 0.9f * sign):F2} m: " +
                                "o sitio de origem estava ocupado");
                    return candidate;
                }
            }

            log.Add($"  AVISO: nao ha sitio livre no tampo para a entrega da {label}. " +
                    "Fica no ponto de origem, provavelmente dentro de outra coisa.");
            return start;
        }

        private static void BuildContents(Transform parent, string destinationName,
            CarryableBox source, GameObject carryPrefab, GameObject placedVisual,
            GameObject secondaryVisual, Vector3 surface, Vector2 footprint, string takePrompt,
            string placePrompt, string doneEvent, string takeThought, string placeThought)
        {
            if (source == null)
            {
                Debug.LogWarning("[Prologue] Caixa em falta para " + destinationName);
                return;
            }

            var task = source.gameObject.AddComponent<BoxContentsTask>();
            task.EditorConfigure(source, carryPrefab, placedVisual, secondaryVisual,
                takePrompt, placePrompt, doneEvent, takeThought, placeThought);

            var destination = new GameObject(destinationName);
            destination.transform.SetParent(parent, false);

            // O volume assenta **em cima** do tampo, e nao a cavalo dele.
            //
            // Estavam centrados a altura do movel, com metade enterrada la dentro.
            // O `PlayerInteractor` marca a oclusao pelo primeiro solido que o cast
            // atravessa e descarta o que estiver atras: com metade do volume dentro
            // do armario, o alvo perdia-se conforme o angulo de aproximacao. Medido
            // no tampo da cozinha, quatro das doze direccoes davam "tapado" a menos
            // de 60 cm. Assente por cima, o que se aponta e o que se ve.
            destination.transform.position = surface + Vector3.up * (DestinationHeight * 0.5f);
            var collider = destination.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(footprint.x, DestinationHeight, footprint.y);
            destination.AddComponent<BoxContentsDestination>().EditorConfigure(task);

            if (placedVisual != null) placedVisual.SetActive(false);
            if (secondaryVisual != null) secondaryVisual.SetActive(false);
        }

        /// <summary>
        /// Um objecto arrumado, **assente** na superficie que recebe.
        ///
        /// Recebia o ponto ja com a altura escrita a mao — 0,94 para o porta-chaves,
        /// 1,10 para o prato — e essas alturas eram palpites sobre moveis que
        /// entretanto mudaram. O prato ficava quatro centimetros acima do tampo e as
        /// ferramentas ficavam a 94 cm de um sitio onde nao havia movel nenhum.
        ///
        /// Agora so o x e o z e que sao escolhidos; a altura sai da malha, depois de
        /// ela ja estar a escala final. E a mesma regra da chave, e pela mesma razao:
        /// o pivo de um prop nao tem de estar dentro dele.
        /// </summary>
        private static GameObject PlaceVisual(string path, string name, Vector3 surface,
            float desiredSize, float yaw)
        {
            var old = FindSceneObject(name);
            if (old != null) Object.DestroyImmediate(old);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            visual.name = name;
            visual.transform.position = surface;
            visual.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            Bounds bounds = VisualBounds(visual);
            float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (largest > 0.001f) visual.transform.localScale *= desiredSize / largest;

            // Medido outra vez: a escala mudou a caixa envolvente toda.
            Bounds scaled = VisualBounds(visual);
            visual.transform.position += new Vector3(
                surface.x - scaled.center.x,
                surface.y - scaled.min.y,
                surface.z - scaled.center.z);

            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            return visual;
        }

        /// <summary>
        /// O centro do topo da malha de um movel.
        ///
        /// Pela malha e nao pelo transform: um `Dresser_Tomas` vazio pode estar a
        /// dois metros do `Dresser_Tomas_Mesh` que se ve, e foi exactamente isso que
        /// pos o destino das ferramentas a pairar no meio do quarto.
        /// </summary>
        private static bool SurfaceTop(string meshName, out Vector3 topCentre)
        {
            topCentre = Vector3.zero;
            var go = FindSceneObject(meshName);
            if (go == null) return false;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            topCentre = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            return true;
        }

        /// <summary>
        /// O centro da tabua virada para cima mais alta abaixo de
        /// <paramref name="maxHeight"/>.
        ///
        /// Um movel de parede nao tem colisor nenhum nesta cena, portanto nao ha
        /// nada para onde deixar cair um objecto: sondar para baixo a partir da
        /// chave respondia "chao, a 1,56 m". A unica fonte de verdade sobre onde
        /// esta a tabua e a propria malha, e e a ela que se pergunta — triangulos
        /// virados para cima, agrupados por altura ao meio centimetro.
        /// </summary>
        private static bool ShelfBoard(string meshName, float maxHeight, out Vector3 centre)
        {
            centre = Vector3.zero;

            var go = FindSceneObject(meshName);
            var filter = go != null ? go.GetComponent<MeshFilter>() : null;
            var mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || mesh.vertexCount == 0) return false;

            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;

            float best = float.MinValue;
            Bounds board = new Bounds();
            bool any = false;

            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector3 a = go.transform.TransformPoint(verts[tris[i]]);
                Vector3 b = go.transform.TransformPoint(verts[tris[i + 1]]);
                Vector3 c = go.transform.TransformPoint(verts[tris[i + 2]]);

                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-8f) continue;
                if (normal.normalized.y < 0.9f) continue;

                float y = (a.y + b.y + c.y) / 3f;
                if (y > maxHeight) continue;

                if (!any || y > best + 0.01f)
                {
                    best = y;
                    board = new Bounds(a, Vector3.zero);
                    board.Encapsulate(b);
                    board.Encapsulate(c);
                    any = true;
                }
                else if (Mathf.Abs(y - best) <= 0.01f)
                {
                    board.Encapsulate(a);
                    board.Encapsulate(b);
                    board.Encapsulate(c);
                }
            }

            if (!any) return false;
            centre = new Vector3(board.center.x, best, board.center.z);
            return true;
        }

        private static GameObject FindSceneObject(string name)
        {
            foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate == null || candidate.name != name) continue;
                if (!candidate.scene.IsValid() || !candidate.scene.isLoaded) continue;
                return candidate;
            }
            return null;
        }

        /// <summary>
        /// O canto do quarto onde as caixas ficam.
        ///
        /// Larga e sem marca no chao: um rectangulo a dizer "pousa aqui" fazia da
        /// mudanca uma tarefa de jogo. Assim o jogador pousa a caixa no quarto e o
        /// quarto aceita-a — falhar so e possivel indo pousa-la a cozinha, o que e
        /// uma decisao e nao um erro.
        /// </summary>
        private static BoxDropZone BuildDropZone(Transform parent)
        {
            var go = new GameObject("BOX_DROP_ZONE");
            go.transform.SetParent(parent, false);
            // O objectivo diz "Bring your boxes in", nao "empilhe-as no canto".
            // A zona antiga cobria apenas o fundo do quarto; pousar as tres junto a
            // porta ou a secretaria parecia correcto, mas nenhuma contava. Este
            // volume cobre a divisao inteira, com uma pequena margem para as paredes.
            go.transform.position = new Vector3(-5.20f, 1.00f, -2.90f);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.50f, 2.00f, 3.80f);

            var zone = go.AddComponent<BoxDropZone>();
            // Quatro desde 11-08: entrou a caixa da roupa. Este numero e a **unica**
            // coisa que decide quando o `boxes_carried` sobe — deixa-lo em tres com
            // quatro caixas no chao trancava o prologo para sempre, sem erro nenhum.
            zone.EditorConfigure("boxes_carried", 4);
            return zone;
        }

        /// <summary>
        /// Uma caixa do set de cartao. Se o modelo faltar, cai num cubo — mais vale
        /// um prologo feio do que um prologo sem caixas para levar.
        /// </summary>
        private static GameObject BoxModel(string child, string name, Transform parent, Material fallback)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(BoxSetPath);
            Transform source = asset != null ? asset.transform.Find(child) : null;

            if (source == null)
            {
                Debug.LogWarning($"[Prologue] Sem '{child}' em {BoxSetPath}; fica um cubo.");
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = name;
                cube.transform.SetParent(parent, false);
                cube.transform.localScale = new Vector3(0.45f, 0.32f, 0.38f);
                if (fallback != null) cube.GetComponent<MeshRenderer>().sharedMaterial = fallback;
                return cube;
            }

            // As tres caixas sao filhas do mesmo FBX, e um filho de uma instancia de
            // prefab nao se deixa reparentar. Instancia-se o conjunto, desembrulha-se,
            // fica-se com a que interessa e deita-se fora o resto.
            var set = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(set, PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);

            Transform wanted = set.transform.Find(child);
            if (wanted == null)
            {
                Object.DestroyImmediate(set);
                Debug.LogWarning($"[Prologue] '{child}' desapareceu depois de desembrulhar.");
                return null;
            }

            var go = wanted.gameObject;
            go.name = name;
            go.transform.SetParent(parent, true);

            // O material so estava a ser posto no cubo de recurso. O modelo a serio
            // ficava com o que viesse do FBX — o branco por omissao do Unity — e foi
            // exactamente isso que se viu a jogar.
            if (fallback != null)
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterial = fallback;

            // O resto do conjunto nao serve. Desactivar antes de destruir, porque o
            // `DestroyImmediate` de um pai leva atras o que ainda estiver pendurado.
            set.SetActive(false);
            Object.DestroyImmediate(set);
            return go;
        }

        private static Bounds VisualBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.4f);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static ThoughtLineSet MakeThoughts(string name, string[] lines)
        {
            string path = $"Assets/Pungent/Dialogue/Thoughts/THT_Prologue_{name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ThoughtLineSet>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ThoughtLineSet>();
                AssetDatabase.CreateAsset(asset, path);
            }
            asset.EditorPopulate(lines, ThoughtLineSet.Order.SequentialLastRepeats, 3.0f);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Material GetCardboardMaterial()
        {
            const string path = "Assets/Pungent/Materials/M_Cardboard.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            // As texturas do set vinham no pacote e **nunca chegaram a ser ligadas**:
            // o material tinha so uma cor base, e a jogar eram tres caixas lisas.
            // Ninguem deu por isso porque o material existia e nao havia erro nenhum
            // — so nao tinha nada dentro.
            Texture(material, "_BaseMap", "cardboard_boxes_albedo");
            Texture(material, "_BumpMap", "cardboard_boxes_normal");
            Texture(material, "_MetallicGlossMap", "cardboard_boxes_orm");
            Texture(material, "_OcclusionMap", "cardboard_boxes_orm");
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_OCCLUSIONMAP");

            // Cor base branca: com a textura ligada, o castanho de antes multiplicava
            // por cima dela e escurecia o cartao ate castanho sujo.
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Liga uma textura do pacote a uma propriedade do material.</summary>
        private static void Texture(Material material, string property, string file)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture>(
                "Assets/ThirdParty/Textures/" + file + ".png");
            if (texture == null)
            {
                Debug.LogWarning($"[Prologue] Textura '{file}' nao encontrada; o cartao fica liso.");
                return;
            }
            material.SetTexture(property, texture);
        }

        /// <summary>
        /// A conversa da chave. A segunda pergunta e a que importa: sai de manha a
        /// que horas. Tudo o que o jogador responder — incluindo nao responder — o
        /// Rui usa depois.
        /// </summary>
        private static DialogueSequenceDefinition BuildRuiConversation()
        {
            var existing = AssetDatabase.LoadAssetAtPath<DialogueSequenceDefinition>(DialoguePath);

            var beats = new[]
            {
                // Ele diz o nome, quem e e o que faz ali, na primeira fala.
                //
                // Antes nao dizia nada disto: o objectivo no HUD dizia "Talk to Rui"
                // e a ficcao nunca chegava a apresenta-lo — o jogador sabia o nome
                // por causa da interface, que e a pior maneira de saber uma coisa.
                // "O outro quarto" e "a chave" dao a relacao e a funcao sem ninguem
                // ter de explicar nada.
                new DialogueSequenceDefinition.Beat
                {
                    Line = "Tomás, right? I'm Rui — I have the other room. Welcome. "
                         + "Here, spare key. The lock sticks if you rush it.",
                    Choices = new[]
                    {
                        new DialogueChoice { Text = "Thanks. Good to meet you.",
                            Reply = "You too. It is quiet here, mostly. Just us two on this floor.",
                            Tone = DialogueTone.Calm },
                        new DialogueChoice { Text = "There is only one spare?",
                            Reply = "There is only one of me.", Tone = DialogueTone.Edgy },
                        new DialogueChoice { Text = "Say nothing",
                            Reply = "…Right. It is on the hook by the door if you want it.",
                            Tone = DialogueTone.Silent },
                    }
                },

                // O anzol. Banal ao ponto de nao se notar.
                new DialogueSequenceDefinition.Beat
                {
                    Line = "What time do you head out in the mornings? So I know not to use the shower.",
                    Choices = new[]
                    {
                        new DialogueChoice { Text = "Early. Tuesdays especially.",
                            Reply = "Tuesdays. Noted.", Tone = DialogueTone.Honest },
                        new DialogueChoice { Text = "It changes. Depends on the week.",
                            Reply = "Sure. I will work it out.", Tone = DialogueTone.Evasive },
                        new DialogueChoice { Text = "Say nothing",
                            Reply = "…No problem. I will just listen for the door.",
                            Tone = DialogueTone.Silent },
                    }
                },

                // A troca de numeros. E daqui que o contacto dele aparece no
                // telemovel — antes disto o jogador tem um telemovel com o pai e
                // mais ninguem, que e exactamente o que uma pessoa tem no primeiro
                // dia numa casa nova. Ver o numero a ser dado faz o telemovel
                // parecer dele e nao um menu.
                //
                // E o Rui que o oferece, nao o Tomas que o pede: quem se oferece
                // primeiro fica com o numero do outro sem o ter pedido.
                new DialogueSequenceDefinition.Beat
                {
                    Line = "Put your number in mine, in case the boiler goes while you are out. "
                         + "I will text you so you have got mine.",
                    UsePhone = true,
                    Choices = new[]
                    {
                        new DialogueChoice { Text = "Sure. Here.",
                            Reply = "Saved. Tomás with the accent on the a, right? Good.",
                            Tone = DialogueTone.Calm },
                        new DialogueChoice { Text = "You can just knock.",
                            Reply = "I will text you anyway. Easier if something happens while you are out.",
                            Tone = DialogueTone.Evasive },
                        new DialogueChoice { Text = "Say nothing",
                            Reply = "I will text you. Then you have got it either way.",
                            Tone = DialogueTone.Silent },
                    }
                },

                new DialogueSequenceDefinition.Beat
                {
                    Line = "Your room is the last door on the left. "
                         + "Take the keys from the stand by the entrance before you go in.",
                }
            };

            var asset = existing;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DialogueSequenceDefinition>();
                AssetDatabase.CreateAsset(asset, DialoguePath);
            }
            asset.EditorPopulate("Rui", beats, 0f, 2.6f);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>
        /// O Rui entra no telemovel porque **ele da o numero**.
        ///
        /// O contacto dele estava no telemovel desde o primeiro segundo do jogo, ao
        /// lado do de um vendedor de pecas que o Tomas so conhece dois dias depois:
        /// a lista nao era uma consequencia da historia, era o indice dos ficheiros.
        ///
        /// Agora e a conversa do prologo que o mete la. Ele diz *"I will text you so
        /// you have got mine"*, e a seguir manda mesmo a mensagem — uma linha banal,
        /// sem resposta possivel, que nao serve para nada excepto o contacto passar
        /// a existir. Ver o numero a chegar faz o telemovel parecer dele e nao um menu.
        ///
        /// A conversa do router muda-se para o fim do Dia 1 pelo mesmo motivo:
        /// esperava por `prologue_talked` e caia **um segundo e meio depois** de ele
        /// se calar — o Rui a perguntar pela internet as onze da noite em que o
        /// Tomas se mudou, para depois as 02:47 nao chegar nada. E a noite das
        /// 02:47 que a pede, e o dono dessa noite e o `day_one_done`.
        /// </summary>
        private static void WireRuiContact(List<string> log)
        {
            var thread = PhoneThreadEditing.Load("PHN_Rui_Router");
            if (thread == null)
            {
                log.Add("  AVISO: PHN_Rui_Router nao encontrado; o contacto do Rui fica " +
                        "como estava. Correr 'Pungent/Dialogue/Build Phone Threads' primeiro.");
                return;
            }

            PhoneThreadEditing.SetUnlock(thread, "prologue_talked");

            const string numberLine = "Rui here. Now you have got it.";
            var steps = PhoneThreadEditing.ReadSteps(thread);

            if (!PhoneThreadEditing.HasLine(thread, numberLine))
            {
                // 22 segundos: o tempo de ele voltar ao quarto dele e escrever. Nao
                // e para chegar durante a conversa.
                steps.Insert(0, PhoneThreadEditing.Message(
                    numberLine, "prologue_talked", 22f, 2f));
                log.Add("  o Rui manda o numero no fim da conversa (e so ai entra no telemovel)");
            }

            foreach (var step in steps)
            {
                if (step == null || step.WaitForEvent != "prologue_talked") continue;
                if (step.Line == numberLine) continue;

                step.WaitForEvent = "day_one_done";
                step.DelaySeconds = Mathf.Max(step.DelaySeconds, 6f);
                log.Add($"  \"{Shorten(step.Line)}\" passa a esperar pelas 02:47 e nao pelo prologo");
            }

            PhoneThreadEditing.Write(thread, thread.ContactName, steps);
        }

        private static string Shorten(string line)
        {
            if (string.IsNullOrEmpty(line)) return string.Empty;
            return line.Length <= 34 ? line : line.Substring(0, 32) + "…";
        }

        private static ChapterDefinition BuildChapter()
        {
            // Reescreve sempre os passos, em vez de devolver o asset intacto.
            //
            // Enquanto isto devolvia o que ja existia, mudar a ordem do prologo aqui
            // no codigo nao mudava nada no jogo: as caixas iam para a entrada e os
            // passos continuavam os antigos, que falavam de as abrir no quarto. O
            // asset e a referencia dele — nao ha texto editado a mao para proteger.
            var existing = AssetDatabase.LoadAssetAtPath<ChapterDefinition>(ChapterPath);

            // Um verbo de cada vez: olhar, andar, carregar, pousar, dormir. E assim
            // que se ensina sem anunciar um tutorial (§5).
            var steps = new[]
            {
                // Primeira entrada: antes de o jogador conhecer a casa, conhece a
                // pessoa que o esta a receber. E aqui que recebe a chave, as
                // indicacoes para o quarto e o numero do Rui.
                new ChapterDefinition.Step
                {
                    Objective = "OBJECTIVE: Talk to Rui.",
                    Thought = string.Empty,
                    Ends = ChapterDefinition.Completion.Event,
                    EventId = "prologue_talked",
                },

                new ChapterDefinition.Step
                {
                    Objective = "OBJECTIVE: Take the keys by the entrance.",
                    Thought = string.Empty,
                    Ends = ChapterDefinition.Completion.Event,
                    EventId = "prologue_keys_taken",
                },

                // Andar. O corredor abaixo, a passar a casa de banho e a porta dele.
                new ChapterDefinition.Step
                {
                    Objective = "OBJECTIVE: Find your room.",
                    Thought = "Last door on the left, he said.",
                    Ends = ChapterDefinition.Completion.ReachArea,
                    Place = new Vector3(-4.90f, 0f, -3.60f),
                    Radius = 2.0f,
                    RaiseOnComplete = new[] { "prologue_room_found" },
                },

                // Carregar. Tres viagens pelo mesmo corredor — e o coracao do
                // prologo, ainda que pareca a parte aborrecida.
                new ChapterDefinition.Step
                {
                    Objective = "OBJECTIVE: Carry all four boxes to your room.",
                    SideObjective = "Drop each box anywhere inside the room",
                    Thought = string.Empty,
                    Ends = ChapterDefinition.Completion.Event,
                    EventId = "boxes_carried",
                    RaiseOnComplete = new[] { "prologue_unpacked" },
                },

                // Pousar. O portatil sai da caixa e vai para a secretaria — a mesma
                // secretaria onde a internet vai cair as 02:47 no Dia 2. Monta-la a
                // mao e o que faz isso doer.
                // **Cada passo diz a coisa, o sitio e a caixa.**
                //
                // Dois dos tres nem sequer diziam onde — "Put your tools away" e
                // "Unpack the kitchen box" mandavam arrumar sem dizer para onde. E
                // nenhum dizia de que caixa, com tres cubos identicos a entrada: a
                // unica maneira de saber era encostar-se a cada um e ler o prompt.
                //
                // A linha secundaria nomeia a caixa pelo que agora esta **escrito
                // nela** (ver `LabelTheBox`), e nao por uma cor ou uma posicao. O
                // jogador le a mesma palavra nos dois sitios.
                new ChapterDefinition.Step
                {
                    Objective = "OBJECTIVE: Put the laptop on the desk.",
                    SideObjective = "It is in the box marked DESK",
                    Thought = string.Empty,
                    Ends = ChapterDefinition.Completion.Event,
                    EventId = "desk_ready",
                },

                new ChapterDefinition.Step
                {
                    Objective = "OBJECTIVE: Put the tools on the dresser in your room.",
                    SideObjective = "The box marked TOOLS",
                    Ends = ChapterDefinition.Completion.Event,
                    EventId = "tools_ready",
                },

                // A quarta caixa, acrescentada a 11-08. Fica **antes** da da cozinha
                // de proposito: e a da cozinha que faz o Rui aparecer ao vao, e o Rui
                // tem de continuar a ser a ultima coisa que acontece neste bloco.
                new ChapterDefinition.Step
                {
                    Objective = "OBJECTIVE: Put your clothes in the drawers.",
                    SideObjective = "The box marked CLOTHES",
                    Ends = ChapterDefinition.Completion.Event,
                    EventId = "clothes_ready",
                },

                new ChapterDefinition.Step
                {
                    Objective = "OBJECTIVE: Put the kitchen things on the worktop.",
                    SideObjective = "The box marked KITCHEN",
                    Ends = ChapterDefinition.Completion.Event,
                    EventId = "kitchen_ready",
                    RaiseOnComplete = new[] { "prologue_rui_arrives" },
                },

                // Ele fica ao vao. O objectivo explicita o gesto para o jogador nao
                // confundir a imobilidade do Rui com outro bloqueio do fluxo.
                new ChapterDefinition.Step
                {
                    Objective = "OBJECTIVE: Close your bedroom door.",
                    Thought = "Rui is standing in my doorway.",
                    Ends = ChapterDefinition.Completion.Event,
                    EventId = "prologue_door_closed",
                },

                new ChapterDefinition.Step
                {
                    Objective = "OBJECTIVE: Lock your bedroom door.",
                    Thought = "The lock sticks. Lift, then turn.",
                    Ends = ChapterDefinition.Completion.Event,
                    EventId = "prologue_door_locked",
                },

                new ChapterDefinition.Step
                {
                    Objective = "OBJECTIVE: Turn off the light and sleep.",
                    Thought = "First night. I should get some sleep.",
                    Ends = ChapterDefinition.Completion.Event,
                    EventId = "prologue_slept",
                    RaiseOnComplete = new[] { "prologue_done" },
                },
            };

            // Reaproveita o asset se existir, para as referencias que apontam para
            // ele — a lista do `ChapterDirector`, por exemplo — nao ficarem nulas.
            var asset = existing;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ChapterDefinition>();
                AssetDatabase.CreateAsset(asset, ChapterPath);
            }

            asset.EditorPopulate("Prologue - The message", string.Empty, steps);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static PrologueStage BuildStage(
            GameObject player,
            GameObject boxes,
            PrologueKeyPickup keys,
            List<string> log)
        {
            // Procurado em toda a cena e nao so no jogador.
            //
            // O `PrologueStage` vivia no `PlayerRoot`; mudou-se para o
            // `APARTMENT_SYSTEMS` quando o jogador se partiu em prefab e sistemas.
            // Enquanto isto foi `player.GetComponent`, cada corrida criava um
            // segundo — dois a encenar o mesmo prologo, e era o velho a ganhar,
            // porque tinha as posicoes antigas e punha o Tomas a nascer no quarto.
            // A casa do `PrologueStage` e o `APARTMENT_SYSTEMS`, e nao o jogador.
            //
            // O `PlayerRoot` e hoje o prefab PLAYER, que entra em todas as cenas: a
            // encenacao do prologo ia atras dele para a garagem e para a estrada, a
            // tentar por o Tomas a nascer na entrada de um apartamento que nao esta
            // la. O prologo so acontece nesta casa e pertence aos sistemas dela.
            var host = GameObject.Find("APARTMENT_SYSTEMS") ?? player;

            PrologueStage stage = host.GetComponent<PrologueStage>();
            foreach (var other in Object.FindObjectsOfType<PrologueStage>(true))
            {
                if (other.gameObject == host) { stage = other; continue; }
                log.Add("  PrologueStage fora de sitio removido de " + other.gameObject.name);
                Object.DestroyImmediate(other);
            }

            if (stage == null)
            {
                stage = Undo.AddComponent<PrologueStage>(host);
                log.Add("  PrologueStage criado em " + host.name);
            }

            var so = new SerializedObject(stage);
            so.FindProperty("deskOpening").objectReferenceValue = player.GetComponent<PlayerDeskOpening>();
            so.FindProperty("intro").objectReferenceValue =
                Object.FindObjectOfType<IntroTextSequence>(true);
            so.FindProperty("openingQuest").objectReferenceValue =
                Object.FindObjectOfType<OpeningQuestDirector>(true);
            so.FindProperty("boxes").objectReferenceValue = boxes;
            so.FindProperty("keyPickup").objectReferenceValue = keys;
            so.FindProperty("balconyDoor").objectReferenceValue =
                GameObject.Find("Door_Balcony")?.GetComponent<DoorDragInteractable>();
            so.FindProperty("router").objectReferenceValue =
                Object.FindObjectOfType<RouterInteractable>(true);
            so.FindProperty("rui").objectReferenceValue = GameObject.Find("NPC_Rui");
            so.FindProperty("ruiRoomTerritory").objectReferenceValue =
                Object.FindObjectOfType<NpcRoomTerritory>(true);
            so.FindProperty("prologueScript").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<DialogueSequenceDefinition>(DialoguePath);
            so.FindProperty("nightScript").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<DialogueSequenceDefinition>(NightDialoguePath);

            // Escritos aqui e nao deixados ao valor por omissao do componente.
            // Mudar o `= new Vector3(...)` no C# nao mexe numa instancia que ja
            // esteja na cena: o valor serializado ganha sempre, e o prologo
            // continuava a comecar no quarto por muito que o codigo dissesse o
            // contrario. E das armadilhas mais silenciosas do Unity.
            so.FindProperty("startPosition").vector3Value = new Vector3(5.55f, 0.05f, -2.05f);
            so.FindProperty("startYaw").floatValue = 270f;
            so.FindProperty("ruiHallSpot").vector3Value = new Vector3(3.20f, 0f, -1.60f);
            so.FindProperty("ruiHallYaw").floatValue = 90f;
            // Usa o mesmo ponto seguro do anchor RUI_CorridorMid. O valor antigo
            // (z = -0.90) ficava dentro da folha da porta do quarto do Rui.
            so.FindProperty("ruiCorridorSpot").vector3Value = new Vector3(-1.60f, 0f, -0.05f);
            so.FindProperty("ruiCorridorYaw").floatValue = 180f;
            so.FindProperty("ruiDoorwaySpot").vector3Value = new Vector3(-4.60f, 0f, -1.05f);
            so.FindProperty("ruiYaw").floatValue = 180f;
            so.FindProperty("toCorridorEvent").stringValue = "prologue_unpacked";
            so.FindProperty("toDoorwayEvent").stringValue = "prologue_rui_arrives";

            var lights = new List<Light>();
            foreach (var name in new[] { "Bedroom_Light", "Corridor_Light", "Kitchen_Light" })
            {
                var go = GameObject.Find(name);
                var light = go != null ? go.GetComponent<Light>() : null;
                if (light != null) lights.Add(light);
            }
            var lp = so.FindProperty("lightsOn");
            lp.arraySize = lights.Count;
            for (int i = 0; i < lights.Count; i++)
                lp.GetArrayElementAtIndex(i).objectReferenceValue = lights[i];
            so.ApplyModifiedProperties();

            // A conversa acontece logo a entrada, mas fechar a porta so conta muito
            // depois, quando o Rui reaparece no vao no fim da montagem da secretaria.
            // O tracker tem de distinguir esses dois momentos.
            var tracker = Object.FindObjectOfType<PrologueTracker>(true);
            if (tracker == null)
            {
                tracker = Undo.AddComponent<PrologueTracker>(host);
                log.Add("  PrologueTracker criado em " + host.name);
            }

            var trackerSo = new SerializedObject(tracker);
            trackerSo.FindProperty("director").objectReferenceValue =
                player.GetComponent<ChapterDirector>();
            trackerSo.FindProperty("stage").objectReferenceValue = stage;
            trackerSo.FindProperty("conversation").objectReferenceValue =
                GameObject.Find("NPC_Rui")?.GetComponent<RuiStoryConversation>();
            trackerSo.FindProperty("bedroomDoor").objectReferenceValue =
                GameObject.Find("Door_Bedroom_Tomas")?.GetComponent<DoorDragInteractable>();
            trackerSo.FindProperty("doorAvailableEvent").stringValue = "prologue_rui_arrives";
            trackerSo.FindProperty("sleep").objectReferenceValue =
                Object.FindObjectOfType<PlayerSleep>(true);
            trackerSo.ApplyModifiedProperties();

            // A abertura a secretaria e o texto deixam de arrancar sozinhos: quem
            // manda agora e o prologo.
            var desk = player.GetComponent<PlayerDeskOpening>();
            if (desk != null)
            {
                var dso = new SerializedObject(desk);
                dso.FindProperty("playOnStart").boolValue = false;
                dso.ApplyModifiedProperties();
                log.Add("  PlayerDeskOpening.playOnStart desligado — o prologo e que o arranca");
            }

            var intro = Object.FindObjectOfType<IntroTextSequence>(true);
            if (intro != null)
            {
                var iso = new SerializedObject(intro);
                iso.FindProperty("playOnStart").boolValue = false;

                // Keep the authored opening in this root's owner as well as in the
                // runtime default. Rebuilding the prologue must never resurrect the
                // removed car premise.
                var introLines = new[]
                {
                    "Four hundred a month, bills included.",
                    "That was the whole reason I took it.",
                    string.Empty,
                    "New city, new school. On Tuesdays, thirty",
                    "fifteen-year-olds who could tell I was nervous.",
                    string.Empty,
                    "My father paid half the deposit and called it a loan.",
                    "He told me three times to lock the door.",
                    "I told him I had heard him the first time.",
                    string.Empty,
                    "Rui had the room next to mine.",
                    "He was polite. He carried a box in for me.",
                };
                var linesProperty = iso.FindProperty("lines");
                linesProperty.arraySize = introLines.Length;
                for (int i = 0; i < introLines.Length; i++)
                    linesProperty.GetArrayElementAtIndex(i).stringValue = introLines[i];

                iso.ApplyModifiedProperties();
                log.Add("  IntroTextSequence.playOnStart desligado — o texto passa para o prologo");
            }

            log.Add($"  {lights.Count} luzes acesas nesta noite");
            return stage;
        }
    }
}
