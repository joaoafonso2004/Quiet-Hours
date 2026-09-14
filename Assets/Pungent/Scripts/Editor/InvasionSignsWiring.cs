using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Fecha o Dia 3: os sinais de invasao e o dono do `day3_ready`.
    ///
    /// O `CH_Day3_Limits` tem tres passos. O primeiro — "Check the apartment" —
    /// nao tinha nada para se encontrar, e o segundo esperava por `day3_ready`,
    /// que ninguem levantava. E a mesma armadilha de sempre: sem dono, o capitulo
    /// para ali e nao ha erro nenhum a dizer porque.
    ///
    /// O `day3_ready` passa a ser as chaves no movel da entrada. Pegar nas chaves e
    /// o gesto de quem vai sair, e ja havia um pensamento a apontar para elas —
    /// "Keys, wallet, charger. The whole of my life fits here."
    /// </summary>
    internal static class InvasionSignsWiring
    {
        private const string Root = "DAY3_SIGNS";
        private const string LinesPath = "Assets/Pungent/Dialogue/Thoughts/THT_Dresser_Opened.asset";

        [MenuItem("Pungent/Blockout/Wire Day 3 Signs", false, 14)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireDay3Signs")) return;

            var scene = EditorSceneManager.GetActiveScene();
            var lines = BuildDresserLines();

            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Wire day 3 signs");

            BuildSigns(root.transform, lines);
            BuildReadyEvent(root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Day3] Sinais de invasao ligados; `day3_ready` passou a ter dono (o portatil).");
        }

        /// <summary>
        /// O que a comoda passa a dizer depois de a gaveta aparecer aberta.
        ///
        /// Nenhuma destas linhas acusa ninguem. Ele repara e desconversa, que e o
        /// que uma pessoa faz sozinha num quarto de manha — a certeza vem depois,
        /// quando o Rui repetir um detalhe que so se sabe la de dentro.
        /// </summary>
        private static ThoughtLineSet BuildDresserLines()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ThoughtLineSet>(LinesPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ThoughtLineSet>();
                AssetDatabase.CreateAsset(asset, LinesPath);
            }

            asset.EditorPopulate(new[]
            {
                "I did not leave that open.",
                "Maybe I did. I was half asleep when I got in.",
                "Nothing is missing. That is the part I keep going back to."
            // A ultima fica a repetir-se: insistir na comoda nao gera texto novo
            // para sempre, e "nada falta" e a nota certa para ficar a ecoar.
            }, ThoughtLineSet.Order.SequentialLastRepeats, 3.2f);

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void BuildSigns(Transform parent, ThoughtLineSet lines)
        {
            var holder = new GameObject("InvasionSigns");
            holder.transform.SetParent(parent, false);
            var signs = holder.AddComponent<InvasionSigns>();

            var so = new SerializedObject(signs);
            so.FindProperty("director").objectReferenceValue = Object.FindObjectOfType<ChapterDirector>();
            so.FindProperty("thoughts").objectReferenceValue = Object.FindObjectOfType<PlayerThoughtDirector>();

            // 1 - a porta do quarto do Tomas.
            var door = GameObject.Find("DOORS_V2/Door_Bedroom_Tomas");
            if (door != null)
                so.FindProperty("bedroomDoor").objectReferenceValue =
                    door.GetComponent<DoorDragInteractable>();
            else Debug.LogWarning("[Day3] Porta do quarto nao encontrada.");

            // 2 - a gaveta. O modelo pode nao ter gaveta separada; nesse caso fica
            // por ligar e o componente salta o sinal em vez de rebentar.
            // **Por nome e nao por caminho.** Isto era
            // `GameObject.Find("ART_PASS_V2/Bedroom_Tomas/Dresser_Tomas")`, e o
            // dressing entretanto mudou a arvore: o caminho deixou de existir, o
            // `Find` devolveu nulo, e **nao havia aviso nenhum** — so o `drawer` e a
            // `sash` se queixavam. A comoda continuava a dizer *"Exactly where I left
            // them. I think."* com a gaveta aberta a frente do jogador, que e
            // precisamente a linha que este sinal existe para substituir.
            //
            // Um caminho escrito a mao e uma dependencia de arrumacao. Um nome nao e.
            var dresser = ByName("Dresser_Tomas");
            if (dresser != null)
            {
                var flavour = dresser.GetComponent<FlavourInteractable>()
                           ?? dresser.GetComponentInChildren<FlavourInteractable>(true);
                if (flavour == null)
                    Debug.LogWarning("[Day3] `Dresser_Tomas` sem `FlavourInteractable`: a comoda "
                                   + "vai continuar a dizer que esta tudo no sitio com a gaveta aberta.");
                so.FindProperty("dresserFlavour").objectReferenceValue = flavour;

                var drawer = FindDrawer(dresser.transform);
                if (drawer != null) so.FindProperty("drawer").objectReferenceValue = drawer;
                else Debug.LogWarning("[Day3] Sem gaveta separada no modelo da comoda: fica " +
                                      "sem o movimento, mas as linhas novas entram na mesma. " +
                                      "Ligar a mao se algum dia houver uma gaveta.");
            }
            so.FindProperty("dresserLinesAfter").objectReferenceValue = lines;

            // 3 - a janela do quarto, a sul. As folhas do modelo chamam-se 01..04;
            // basta uma para a janela se ler como aberta.
            var sash = FindWindowSash("Window_South_1");
            if (sash != null) so.FindProperty("windowSash").objectReferenceValue = sash;
            else Debug.LogWarning("[Day3] Folha da janela do quarto nao encontrada.");

            // Mesma correccao, mesma razao: era um caminho e passa a ser um nome. Sem
            // isto, o sinal da janela aberta e mudo — a cidade nao entra mais alto, e
            // metade do que faz aquele sinal existir e o som.
            var city = ByName("AMB_BalconyCity");
            if (city != null)
                so.FindProperty("cityAmbience").objectReferenceValue = city.GetComponent<AudioSource>();
            else
                Debug.LogWarning("[Day3] `AMB_BalconyCity` nao encontrado: a janela aberta fica muda. "
                               + "Correr o 'Wire Home Tasks' primeiro.");

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Procura por nome em toda a cena, inclusive no que estiver desligado.
        ///
        /// Substitui os `GameObject.Find("A/B/C")` que este ficheiro usava: um caminho
        /// escrito a mao e uma dependencia da arrumacao da hierarquia, e a arrumacao
        /// muda sempre que alguem corre o dressing. Dois campos ficaram a nulo assim,
        /// em silencio, ate alguem os contar.
        /// </summary>
        private static GameObject ByName(string name)
        {
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }

        /// <summary>The laptop closes the morning without sending Tomas outside.</summary>
        private static void BuildReadyEvent(Transform parent)
        {
            var desk = ByName("Laptop_Screen");

            // Pelas bounds e nao pelo transform. O movel da entrada e um dos props
            // "holder + malha com offset local" deste projecto: o transform esta a
            // 1,10 e a prateleira que se ve esta a 1,72. Somar altura ao transform
            // punha o volume a 2,20 — acima do topo da prateleira e acima da linha
            // dos olhos (1,70). O prompt aparecia no ar, e as chaves ficavam por
            // pegar sem nada a dizer porque.
            Vector3 position = new Vector3(-4.45f, 1.10f, -4.28f);
            if (desk == null)
            {
                Debug.LogWarning("[Day3] Laptop_Screen not found; using the measured desk position.");
            }
            else
            {
                var rends = desk.GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    var bounds = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) bounds.Encapsulate(rends[i].bounds);
                    position = bounds.center;
                }
                else position = desk.transform.position;
            }

            var go = new GameObject("EVT_Day3Ready");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            // Trigger, never a solid blocker around the desk.
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.9f, 0.9f, 0.9f);

            var raiser = go.AddComponent<ChapterEventRaiser>();
            raiser.EditorConfigure("day3_ready", ChapterEventRaiser.Trigger.Interact,
                "Finish the morning's work", "Enough. I can leave the upload running.",
                "day3_chores");
            EditorUtility.SetDirty(raiser);
        }

        private static Transform FindDrawer(Transform dresser)
        {
            foreach (var child in dresser.GetComponentsInChildren<Transform>(true))
            {
                string n = child.name.ToLowerInvariant();
                if (n.Contains("drawer") || n.Contains("gaveta")) return child;
            }
            return null;
        }

        private static Transform FindWindowSash(string windowName)
        {
            var windows = GameObject.Find("WINDOWS_V2");
            if (windows == null) return null;

            foreach (var child in windows.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != windowName) continue;
                // As folhas do FBX chamam-se 01..04; a primeira chega.
                foreach (var part in child.GetComponentsInChildren<Transform>(true))
                    if (part.name == "01") return part;
                return child;
            }
            return null;
        }
    }
}
