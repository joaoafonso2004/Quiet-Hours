using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Poe um jogador a mexer numa cena qualquer.
    ///
    /// Depois de o `PlayerRoot` ter dado dois prefabs (ver
    /// <see cref="PlayerPrefabBuilder"/>), montar uma cena nova passou a ser isto:
    /// o jogador, os sistemas que levam a historia, e um sitio onde nascer. A
    /// oficina precisa disto hoje; a estrada e o climax vao precisar do mesmo, e e
    /// por isso que a ferramenta nao sabe nada sobre garagens.
    ///
    /// O sitio onde nasce e um objecto vazio chamado `PLAYER_SPAWN`. Sem ele o
    /// jogador fica na origem, que na oficina e dentro da nave e a meio do chao.
    /// </summary>
    internal static class ScenePlayerSetup
    {
        private const string PlayerPrefabPath = "Assets/Pungent/Prefabs/PLAYER.prefab";
        private const string SystemsPrefabPath = "Assets/Pungent/Prefabs/GAME_SYSTEMS.prefab";
        private const string SpawnName = "PLAYER_SPAWN";

        [MenuItem("Pungent/Blockout/Add Player + Systems to Scene", false, 16)]
        internal static void Setup()
        {
            if (BuildGuard.Blocked("AddPlayerToScene")) return;

            var scene = EditorSceneManager.GetActiveScene();

            var player = Ensure("PlayerRoot", PlayerPrefabPath);
            var systems = Ensure("GAME_SYSTEMS", SystemsPrefabPath);
            if (player == null || systems == null) return;

            var spawn = GameObject.Find(SpawnName);
            if (spawn != null)
            {
                // O CharacterController escreve por cima de `position` quando esta
                // ligado, e uma cena aberta em Edit Mode nao o desliga sozinho.
                var controller = player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;

                player.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);

                if (controller != null) controller.enabled = true;
            }
            else
            {
                Debug.LogWarning($"[Player] Sem `{SpawnName}` nesta cena: o jogador fica na " +
                                 "origem. Criar um objecto vazio com esse nome onde ele deve nascer.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Player] '{scene.name}' tem jogador e sistemas.");
        }

        private static GameObject Ensure(string sceneName, string prefabPath)
        {
            var existing = GameObject.Find(sceneName);
            if (existing != null) return existing;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[Player] Falta '{prefabPath}'. Correr primeiro " +
                               "'Build Player + Systems Prefabs' com o apartamento aberto.");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = sceneName;
            Undo.RegisterCreatedObjectUndo(instance, "Add player to scene");
            return instance;
        }
    }
}
