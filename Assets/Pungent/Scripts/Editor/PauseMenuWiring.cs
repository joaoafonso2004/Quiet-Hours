using UnityEditor;
using UnityEngine;
using Pungent.Menu;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Poe o menu de pausa no `GAME_SYSTEMS`.
    ///
    /// La e nao numa cena: e o unico sitio que sobrevive as tres cenas e a recarga
    /// do apartamento para o climax. Um menu de pausa que existisse so no
    /// apartamento deixava o jogador sem pausa exactamente nos dois capitulos em que
    /// ela faz mais falta — a estrada, que nao se pode parar a meio, e a garagem.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class PauseMenuWiring
    {
        private const string SystemsPrefab = "Assets/Pungent/Prefabs/GAME_SYSTEMS.prefab";

        [MenuItem("Pungent/Blockout/Wire Pause Menu", false, 56)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WirePauseMenu")) return;

            var root = PrefabUtility.LoadPrefabContents(SystemsPrefab);
            if (root == null)
            {
                Debug.LogError("[Pausa] Sem " + SystemsPrefab + ".");
                return;
            }

            var host = root.transform.Find("PAUSE");
            GameObject go;
            if (host != null) go = host.gameObject;
            else
            {
                go = new GameObject("PAUSE");
                go.transform.SetParent(root.transform, false);
            }

            if (go.GetComponent<PauseMenu>() == null) go.AddComponent<PauseMenu>();

            PrefabUtility.SaveAsPrefabAsset(root, SystemsPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();

            Debug.Log("[Pausa] Menu de pausa no GAME_SYSTEMS. Esc abre; setas e enter " +
                      "escolhem. O canvas e construido em jogo, portanto nao ha nada " +
                      "para arrastar.");
        }
    }
}
