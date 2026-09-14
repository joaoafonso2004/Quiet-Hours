using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Interaction;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Poe o Vitor a pegar na outra ponta do radiador.
    ///
    /// ---
    ///
    /// **Porque e o radiador e nao uma peca nova.**
    ///
    /// A oficina ja tem tres pecas para carregar — duas jantes e um radiador — e a
    /// `CAR_BOOT` espera exactamente tres para levantar o `parts_loaded`. Acrescentar
    /// uma quarta so para isto obrigava a mexer nessa conta e dava ao jogador mais uma
    /// viagem num dia que ja tem quatro.
    ///
    /// O radiador e o mais alto e o mais desajeitado dos tres, e um radiador e uma
    /// coisa que duas pessoas carregam a serio. Passa a `SharedCarry` e as jantes
    /// ficam como estao. A `BoxDropZone.Accept` ignora a caixa que recebe e limita-se
    /// a contar, por isso a conta das tres continua certa sem lhe tocar.
    ///
    /// ---
    ///
    /// **Os numeros, e porque sao estes.**
    ///
    /// O jogador anda a 2,8 m/s. Com a peca ao colo fica a 62% — 1,74 m/s — e o Vitor
    /// anda a 1,25. **A diferenca e de propósito:** dá para o apressar, e apressa-lo
    /// nao serve de nada. A peca arrasta, ele diz *"Easy."*, e o passo continua a ser
    /// o dele. Se ele andasse a mesma velocidade que o jogador, a trela nunca mordia e
    /// isto era so uma caminhada acompanhada.
    ///
    /// Do monte de pecas ate a bagageira sao 15,4 m. A 1,4 m/s de media dao onze
    /// segundos, e e por isso que as falas estao a quatro segundos umas das outras e
    /// nao a sete: com sete, a terceira nunca chegava a sair.
    ///
    /// ---
    ///
    /// **Correr depois de `Wire Garage (Day 4)` e `Dress Garage`.** Re-executavel.
    /// </summary>
    internal static class SharedCarryWiring
    {
        private const string HeavyPart = "Radiator";

        [MenuItem("Pungent/Blockout/Wire Shared Carry (Day 4)", false, 23)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireSharedCarry")) return;

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Garage"))
            {
                Debug.LogError("[Carga] Abrir a `Garage_Blockout` primeiro.");
                return;
            }

            GameObject part = FindAnywhere(HeavyPart);
            if (part == null)
            {
                Debug.LogError("[Carga] Sem `" + HeavyPart + "` na cena. Correr " +
                               "'Dress Garage' primeiro: e ele que monta as pecas.");
                return;
            }

            var seller = GameObject.Find("SELLER");
            if (seller == null)
            {
                Debug.LogError("[Carga] Sem `SELLER`. Correr 'Wire Garage (Day 4)' primeiro.");
                return;
            }

            var zone = Object.FindObjectOfType<BoxDropZone>(true);
            if (zone == null)
            {
                Debug.LogError("[Carga] Sem `BoxDropZone` (a bagageira).");
                return;
            }

            // A peca deixa de ser carregavel sozinha. Destruir e nao desactivar: um
            // `CarryableBox` desactivado continua a responder ao `Resolve` do
            // interactor pela hierarquia e os dois prompts disputavam o mesmo objecto.
            var solo = part.GetComponent<CarryableBox>();
            if (solo != null) Object.DestroyImmediate(solo);

            var shared = part.GetComponent<SharedCarry>();
            if (shared == null) shared = Undo.AddComponent<SharedCarry>(part);

            // O esforco sai do proprio objecto: e ele que nao levanta.
            var strain = part.GetComponent<AudioSource>();
            if (strain == null)
            {
                strain = part.AddComponent<AudioSource>();
                strain.playOnAwake = false;
                strain.spatialBlend = 1f;
                strain.rolloffMode = AudioRolloffMode.Linear;
                strain.minDistance = 0.8f;
                strain.maxDistance = 9f;
            }

            // Reaproveita o trinco metalico que ja existe: chapa a raspar em chapa.
            var strainClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/ThirdParty/Audio/KenneyRPG/metalLatch.ogg");

            shared.EditorConfigure(seller, zone, string.Empty, strain, strainClip,
                new[]
                {
                    // Banais, todas. A do meio e a unica que interessa, e nao esta
                    // escrita de maneira nenhuma que a distinga das outras — e essa
                    // a regra. Ele nao sabe que disse nada.
                    "Mind the edge, it has a lip on it.",
                    "You teach, right? Tuesdays.",
                    "Ninety was a good price. You will see.",
                });

            var so = new SerializedObject(shared);
            so.FindProperty("pace").floatValue = 1.25f;
            so.FindProperty("carrySpeedFactor").floatValue = 0.62f;
            so.FindProperty("helperGap").floatValue = 1.35f;
            so.FindProperty("secondsBetweenLines").floatValue = 4f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shared);

            // O controlador dos figurantes ganha as duas poses, se ainda nao as tiver.
            PsxCharacter.BystanderController();

            // **Pelo asset e nao pelo `Animator` da cena.** O `HasState` so responde
            // com o animator inicializado, ou seja em Play: em edit mode devolve falso
            // para tudo, incluindo estados que existem de certeza. Perguntado assim,
            // este aviso dizia "EM FALTA" para sempre e treinava-se a ignora-lo.
            bool hasCarry = HasState(seller, "CarryWalk") && HasState(seller, "CarryIdle");

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[Carga] O radiador passa a precisar de dois.\n" +
                      "  o Vitor vem quando o jogador tentar sozinho, e leva-o a 1,25 m/s\n" +
                      "  o jogador pode ir a 1,74 e nao adianta nada: a peca arrasta e ele diz 'Easy.'\n" +
                      "  tres falas a quatro segundos, ao longo dos 15,4 m ate a bagageira\n" +
                      "  poses de carregar: " + (hasCarry ? "ligadas" : "EM FALTA — ele leva de bracos caidos") + "\n" +
                      "  a conta das tres pecas nao muda: a bagageira conta, nao pergunta o que recebeu");
        }

        /// <summary>
        /// Se o controlador daquele figurante tem este estado, perguntado ao asset.
        ///
        /// Ver a nota acima: o `Animator.HasState` precisa do animator vivo e nao
        /// responde fora de Play. O `AnimatorController` sabe sempre.
        /// </summary>
        private static bool HasState(GameObject who, string stateName)
        {
            var animator = who != null ? who.GetComponentInChildren<Animator>(true) : null;
            var controller = animator != null
                ? animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController
                : null;
            if (controller == null || controller.layers.Length == 0) return false;

            foreach (var child in controller.layers[0].stateMachine.states)
                if (child.state != null && child.state.name == stateName &&
                    child.state.motion != null) return true;

            return false;
        }

        private static GameObject FindAnywhere(string name)
        {
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
