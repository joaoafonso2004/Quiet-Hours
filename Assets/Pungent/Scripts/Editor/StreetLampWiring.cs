using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Da corpo as luzes da rua vistas da varanda.
    ///
    /// ---
    ///
    /// **Os `StreetGlow_*` sao oito `Light` sem nada.** Funcionavam porque, a dezoito
    /// metros de altura e de noite, uma poca de luz no alcatrao chega para a rua
    /// parecer uma rua.
    ///
    /// Deixaram de chegar quando uma delas passou a fundir-se. O momento da noite das
    /// 02:47 e *a lampada da rua apaga-se enquanto ele bebe agua* — e uma lampada que
    /// nao tem candeeiro nao se funde: desaparece um bocado de chao. O jogador ve a
    /// rua ficar mais escura e nao tem onde pousar a explicacao, e uma mudanca sem
    /// explicacao inocente possivel quebra a regra que governa todas as outras.
    ///
    /// Com um poste la, a mesma coisa le-se como o que e: aquele candeeiro apagou-se.
    ///
    /// ---
    ///
    /// **Nao ha asset novo.** O pack `Sat Productions / Street Lights Pack 01` ja esta
    /// no projecto e ja e usado pela estrada da noite. Isto e o mesmo poste, na rua
    /// deste predio.
    ///
    /// A luz sobe para o cimo do poste. Estava a 3,18 m do alcatrao por ser um numero
    /// escolhido sem nada por onde o medir; o candeeiro tem 3,95 m, e a lampada dele
    /// esta onde esta. Depois disto o angulo com que a luz cai na rua passa a ter uma
    /// razao geometrica em vez de ser um valor no Inspector.
    ///
    /// Re-executavel. Sem colisores: nada disto e alcancavel, esta dezoito metros
    /// abaixo do apartamento.
    /// </summary>
    internal static class StreetLampWiring
    {
        private const string Root = "STREET_LAMPS";
        private const string LampPrefab =
            "Assets/Sat Productions/G-01/Street Lights Pack 01/Prefabs/Street_Light_A_01.prefab";

        /// <summary>Superficie da rua, sondada na malha do `CITY_EXTERIOR`.</summary>
        private const float StreetY = -21.18f;

        /// <summary>Altura da lampada no poste, medida no proprio prefab (3,95 m de alto).</summary>
        private const float LampHeight = 3.60f;

        [MenuItem("Pungent/Blockout/Wire Street Lamps (apartment)", false, 21)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireStreetLamps")) return;

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Apartment"))
            {
                Debug.LogError("[Candeeiros] Abrir a `Apartment_Blockout_V2` primeiro.");
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(LampPrefab);
            if (asset == null)
            {
                Debug.LogError("[Candeeiros] Sem o prefab em " + LampPrefab +
                               ". E o mesmo pack que a estrada da noite usa.");
                return;
            }

            var old = GameObject.Find(Root);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Wire street lamps");

            // Debaixo do `CITY_EXTERIOR`, para acompanhar o resto da cidade se alguem
            // a mover ou desligar em bloco.
            var city = GameObject.Find("CITY_EXTERIOR");
            if (city != null) root.transform.SetParent(city.transform, true);

            var log = new List<string>();
            int built = 0;

            foreach (var light in Object.FindObjectsOfType<Light>(true))
            {
                if (!light.gameObject.name.StartsWith("StreetGlow_")) continue;

                Vector3 at = light.transform.position;

                var post = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
                post.name = "Post_" + light.gameObject.name.Substring("StreetGlow_".Length);
                post.transform.position = new Vector3(at.x, StreetY, at.z);

                // Virado para a estrada: os do lado norte olham para sul e vice-versa.
                // Le-se pelo Z do proprio nome do sitio onde estao.
                post.transform.rotation = Quaternion.Euler(0f, at.z > 0f ? 180f : 0f, 0f);

                // Nada disto e tocavel nem pisavel — esta dezoito metros abaixo do
                // apartamento. Os colisores importados so custam memoria.
                foreach (var collider in post.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

                // As luzes que o proprio prefab traz ficam desligadas: quem manda na
                // iluminacao desta rua sao os `StreetGlow_*`, e um deles tem de poder
                // fundir-se. Duas fontes no mesmo poste e uma lampada que se apaga
                // sem a rua escurecer.
                foreach (var own in post.GetComponentsInChildren<Light>(true))
                    own.enabled = false;

                GameObjectUtility.SetStaticEditorFlags(post,
                    StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.ReflectionProbeStatic);

                // A lampada sobe para o cimo do poste.
                light.transform.position = new Vector3(at.x, StreetY + LampHeight, at.z);
                EditorUtility.SetDirty(light);

                built++;
            }

            EditorSceneManager.MarkSceneDirty(scene);

            log.Add(built + " candeeiros postos na rua, a " + StreetY.ToString("F2") + " m");
            log.Add("as lampadas subiram para " + (StreetY + LampHeight).ToString("F2") +
                    " m (estavam a -18,00, sem nada que as segurasse)");
            log.Add("o `StreetGlow_N_-8` — o que se funde na noite das 02:47 — passa a ter poste");

            Debug.Log("[Candeeiros] Ligados.\n  " + string.Join("\n  ", log));
        }
    }
}
