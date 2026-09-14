using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Configura os FBX de animação do Mixamo e constrói o AnimatorController do Rui.
    ///
    /// Re-executável: reconfigura os importers, recria o controller e volta a ligá-lo
    /// ao modelo em cena. Acrescentar animações novas é acrescentar linhas em
    /// <see cref="Locomotion"/> ou em <see cref="OneShots"/>.
    /// </summary>
    public static class RuiAnimatorSetup
    {
        private const string AnimationFolder = "Assets/ThirdParty/Mixamo/Animations/";
        private const string ControllerFolder = "Assets/Pungent/Animation/";
        private const string ControllerPath = ControllerFolder + "AC_Rui.controller";
        private const string CharacterPath = "Assets/ThirdParty/CharactersPSX/Models/Male/Character_16.fbx";

        public const string SpeedParameter = "Speed";

        /// <summary>
        /// Clips de locomoção: a que velocidade (m/s) cada um corresponde e a que
        /// ritmo é reproduzido.
        ///
        /// O `timeScale` é o que resolve o deslizar de pés. Os clips do Mixamo são
        /// autorados a ~1.4 m/s e o circuito doméstico corre a 0.8 m/s, pelo que
        /// reproduzir o walk à velocidade nominal fazia os pés patinarem no chão.
        /// 0.8 / 1.4 ≈ 0.6.
        /// </summary>
        ///
        /// ---
        ///
        /// **O `run` estava a um limiar que o Rui nunca atinge.**
        ///
        /// O limiar dele era 3.2 m/s. A velocidade mais alta que existe no
        /// `PrototypeNpcRoutine` e o `evictSpeed`, a 2.4 — o circuito anda a 0.85 e
        /// o regresso a 1.1. Num blend tree 1D o peso e interpolado entre limiares,
        /// portanto a 2.4 dava **(2.4-0.8)/(3.2-0.8) = 67% de corrida e 33% de
        /// andar**, misturados. A corrida nunca foi vista sozinha, nem uma vez: o
        /// que aparecia era sempre uma pessoa a arrastar-se depressa, que e
        /// exactamente o aspecto de uma animacao fraca.
        ///
        /// O limiar passa a ser a velocidade que ele mesmo tem quando corre. E o
        /// `timeScale` acompanha: o clip esta autorado para os tais 3.2 m/s, e
        /// toca-lo a ritmo cheio enquanto o corpo avanca a 2.4 punha os pes a
        /// andar mais depressa do que o chao. 2.4/3.2 = 0.75.
        private static readonly (string File, float Threshold, float TimeScale)[] Locomotion =
        {
            ("idle", 0.0f, 1.0f),
            ("walk", 0.80f, 0.60f),
            ("run", 2.4f, 0.75f),
        };

        /// <summary>
        /// Ações contextuais. Ficam como estados soltos no controller e são
        /// chamadas por `Animator.CrossFade` a partir da rotina — assim não é
        /// preciso desenhar transições nem parâmetros para cada uma, e acrescentar
        /// uma ação nova é acrescentar uma linha aqui.
        /// </summary>
        public static readonly (string State, string File, bool Loop)[] Actions =
        {
            ("Stove", "CounterLean", true),
            ("CounterLean", "CounterLean", true),
            ("Fridge", "fridge", false),
            ("CouchSit", "sit_watching_tv", true),
            ("PhoneIdle", "idle_phone", true),
            ("TurnLeft", "turn_left", false),
            ("TurnRight", "turn_right", false),

            // Transicoes de sentar. Sem elas o CrossFade saltava direto para a pose
            // sentada e ele aparecia no sofa em vez de se sentar nele.
            ("SitDown", "sit_down", false),
            ("StandUp", "stand_up", false),

            // O clip "stare" e na pratica um idle tenso, nao um encarar. Serve as
            // duas coisas e por isso entra duas vezes: como pose do stare-down e
            // como idle de quem ja nao esta descontraido.
            ("Stare", "stare", true),
            ("IdleAlert", "stare", true),

            // Olhar para tras a meio do circuito, sem o jogador estar a olhar. E a
            // coisa mais barata que faz um NPC parecer que sabe que estas la.
            ("LookOverShoulder", "look_over_shoulder", false),

            // Expulsao do quarto do Rui.
            ("RoomKick", "rui_room_kick", false),

            // Bater a porta do quarto do Tomas, no Dia 3. Duas ou tres batidas com o
            // punho; a seguir ele encosta-se a ombreira e fica la — ver a
            // `DoorConversation`, que pede este estado e o `ListenAtDoor` por ordem.
            //
            // **O clip ainda nao existe na pasta.** Isso nao e erro: o
            // `ConfigureAnimation` avisa e salta a linha, e a conversa a porta corre
            // na mesma com o som e sem o gesto. Largar `knock.fbx` em
            // `ThirdParty/Mixamo/Animations/` e correr esta ferramenta chega — nao ha
            // mais nada a ligar em lado nenhum.
            ("Knock", "knock", false),

            // ----------------------------------------------------------------
            // A caca do Dia 5.
            //
            // Ate aqui o `RuiHunt` so mexia no `Speed`: ele atravessava a casa a
            // andar e fazia tudo o resto em pe, parado, com a mesma pose com que
            // cozinha. Procurar uma pessoa pela casa e olhar por baixo de uma cama
            // liam-se exactamente da mesma maneira — nenhuma.
            // ----------------------------------------------------------------

            // A pausa em cada divisao. E o unico momento em que o jogador o pode
            // atravessar por tras, e por isso tem de se ver que ele esta a olhar
            // para outro lado.
            ("SearchScan", "search_scan", false),

            // Do lado de fora da porta do quarto. A cena inteira depende de haver
            // uma folha fechada entre os dois; a pose e o que a torna uma pessoa
            // encostada a porta em vez de um NPC parado num corredor.
            ("ListenAtDoor", "listen_at_door", true),

            // ----------------------------------------------------------------
            // Espreitar da esquina.
            //
            // O `RuiPeeking` pedia um estado `WallLean` que **nunca existiu**. Um
            // `CrossFade` para um estado que o controller nao tem nao da erro: nao
            // faz nada. O sistema corria inteiro — ele escolhia a esquina, ia la,
            // esperava, era visto e recolhia — e em todo esse tempo ficava na pose
            // de idle de quem espera o autocarro. Nao havia nada partido a apontar,
            // e por isso ninguem foi la ver.
            //
            // O corpo e o mesmo do `listen_at_door`: alguem encostado a uma
            // superficie, de lado, quieto. Encostado a uma ombreira le-se como
            // espreitar; e o mesmo emprestimo que o clip `stare` ja faz duas vezes.
            ("Peek", "listen_at_door", true),

            // O instante em que e apanhado. Nao e recuar — e a cabeca a voltar para
            // tras um segundo antes de ele sair, que e o que o jogador leva consigo.
            ("PeekWithdraw", "look_over_shoulder", false),

            // Debaixo da cama e dentro do roupeiro. A tensao de estar escondido
            // depende de ele **parecer** que verifica.
            ("CrouchLookUnder", "crouch_look_under", false),

            // Abrir uma porta. Duas, porque a folha roda sempre para o mesmo lado
            // do mundo e ele atravessa cada vao nos dois sentidos ao longo de um
            // circuito: do corredor para o quarto empurra, do quarto para o
            // corredor puxa. Qual das duas se toca decide-se pelo lado em que ele
            // esta — ver `RuiHunt.OpenNearbyDoor`.
            ("OpenDoorPush", "open_door_push", false),
            ("OpenDoorPull", "open_door_pull", false),
        };

        [MenuItem("Pungent/Blockout/Setup Rui Animator", false, 40)]
        public static void Setup()
        {
            if (BuildGuard.Blocked("RuiAnimator")) return;

            Avatar avatar = LoadAvatar(CharacterPath);
            if (avatar == null)
            {
                Debug.LogError($"[RuiAnimator] Sem avatar em {CharacterPath}. O modelo está importado como Humanoid?");
                return;
            }

            var clips = new Dictionary<string, AnimationClip>();
            foreach (var (file, _, _) in Locomotion)
            {
                AnimationClip clip = ConfigureAnimation(file, avatar, loop: true);
                if (clip != null) clips[file] = clip;
            }

            foreach (var (_, file, loop) in Actions)
            {
                if (clips.ContainsKey(file)) continue;
                AnimationClip clip = ConfigureAnimation(file, avatar, loop);
                if (clip != null) clips[file] = clip;
            }

            if (clips.Count == 0)
            {
                Debug.LogError("[RuiAnimator] Nenhum clip válido encontrado em " + AnimationFolder);
                return;
            }

            BuildController(clips);
            AssignToScene();
        }

        // ------------------------------------------------------------------

        private static Avatar LoadAvatar(string modelPath)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (asset is Avatar found && found.isValid)
                    return found;
            return null;
        }

        /// <summary>
        /// Aplica os import settings que o circuito do Rui exige e devolve o clip.
        /// O Bake Into Pose em XZ é obrigatório: sem ele a animação desloca o
        /// personagem e entra em conflito com o NavMeshAgent e com o MoveTowards.
        /// </summary>
        private static AnimationClip ConfigureAnimation(string fileName, Avatar avatar, bool loop)
        {
            string path = AnimationFolder + fileName + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[RuiAnimator] Ficheiro em falta: {path}");
                return null;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            // Avatar próprio para cada FBX de animação, sempre.
            //
            // "Copy From Other Avatar" exige a MESMA hierarquia e a MESMA escala do
            // modelo de destino. Os FBX do Mixamo vêm a globalScale 1 e o
            // Character_16 está a 0.38, pelo que copiar o avatar fazia a posição
            // vertical do root ser interpretada na escala errada e o Rui afundava
            // no chão. Com avatar próprio o retargeting passa pelo espaço de
            // músculos, que é normalizado pela altura, e a escala deixa de importar.
            // Resolve ao mesmo tempo o namespace numerado ("mixamorig9:") do walk.
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.sourceAvatar = null;

            var clipSettings = importer.defaultClipAnimations;
            for (int i = 0; i < clipSettings.Length; i++)
            {
                clipSettings[i].loopTime = loop;
                clipSettings[i].loopPose = loop;

                // Root motion travado: quem move o NPC é a rotina, não a animação.
                clipSettings[i].lockRootRotation = true;
                clipSettings[i].keepOriginalOrientation = false;
                clipSettings[i].lockRootHeightY = true;
                clipSettings[i].keepOriginalPositionY = false;
                clipSettings[i].heightFromFeet = true;
                clipSettings[i].lockRootPositionXZ = true;
                clipSettings[i].keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clipSettings;
            importer.SaveAndReimport();

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;

            Debug.LogWarning($"[RuiAnimator] Sem clip utilizável em {path}");
            return null;
        }

        private static void BuildController(Dictionary<string, AnimationClip> clips)
        {
            if (!AssetDatabase.IsValidFolder(ControllerFolder))
                AssetDatabase.CreateFolder("Assets/Pungent", "Animation");

            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(SpeedParameter, AnimatorControllerParameterType.Float);

            // Blend tree 1D: sem transições para gerir, e a mistura acompanha a
            // velocidade real do NPC em vez de estados discretos.
            var blendTree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = SpeedParameter,
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(blendTree, controller);

            foreach (var (file, threshold, _) in Locomotion)
                if (clips.TryGetValue(file, out AnimationClip clip))
                    blendTree.AddChild(clip, threshold);

            // O timeScale só é aplicável depois de os filhos existirem.
            var children = blendTree.children;
            int index = 0;
            foreach (var (file, _, timeScale) in Locomotion)
            {
                if (!clips.ContainsKey(file)) continue;
                if (index >= children.Length) break;
                children[index].timeScale = timeScale;
                index++;
            }
            blendTree.children = children;

            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.AddState("Locomotion");
            state.motion = blendTree;
            stateMachine.defaultState = state;

            // Estados soltos, sem transições: entram e saem por CrossFade.
            foreach (var (stateName, file, _) in Actions)
            {
                if (!clips.TryGetValue(file, out AnimationClip clip)) continue;
                var actionState = stateMachine.AddState(stateName);
                actionState.motion = clip;
                actionState.writeDefaultValues = false;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RuiAnimator] Controller criado com {blendTree.children.Length} clips de locomoção.");
        }

        private static void AssignToScene()
        {
            var rui = GameObject.Find("NPC_Rui");
            if (rui == null) { Debug.LogWarning("[RuiAnimator] NPC_Rui não está na cena."); return; }

            var animator = rui.GetComponentInChildren<Animator>(true);
            if (animator == null) { Debug.LogWarning("[RuiAnimator] Sem Animator no modelo do Rui."); return; }

            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            // A pose provisória deixa de fazer falta assim que há clips reais.
            var restPose = animator.GetComponent<Pungent.NPC.HumanoidRestPose>();
            if (restPose != null) restPose.enabled = false;

            var routine = rui.GetComponent<Pungent.NPC.PrototypeNpcRoutine>();
            if (routine != null)
            {
                var so = new SerializedObject(routine);
                so.FindProperty("animator").objectReferenceValue = animator;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(animator);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            Debug.Log("[RuiAnimator] Controller ligado ao Rui; HumanoidRestPose desativado.");
        }
    }
}
