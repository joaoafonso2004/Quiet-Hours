using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.NPC;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Liga as duas encenacoes do Rui: espreitar das esquinas, e o episodio do
    /// quarto.
    ///
    /// ---
    ///
    /// **Os poleiros estavam nos sitios errados, e ninguem podia ter dado por isso
    /// a olho.**
    ///
    /// Havia quatro, todos no lado nascente da casa — o hall, a porta do quarto do
    /// Tomas, a casa de banho, a varanda. O Rui vive do outro lado: cozinha, bancada,
    /// frigorifico. Medido com `NavMesh.CalculatePath` a partir de cada sitio da
    /// rotina dele:
    ///
    /// - **`Peek_HallCorner` e `Peek_BedroomDoor` nao teem caminho nenhum.** De
    ///   lado nenhum da casa. Estao atras de portas que cortam a NavMesh, e portanto
    ///   nunca foram alcancaveis. Com o codigo antigo — que andava em linha recta
    ///   por cima do transform — ele **ia la a atravessar as paredes**, e e isso que
    ///   a jogar se le como um teleporte;
    /// - da cozinha, o poleiro mais proximo que serve fica a **9,7 m a andar**. A
    ///   ida e mais comprida do que a espreitadela.
    ///
    /// Por isso isto acrescenta tres do lado poente, todos medidos: caminho completo
    /// a partir da cozinha, entre tres e cinco metros, e com parede a volta em pelo
    /// menos tres das oito direccoes — um poleiro sem nada atras de que estar nao e
    /// um poleiro, e uma pessoa parada no meio de uma sala.
    ///
    /// Sao `Transform`s na cena: se a posicao nao servir, arrasta-se e fica.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class RuiEpisodesWiring
    {
        /// <summary>
        /// Poleiros do lado poente. O `forward` e para onde ele fica **virado** — a
        /// zona onde o jogador passa — e nao para a parede.
        /// </summary>
        private static readonly (string Name, Vector3 Position, float Yaw)[] WestAnchors =
        {
            // Ombreira entre a cozinha e o corredor. 3,9 m do fogao, 3,0 do
            // frigorifico, parede em 3 das 8 direccoes.
            ("Peek_KitchenDoorway", new Vector3(-4.57f, 0f, 1.12f), 150f),

            // Fundo do corredor poente. O mais fechado dos tres — parede em 5 das 8
            // — e o que da a silhueta contra o corredor.
            ("Peek_CorridorWest", new Vector3(-6.40f, 0f, 0.10f), 95f),

            // Meio do corredor, a caminho dos quartos. 2,1 m da mesa de jantar.
            ("Peek_CorridorMid", new Vector3(-3.00f, 0f, -0.10f), 75f),
        };

        private const string InsideRoomName = "RUI_RoomInside";
        private const string OutsideRoomName = "RUI_RoomOutside";
        private static readonly Vector3 InsideRoom = new Vector3(-1.90f, 0f, -3.60f);
        private static readonly Vector3 OutsideRoom = new Vector3(-1.95f, 0f, -0.20f);

        [MenuItem("Pungent/Blockout/Wire Rui Episodes", false, 41)]
        internal static void Wire()
        {
            var rui = GameObject.Find("NPC_Rui");
            if (rui == null) { Debug.LogError("[RuiEpisodes] NPC_Rui nao esta na cena."); return; }

            var log = new List<string>();
            Transform root = EpisodeRoot();

            // ---- poleiros ----
            var anchors = new List<Transform>();
            foreach (var existing in ExistingAnchors())
            {
                if (Reachable(rui.transform.position, existing.position))
                {
                    anchors.Add(existing);
                    continue;
                }

                log.Add($"  DESLIGADO: '{existing.name}' nao tem caminho na NavMesh de " +
                        "lado nenhum. Fica na cena, fora da lista.");
            }

            foreach (var (name, position, yaw) in WestAnchors)
            {
                Transform anchor = Ensure(root, name, position, yaw);
                if (!anchors.Contains(anchor)) anchors.Add(anchor);
            }

            // ---- pontos do quarto ----
            Transform inside = Ensure(root, InsideRoomName, InsideRoom, 0f);
            Transform outside = Ensure(root, OutsideRoomName, OutsideRoom, 180f);

            var routine = rui.GetComponent<PrototypeNpcRoutine>();
            var camera = Camera.main;
            var player = GameObject.Find("PlayerRoot");
            var dialogue = Object.FindObjectOfType<WorldDialogueController>();
            var voice = rui.GetComponentInChildren<NpcMumbleVoice>();

            DoorDragInteractable door = null;
            foreach (var d in Object.FindObjectsOfType<DoorDragInteractable>(true))
                if (d.gameObject.name == "Door_Bedroom_Rui") door = d;
            if (door == null) log.Add("  AVISO: 'Door_Bedroom_Rui' nao encontrada; " +
                                      "o episodio corre com a porta aberta.");

            // ---- espreitar: saiu do jogo ----
            //
            // O `RuiPeeking` era o sistema com mais estados do projecto e o que dava
            // mais maneiras de encravar: ele espreitava de costas, em campo aberto,
            // com a pose a arrancar enquanto ainda andava, e a rotina suspensa a meio
            // deixava o agente sem dono. O que ele existia para dar — a duvida de o
            // ter visto a olhar — a `HumanoidHeadLook` ja da sem maquina de estados
            // nenhuma: a cabeca dele segue o Tomas e desiste quando ele passa para
            // tras. Os poleiros ficam na cena; nao custam nada e servem se algum dia
            // isto voltar como aparicao guionada.
            log.Add($"  espreitar: removido ({anchors.Count} poleiros ficam sem uso)");

            // ---- episodio do quarto ----
            var fit = rui.GetComponent<RuiScreamingFit>();
            if (fit == null) fit = Undo.AddComponent<RuiScreamingFit>(rui);
            fit.EditorConfigure(rui.transform, player != null ? player.transform : null,
                routine, inside, outside, door);
            EditorUtility.SetDirty(fit);

            // O grito ja estava na pasta de audio e o campo estava vazio.
            //
            // A ferramenta limitava-se a avisar que faltava preencher, e o aviso e o
            // sitio errado para isto: o ficheiro nao faltava, faltava alguem arrastar
            // um objecto de uma pasta para um campo de um componente que vive dentro
            // do `NPC_Rui`. E o mesmo padrao que o `LifeSoundsWiring` ja resolve para
            // a tosse — quem faz o asset larga o ficheiro com o nome certo e nao mexe
            // em mais nada.
            var fitSo = new SerializedObject(fit);
            var scream = fitSo.FindProperty("screamClip");
            if (scream.objectReferenceValue == null)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/ThirdParty/Audio/scream.mp3");
                if (clip != null)
                {
                    scream.objectReferenceValue = clip;
                    fitSo.ApplyModifiedPropertiesWithoutUndo();
                    log.Add("  grito: `scream.mp3` ligado ao RuiScreamingFit");
                }
                else
                {
                    log.Add("  POR PREENCHER: `Assets/ThirdParty/Audio/scream.mp3` nao " +
                            "existe. O episodio corre todo, em silencio.");
                }
            }

            // ---- confronto ----
            var denial = rui.GetComponent<RuiDenial>();
            if (denial == null) denial = Undo.AddComponent<RuiDenial>(rui);
            denial.EditorConfigure(fit, dialogue, voice, routine);
            EditorUtility.SetDirty(denial);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[RuiEpisodes] Ligado.\n" + string.Join("\n", log));
        }

        /// <summary>Faz o episodio do quarto acontecer agora.</summary>
        [MenuItem("Pungent/Debug/Rui: episodio do quarto agora", false, 211)]
        internal static void FitNow()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[RuiEpisodes] So em Play."); return; }

            var fit = Object.FindObjectOfType<RuiScreamingFit>();
            if (fit == null) { Debug.LogWarning("[RuiEpisodes] Sem RuiScreamingFit na cena."); return; }

            typeof(RuiScreamingFit)
                .GetField("nextAttemptAt", System.Reflection.BindingFlags.NonPublic
                                         | System.Reflection.BindingFlags.Instance)
                ?.SetValue(fit, 0f);

            Debug.Log("[RuiEpisodes] Episodio do quarto pedido. Nao comeca com o jogador " +
                      "colado a porta do quarto dele.");
        }

        private static Transform EpisodeRoot()
        {
            var existing = GameObject.Find("RUI_EPISODES");
            if (existing != null) return existing.transform;

            var root = new GameObject("RUI_EPISODES");
            Undo.RegisterCreatedObjectUndo(root, "Wire Rui Episodes");
            return root.transform;
        }

        /// <summary>Os poleiros que ja la estavam, venham de onde vierem.</summary>
        private static IEnumerable<Transform> ExistingAnchors()
        {
            foreach (var go in Object.FindObjectsOfType<GameObject>(true))
                if (go.name.StartsWith("Peek_") && go.scene.IsValid())
                    yield return go.transform;
        }

        /// <summary>
        /// Ha caminho **completo** ate ali a partir de onde o Rui esta.
        ///
        /// `CalculatePath` devolve verdadeiro mesmo para um caminho parcial, e um
        /// caminho parcial e exactamente o caso que interessa apanhar: o agente
        /// aceita-o, anda ate ao ponto mais proximo que consegue, e fica parado
        /// contra a parede do outro lado da qual esta o destino.
        /// </summary>
        private static bool Reachable(Vector3 from, Vector3 to)
        {
            NavMeshHit start, end;
            if (!NavMesh.SamplePosition(from, out start, 2f, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(to, out end, 1.5f, NavMesh.AllAreas)) return false;

            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path))
                return false;
            return path.status == NavMeshPathStatus.PathComplete;
        }

        private static Transform Ensure(Transform parent, string name, Vector3 position, float yaw)
        {
            Transform existing = parent.Find(name);
            if (existing == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Wire Rui Episodes");
                go.transform.SetParent(parent, false);
                existing = go.transform;
            }

            // Colado a NavMesh: um poleiro a dez centimetros do chao navegavel manda
            // o agente para um destino que ele nunca alcanca, e ele fica a rodar no
            // sitio a tentar la chegar.
            NavMeshHit hit;
            Vector3 placed = NavMesh.SamplePosition(position, out hit, 1.5f, NavMesh.AllAreas)
                ? hit.position
                : position;

            existing.position = placed;
            existing.rotation = Quaternion.Euler(0f, yaw, 0f);
            return existing;
        }
    }
}
