using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Parte o `PlayerRoot` em tres e guarda dois prefabs.
    ///
    /// Nao havia prefab do jogador: ele so existia dentro da cena do apartamento,
    /// com duas dezenas de componentes ligados a mao. A garagem, a estrada e o
    /// climax precisam todos de um jogador, e reconstrui-lo a mao em cada cena e
    /// como se perde um projecto.
    ///
    /// So que o `PlayerRoot` nunca foi so o jogador. Tinha lá dentro o
    /// `ChapterDirector`, o `PrologueTracker`, o `DayTwoDirector` — os sistemas que
    /// levam a historia. Clona-lo inteiro para um prefab dava a cada cena o seu
    /// `ChapterDirector`, com a lista de acontecimentos vazia: o `day_four` era
    /// levantado no apartamento e o Dia 4 nunca comecava na garagem. Sem erro
    /// nenhum, como sempre.
    ///
    /// Por isso sao tres montes:
    ///
    /// - **PLAYER** — mover, olhar, interagir, HUD, telemovel, pensamentos. Vai para
    ///   prefab e entra em todas as cenas;
    /// - **GAME_SYSTEMS** — director de capitulos, mensagens, cartao, fade e
    ///   palpebras. Vai para prefab, sobrevive as trocas de cena e volta a atar as
    ///   pontas ao jogador novo (ver <see cref="GameSystemsRoot"/>);
    /// - **APARTMENT_SYSTEMS** — prologo, Dia 2, tarefas domesticas, a secretaria.
    ///   Fica na cena do apartamento, que e o unico sitio onde isso acontece.
    ///
    /// Re-executavel: se ja estiver partido, so volta a gravar os prefabs.
    /// </summary>
    internal static class PlayerPrefabBuilder
    {
        private const string PlayerPrefabPath = "Assets/Pungent/Prefabs/PLAYER.prefab";
        private const string SystemsPrefabPath = "Assets/Pungent/Prefabs/GAME_SYSTEMS.prefab";
        private const string SystemsRoot = "GAME_SYSTEMS";
        private const string ApartmentRoot = "APARTMENT_SYSTEMS";

        /// <summary>Sobrevivem a cena: levam a historia e nao pertencem a sitio nenhum.</summary>
        private static readonly string[] Persistent =
        {
            "ChapterDirector", "PhoneMessageService", "ChapterCard", "ScreenFade", "ScreenEyelid"
        };

        /// <summary>So acontecem no apartamento.</summary>
        private static readonly string[] ApartmentOnly =
        {
            "PlayerDeskOpening", "HomeTaskDirector", "DayTwoDirector", "PrologueStage", "PrologueTracker"
        };

        /// <summary>Filhos do `PlayerRoot` que sao encenacao do prologo, nao jogador.</summary>
        private static readonly string[] ApartmentChildren = { "INTRO_SEQUENCE", "OPENING_QUEST" };

        [MenuItem("Pungent/Blockout/Build Player + Systems Prefabs", false, 15)]
        internal static void Build()
        {
            if (BuildGuard.Blocked("BuildPlayerPrefabs")) return;

            var scene = EditorSceneManager.GetActiveScene();
            var player = GameObject.Find("PlayerRoot");
            if (player == null)
            {
                Debug.LogError("[Player] Sem `PlayerRoot` nesta cena. Correr isto com o " +
                               "apartamento aberto: e de la que se tira o jogador ja afinado.");
                return;
            }

            EnsureFolder();

            var systems = FindOrCreate(SystemsRoot);
            var apartment = FindOrCreate(ApartmentRoot);
            if (systems.GetComponent<GameSystemsRoot>() == null)
                systems.AddComponent<GameSystemsRoot>();

            // Copiar tudo primeiro, so depois apagar. Uma referencia para um
            // componente apagado nao da erro: fica nula e o sistema cala-se. Enquanto
            // os velhos estao vivos ha por onde re-apontar.
            var moved = new Dictionary<Object, Object>();
            Relocate(player, systems, Persistent, moved);
            Relocate(player, apartment, ApartmentOnly, moved);

            int repointed = Repoint(moved);
            foreach (var pair in moved) Object.DestroyImmediate((Component)pair.Key);

            foreach (var childName in ApartmentChildren)
            {
                var child = player.transform.Find(childName);
                if (child != null) child.SetParent(apartment.transform, true);
            }

            SavePrefabs(player, systems);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Player] Partido em tres: {moved.Count} componentes mudaram de dono, " +
                      $"{repointed} referencias re-apontadas. Prefabs em 'Assets/Pungent/Prefabs'.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Pungent/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Pungent", "Prefabs");
        }

        private static GameObject FindOrCreate(string name)
        {
            var found = GameObject.Find(name);
            if (found != null) return found;

            var created = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(created, "Split player root");
            return created;
        }

        /// <summary>
        /// Copia para o destino os componentes indicados, guardando velho -> novo.
        /// O `ComponentUtility` leva os valores todos atras, que e a razao de nao se
        /// reconstruir isto a mao: sao vinte e tal componentes afinados um a um.
        /// </summary>
        private static void Relocate(GameObject from, GameObject to, string[] typeNames,
            Dictionary<Object, Object> moved)
        {
            foreach (var name in typeNames)
            {
                foreach (var component in from.GetComponents<Component>())
                {
                    // O `PlayerRoot` tem um slot de componente pendurado, de um script
                    // que ja nao existe. Vem a null e nao se copia.
                    if (component == null || component.GetType().Name != name) continue;

                    if (!ComponentUtility.CopyComponent(component)) continue;
                    if (!ComponentUtility.PasteComponentAsNew(to)) continue;

                    var pasted = to.GetComponents(component.GetType());
                    if (pasted.Length > 0) moved[component] = pasted[pasted.Length - 1];
                }
            }
        }

        /// <summary>
        /// Manda toda a cena voltar a apontar para os componentes novos. Desce em
        /// arrays e em UnityEvents, que e onde as ligacoes deste projecto se escondem.
        /// </summary>
        private static int Repoint(Dictionary<Object, Object> moved)
        {
            if (moved.Count == 0) return 0;

            int changed = 0;
            foreach (var go in Object.FindObjectsOfType<GameObject>(true))
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;

                    var so = new SerializedObject(component);
                    var it = so.GetIterator();
                    bool dirty = false;

                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference) continue;

                        var value = it.objectReferenceValue;
                        if (value == null) continue;

                        Object replacement;
                        if (!moved.TryGetValue(value, out replacement)) continue;

                        it.objectReferenceValue = replacement;
                        dirty = true;
                        changed++;
                    }

                    if (dirty) so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            return changed;
        }

        /// <summary>
        /// Grava os dois prefabs.
        ///
        /// As ligacoes de um para o outro — o director a saber do HUD, o telemovel a
        /// saber do servico de mensagens — ficam nulas dentro dos ficheiros, porque um
        /// prefab nao pode guardar uma referencia para outro objecto da cena. E de
        /// proposito: e o `Rebind` que as ata no arranque e a cada cena carregada.
        /// </summary>
        private static void SavePrefabs(GameObject player, GameObject systems)
        {
            SaveOne(player, PlayerPrefabPath);
            SaveOne(systems, SystemsPrefabPath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Grava, saiba-se ou nao se o objecto ja veio de um prefab.
        ///
        /// Chamar `SaveAsPrefabAssetAndConnect` sobre uma instancia que ja esta
        /// ligada aquele prefab da "Prefab instance data layout did not match" e nao
        /// grava nada — que e o pior dos dois mundos numa ferramenta feita para se
        /// correr as vezes que forem precisas. Ligada ao mesmo prefab, aplica-se;
        /// ligada a outro, desembrulha-se primeiro.
        /// </summary>
        private static void SaveOne(GameObject go, string path)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                if (source != null && AssetDatabase.GetAssetPath(source) == path)
                {
                    PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
                    return;
                }

                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }

            PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.AutomatedAction);
        }
    }
}
