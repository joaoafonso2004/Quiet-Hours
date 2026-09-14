using UnityEditor;
using UnityEngine;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// A tabela de quando o jogo muda de cena.
    ///
    /// Um `ChapterSceneLoader` por transicao, cada um no seu objecto — o componente
    /// e `DisallowMultipleComponent`, e ter varios no mesmo objecto nao daria para
    /// os distinguir na hierarquia de qualquer maneira. Assim ve-se a lista toda de
    /// um relance dentro do `GAME_SYSTEMS`, que e onde ela pertence: os sistemas que
    /// atravessam cenas sao os unicos que podem carregar a seguinte.
    ///
    /// Acrescentar uma transicao e acrescentar uma linha a <see cref="Table"/>.
    /// O climax e o epilogo entram aqui quando existirem.
    /// </summary>
    internal static class SceneTransitionWiring
    {
        private const string PrefabPath = "Assets/Pungent/Prefabs/GAME_SYSTEMS.prefab";

        /// <summary>evento que a levanta, cena que abre, nome do objecto.</summary>
        ///
        /// **A garagem e a estrada sairam desta tabela com o corte de 11-08, e sair
        /// daqui era obrigatorio.** O `day_four` deixou de abrir a `Garage_Blockout` e
        /// passou a abrir o Dia 4 dentro de casa; com a linha antiga de pe, o
        /// acontecimento continuava a mandar carregar uma cena que ja nao esta nas
        /// Build Settings — o ecra escurecia e o jogo parava ali. Nao dava erro de
        /// compilacao nenhum, e o `Verificar tudo` tambem nao ve isto.
        private static readonly string[,] Table =
        {
            // Fica, e e inofensiva: o jogo passou a ter uma cena so, e o
            // `ChapterSceneLoader` desiste sozinho quando ja esta na cena de destino.
            // Vale como rede se alguem voltar a por o climax noutro sitio.
            { "climax",     "Apartment_Blockout_V2",  "SCENE_Climax" }
        };

        [MenuItem("Pungent/Blockout/Wire Scene Transitions", false, 18)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireSceneTransitions")) return;

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError("[Cenas] Sem " + PrefabPath + ". Correr primeiro " +
                               "'Build Player + Systems Prefabs'.");
                return;
            }

            // O primeiro loader foi escrito na raiz, quando so havia uma transicao.
            // Sai daqui para a lista nao ficar meia num sitio e meia noutro.
            var stray = root.GetComponent<ChapterSceneLoader>();
            if (stray != null) Object.DestroyImmediate(stray, true);

            // **Tirar uma linha da tabela nao chegava.** Os objectos ficavam no prefab
            // com a configuracao antiga, e um `SCENE_Garage` esquecido continua a
            // mandar carregar a garagem — a ferramenta escrevia mas nunca apagava, que
            // e a mesma familia de armadilha que ja custou tres coisas neste projecto.
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i);
                if (!child.name.StartsWith("SCENE_", System.StringComparison.Ordinal)) continue;

                bool wanted = false;
                for (int row = 0; row < Table.GetLength(0); row++)
                    if (Table[row, 2] == child.name) { wanted = true; break; }

                if (wanted) continue;

                Debug.Log("[Cenas] `" + child.name + "` retirado: ja nao esta na tabela.");
                Object.DestroyImmediate(child.gameObject, true);
            }

            for (int i = 0; i < Table.GetLength(0); i++)
            {
                string raisedEvent = Table[i, 0];
                string sceneName = Table[i, 1];
                string objectName = Table[i, 2];

                var child = root.transform.Find(objectName);
                if (child == null)
                {
                    var go = new GameObject(objectName);
                    go.transform.SetParent(root.transform, false);
                    child = go.transform;
                }

                var loader = child.GetComponent<ChapterSceneLoader>();
                if (loader == null) loader = child.gameObject.AddComponent<ChapterSceneLoader>();
                loader.EditorConfigure(raisedEvent, sceneName);
                EditorUtility.SetDirty(loader);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Cenas] {Table.GetLength(0)} transicoes ligadas no GAME_SYSTEMS.");
        }
    }
}
