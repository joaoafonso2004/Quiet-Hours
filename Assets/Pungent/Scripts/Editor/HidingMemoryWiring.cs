using Pungent.Narrative;
using UnityEditor;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Poe o <see cref="HidingMemory"/> no `GAME_SYSTEMS`.
    ///
    /// ---
    ///
    /// **No prefab e nao na cena.** O que isto guarda tem de atravessar a recarga
    /// do apartamento entre a manha do Dia 3 e a noite do Dia 5, e o unico sitio
    /// deste projecto que atravessa recargas e a raiz persistente. Posto numa cena,
    /// a memoria morria com ela e a caca do Dia 5 comportava-se exactamente como se
    /// comporta hoje — sem erro nenhum a dizer que a metade nova nunca corre.
    ///
    /// Escrito no asset e nao na instancia da cena aberta, pela mesma razao por que
    /// o `BlackboardWiring` o faz: as tres cenas do jogo trazem cada uma o seu
    /// `GAME_SYSTEMS`, e so o prefab as alcanca a todas.
    ///
    /// Re-executavel: se o componente ja la estiver, nao lhe mexe no valor — um
    /// jogo a meio nao perde o que ja lembrou por alguem correr uma ferramenta.
    /// </summary>
    internal static class HidingMemoryWiring
    {
        private const string SystemsPrefab = "Assets/Pungent/Prefabs/GAME_SYSTEMS.prefab";

        [MenuItem("Pungent/Narrativa/Wire Hiding Memory", false, 65)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("HidingMemory")) return;

            var root = PrefabUtility.LoadPrefabContents(SystemsPrefab);
            if (root == null)
            {
                Debug.LogError("[Memoria] Sem " + SystemsPrefab + ".");
                return;
            }

            var memory = root.GetComponentInChildren<HidingMemory>(true);
            bool created = memory == null;
            if (created) memory = root.AddComponent<HidingMemory>();

            PrefabUtility.SaveAsPrefabAsset(root, SystemsPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();

            Debug.Log("[Memoria] HidingMemory " + (created ? "acrescentado ao" : "ja estava no") +
                      " GAME_SYSTEMS. Escreve-o o `NothingHappened` na manha do Dia 3; " +
                      "le-o o `RuiHunt` na primeira busca do Dia 5.");
        }
    }
}
