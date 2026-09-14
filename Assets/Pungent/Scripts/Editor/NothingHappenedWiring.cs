using System.Collections.Generic;
using System.Linq;
using Pungent.Interaction;
using Pungent.Narrative;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Monta o <see cref="NothingHappened"/>: o Rui a chegar a casa na manha do
    /// Dia 3, com o Tomas dentro do quarto dele, e a nao acontecer nada.
    ///
    /// ---
    ///
    /// **A ferramenta nao cria quase nada, e e esse o ponto.** Tudo o que a cena
    /// precisa ja existe na casa: as tres portas, os passos do Rui com o filtro das
    /// paredes, os quatro esconderijos do Dia 5 e a mudez da divisao. O que faltava
    /// era um dono que os pusesse pela ordem certa durante quarenta segundos.
    ///
    /// A unica coisa nova e a fonte do puxador — e ate essa fica sem clip, porque a
    /// folha a estremecer contra o aro ja diz o que ha a dizer.
    ///
    /// ---
    ///
    /// **A caixa do quarto sai da malha do chao.** O `Floor_Bedroom_Rui` mede
    /// 3,30 x 4,46 e esta em (-1,75 / -3,03); escrever esses numeros a mao era
    /// escrever quatro numeros que ficam errados quando alguem mexer numa parede,
    /// sem dar erro nenhum. Sai das bounds do renderer, como os destinos das
    /// tarefas domesticas ja saem.
    ///
    /// **O `DEADAIR_RuiRoom` e passado como guarda e nao como efeito.** Ver a nota
    /// do campo `roomSilence`: os dois disparam na mesma condicao e sobrepo-los
    /// apaga a casa para sempre.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class NothingHappenedWiring
    {
        private const string Group = "DAY3_NothingHappened";

        /// <summary>Onde o grupo vive. O Dia 3 ja tem raiz propria.</summary>
        private const string ParentRoot = "DAY3_SIGNS";

        /// <summary>A chave a entrar na 3B. Carregada por caminho, como o resto.</summary>
        private const string UnlockClip = "Assets/ThirdParty/Audio/door_unlock.mp3";

        /// <summary>Os quatro sitios onde caber. Sao do Dia 5 e sao emprestados.</summary>
        private static readonly string[] SpotNames =
        {
            "HIDE_Wardrobe", "HIDE_UnderBed", "HIDE_Couch", "HIDE_Storage"
        };

        [MenuItem("Pungent/Narrativa/Wire Nothing Happened (Day 3)", false, 64)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("NothingHappened")) return;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.name.StartsWith("Apartment"))
            {
                Debug.LogError("[Nada aconteceu] Abre a cena do apartamento primeiro.");
                return;
            }

            var rui = ApartmentV2WiringUtil.Find(scene, "NPC_Rui");
            if (rui == null) { Debug.LogError("[Nada aconteceu] Sem NPC_Rui."); return; }

            // ---- o grupo ----
            var parent = ApartmentV2WiringUtil.Find(scene, ParentRoot);
            if (parent == null)
            {
                Debug.LogError($"[Nada aconteceu] Sem '{ParentRoot}'. Corre primeiro " +
                               "'Pungent/Blockout/Wire Day 3 (Limits)'.");
                return;
            }

            var old = parent.transform.Find(Group);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var holder = new GameObject(Group);
            holder.transform.SetParent(parent.transform, false);
            Undo.RegisterCreatedObjectUndo(holder, "Wire Nothing Happened");

            // ---- a divisao ----
            Vector3 centre = new Vector3(-1.75f, 0f, -3.03f);
            Vector3 half = new Vector3(1.65f, 3f, 2.23f);

            var floor = ApartmentV2WiringUtil.Find(scene, "Floor_Bedroom_Rui");
            var floorRenderer = floor != null ? floor.GetComponentInChildren<Renderer>(true) : null;
            if (floorRenderer != null)
            {
                var b = floorRenderer.bounds;
                centre = new Vector3(b.center.x, 0f, b.center.z);

                // Uma margem para dentro: com a caixa a bater certo com a parede, um
                // jogador encostado ao vao contava como estando la dentro e a cena
                // comecava com ele ja de saida.
                half = new Vector3(Mathf.Max(0.5f, b.extents.x - 0.35f), 3f,
                                   Mathf.Max(0.5f, b.extents.z - 0.35f));
            }
            else
            {
                Debug.LogWarning("[Nada aconteceu] Sem `Floor_Bedroom_Rui` — a caixa " +
                                 "do quarto fica nos numeros por omissao.");
            }

            // ---- as portas ----
            var front = FindDoor(scene, "Door_Front_3B");
            var bedroom = FindDoor(scene, "Door_Bedroom_Rui");
            var bathroom = FindDoor(scene, "Door_Bathroom");

            if (front == null) Debug.LogWarning("[Nada aconteceu] Sem `Door_Front_3B` — ele entra em silencio.");

            // O som da 3B, emprestado a propria porta.
            //
            // Ela e `staticDoor`, e nesse caso o `DoorDragInteractable` **nunca**
            // toca estes clips: o `ForceOpen` sai antes de chegar la e o `SetOpen`
            // sai a entrada. Ou seja, a porta por onde comeca esta cena abria e
            // fechava em silencio absoluto, sem erro nenhum a dizer porque. Medido a
            // correr. O componente passa a toca-los a mao — e nao ha segundo dono,
            // porque nesta porta nao ha primeiro.
            AudioSource frontSource = null;
            AudioClip[] frontOpening = new AudioClip[0];
            AudioClip[] frontClosing = new AudioClip[0];
            if (front != null)
            {
                var fso = new SerializedObject(front);
                frontSource = fso.FindProperty("audioSource").objectReferenceValue as AudioSource;
                if (frontSource == null) frontSource = front.GetComponent<AudioSource>();
                frontOpening = ReadClipArray(front, "openingClips");
                frontClosing = ReadClipArray(front, "closingClips");

                if (!fso.FindProperty("staticDoor").boolValue)
                    Debug.Log("[Nada aconteceu] A `Door_Front_3B` deixou de ser estatica. " +
                              "Os clips vao tocar duas vezes — uma pela porta e outra por " +
                              "esta cena. Tirar `frontDoorOpening`/`frontDoorClosing`.");
            }

            var unlock = AssetDatabase.LoadAssetAtPath<AudioClip>(UnlockClip);
            if (unlock == null)
                Debug.LogWarning($"[Nada aconteceu] Sem `{UnlockClip}` — a chave nao se ouve, " +
                                 "e a chave e o primeiro som da cena.");
            if (bedroom == null) Debug.LogWarning("[Nada aconteceu] Sem `Door_Bedroom_Rui` — nao ha puxador.");
            if (bathroom == null) Debug.LogWarning("[Nada aconteceu] Sem `Door_Bathroom` — ele vai a lado nenhum.");

            // ---- o puxador ----
            //
            // Fonte propria e nao a da porta: a porta toca os clips dela quando
            // alguem lhe mexe, e o que aqui se quer e um som que sai da fechadura
            // sem a folha se abrir. Fica sem clip — ver a nota do campo no
            // componente.
            var handleGo = new GameObject("SFX_Handle");
            handleGo.transform.SetParent(holder.transform, false);
            handleGo.transform.position = bedroom != null
                ? bedroom.transform.position + Vector3.up * 1.05f
                : new Vector3(-2.00f, 1.05f, -0.80f);
            var handleSource = handleGo.AddComponent<AudioSource>();
            handleSource.playOnAwake = false;
            handleSource.spatialBlend = 1f;
            handleSource.minDistance = 0.8f;
            handleSource.maxDistance = 10f;
            handleSource.rolloffMode = AudioRolloffMode.Linear;

            // ---- os passos ----
            AudioSource steps = null;
            var stepsTransform = rui.transform.Find("Rui_Footsteps");
            if (stepsTransform != null) steps = stepsTransform.GetComponent<AudioSource>();
            if (steps == null)
                Debug.LogWarning("[Nada aconteceu] Sem `Rui_Footsteps` no NPC_Rui — a cena " +
                                 "corre muda, e a cena **e** os passos.");

            AudioClip[] stepClips = BorrowFootstepClips(rui);
            if (stepClips.Length == 0)
                Debug.LogWarning("[Nada aconteceu] Ninguem no NPC_Rui tem clips de passos. " +
                                 "Corre 'Pungent/Blockout/Wire Climax' — e de la que eles vem.");

            // ---- a mudez que ja existe ----
            var roomSilence = FindRoomDeadAir(scene);
            if (roomSilence == null)
                Debug.LogWarning("[Nada aconteceu] Sem `DEADAIR_RuiRoom` — a cena corre na " +
                                 "mesma, mas perde a guarda que a impede de comecar dentro " +
                                 "do silencio da divisao.");

            // ---- os esconderijos emprestados ----
            var group = ApartmentV2WiringUtil.Find(scene, "CLIMAX_CONTENT");
            var spots = new List<HidingSpot>();
            if (group != null)
            {
                foreach (var name in SpotNames)
                {
                    var child = group.transform.Find(name);
                    var spot = child != null ? child.GetComponent<HidingSpot>() : null;
                    if (spot != null) spots.Add(spot);
                    else Debug.LogWarning($"[Nada aconteceu] Sem `{name}` dentro do CLIMAX_CONTENT.");
                }
            }
            else
            {
                Debug.LogWarning("[Nada aconteceu] Sem `CLIMAX_CONTENT`. Corre " +
                                 "'Pungent/Blockout/Wire Climax' — sem esconderijos esta cena " +
                                 "acontece, mas nao ha nada a fazer nela e o Dia 5 nao herda " +
                                 "memoria nenhuma.");
            }

            // ---- o que se cala no Rui ----
            //
            // Apanhado por estado e nao por uma lista de tipos escrita a mao: uma
            // lista fica desactualizada na primeira vez que alguem lhe acrescentar
            // um comportamento, e o sintoma seria a rotina domestica a puxa-lo para
            // o fogao a meio do corredor. O componente guarda o estado de cada um e
            // repoe-o — no Dia 3 quase todos ja vem desligados pelo `DayThreeStage`.
            MonoBehaviour[] suppress = rui.GetComponents<MonoBehaviour>()
                .Where(m => m != null)
                .ToArray();

            var agent = rui.GetComponent<NavMeshAgent>();
            Behaviour[] alsoOff = agent != null ? new Behaviour[] { agent } : new Behaviour[0];

            // ---- o componente ----
            var beat = holder.AddComponent<NothingHappened>();
            beat.EditorConfigure(rui, rui.GetComponentInChildren<Animator>(true),
                suppress, alsoOff, centre, half,
                Object.FindObjectOfType<DayThreeStage>(),
                front, bedroom, bathroom,
                steps, stepClips, handleSource,
                roomSilence, group, spots.ToArray(),
                frontSource, frontOpening, frontClosing, unlock);

            EditorUtility.SetDirty(beat);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[Nada aconteceu] Montado em {ParentRoot}/{Group}. Janela " +
                      "day_two -> day3_ready, dispara com o jogador dentro do quarto do Rui " +
                      $"(caixa {centre.ToString("F2")} +/- {half.x:F2} x {half.z:F2}). " +
                      $"Empresta {spots.Count} esconderijos e {suppress.Length} componentes do Rui. " +
                      "SEM SOM DE PUXADOR de proposito: a folha a estremecer faz o trabalho. " +
                      "Se algum dia houver `door_handle.mp3`, o campo esta em SFX_Handle.");
        }

        private static DoorDragInteractable FindDoor(UnityEngine.SceneManagement.Scene scene, string name)
        {
            var go = ApartmentV2WiringUtil.Find(scene, name);
            return go != null ? go.GetComponent<DoorDragInteractable>() : null;
        }

        /// <summary>
        /// A mudez da divisao, procurada pelo nome exacto que o `DreadPassWiring`
        /// lhe da.
        ///
        /// Pelo nome e nao "o `DeadAir` mais perto do quarto": a casa tem tres e o
        /// que interessa e aquele — os outros dois vivem em janelas que nunca se
        /// cruzam com esta.
        /// </summary>
        private static Pungent.Audio.DeadAir FindRoomDeadAir(UnityEngine.SceneManagement.Scene scene)
        {
            var go = ApartmentV2WiringUtil.Find(scene, "DEADAIR_RuiRoom");
            return go != null ? go.GetComponent<Pungent.Audio.DeadAir>() : null;
        }

        /// <summary>
        /// Os clips dos pes dele, pedidos ao dono que ja os tem.
        ///
        /// Copiados no momento da montagem e nao escolhidos aqui: uma segunda lista
        /// escolhida por esta ferramenta era um segundo dono do som dos passos dele,
        /// que e exactamente o erro que este projecto ja mediu — dois passos por
        /// passo.
        ///
        /// **O `RuiHunt` primeiro, e nao o `PrototypeNpcRoutine`.** A rotina parece
        /// o dono obvio — e ela que manda no corpo dele nos dias normais — mas na
        /// cena os campos dela estao **vazios**: quem tem a fonte e os dez clips e a
        /// caca do Dia 5, montada pelo `Wire Climax`, e a rotina pede-lhos
        /// emprestados a correr (`ShareFootstepAudio`). Ler a rotina dava zero clips
        /// e uma cena muda sem uma unica linha na consola a dizer porque.
        /// </summary>
        private static AudioClip[] BorrowFootstepClips(GameObject rui)
        {
            var hunt = rui.GetComponent<Pungent.NPC.RuiHunt>();
            var clips = ReadClipArray(hunt, "footstepClips");
            if (clips.Length > 0) return clips;

            var routine = rui.GetComponent<Pungent.NPC.PrototypeNpcRoutine>();
            return ReadClipArray(routine, "footstepClips");
        }

        private static AudioClip[] ReadClipArray(Object owner, string property)
        {
            if (owner == null) return new AudioClip[0];

            var so = new SerializedObject(owner);
            var array = so.FindProperty(property);
            if (array == null || !array.isArray) return new AudioClip[0];

            var clips = new AudioClip[array.arraySize];
            for (int i = 0; i < clips.Length; i++)
                clips[i] = array.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
            return clips;
        }
    }
}
