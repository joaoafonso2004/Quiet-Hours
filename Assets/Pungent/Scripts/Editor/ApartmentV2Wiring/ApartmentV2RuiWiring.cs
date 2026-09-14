using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Pungent.NPC;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Tudo o que faz do NPC_Rui um habitante: posicao e circuito de rotina, o
    /// humano PSX que substitui a capsula, a voz na cabeca e o volume territorial
    /// do quarto dele.
    /// </summary>
    internal static class ApartmentV2RuiWiring
    {
        private const string RuiCharacter = "Character_16";

        internal static void WireRoutine(Scene scene)
        {
            var rui = GameObject.Find("NPC_Rui");
            if (rui == null) { Debug.LogWarning("[WireV2] NPC_Rui nao encontrado."); return; }

            var agent = rui.GetComponent<UnityEngine.AI.NavMeshAgent>();
            bool agentWasEnabled = agent != null && agent.enabled;
            if (agent != null) agent.enabled = false;
            // Pivo nos pes: alinha com os anchors (y = 0), com a NavMesh e faz com que
            // o alvo de olhar (+1.35 m) passe a apontar ao peito em vez de ao teto.
            rui.transform.position = new Vector3(-3.94f, 0f, 3.70f);
            rui.transform.localScale = Vector3.one;
            rui.transform.rotation = Quaternion.identity;
            if (agent != null) agent.enabled = agentWasEnabled;

            var anchors = new Dictionary<string, Transform>();
            var art = ApartmentV2Dressing.FindRoot(scene);
            if (art != null)
                foreach (var t in art.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("RUI_"))
                        anchors[t.name] = t;

            var route = new List<Transform>();
            foreach (var name in ApartmentV2Dressing.RoutineRoute)
            {
                if (anchors.TryGetValue(name, out var t)) route.Add(t);
                else Debug.LogWarning($"[WireV2] Anchor de rotina em falta: {name}");
            }

            var routine = rui.GetComponent<PrototypeNpcRoutine>();
            if (routine == null) { Debug.LogWarning("[WireV2] PrototypeNpcRoutine em falta."); return; }

            var so = new SerializedObject(routine);
            var array = so.FindProperty("waypoints");
            array.arraySize = route.Count;
            for (int i = 0; i < route.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = route[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Os waypoints antigos do graybox deixam de ser referenciados.
            foreach (var n in new[] { "Rui_Waypoint_Living", "Rui_Waypoint_Kitchen", "Rui_Waypoint_Hall" })
            {
                var go = GameObject.Find(n);
                if (go != null) go.SetActive(false);
            }
        }

        /// <summary>
        /// Substitui a capsula placeholder do Rui pelo humano PSX, sem apagar nada:
        /// os renderers antigos ficam apenas desativados e todos os componentes de
        /// comportamento continuam no NPC_Rui.
        /// </summary>
        internal static void WireModel()
        {
            var rui = GameObject.Find("NPC_Rui");
            if (rui == null) return;

            // Collider coerente com o raio do agente de navegacao.
            var capsule = rui.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                capsule.center = new Vector3(0f, 0.9f, 0f);
                capsule.height = 1.8f;
                capsule.radius = 0.32f;
            }

            var agent = rui.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.baseOffset = 0f;

            // Placeholder desativado, nao apagado.
            var placeholder = rui.GetComponent<MeshRenderer>();
            if (placeholder != null) placeholder.enabled = false;
            var head = rui.transform.Find("Rui_Head");
            if (head != null) head.gameObject.SetActive(false);

            var stale = rui.transform.Find(RuiCharacter);
            if (stale != null) Object.DestroyImmediate(stale.gameObject);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ThirdParty/CharactersPSX/Models/Male/" + RuiCharacter + ".fbx");
            if (source == null)
            {
                Debug.LogWarning($"[WireV2] Modelo '{RuiCharacter}' nao encontrado.");
                return;
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = RuiCharacter;
            model.transform.SetParent(rui.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Pungent/Materials/M_PSX_" + RuiCharacter + ".mat");
            if (material != null)
                foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.sharedMaterial = material;

            // Sem clips no pack, a bind pose e uma T-pose: postura provisoria por codigo.
            if (model.GetComponent<Animator>() != null && model.GetComponent<HumanoidRestPose>() == null)
                model.AddComponent<HumanoidRestPose>();

            // Cabeca a seguir o jogador em conversa, stare-down e expulsao.
            var headLook = model.GetComponent<HumanoidHeadLook>();
            if (headLook == null) headLook = model.AddComponent<HumanoidHeadLook>();

            var playerRoot = GameObject.Find("PlayerRoot");
            var playerCamera = playerRoot != null ? playerRoot.GetComponentInChildren<Camera>(true) : null;
            var headLookSo = new SerializedObject(headLook);
            if (playerCamera != null)
                headLookSo.FindProperty("target").objectReferenceValue = playerCamera.transform;
            headLookSo.ApplyModifiedPropertiesWithoutUndo();

            var routineSo = new SerializedObject(rui.GetComponent<PrototypeNpcRoutine>());
            routineSo.FindProperty("headLook").objectReferenceValue = headLook;
            routineSo.ApplyModifiedPropertiesWithoutUndo();

            MoveVoiceToHead(rui, model);
        }

        /// <summary>
        /// A voz indistinta tem de sair da cabeca do NPC (secção 12.4 do plano).
        /// Com o pivo nos pes, deixada no root a voz vinha do chao.
        /// </summary>
        private static void MoveVoiceToHead(GameObject rui, GameObject model)
        {
            var animator = model.GetComponent<Animator>();
            Transform headBone = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Head)
                : null;
            if (headBone == null)
            {
                Debug.LogWarning("[WireV2] Sem bone de cabeca: a voz fica no root do Rui.");
                return;
            }

            var oldVoice = rui.GetComponent<Pungent.Dialogue.NpcMumbleVoice>();
            var oldSource = rui.GetComponent<AudioSource>();

            var anchorTransform = headBone.Find("Rui_VoiceAnchor");
            GameObject anchor = anchorTransform != null ? anchorTransform.gameObject : null;
            if (anchor == null)
            {
                anchor = new GameObject("Rui_VoiceAnchor");
                anchor.transform.SetParent(headBone, false);
                anchor.transform.localPosition = Vector3.zero;
            }

            var source = anchor.GetComponent<AudioSource>();
            if (source == null) source = anchor.AddComponent<AudioSource>();
            if (oldSource != null)
            {
                source.spatialBlend = oldSource.spatialBlend;
                source.minDistance = oldSource.minDistance;
                source.maxDistance = oldSource.maxDistance;
                source.rolloffMode = oldSource.rolloffMode;
                source.volume = oldSource.volume;
            }
            else
            {
                source.spatialBlend = 1f;
                source.minDistance = 0.8f;
                source.maxDistance = 9f;
            }
            source.playOnAwake = false;

            var voice = anchor.GetComponent<Pungent.Dialogue.NpcMumbleVoice>();
            if (voice == null) voice = anchor.AddComponent<Pungent.Dialogue.NpcMumbleVoice>();
            if (oldVoice != null)
            {
                EditorUtility.CopySerialized(oldVoice, voice);
                Object.DestroyImmediate(oldVoice);
            }

            // Repontar quem usava a voz antiga.
            var interactable = rui.GetComponent<NpcDialogueInteractable>();
            if (interactable != null)
            {
                var so = new SerializedObject(interactable);
                so.FindProperty("mumbleVoice").objectReferenceValue = voice;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Volume do quarto do Rui. Entrar la dentro fa-lo acorrer e mandar sair,
        /// e ele nao retoma o circuito enquanto o jogador nao sair.
        /// </summary>
        internal static void WireTerritory(Scene scene)
        {
            var rui = GameObject.Find("NPC_Rui");
            var player = GameObject.Find("PlayerRoot");
            if (rui == null || player == null) { Debug.LogWarning("[WireV2] Territorio: Rui ou jogador em falta."); return; }

            var existing = ApartmentV2WiringUtil.Find(scene, "TERRITORY_Rui_Bedroom");
            if (existing != null) Object.DestroyImmediate(existing);

            var go = new GameObject("TERRITORY_Rui_Bedroom");
            go.transform.SetParent(rui.transform.parent, true);
            // Quarto do Rui: X [-3.4, -0.1], Z [-5.0, -0.8]. Ligeiramente recolhido
            // para o limiar da porta nao disparar sozinho.
            go.transform.position = new Vector3(-1.75f, 1.1f, -2.95f);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3.2f, 2.2f, 4.0f);

            var territory = go.AddComponent<NpcRoomTerritory>();
            var so = new SerializedObject(territory);
            so.FindProperty("owner").objectReferenceValue = rui.GetComponent<PrototypeNpcRoutine>();
            so.FindProperty("player").objectReferenceValue = player.transform;
            so.FindProperty("dialogue").objectReferenceValue = player.GetComponent<Pungent.Dialogue.WorldDialogueController>();
            // A voz vive num anchor na cabeça do modelo, não no root.
            so.FindProperty("voice").objectReferenceValue =
                rui.GetComponentInChildren<Pungent.Dialogue.NpcMumbleVoice>(true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
