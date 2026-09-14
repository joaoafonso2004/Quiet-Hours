using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using Pungent.Menu;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Monta a cena do menu inicial.
    ///
    /// ---
    ///
    /// **Numa cena propria, e nao por cima do apartamento.** O apartamento arranca o
    /// prologo sozinho — o `PrologueStage` toma conta do jogador no primeiro frame — e
    /// um menu a viver la dentro teria de o suspender e devolver, o que e mais uma
    /// coisa a correr por baixo do jogo para sempre. Uma cena vazia com uma camara e
    /// uma imagem nao tem nada a suspender.
    ///
    /// A cena entra no build **em primeiro**, e o apartamento passa para segundo. O
    /// `ChapterSceneLoader` carrega por nome e nao por indice, por isso nada disso lhe
    /// mexe.
    ///
    /// ---
    ///
    /// **A imagem e opcional e o menu funciona sem ela.** Larga-se em
    /// `Assets/Pungent/Art/Menu/key_art.png` e a ferramenta liga-a. Sem ela, o menu
    /// abre em preto e escreve o titulo — feio, mas nao partido.
    ///
    /// Re-executavel: reconstroi a cena do menu do zero.
    /// </summary>
    internal static class MainMenuWiring
    {
        private const string MenuScene = "Assets/Scenes/Main_Menu.unity";
        private const string FirstScene = "Apartment_Blockout_V2";
        private const string ArtFolder = "Assets/Pungent/Art/Menu/";
        private const string AudioFolder = "Assets/ThirdParty/Audio/";

        [MenuItem("Pungent/Blockout/Build Main Menu", false, 10)]
        internal static void Build()
        {
            if (BuildGuard.Blocked("BuildMainMenu")) return;

            var log = new List<string>();

            if (!AssetDatabase.IsValidFolder("Assets/Pungent/Art"))
                AssetDatabase.CreateFolder("Assets/Pungent", "Art");
            if (!AssetDatabase.IsValidFolder(ArtFolder.TrimEnd('/')))
                AssetDatabase.CreateFolder("Assets/Pungent/Art", "Menu");

            var art = PrepareArt(log);
            var loop = FindLoop(log);

            // Cena nova de raiz. Reconstruir e mais seguro do que remendar: um menu e
            // quatro objectos, e nao ha nada la dentro que valha a pena preservar.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

            var cameraObject = new GameObject("MENU_CAMERA");

            // Com a tag, senao `Camera.main` e nulo nesta cena. Nada do menu a usa
            // hoje — o `OnGUI` desenha sem camara — mas e a unica cena do jogo onde
            // ela nao existe, e uma excepcao dessas custa mais a descobrir do que
            // vale a poupar.
            cameraObject.tag = "MainCamera";

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;          // nao ha nada 3D para desenhar
            cameraObject.AddComponent<AudioListener>();

            var root = new GameObject("MAIN_MENU");

            var room = new GameObject("MENU_AMBIENCE");
            room.transform.SetParent(root.transform, false);
            var roomSource = room.AddComponent<AudioSource>();
            roomSource.playOnAwake = false;
            roomSource.spatialBlend = 0f;
            roomSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                AudioFolder + "Apartment_ambience_sound.mp3");

            var creaks = new GameObject("MENU_CREAK");
            creaks.transform.SetParent(root.transform, false);
            var creakSource = creaks.AddComponent<AudioSource>();
            creakSource.playOnAwake = false;
            creakSource.spatialBlend = 0f;

            var creakClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                AudioFolder + "LaleksicVarious/floor_creak2.wav");

            var menu = root.AddComponent<MainMenu>();
            // O titulo so e escrito quando nao ha arte nenhuma — nem video nem imagem.
            menu.EditorConfigure(art, art != null || loop != null, FirstScene,
                roomSource, creakSource, creakClip, loop);
            EditorUtility.SetDirty(menu);

            EditorSceneManager.SaveScene(scene, MenuScene);
            log.Add("cena criada em " + MenuScene);

            if (loop != null)
                log.Add("ciclo de video ligado (" + loop.width + "x" + loop.height +
                        ", " + loop.length.ToString("F1") + " s), sem som"
                      + (art != null ? "; a imagem segura o ecra ate ao primeiro frame"
                                     : "; sem imagem de reserva — o primeiro frame abre a preto"));
            else if (art != null)
                log.Add("imagem ligada; o titulo nao e escrito por cima");
            else
                log.Add(WhyNoArt());
            log.Add(roomSource.clip != null
                ? "a casa comeca a ouvir-se ao fim de 40 s parado, e uma tabua aos 66"
                : "AVISO: sem `Apartment_ambience_sound.mp3` — o menu fica em silencio");

            log.Add(PutFirstInBuild());

            Debug.Log("[Menu] Pronto.\n  " + string.Join("\n  ", log));
        }

        /// <summary>
        /// Prepara a imagem para ser desenhada em OnGUI.
        ///
        /// Um PNG importado por omissao vem como `Sprite`, e o `GUI.DrawTexture` quer
        /// uma `Texture2D`. Sem isto a referencia ficava nula e o menu abria preto sem
        /// dizer porque — o tipo de falha que custa meia hora a encontrar por ser
        /// invisivel dos dois lados.
        ///
        /// `mipmapEnabled` desligado e `wrapMode` fixo: a imagem cobre o ecra inteiro e
        /// nunca e vista pequena, por isso mipmaps so lhe tiravam nitidez.
        /// </summary>
        private static Texture2D PrepareArt(List<string> log)
        {
            string artFile = FirstAssetIn<Texture2D>();
            if (artFile == null) return null;

            var importer = AssetImporter.GetAtPath(artFile) as TextureImporter;
            if (importer == null) return null;

            bool changed = false;
            if (importer.textureType != TextureImporterType.Default)
            { importer.textureType = TextureImporterType.Default; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp)
            { importer.wrapMode = TextureWrapMode.Clamp; changed = true; }
            if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; changed = true; }

            if (changed)
            {
                importer.SaveAndReimport();
                log.Add("imagem reimportada como textura (era sprite, e o OnGUI nao a desenhava)");
            }

            log.Add("imagem: " + System.IO.Path.GetFileName(artFile));
            return AssetDatabase.LoadAssetAtPath<Texture2D>(artFile);
        }

        /// <summary>
        /// O primeiro asset do tipo pedido que estiver na pasta do menu.
        ///
        /// **Pela pasta e nao pelo nome.** Isto procurava `key_art.png` e mais nada,
        /// e o ficheiro que la estava chamava-se `quiet_hours.png` — a ferramenta
        /// dizia "POR PREENCHER" com a imagem a um metro de distancia, e a unica
        /// saida era renomear um asset, o que arrasta o `.meta` por uma razao que
        /// nao existe. Quem larga arte numa pasta chamada `Art/Menu` ja disse o que
        /// queria; exigir tambem um nome e uma pergunta a mais.
        /// </summary>
        /// <summary>
        /// Porque e que nao ha arte — pelos ficheiros que estao mesmo no disco.
        ///
        /// "Sem arte" e uma resposta inutil quando se acabou de la pôr um ficheiro:
        /// nao distingue **nao esta la** de **o Unity ainda nao a importou** de **e
        /// de um tipo que isto nao usa**. Sao tres causas com tres solucoes
        /// diferentes, e a diferenca entre elas custou uma ida e volta a esta
        /// ferramenta.
        ///
        /// Le do disco e nao da `AssetDatabase` de proposito: e a leitura que ainda
        /// funciona quando o problema e precisamente a base de dados nao saber do
        /// ficheiro.
        /// </summary>
        private static string WhyNoArt()
        {
            string folder = System.IO.Path.GetFullPath(ArtFolder.TrimEnd('/'));
            var found = new List<string>();

            if (System.IO.Directory.Exists(folder))
                foreach (string file in System.IO.Directory.GetFiles(folder))
                    if (!file.EndsWith(".meta"))
                        found.Add(System.IO.Path.GetFileName(file));

            if (found.Count == 0)
                return "sem arte: " + ArtFolder + " esta vazia. Larga la uma imagem "
                     + "ou um video, com o nome que quiseres, e corre isto outra vez";

            return "sem arte USAVEL, mas a pasta tem: " + string.Join(", ", found)
                 + ". Se o ficheiro esta ai, o Unity ainda nao o importou — Assets > "
                 + "Refresh e corre isto de novo. Ate la o menu desenha-se sozinho";
        }

        private static string FirstAssetIn<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name, new[]
            {
                ArtFolder.TrimEnd('/')
            });

            System.Array.Sort(guids);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return path;
            }

            return null;
        }

        /// <summary>
        /// O ciclo de video, se estiver la.
        ///
        /// Aceita `.mp4`, `.webm` e `.mov` porque nao vale a pena obrigar a converter
        /// quando o Unity le os tres. O primeiro que encontrar ganha.
        ///
        /// **Avisa quando o ficheiro e grande.** Um menu e a primeira coisa que
        /// carrega, e um ciclo de trinta segundos em 4K poe um jogo de dez minutos a
        /// pesar mais do que o resto todo junto. Nao impede nada — so escreve o
        /// numero, que e o que falta a alguem para decidir.
        /// </summary>
        private static VideoClip FindLoop(List<string> log)
        {
            string path = FirstAssetIn<VideoClip>();
            if (path == null) return null;

            var info = new System.IO.FileInfo(System.IO.Path.GetFullPath(path));
            float megabytes = info.Exists ? info.Length / 1048576f : 0f;
            if (megabytes > 40f)
                log.Add("AVISO: o ciclo tem " + megabytes.ToString("F0") + " MB. " +
                        "E a primeira coisa que o jogo carrega — vale a pena cortar " +
                        "a duracao ou a resolucao antes de publicar");

            return AssetDatabase.LoadAssetAtPath<VideoClip>(path);
        }

        /// <summary>
        /// Poe o menu em primeiro no build, sem perder as outras cenas.
        ///
        /// O apartamento passa de indice 0 para 1. Nada no jogo depende disso: as
        /// tres transicoes do `ChapterSceneLoader` carregam **por nome**.
        /// </summary>
        private static string PutFirstInBuild()
        {
            var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            existing.RemoveAll(s => s.path == MenuScene);
            existing.Insert(0, new EditorBuildSettingsScene(MenuScene, true));
            EditorBuildSettings.scenes = existing.ToArray();

            var names = new List<string>();
            foreach (var s in existing) names.Add(System.IO.Path.GetFileNameWithoutExtension(s.path));
            return "build: " + string.Join(" -> ", names);
        }
    }
}
