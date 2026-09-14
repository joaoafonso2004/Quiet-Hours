using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// A desarrumação do apartamento, colocada à mão.
    ///
    /// O pilar P7 do plano mestre pede uma casa onde duas pessoas vivem mesmo, e o
    /// P3 pede que ela se degrade. Mas o risco assinalado na secção 22 é o
    /// contrário: espalhar um pack de lixo ao acaso lê-se como asset flip, não como
    /// vida. Por isso nada aqui é aleatório — cada objecto está onde alguém o
    /// teria deixado, e diz alguma coisa sobre quem o deixou.
    ///
    /// O Tomás anda acordado a trabalhar: canecas e latas à volta da secretária. A
    /// sala é o espaço partilhado: é lá que se acumula. O quarto do Rui fica quase
    /// limpo de propósito — ele é o organizado, e essa diferença é caracterização,
    /// não falta de tempo a mobilar.
    /// </summary>
    internal static class MessScatter
    {
        private const string Root = "MESS_V2";
        private const string Low = "Assets/Mess Maker Free/Prefabs/Low Poly";

        private struct Piece
        {
            public string Prefab;
            public Vector3 Position;   // y = superfície onde assenta
            public float Yaw;
            public bool Upright;       // false = tombado, que é o estado natural do lixo
            public float RealSize;     // maior dimensão em metros, no mundo real
        }

        // As alturas e os limites de cada superfície foram medidos na cena, não
        // estimados: mesa de centro 0.48, mesa de jantar 0.74, bancada 1.05,
        // secretária 0.84, mesas de cabeceira 0.46/0.52, chão 0.
        //
        // Os modelos do pack estão deitados ao longo de Z e vêm ~3.5x maiores do que
        // o real — uma lata mede 0.43 m de comprimento. Por isso cada peça declara o
        // tamanho verdadeiro e a escala sai daí.
        private static readonly Piece[] Pieces =
        {
            // --- Secretária do Tomás (0.84, x -5.16..-3.84, z -4.96..-4.34).
            //     O portátil está a (-4.45, -4.64), por isso fica tudo à volta. ---
            new Piece { Prefab = "Porcelin and Pottery/Mug White", Position = new Vector3(-4.98f, 0.84f, -4.52f), Yaw = 24f, Upright = true, RealSize = 0.10f },
            new Piece { Prefab = "Cans/Soda Can Red", Position = new Vector3(-3.98f, 0.84f, -4.50f), Yaw = 200f, Upright = true, RealSize = 0.122f },
            new Piece { Prefab = "Cans/Soda Can Red Crushed", Position = new Vector3(-4.05f, 0.84f, -4.82f), Yaw = 61f, RealSize = 0.10f },

            // --- Quarto do Tomás: caixote a (-3.67, -4.70), cabeceira a 0.46 ---
            new Piece { Prefab = "Cans/Beer Can Green Crushed", Position = new Vector3(-3.98f, 0.00f, -4.42f), Yaw = 130f, RealSize = 0.10f },
            new Piece { Prefab = "Porcelin and Pottery/Mug Wood", Position = new Vector3(-5.63f, 0.46f, -4.71f), Yaw = 310f, Upright = true, RealSize = 0.10f },

            // --- Sala: mesa de centro (0.48, x 3.29..4.11, z 2.89..3.71) ---
            new Piece { Prefab = "Glass Bottles/Beer Bottle Green with Label", Position = new Vector3(3.52f, 0.48f, 3.16f), Yaw = 15f, Upright = true, RealSize = 0.24f },
            new Piece { Prefab = "Glass Bottles/Beer Bottle Green", Position = new Vector3(3.86f, 0.48f, 3.42f), Yaw = 285f, Upright = true, RealSize = 0.24f },
            new Piece { Prefab = "Cans/Beer Can Blue", Position = new Vector3(3.62f, 0.48f, 3.52f), Yaw = 70f, Upright = true, RealSize = 0.122f },
            new Piece { Prefab = "Porcelin and Pottery/Pint Glass", Position = new Vector3(3.90f, 0.48f, 3.04f), Yaw = 0f, Upright = true, RealSize = 0.15f },
            // O hamburguer na mesa de centro foi retirado a mao pelo dono do
            // projecto. Fica registado aqui para nao voltar no proximo Scatter.
            // Tombada no tapete, a que caiu da mesa.
            new Piece { Prefab = "Cans/Beer Can Blue Crushed", Position = new Vector3(3.05f, 0.00f, 2.72f), Yaw = 190f, RealSize = 0.10f },
            new Piece { Prefab = "Glass Bottles/Beer Bottle Red", Position = new Vector3(4.22f, 0.00f, 2.60f), Yaw = 55f, RealSize = 0.24f },
            // Em cima do móvel da televisão (0.65).
            new Piece { Prefab = "Porcelin and Pottery/Mug White", Position = new Vector3(5.45f, 0.65f, 2.60f), Yaw = 120f, Upright = true, RealSize = 0.10f },

            // --- Cozinha: bancada a 1.05, x -6.28..-3.04, z 4.27..5.01 ---
            new Piece { Prefab = "Porcelin and Pottery/Mug White", Position = new Vector3(-5.90f, 1.05f, 4.55f), Yaw = 95f, Upright = true, RealSize = 0.10f },
            new Piece { Prefab = "Porcelin and Pottery/Broken Plate", Position = new Vector3(-4.70f, 1.05f, 4.58f), Yaw = 12f, RealSize = 0.22f },
            new Piece { Prefab = "Cans/Soda Can Orange", Position = new Vector3(-3.35f, 1.05f, 4.60f), Yaw = 250f, Upright = true, RealSize = 0.122f },
            new Piece { Prefab = "Food/Apple Core Red", Position = new Vector3(-3.60f, 1.05f, 4.48f), Yaw = 150f, RealSize = 0.08f },

            // --- Jantar: mesa a 0.74, x -2.13..-0.87, z 2.27..3.53 ---
            new Piece { Prefab = "Porcelin and Pottery/Mug White", Position = new Vector3(-1.35f, 0.74f, 2.72f), Yaw = 200f, Upright = true, RealSize = 0.10f },
            new Piece { Prefab = "Food/Chicken Bone", Position = new Vector3(-1.62f, 0.74f, 3.05f), Yaw = 55f, RealSize = 0.10f },

            // --- Quarto do Rui: uma caneca na cabeceira (0.52) e mais nada.
            //     Ele é o arrumado, e essa diferença é caracterização. ---
            new Piece { Prefab = "Porcelin and Pottery/Mug White", Position = new Vector3(-1.95f, 0.52f, -4.55f), Yaw = 0f, Upright = true, RealSize = 0.10f },

            // --- Varanda: chão, onde se vai fumar ---
            new Piece { Prefab = "Glass Bottles/Beer Bottle Green", Position = new Vector3(1.35f, 0.00f, 6.32f), Yaw = 100f, RealSize = 0.24f },
            new Piece { Prefab = "Cans/Beer Can Red Crushed", Position = new Vector3(3.95f, 0.00f, 6.28f), Yaw = 20f, RealSize = 0.10f },
        };

        [MenuItem("Tools/Pungent/Scatter Mess")]
        internal static void Run()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var old = ApartmentV2WiringUtil.Find(scene, Root);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Scatter mess");

            int placed = 0, missing = 0;
            foreach (var piece in Pieces)
            {
                string path = $"{Low}/{piece.Prefab}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { Debug.LogWarning($"[Mess] Em falta: {path}"); missing++; continue; }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.transform.SetParent(root.transform, false);

                // Os modelos vêm deitados ao longo de Z: -90 em X põe-nos de pé.
                instance.transform.rotation = piece.Upright
                    ? Quaternion.Euler(-90f, piece.Yaw, 0f)
                    : Quaternion.Euler(0f, piece.Yaw, 0f);

                Rescale(instance, piece.RealSize);
                instance.transform.position = piece.Position;

                // O pivô destes prefabs não está na base. Sem isto metade ficava
                // enterrada na bancada e a outra metade a flutuar.
                SitOnSurface(instance, piece.Position.y);

                // São cenário: não podem ser empurrados nem apanhar o raycast de
                // interacção, senão o jogador tenta interagir com uma lata vazia.
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.gameObject.isStatic = true;

                placed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Mess] {placed} objectos colocados, {missing} em falta.");
        }

        /// <summary>
        /// Põe a peça à escala humana. O pack vem ~3.5x maior do que a vida real —
        /// uma lata de cerveja mede 0.43 m de comprimento —, e ao pé de mobília com
        /// medidas certas isso lê-se logo como errado.
        /// </summary>
        private static void Rescale(GameObject instance, float realSize)
        {
            if (realSize <= 0f) return;

            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (longest <= 0.0001f) return;

            instance.transform.localScale *= realSize / longest;
        }

        /// <summary>Assenta a base da malha exactamente na altura pedida.</summary>
        private static void SitOnSurface(GameObject instance, float surfaceY)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            float lift = surfaceY - bounds.min.y;
            instance.transform.position += new Vector3(0f, lift, 0f);
        }
    }
}
