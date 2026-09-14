using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Ajudas partilhadas pelos passos do <see cref="ApartmentV2SceneWiring"/>.
    /// So vive aqui o que mais do que um passo usa; o que serve um passo unico
    /// fica no ficheiro desse passo.
    /// </summary>
    internal static class ApartmentV2WiringUtil
    {
        /// <summary>Cria a layer se ainda nao existir e devolve o seu indice.</summary>
        internal static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                return i;
            }

            Debug.LogWarning($"[WireV2] Sem slots livres para a layer '{name}'.");
            return 0;
        }

        internal static void Move(string name, Vector3 position, Vector3? euler)
        {
            var go = GameObject.Find(name);
            if (go == null) { Debug.LogWarning($"[WireV2] '{name}' nao encontrado."); return; }
            go.transform.position = position;
            if (euler.HasValue) go.transform.rotation = Quaternion.Euler(euler.Value);
        }

        /// <summary>
        /// Procura por nome na cena inteira, incluindo objetos desativados, que o
        /// <see cref="GameObject.Find(string)"/> do Unity ignora.
        /// </summary>
        internal static GameObject Find(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            }
            return null;
        }
    }
}
