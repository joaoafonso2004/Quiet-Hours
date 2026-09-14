using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Coloca as tarefas domesticas opcionais pelo apartamento.
    ///
    /// Sao o contrapeso da cadeia de objectivos: nenhuma e precisa para acabar a
    /// noite, e e por isso que fazem a casa parecer uma casa. O jogador vai a
    /// varanda porque lhe apetece, e e la parado, a fumar, que repara no que se
    /// passa nas outras janelas.
    /// </summary>
    internal static class HomeTaskWiring
    {
        private const string Root = "HOME_TASKS";

        [MenuItem("Tools/Pungent/Wire Home Tasks")]
        internal static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var player = GameObject.Find("PlayerRoot");
            if (player == null) { Debug.LogError("[HomeTasks] PlayerRoot nao encontrado."); return; }

            // --- director e ponte para o HUD, no jogador ---
            var director = Object.FindObjectOfType<HomeTaskDirector>();
            if (director == null) director = player.AddComponent<HomeTaskDirector>();
            var bridge = player.GetComponent<PrototypeHudBridge>();
            if (bridge == null) bridge = player.AddComponent<PrototypeHudBridge>();
            var bridgeSo = new SerializedObject(bridge);
            bridgeSo.FindProperty("hud").objectReferenceValue =
                player.GetComponent<Pungent.Interaction.PrototypeHUD>();
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();
            var directorSo = new SerializedObject(director);
            directorSo.FindProperty("hudBridge").objectReferenceValue = bridge;
            directorSo.ApplyModifiedPropertiesWithoutUndo();

            var thoughts = player.GetComponent<PlayerThoughtDirector>();

            var old = ApartmentV2WiringUtil.Find(scene, Root);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Wire home tasks");

            BuildBalcony(root.transform, director, thoughts, player);
            BuildBalconyAmbience(root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[HomeTasks] Tarefas domesticas colocadas.");
        }

        /// <summary>
        /// Som da cidade na varanda. Fica na propria varanda e nao no jogador: e a
        /// chegada ao sitio que tem de trazer o som, e a porta fechada tem de o
        /// abafar.
        /// </summary>
        private static void BuildBalconyAmbience(Transform root)
        {
            const string path = "Assets/ThirdParty/Audio/city_ambience.mp3";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);

            var holder = new GameObject("AMB_BalconyCity");
            holder.transform.SetParent(root, false);
            holder.transform.position = new Vector3(2.50f, 1.20f, 6.40f);

            var source = holder.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2.5f;
            // Chega ate a sala pela porta aberta, mas nao enche o apartamento.
            source.maxDistance = 9f;
            source.volume = 0.32f;
            source.dopplerLevel = 0f;

            if (clip == null)
                Debug.LogWarning($"[HomeTasks] '{path}' nao existe. A fonte fica montada " +
                                 "e o clip entra sozinho quando o ficheiro for adicionado.");
        }

        private static void BuildBalcony(Transform root, HomeTaskDirector director,
            PlayerThoughtDirector thoughts, GameObject player)
        {
            // A laje vai de x 0 a 5 e de z 5.25 a 6.70.
            var zone = new GameObject("Zone_Balcony");
            zone.transform.SetParent(root, false);
            zone.transform.position = new Vector3(2.50f, 1.00f, 5.97f);
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(4.80f, 2.00f, 1.30f);

            var zoneComponent = zone.AddComponent<HomeTaskZone>();
            var zoneSo = new SerializedObject(zoneComponent);
            zoneSo.FindProperty("id").stringValue = "balcony";
            zoneSo.FindProperty("thought").stringValue = "My favourite part of the house.";
            zoneSo.FindProperty("thoughts").objectReferenceValue = thoughts;
            zoneSo.FindProperty("tasks").objectReferenceValue = director;
            zoneSo.ApplyModifiedPropertiesWithoutUndo();

            // O cigarro fuma-se encostado a guarda norte, virado para a cidade.
            var smoke = new GameObject("Task_Smoke");
            smoke.transform.SetParent(root, false);
            smoke.transform.position = new Vector3(2.50f, 1.00f, 6.58f);
            var smokeBox = smoke.AddComponent<BoxCollider>();
            smokeBox.size = new Vector3(1.80f, 0.34f, 0.26f);

            var task = smoke.AddComponent<HomeTaskInteractable>();
            var taskSo = new SerializedObject(task);
            taskSo.FindProperty("id").stringValue = "smoke";
            taskSo.FindProperty("prompt").stringValue = "Smoke a cigarette";
            taskSo.FindProperty("repeatPrompt").stringValue = "Have another one";
            taskSo.FindProperty("maximumTimes").intValue = 2;
            taskSo.FindProperty("exhaustedThought").stringValue = "That was the last one.";
            // **Um segundo, e nao catorze.** A barra de progresso do cigarro nao media
            // nada que se visse: quem fuma ve o cigarro a arder, e o `HeldCigarette` ja
            // trata disso a sua velocidade. A barra so dizia ao jogador para esperar.
            //
            // O minimo do campo e 1, por isso e 1: a tarefa fecha-se de imediato e o
            // cigarro na mao passa a ser o unico relogio da cena, que e o que ele
            // sempre devia ter sido.
            taskSo.FindProperty("seconds").floatValue = 1f;
            taskSo.FindProperty("thoughts").objectReferenceValue = thoughts;
            taskSo.FindProperty("tasks").objectReferenceValue = director;

            // **A trela, em vez de os pes pregados ao chao.**
            //
            // O cigarro prendia o jogador no sitio catorze segundos. E muito tempo
            // sem poder dar um passo, e transforma a unica tarefa contemplativa do
            // jogo num temporizador a olhar para uma barra invisivel. Com a trela
            // ele anda na varanda, encosta-se a guarda, olha para a rua e para o
            // carro — que e exactamente o que esta tarefa existe para lhe dar — e
            // continua sem poder entrar com o cigarro aceso.
            //
            // O volume e o da propria varanda, so que mais alto: o do `Zone_Balcony`
            // tem 2 m e serve para detectar a entrada, e um jogador de 1,80 encostado
            // a guarda saia-lhe pelo cimo. Este vive num objecto proprio para as duas
            // coisas poderem ter tamanhos diferentes sem se estragarem uma a outra.
            var leash = new GameObject("Leash_Balcony");
            leash.transform.SetParent(root, false);
            leash.transform.position = new Vector3(2.50f, 1.40f, 5.97f);
            var leashBox = leash.AddComponent<BoxCollider>();
            leashBox.isTrigger = true;
            leashBox.size = new Vector3(4.80f, 3.00f, 1.40f);

            taskSo.FindProperty("confineTo").objectReferenceValue = leashBox;
            taskSo.FindProperty("leashThought").stringValue =
                "Rui would kill me if I smoked inside.";

            // O cigarro vive na camara, nao na varanda: e a mao do jogador que o
            // segura. A luz laranja que aqui estava antes lia-se como uma bola a
            // flutuar, porque nao havia nada por tras dela.
            var camera = player.GetComponentInChildren<Camera>(true);
            var cigarette = camera.GetComponent<HeldCigarette>();
            if (cigarette == null) cigarette = camera.gameObject.AddComponent<HeldCigarette>();
            taskSo.FindProperty("progressVisual").objectReferenceValue = cigarette;

            var beats = taskSo.FindProperty("beats");
            string[] lines =
            {
                "Cold out here.",
                "The city never actually goes quiet. It just gets further away.",
                "One more and I sleep. That is the deal."
            };
            beats.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
                beats.GetArrayElementAtIndex(i).stringValue = lines[i];

            taskSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
