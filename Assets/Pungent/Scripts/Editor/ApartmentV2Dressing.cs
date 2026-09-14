using System.Collections.Generic;
using System.Linq;
using StringComparer = System.StringComparer;
using Pungent.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Mobila o Apartment_Blockout_V2 com o pack Quaternius Ultimate House Interior (CC0).
    /// Re-executavel: destroi e reconstroi apenas o root ART_PASS_V2.
    ///
    /// Convencao de orientacao validada visualmente em 2026-07-26:
    /// os FBX sao Z-up, pelo que a rotacao base e Euler(270, yaw, 0) e, com yaw = 0,
    /// o objeto fica virado para +Z (norte) com as costas em -Z.
    ///   yaw 0   -> costas a sul  (virado a norte)
    ///   yaw 90  -> costas a oeste (virado a este)
    ///   yaw 180 -> costas a norte (virado a sul)
    ///   yaw 270 -> costas a este  (virado a oeste)
    ///
    /// Os colliders sao BoxCollider simples derivados das bounds da malha, conforme o
    /// criterio de aceitacao do plano mestre ("colliders simples").
    /// </summary>
    public static class ApartmentV2Dressing
    {
        public const string RootName = "ART_PASS_V2";
        private const string ModelFolder = "Assets/ThirdParty/Quaternius/UltimateHouseInterior/FBX/";

        private const float CounterZ = 4.64f;    // linha da bancada da cozinha
        private const float CounterTop = 1.02f;

        private struct Prop
        {
            public string Model;
            public string Name;
            public float Scale;
            public Vector3 Position;
            public float Yaw;
            public bool NoCollider;
        }

        private static readonly List<Prop> Props = new List<Prop>();
        private static Transform _currentGroup;

        // These visuals were removed manually from the current apartment on
        // 2026-08-11. Keep their wrappers and colliders because story wiring still
        // targets several of them, but never recreate the visible prefab child.
        private static readonly HashSet<string> ManuallyRemovedVisuals =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Living_FloorLamp", "Carpet_Tomas", "Bookshelf_Tomas",
                "Dining_Chair_N", "Kit_Table", "Living_Armchair", "Carpet_Rui",
                "Kit_Chair_A", "Dining_Shelf", "Shelf_Storage", "Balcony_Stool",
                "Kit_Chair_B", "Dining_Carpet", "Living_Bookshelf", "Dining_Chair_S"
            };

        /// <summary>
        /// O que esta ferramenta escreveu da ultima vez. Sem isto ela nao consegue
        /// distinguir o que e dela do que e de quem trabalhou na cena a mao — e por
        /// nao conseguir distinguir, apagava tudo.
        /// </summary>
        private const string ManifestPath = "Assets/Pungent/Narrative/ART_PASS_V2_manifesto.txt";

        /// <summary>Posto pela entrada "forcar". Volta a falso no fim, sempre.</summary>
        private static bool forced;

        /// <summary>
        /// Pode esta execucao apagar o que esta na cena?
        ///
        /// So se tudo o que la esta tiver sido posto por ela. Qualquer objecto que nao
        /// conste do manifesto e trabalho de outra pessoa, e apagar trabalho de outra
        /// pessoa sem avisar foi o que custou um dia inteiro a 11-08.
        ///
        /// **Sem manifesto, recusa.** A primeira vez que isto correr depois desta
        /// mudanca nao ha lista nenhuma para comparar — e nesse caso o mais provavel e
        /// que a cena tenha trabalho a mao, porque teve sempre.
        /// </summary>
        /// <summary>
        /// Grava a lista do que esta execucao criou. E o que permite a proxima
        /// distinguir o trabalho dela do trabalho de uma pessoa.
        /// </summary>
        private static void WriteManifest(GameObject root)
        {
            var lines = new List<string>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root.transform) continue;
                lines.Add(string.Join("|", RelPath(root.transform, t),
                    Fmt(t.localPosition), Fmt(t.localEulerAngles), Fmt(t.localScale)));
            }

            var text = "# Escrito pelo `Dress Apartment V2`. Nao editar a mao.\n"
                     + "# caminho|posicao|rotacao|escala — como a ferramenta os deixou.\n"
                     + "# A execucao seguinte compara a cena com isto e **recusa** se algo\n"
                     + "# tiver sido acrescentado, apagado ou movido a mao.\n"
                     + string.Join("\n", lines.OrderBy(l => l, StringComparer.Ordinal));

            System.IO.File.WriteAllText(ManifestPath, text);
            AssetDatabase.ImportAsset(ManifestPath);
        }

        /// <summary>Caminho a partir da raiz. Ha nomes repetidos — "Mug White" aparece
        /// cinco vezes — e um nome sozinho nao identifica um objecto.</summary>
        private static string RelPath(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (var c = t; c != null && c != root; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string Fmt(Vector3 v) =>
            v.x.ToString("F3") + "," + v.y.ToString("F3") + "," + v.z.ToString("F3");

        private static bool Same(string recorded, Vector3 now, float tolerance)
        {
            var p = recorded.Split(',');
            if (p.Length != 3) return true;
            if (!float.TryParse(p[0], out float x)) return true;
            if (!float.TryParse(p[1], out float y)) return true;
            if (!float.TryParse(p[2], out float z)) return true;
            return Mathf.Abs(x - now.x) <= tolerance
                && Mathf.Abs(y - now.y) <= tolerance
                && Mathf.Abs(z - now.z) <= tolerance;
        }

        private static bool MayOverwrite(GameObject root)
        {
            var current = new List<string>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root.transform) current.Add(t.name);

            var manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(ManifestPath);
            if (manifest == null)
            {
                Debug.LogError(
                    "[DressV2] **RECUSADO.** Nao ha manifesto — esta ferramenta nunca declarou o "
                  + "que escreveu, por isso nao sei distinguir o que e dela do que e teu. O "
                  + $"`{RootName}` tem {current.Count} objectos e correr agora apagava-os todos.\n\n"
                  + "Se o que la esta e mesmo so trabalho desta ferramenta, corre "
                  + "'Dress Apartment V2 (forcar)' uma vez: ela reconstroi e passa a partir dai a "
                  + "declarar o que escreveu, e este aviso nao volta.\n\n"
                  + "Se tens trabalho a mao no apartamento — moveste, apagaste ou acrescentaste "
                  + "props — **nao forces**. Diz o que mudaste para ir para a tabela primeiro.");
                return false;
            }

            // **Tres perguntas, e nao uma.**
            //
            // A primeira versao disto so procurava objectos **a mais** — e por isso nao
            // teria travado nada do que aconteceu a 11-08. Quem apaga um movel a mao
            // nao deixa um estranho na cena: deixa um buraco. Quem move um movel nao
            // deixa nada. Nos dois casos a ferramenta corria e desfazia o trabalho, e o
            // travao ficava a olhar.
            var known = new Dictionary<string, string[]>(StringComparer.Ordinal);
            foreach (var line in manifest.text.Split('\n'))
            {
                var clean = line.Trim();
                if (clean.Length == 0 || clean[0] == '#') continue;
                var parts = clean.Split('|');
                if (parts.Length == 4) known[parts[0]] = parts;
            }

            var live = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root.transform) live[RelPath(root.transform, t)] = t;

            var strangers = live.Keys.Where(p => !known.ContainsKey(p)).ToList();
            var deleted = known.Keys.Where(p => !live.ContainsKey(p)).ToList();
            var moved = new List<string>();

            foreach (var pair in known)
            {
                if (!live.TryGetValue(pair.Key, out var t)) continue;
                if (!Same(pair.Value[1], t.localPosition, 0.01f)
                 || !Same(pair.Value[2], t.localEulerAngles, 1.0f)
                 || !Same(pair.Value[3], t.localScale, 0.01f))
                    moved.Add(pair.Key);
            }

            if (strangers.Count == 0 && deleted.Count == 0 && moved.Count == 0) return true;

            var report = new System.Text.StringBuilder();
            report.AppendLine($"[DressV2] **RECUSADO.** O `{RootName}` nao esta como esta ferramenta o "
                            + "deixou. Correr agora desfazia isto, e nao ha como o trazer de volta:");
            Section(report, "acrescentados a mao (seriam apagados)", strangers);
            Section(report, "apagados a mao (seriam ressuscitados)", deleted);
            Section(report, "movidos, rodados ou redimensionados a mao (voltariam ao sitio antigo)", moved);
            report.AppendLine();
            report.AppendLine("Leva estas mudancas para a tabela desta ferramenta, ou corre "
                            + "'Dress Apartment V2 (forcar)' se tiveres mesmo a certeza de que podem morrer.");

            Debug.LogError(report.ToString());
            return false;
        }

        private static void Section(System.Text.StringBuilder into, string title, List<string> items)
        {
            if (items.Count == 0) return;
            into.AppendLine();
            into.AppendLine($"  {items.Count} {title}:");
            foreach (var item in items.Take(25)) into.AppendLine("    " + item);
            if (items.Count > 25) into.AppendLine($"    ... e mais {items.Count - 25}");
        }

        /// <summary>
        /// A saida de emergencia. Existe porque um travao sem saida acaba comentado
        /// por alguem com pressa — e nesse dia deixa de haver travao nenhum.
        ///
        /// O nome diz o que faz. Quem o carrega nao pode dizer que nao sabia.
        /// </summary>
        [MenuItem("Pungent/Blockout/Dress Apartment V2 (forcar, apaga trabalho manual)", false, 21)]
        public static void DressForced()
        {
            forced = true;
            Dress();
        }

        [MenuItem("Pungent/Blockout/Dress Apartment V2", false, 20)]
        public static void Dress()
        {
            if (BuildGuard.Blocked("DressV2")) return;

            var scene = EditorSceneManager.GetActiveScene();
            var existing = FindRoot(scene);

            // **O travao.**
            //
            // A 11-08 esta ferramenta apagou o trabalho de um dia inteiro de arrumacao
            // a mao. Nao foi um acidente de codigo: ela reconstroi o `ART_PASS_V2` a
            // partir de uma tabela escrita aqui dentro, e a tabela nao sabe nada do
            // que alguem tenha acrescentado, movido ou apagado na cena. Correr era
            // sempre destruir, e ninguem avisava.
            //
            // **A ferramenta passa a declarar o que escreveu.** No fim de cada
            // execucao grava a lista dos objectos que criou. Na execucao seguinte
            // compara: se houver na cena coisas que ela nao pos la, para e diz quais.
            //
            // Isto e a mesma ideia que o `Verificar tudo` diz que falta em todo o
            // projecto — *"nenhuma ferramenta declara o que escreveu, por isso nao ha
            // contra o que comparar"*. E a primeira que passa a declarar.
            if (existing != null && !forced && !MayOverwrite(existing))
            {
                forced = false;
                return;
            }

            if (existing != null)
                Object.DestroyImmediate(existing);

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Dress Apartment V2");

            BuildBedroomTomas(Group(root, "Bedroom_Tomas"));
            BuildBedroomRui(Group(root, "Bedroom_Rui"));
            BuildBathroom(Group(root, "Bathroom"));
            BuildLaundry(Group(root, "Laundry"));
            BuildHall(Group(root, "Hall"));
            BuildStorage(Group(root, "Storage"));
            BuildKitchen(Group(root, "Kitchen"));
            BuildDining(Group(root, "Dining"));
            BuildLiving(Group(root, "Living"));
            BuildBalcony(Group(root, "Balcony"));
            BuildRuiAnchors(Group(root, "RUI_ANCHORS"));
            ApplyFlavour(root);

            EditorSceneManager.MarkSceneDirty(scene);
            WriteManifest(root);
            forced = false;

            Debug.Log($"[DressV2] {root.GetComponentsInChildren<MeshRenderer>(true).Length} renderers colocados.");
        }

        public static GameObject FindRoot(UnityEngine.SceneManagement.Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == RootName)
                    return go;
            return null;
        }

        // ------------------------------------------------------------------
        // Divisoes
        // ------------------------------------------------------------------

        private static void BuildBedroomTomas(Transform g)
        {
            _currentGroup = g;
            // Cama com a cabeceira encostada a parede oeste.
            P("Bed_Single", "Bed_Tomas", 0.56f, -5.90f, -3.70f, 90f);
            P("NightStand_1", "NightStand_Tomas", 0.55f, -6.60f, -2.75f, 90f);
            // A secretaria e o portatil vem do set dressing original, reposicionados sob a janela sul.
            P("Chair_2", "Chair_Desk_Tomas", 0.58f, -4.45f, -4.05f, 180f);
            P("Bookshelf", "Bookshelf_Tomas", 0.40f, -3.55f, -2.00f, 270f);
            P("Drawer_1", "Dresser_Tomas", 0.60f, -3.75f, -3.80f, 270f);
            P("Carpet_1", "Carpet_Tomas", 0.60f, -5.00f, -2.60f, 0f, true);
            P("Trashcan_Small1", "Trash_Tomas", 0.55f, -4.20f, -4.70f, 0f);
            P("Houseplant_3", "Plant_Tomas", 0.70f, -3.70f, -1.20f, 0f);
            Ceiling("Light_Ceiling1", "Light_Tomas", 0.55f, -5.20f, -2.90f);
        }

        private static void BuildBedroomRui(Transform g)
        {
            _currentGroup = g;
            P("Bed_Single", "Bed_Rui", 0.56f, -2.85f, -3.90f, 0f);
            P("NightStand_2", "NightStand_Rui", 0.55f, -1.95f, -4.55f, 0f);
            P("Drawer_2", "Wardrobe_Rui", 0.50f, -0.42f, -3.00f, 270f);
            P("Bookshelf", "Bookshelf_Rui", 0.40f, -0.35f, -1.45f, 270f);
            P("Carpet_2", "Carpet_Rui", 0.40f, -2.00f, -2.30f, 0f, true);
            P("Trashcan_Small2", "Trash_Rui", 0.55f, -3.15f, -1.30f, 0f);
            Ceiling("Light_Ceiling2", "Light_Rui", 0.55f, -1.75f, -2.90f);
        }

        private static void BuildBathroom(Transform g)
        {
            _currentGroup = g;
            // Banheira na parede este e loucas na parede sul: liberta a faixa central
            // de circulacao de que a NavMesh precisa.
            P("Bathroom_Toilet", "Toilet", 0.42f, 0.35f, -2.62f, 0f);
            // O lavatorio fica a este da porta da lavandaria, que tem de ficar desimpedida.
            P("Bathroom_Sink", "Sink_Bath", 0.55f, 1.95f, -2.77f, 0f);
            P("Bathroom_Bathtub", "Bathtub", 0.44f, 2.05f, -1.66f, 0f);
            // Com collider: o espelho e um dos objetos que responde com um pensamento.
            P("Bathroom_Mirror1", "Mirror_Bath", 0.90f, 1.95f, -2.90f, 0f, false, 1.25f);
            P("Bathroom_Towel", "Towel_Bath", 0.55f, 0.05f, -1.80f, 90f, true, 1.35f);
            Ceiling("Light_Ceiling5", "Light_Bath", 0.55f, 1.15f, -1.90f);
        }

        private static void BuildLaundry(Transform g)
        {
            _currentGroup = g;
            // Tudo encostado ao sul e ao este: a faixa junto a porta fica livre.
            P("Bathroom_WashingMachine", "WashingMachine", 0.55f, 0.40f, -4.60f, 0f);
            P("Trashcan_Large", "Basket_Laundry", 0.50f, 1.40f, -4.60f, 0f);
            P("Shelf_1", "Shelf_Laundry", 0.50f, 2.20f, -4.55f, 270f);
            P("Bathroom_ToiletPaperPile", "Supplies_Laundry", 0.70f, 2.15f, -3.25f, 0f);
            Ceiling("Light_Ceiling6", "Light_Laundry", 0.55f, 1.15f, -4.00f);
        }

        private static void BuildHall(Transform g)
        {
            _currentGroup = g;
            // Sapateira na parede oeste do hall: a sul ficava em frente a porta dos arrumos.
            P("Drawer_5", "ShoeCabinet_Hall", 0.50f, 2.70f, -1.60f, 90f);
            P("Shelf_Small2", "KeyShelf_Hall", 0.60f, 5.62f, -2.80f, 270f, true, 1.10f);
            // Canto sudoeste do hall: a meio da passagem cortava o percurso corredor -> router.
            P("Houseplant_6", "Plant_Hall", 0.65f, 2.70f, -2.60f, 0f);
            P("Trashcan_Small2", "Trash_Hall", 0.50f, 5.45f, -2.85f, 0f);
            Ceiling("Light_Ceiling3", "Light_Hall", 0.55f, 4.10f, -1.80f);
            // Quadro eletrico junto ao router, na parede este.
            //
            // **Estava pequeno de mais** — 42 x 32 cm, que e um quadro de casa de
            // banho e nao o de uma casa inteira. Reportado a jogar: "way too small on
            // the wall". Passa a 58 x 44, que e a medida de um quadro real de seis a
            // oito disjuntores, e desce cinco centimetros para ficar a altura da mao.
            //
            // Nao e so decoracao: o Dia 3 tem um `AttentionSnap` e um `WitnessedCue`
            // apontados a este objecto. Uma coisa a que a camara se vira sozinha tem
            // de se ler como uma coisa, e nao como uma marca na parede.
            Box(g, "ElectricalPanel", new Vector3(5.68f, 1.70f, -0.55f), new Vector3(0.10f, 0.58f, 0.44f),
                "Assets/Pungent/Materials/M_Plastic_Beige.mat");
        }

        private static void BuildStorage(Transform g)
        {
            _currentGroup = g;
            // Encostado as paredes para deixar livre a entrada vinda do hall.
            P("Shelf_Large", "Shelf_Storage", 0.40f, 3.30f, -4.82f, 0f);
            P("Drawer_4", "Drawer_Storage", 0.55f, 4.95f, -4.55f, 0f);
            P("Trashcan_Large", "Bin_Storage", 0.55f, 2.75f, -3.35f, 0f);
            Box(g, "Box_Storage_A", new Vector3(5.35f, 0.18f, -3.40f), new Vector3(0.55f, 0.36f, 0.45f),
                "Assets/Pungent/Materials/M_Wood_Dark.mat");
            Box(g, "Box_Storage_B", new Vector3(5.40f, 0.52f, -3.45f), new Vector3(0.42f, 0.32f, 0.38f),
                "Assets/Pungent/Materials/M_Wood_Dark.mat");
            Ceiling("Light_Ceiling6", "Light_Storage", 0.55f, 4.10f, -4.00f);
        }

        private static void BuildKitchen(Transform g)
        {
            _currentGroup = g;
            // Conjunto continuo ao longo da parede norte (viradas a sul => yaw 180).
            P("Kitchen_Cabinet1", "Kit_Cabinet_A", 0.64f, -5.95f, CounterZ, 180f);
            P("Kitchen_Sink", "Kit_Sink", 0.64f, -5.28f, CounterZ, 180f);
            P("Kitchen_1Drawers", "Kit_Drawers", 0.64f, -4.61f, CounterZ, 180f);
            P("Kitchen_Oven", "Kit_Oven", 0.64f, -3.94f, CounterZ, 180f);
            P("Kitchen_CabinetSmall", "Kit_Cabinet_B", 0.64f, -3.35f, CounterZ, 180f);
            // Frigorifico na parede oeste, porta virada a este.
            P("Kitchen_Fridge", "Kit_Fridge", 0.64f, -6.64f, 3.90f, 90f);

            // Bancada continua e armarios superiores.
            Box(g, "Kit_Worktop", new Vector3(-4.66f, CounterTop, CounterZ), new Vector3(3.24f, 0.05f, 0.74f),
                "Assets/Pungent/Materials/M_Wood_Dark.mat");
            Box(g, "Kit_UpperCabinets", new Vector3(-3.75f, 1.78f, 4.82f), new Vector3(1.45f, 0.62f, 0.35f),
                "Assets/Pungent/Materials/M_Wood_Dark.mat");
            Box(g, "Kit_Backsplash", new Vector3(-4.66f, 1.30f, 4.97f), new Vector3(3.24f, 0.52f, 0.04f),
                "Assets/Pungent/Materials/M_PH_Wall_Tile.mat");

            // Mesa pequena de pequeno-almoco, afastada da linha de passagem para a porta.
            P("Table_RoundSmall", "Kit_Table", 0.55f, -4.10f, 1.95f, 0f);
            P("Chair_1", "Kit_Chair_A", 0.58f, -4.85f, 1.95f, 90f);
            P("Chair_1", "Kit_Chair_B", 0.58f, -3.35f, 1.95f, 270f);
            P("Trashcan_Cylindric", "Kit_Trash", 0.55f, -3.35f, 3.30f, 0f);
            P("Houseplant_5", "Kit_Plant", 0.70f, -6.60f, 1.30f, 0f);
            Ceiling("Light_Ceiling4", "Light_Kitchen", 0.55f, -5.00f, 3.00f);
        }

        private static void BuildDining(Transform g)
        {
            _currentGroup = g;
            P("Table_RoundSmall", "Dining_Table", 0.65f, -1.50f, 2.90f, 0f);
            P("Chair_1", "Dining_Chair_N", 0.58f, -1.50f, 3.75f, 180f);
            // Mais encostada a mesa: a 2.05 cortava a linha de passagem do Rui.
            P("Chair_1", "Dining_Chair_S", 0.58f, -1.50f, 2.20f, 0f);
            P("Chair_1", "Dining_Chair_E", 0.58f, -0.65f, 2.90f, 270f);
            P("Chair_1", "Dining_Chair_W", 0.58f, -2.35f, 2.90f, 90f);
            P("Carpet_Round", "Dining_Carpet", 0.55f, -1.50f, 2.90f, 0f, true);
            P("Shelf_Small1", "Dining_Shelf", 0.60f, -2.90f, 4.40f, 90f, true, 1.20f);
            Ceiling("Light_Chandelier", "Light_Dining", 0.60f, -1.50f, 2.90f);
        }

        private static void BuildLiving(Transform g)
        {
            _currentGroup = g;
            // Sofa virado a este, de costas para o arco da sala de jantar.
            // Recuado para norte para deixar um corredor de circulacao a sul (rota do Rui).
            P("Couch_Large1", "Living_Couch", 0.48f, 2.10f, 3.30f, 90f);
            // 0.42 da uma mesa de centro com ~0.48 m de altura; a 0.55 lia-se como mesa de jantar.
            P("Table_RoundSmall", "Living_CoffeeTable", 0.42f, 3.70f, 3.30f, 0f);
            P("Carpet_2", "Living_Carpet", 0.55f, 3.50f, 3.30f, 0f, true);
            // Movel de TV encostado a parede este.
            P("Drawer_2", "Living_MediaUnit", 0.50f, 5.45f, 3.30f, 270f);
            Box(g, "Living_TV", new Vector3(5.38f, 1.08f, 3.30f), new Vector3(0.06f, 0.62f, 1.10f),
                "Assets/Pungent/Materials/M_Black_Matte.mat");
            P("Bookshelf", "Living_Bookshelf", 0.40f, 4.60f, 0.90f, 0f);
            // Fora do arco de abertura da porta da varanda (dobradica em x=0.30, folha de 1.20).
            P("Light_Floor1", "Living_FloorLamp", 0.60f, 1.10f, 3.85f, 0f);
            P("Houseplant_1", "Living_Plant", 0.80f, 5.30f, 4.60f, 0f);
            P("Couch_Small2", "Living_Armchair", 0.48f, 3.30f, 4.45f, 180f);
            P("Curtains_Double", "Living_Curtains", 0.55f, 3.50f, 4.86f, 180f, true);
            Ceiling("Light_Ceiling4", "Light_Living", 0.55f, 3.20f, 2.60f);
        }

        private static void BuildBalcony(Transform g)
        {
            _currentGroup = g;
            P("Houseplant_4", "Balcony_Plant_A", 0.80f, 0.45f, 5.70f, 0f);
            P("Houseplant_7", "Balcony_Plant_B", 0.70f, 4.55f, 5.75f, 0f);
            P("Stool", "Balcony_Stool", 0.55f, 2.60f, 5.85f, 180f);
        }

        /// <summary>
        /// Anchors de rotina domestica do Rui. O forward de cada anchor e a direcao
        /// para onde o Rui deve ficar virado ao chegar.
        ///
        /// IMPORTANTE: a PrototypeNpcRoutine atual desloca-se em linha reta (MoveTowards),
        /// sem pathfinding. Os anchors usados como waypoints tem de formar segmentos
        /// desobstruidos. A ordem do circuito esta em <see cref="RoutineRoute"/>.
        /// </summary>
        private static void BuildRuiAnchors(Transform g)
        {
            // Anchors com ação: é aqui que ele deixa de andar em círculos e passa a
            // viver na casa. Ver RoutineActionAnchor e RuiAnimatorSetup.Actions.
            Act(Anchor(g, "RUI_Stove", new Vector3(-3.94f, 0f, 3.70f), 0f), "Stove", 9f);
            // **Encostado ao balcao, e nao a 25 cm dele.**
            //
            // Estes numeros estavam desactualizados: a cena foi corrigida a mao a
            // 06-08 e a ferramenta ficou com os antigos, portanto correr o dressing
            // desfazia a correccao. Agora sao os mesmos dos dois lados.
            //
            // O offset e **local**, e com yaw 180 o -Z local aponta para +Z no
            // mundo — ou seja, para o balcao. Medido com ele na pose: as costas
            // ficam 3,9 cm a frente do pivot, a frente do tampo esta em z=4,235,
            // logo o pivot tem de chegar a 4,21 para as costas assentarem em 4,25.
            // Da anchor em 3,90 sao 31 cm de deslocamento.
            //
            // Antes ficava com o corpo a terminar em z=3,99 — vinte e cinco
            // centimetros de ar entre ele e o movel em que devia estar encostado.
            Act(Anchor(g, "RUI_CounterLean", new Vector3(-4.60f, 0f, 3.90f), 180f), "CounterLean", 7f,
                new Vector3(0f, 0f, -0.31f));
            // O modelo final do frigorifico tem a porta virada a sul (z menor).
            // O anchor antigo vinha do placeholder: punha o Rui a leste, de lado
            // para a porta. Agora fica centrado em frente e olha para o interior.
            Act(Anchor(g, "RUI_Fridge", new Vector3(-6.64f, 0f, 3.20f), 0f), "Fridge", 5f);
            Anchor(g, "RUI_KitchenDoor", new Vector3(-5.65f, 0f, 2.00f), 180f);
            Anchor(g, "RUI_CorridorWest", new Vector3(-5.65f, 0f, -0.05f), 90f);
            Anchor(g, "RUI_CorridorMid", new Vector3(-1.60f, 0f, -0.05f), 90f);
            Act(Anchor(g, "RUI_DiningTable", new Vector3(-1.55f, 0f, 1.45f), 0f), "PhoneIdle", 6f);
            Anchor(g, "RUI_LivingApproach", new Vector3(0.60f, 0f, 1.45f), 90f);
            // Em frente ao assento, encostado a aresta da frente do sofa (x = 2.73) e
            // ainda a cabar entre ele e a mesa de centro. Yaw 90 = virado para +X,
            // que e para onde o sofa aponta: sentar-se e recuar, nao rodar.
            //
            // Nao pode ir para cima do sofa. A NavMesh e cozida a partir dos meshes,
            // logo o sofa e um buraco: um anchor la dentro nao tem NavMesh nenhuma
            // por baixo, o agente nunca la chega e o Rui fica encravado no movel.
            // O anchor fica onde a NavMesh chega, a frente do sofa; o corpo entra os
            // ultimos 74 cm ate ao assento com o agente desligado. O offset e local:
            // com yaw 90, -Z local recua para dentro do sofa.
            Act(Anchor(g, "RUI_CouchSide", new Vector3(3.06f, 0f, 2.88f), 90f), "CouchSit", 13f,
                new Vector3(-0.22f, 0f, -0.706f), 0.9f);
            // Fora do circuito, reservados para sequencias coreografadas.
            Anchor(g, "RUI_Hall", new Vector3(4.10f, 0f, -1.70f), 270f);
            Anchor(g, "RUI_BedroomDoor", new Vector3(-2.00f, 0f, -1.35f), 180f);
        }

        /// <summary>
        /// Circuito de ida e volta do Rui. Tem de terminar num ponto com linha reta
        /// livre ate ao primeiro, porque a rotina faz wrap para o indice 0.
        /// </summary>
        public static readonly string[] RoutineRoute =
        {
            "RUI_Stove", "RUI_CounterLean", "RUI_Fridge", "RUI_KitchenDoor",
            "RUI_CorridorWest", "RUI_CorridorMid", "RUI_DiningTable",
            "RUI_LivingApproach", "RUI_CouchSide",
            "RUI_LivingApproach", "RUI_DiningTable", "RUI_CorridorMid",
            "RUI_CorridorWest", "RUI_KitchenDoor", "RUI_Fridge", "RUI_CounterLean"
        };

        // ------------------------------------------------------------------
        // Pensamentos do protagonista ligados aos objetos (seccao 13.5 do plano)
        // ------------------------------------------------------------------

        private struct Flavour
        {
            public string Prop;
            public string Prompt;
            public string[] Lines;
        }

        private static Flavour F(string prop, string prompt, params string[] lines)
            => new Flavour { Prop = prop, Prompt = prompt, Lines = lines };

        /// <summary>
        /// Nem toda a interacao produz uma acao. A maioria destes objetos so
        /// devolve uma frase que caracteriza o Rui, o apartamento ou o Tomas.
        /// </summary>
        private static readonly Flavour[] FlavourTable =
        {
            // --- Sala ---
            F("Living_TV", "Look at the TV",
                "Rui broke the TV last weekend. He said he would buy another one.",
                "Still broken. He has not mentioned it since."),
            F("Living_MediaUnit", "Look at the cabinet",
                "Half these films are the same one twice."),
            F("Living_Couch", "Look at the sofa",
                "He sits here at night. Sometimes with the TV off."),
            F("Living_CoffeeTable", "Look at the table",
                "Two mugs. I only ever use one."),
            F("Living_Bookshelf", "Look at the shelf",
                "Most of this came with the flat."),
            F("Living_Armchair", "Look at the armchair",
                "This is his chair. He made that clear on day one."),

            // --- Jantar ---
            F("Dining_Table", "Look at the table",
                "We ate here once. He asked a lot about my father."),

            // --- Cozinha ---
            F("Kit_Fridge", "Look in the fridge",
                "Half of this is his. He labels everything.",
                "Nothing I feel like eating."),
            F("Kit_Oven", "Look at the stove",
                "Still warm. He cooked something late again."),
            F("Kit_Sink", "Look at the sink",
                "One plate. One glass. He washes up straight away."),
            F("Kit_Table", "Look at the table",
                "There is always a second chair pulled out."),
            F("Kit_Worktop", "Look at the counter",
                "He leans here when he talks to me. Right in the doorway."),

            // --- Quarto do Tomas ---
            F("Bed_Tomas", "Look at the bed",
                "I should try to sleep once the internet is back."),
            F("Dresser_Tomas", "Look at the drawers",
                "My things. Exactly where I left them. I think."),
            F("Bookshelf_Tomas", "Look at the shelf",
                "Textbooks I have not opened since September."),
            F("NightStand_Tomas", "Look at the nightstand",
                "Keys, wallet, charger. The whole of my life fits here."),

            // --- Corredor e entrada ---
            F("ShoeCabinet_Hall", "Look at the shoes",
                "His shoes are wet. It has not rained since Tuesday."),
            F("Plant_Hall", "Look at the plant",
                "Someone waters it. It is not me."),
            F("ElectricalPanel", "Look at the breaker box",
                "Everything on this floor runs through here."),

            // --- Casa de banho e lavandaria ---
            F("Mirror_Bath", "Look in the mirror",
                "I look worse than I feel. Or the other way around."),
            F("Bathtub", "Look at the bath",
                "The tap drips. I keep meaning to say something."),
            F("WashingMachine", "Look at the machine",
                "He runs this at three in the morning sometimes."),

            // --- Arrumos ---
            F("Shelf_Storage", "Look at the boxes",
                "Boxes I never got round to unpacking."),
            F("Drawer_Storage", "Try the drawer",
                "Locked. It was not locked when I moved in."),

            // --- Varanda ---
            F("Balcony_Stool", "Look out",
                "You can see the car from here. That is why I took the room."),
        };

        private static void ApplyFlavour(GameObject root)
        {
            var byName = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (!byName.ContainsKey(t.name))
                    byName[t.name] = t;

            int applied = 0;
            foreach (var flavour in FlavourTable)
            {
                if (!byName.TryGetValue(flavour.Prop, out Transform target))
                {
                    Debug.LogWarning($"[DressV2] Objeto sem correspondencia para pensamento: {flavour.Prop}");
                    continue;
                }

                if (target.GetComponent<Collider>() == null)
                {
                    Debug.LogWarning($"[DressV2] '{flavour.Prop}' nao tem collider: nao pode ser olhado.");
                    continue;
                }

                var component = target.gameObject.GetComponent<FlavourInteractable>();
                if (component == null)
                    component = target.gameObject.AddComponent<FlavourInteractable>();

                var so = new SerializedObject(component);
                so.FindProperty("prompt").stringValue = flavour.Prompt;
                so.FindProperty("requiresEvent").stringValue = "prologue_done";
                // E cala-se no Dia 5. Um movel que oferece "Look" com o Rui a
                // procura do jogador e o jogo a garantir-lhe que nao ha perigo
                // nenhum. O `AmbientThoughtsWiring` repoe isto sozinho, para quem
                // correr as ferramentas por outra ordem.
                so.FindProperty("silencedByEvent").stringValue = "climax";
                var lines = so.FindProperty("thoughts");
                lines.arraySize = flavour.Lines.Length;
                for (int i = 0; i < flavour.Lines.Length; i++)
                    lines.GetArrayElementAtIndex(i).stringValue = flavour.Lines[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                applied++;
            }

            Debug.Log($"[DressV2] {applied} objetos com pensamento associado.");
        }

        // ------------------------------------------------------------------
        // Utilitarios
        // ------------------------------------------------------------------

        private static Transform Group(GameObject root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            return go.transform;
        }

        private static GameObject Anchor(Transform parent, string name, Vector3 position, float yaw)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        /// <param name="offset">
        /// Deslocamento local para onde o corpo desliza durante a accao, com o
        /// agente desligado. Serve os sitios a que a NavMesh nao chega — o assento
        /// do sofa e um buraco na malha, porque ela e cozida a partir dos meshes.
        /// </param>
        private static void Act(GameObject anchor, string state, float seconds,
            Vector3 offset = default, float settle = 0.7f)
        {
            var action = anchor.AddComponent<Pungent.NPC.RoutineActionAnchor>();
            var so = new SerializedObject(action);
            so.FindProperty("animatorState").stringValue = state;
            so.FindProperty("seconds").floatValue = seconds;
            so.FindProperty("actionOffset").vector3Value = offset;
            so.FindProperty("settleSeconds").floatValue = settle;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void P(string model, string name, float scale, float x, float z, float yaw,
                              bool noCollider = false, float y = 0f)
        {
            Place(_currentGroup, model, name, scale, new Vector3(x, y, z), yaw, noCollider);
        }

        private static void Ceiling(string model, string name, float scale, float x, float z)
        {
            // Os candeeiros de teto usam a rotacao invertida (X = 90) como no passe original.
            var go = Instantiate(model, name, _currentGroup);
            if (go == null) return;
            go.transform.position = new Vector3(x, 2.62f, z);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * scale;
            StripColliders(go);
            MarkStatic(go);
        }

        private static void Place(Transform parent, string model, string name, float scale,
                                  Vector3 position, float yaw, bool noCollider)
        {
            // Wrapper sem escala nem tombo, para que o BoxCollider fique alinhado ao objeto.
            var wrapper = new GameObject(name);
            wrapper.transform.SetParent(parent, false);
            wrapper.transform.position = position;
            wrapper.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // Preserve the owner's manual removals without breaking components
            // which use these wrappers as stable scene anchors.
            if (ManuallyRemovedVisuals.Contains(name))
            {
                if (!noCollider && TryGetMeshBounds(model, out Bounds removedMesh))
                {
                    var removedBox = wrapper.AddComponent<BoxCollider>();
                    removedBox.size = new Vector3(
                        removedMesh.size.x, removedMesh.size.z, removedMesh.size.y) * scale;
                    removedBox.center = new Vector3(
                        removedMesh.center.x, removedMesh.center.z, -removedMesh.center.y) * scale;
                }
                MarkStatic(wrapper);
                return;
            }

            var go = Instantiate(model, name + "_Mesh", wrapper.transform);
            if (go == null)
            {
                Object.DestroyImmediate(wrapper);
                return;
            }

            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(270f, 0f, 0f);
            go.transform.localScale = Vector3.one * scale;
            StripColliders(go);

            if (!noCollider && TryGetMeshBounds(model, out Bounds mesh))
            {
                // Rx(-90): (x, y, z) -> (x, z, -y)
                var box = wrapper.AddComponent<BoxCollider>();
                box.size = new Vector3(mesh.size.x, mesh.size.z, mesh.size.y) * scale;
                box.center = new Vector3(mesh.center.x, mesh.center.z, -mesh.center.y) * scale;
            }

            MarkStatic(wrapper);
        }

        private static GameObject Instantiate(string model, string name, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + model + ".fbx");
            if (asset == null)
            {
                Debug.LogWarning($"[DressV2] Modelo em falta: {model}");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.name = name;
            go.transform.SetParent(parent, false);
            return go;
        }

        private static bool TryGetMeshBounds(string model, out Bounds bounds)
        {
            bounds = new Bounds();
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + model + ".fbx");
            if (asset == null) return false;

            bool first = true;
            foreach (var mf in asset.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (first) { bounds = mf.sharedMesh.bounds; first = false; }
                else bounds.Encapsulate(mf.sharedMesh.bounds);
            }
            return !first;
        }

        private static void StripColliders(GameObject go)
        {
            // Os MeshColliders importados sao caros e desnecessarios: o plano pede colliders simples.
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, string materialPath)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = size;

            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = material;

            MarkStatic(go);
            return go;
        }

        private static void MarkStatic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.ContributeGI |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.ReflectionProbeStatic);
        }
    }
}
