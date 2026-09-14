using UnityEditor;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Poe uma pessoa do pack PSX numa cena.
    ///
    /// O Rui e o `Character_16` desta pasta, montado a mao dentro da cena do
    /// apartamento. A partir do momento em que ha mais gente no jogo — o vendedor
    /// na oficina, o estranho na berma — repetir esse trabalho a mao em cada cena e
    /// o mesmo erro que o `PlayerRoot` custou: o que se monta a mao uma vez
    /// monta-se a mao para sempre.
    ///
    /// Nao monta comportamento nenhum. Da corpo, material e altura certa; quem ele
    /// e, o que diz e como se mexe pertence a quem o chama.
    /// </summary>
    internal static class PsxCharacter
    {
        private const string ModelFolder = "Assets/ThirdParty/CharactersPSX/Models/Male/";
        private const string MaterialFolder = "Assets/Pungent/Materials/M_PSX_";

        /// <summary>Quem ja esta usado, para nao dar duas caras iguais a pessoas diferentes.</summary>
        internal const string Rui = "Character_16";
        internal const string Seller = "Character_11";
        internal const string Stranger = "Character_08";

        /// <summary>
        /// Instancia o modelo, veste-o e devolve-o. Devolve null e avisa se o pack
        /// nao estiver la — quem chama decide se cai para uma capsula.
        /// </summary>
        internal static GameObject Place(Transform parent, string modelName, string objectName)
        {
            string path = ModelFolder + modelName + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                Debug.LogWarning($"[PSX] Sem modelo em {path}.");
                return null;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.name = objectName;
            go.transform.SetParent(parent, false);

            // O FBX vem com o material do importador, que e cinzento. O material URP
            // com a textura da personagem esta a parte, como o do Rui.
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + modelName + ".mat");
            if (material != null)
            {
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++) slots[i] = material;
                    renderer.sharedMaterials = slots;
                }
            }
            else Debug.LogWarning($"[PSX] Sem material para {modelName}; fica cinzento.");

            var animator = go.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.runtimeAnimatorController == null)
                animator.runtimeAnimatorController = BystanderController();

            return go;
        }

        /// <summary>
        /// Um controller minimo para quem nao e o Rui: parado ou a andar.
        ///
        /// Sem isto, um modelo com `Animator` e sem controller fica na pose de bind —
        /// de bracos abertos em T, que e a maneira mais rapida de transformar uma
        /// cena tensa em piada. O `AC_Rui` nao serve: tem catorze estados de rotina
        /// domestica que ninguem aqui vai usar, e transicoes que esperam por
        /// parametros que so o apartamento levanta.
        ///
        /// Os clips sao Mixamo humanoides e estes modelos tem rig `Human`, portanto
        /// retargetam sem trabalho nenhum.
        /// </summary>
        internal static UnityEditor.Animations.AnimatorController BystanderController()
        {
            const string path = "Assets/Pungent/Animation/AC_Bystander.controller";

            var existing = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
            if (existing != null) { EnsurePhone(existing); EnsureCarry(existing); return existing; }

            var controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Walking", AnimatorControllerParameterType.Bool);

            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Idle");
            idle.motion = Clip("idle");
            var walk = machine.AddState("Walk");
            walk.motion = Clip("walk");
            machine.defaultState = idle;

            var toWalk = idle.AddTransition(walk);
            toWalk.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "Walking");
            toWalk.duration = 0.25f;
            toWalk.hasExitTime = false;

            var toIdle = walk.AddTransition(idle);
            toIdle.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "Walking");
            toIdle.duration = 0.3f;
            toIdle.hasExitTime = false;

            EnsurePhone(controller);
            EnsureCarry(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        /// <summary>
        /// As duas poses de carregar a outra ponta de uma coisa pesada.
        ///
        /// **Estados soltos e nao transicoes por parametro**, ao contrario do
        /// `Walking`: quem manda neles e o <see cref="Pungent.Interaction.SharedCarry"/>,
        /// que sabe exactamente quando o agente esta a mexer-se e quando parou. Um
        /// `bool` a mais aqui seria um segundo dono a discutir com ele.
        ///
        /// O `SharedCarry` verifica `HasState` antes de chamar, por isso um controlador
        /// sem estes estados nao rebenta — o homem carrega de bracos caidos, que e
        /// feio e nao e um erro.
        /// </summary>
        private static void EnsureCarry(UnityEditor.Animations.AnimatorController controller)
        {
            if (controller == null) return;

            AddLooseState(controller, "CarryWalk", "carry_walk");
            AddLooseState(controller, "CarryIdle", "carry_idle");
        }

        private static void AddLooseState(UnityEditor.Animations.AnimatorController controller,
            string stateName, string clipName)
        {
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states)
                if (child.state != null && child.state.name == stateName) return;

            var clip = Clip(clipName);
            if (clip == null)
            {
                Debug.LogWarning("[PSX] Sem `" + clipName + "` na pasta de animacoes: o " +
                                 "estado `" + stateName + "` fica por criar.");
                return;
            }

            var state = machine.AddState(stateName);
            state.motion = clip;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Acrescenta a pose de telemovel ao controlador dos figurantes.
        ///
        /// **A cena do Dia 4 sao vinte segundos a olhar para as costas de um homem ao
        /// telemovel.** Sem isto, o que se ve e um figurante em T-pose parado num
        /// canto do patio a falar sozinho — o que nao e ligeiramente pior, e
        /// completamente outra coisa: o momento em que a suspeita domestica vira
        /// perigo concreto passa a ler-se como um erro do jogo.
        ///
        /// O clip ja existia na pasta (`idle_phone.fbx`) e era usado so pelo Rui. Aqui
        /// nao ha modelo novo nem animacao nova: ha um estado a mais num controlador
        /// que ja tinha dois.
        ///
        /// Idempotente: chamado tambem sobre controladores ja criados, porque foi
        /// assim que este ficou de fora — o `return` do inicio devolvia o antigo sem
        /// nunca lhe acrescentar nada.
        /// </summary>
        private static void EnsurePhone(UnityEditor.Animations.AnimatorController controller)
        {
            if (controller == null) return;

            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states)
                if (child.state != null && child.state.name == "Phone") return;

            bool hasParameter = false;
            foreach (var parameter in controller.parameters)
                if (parameter.name == "OnPhone") { hasParameter = true; break; }
            if (!hasParameter)
                controller.AddParameter("OnPhone", AnimatorControllerParameterType.Bool);

            var clip = Clip("idle_phone");
            if (clip == null)
            {
                Debug.LogWarning("[PSX] Sem `idle_phone` na pasta de animacoes: o " +
                                 "vendedor atende em T-pose.");
                return;
            }

            var phone = machine.AddState("Phone");
            phone.motion = clip;

            // De qualquer estado, e nao so do idle: ele atende **depois** de andar
            // ate ao canto, e uma transicao so a partir do idle deixava-o preso na
            // pose de andar se o agente parasse um frame mais tarde.
            var toPhone = machine.AddAnyStateTransition(phone);
            toPhone.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "OnPhone");
            toPhone.duration = 0.35f;
            toPhone.hasExitTime = false;
            toPhone.canTransitionToSelf = false;

            UnityEditor.Animations.AnimatorState idle = null;
            foreach (var child in machine.states)
                if (child.state != null && child.state.name == "Idle") idle = child.state;

            if (idle != null)
            {
                var back = phone.AddTransition(idle);
                back.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "OnPhone");
                back.duration = 0.4f;
                back.hasExitTime = false;
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private static AnimationClip Clip(string fbxName)
        {
            string path = "Assets/ThirdParty/Mixamo/Animations/" + fbxName + ".fbx";
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is AnimationClip clip && !clip.name.StartsWith("__")) return clip;

            Debug.LogWarning($"[PSX] Sem clip em {path}.");
            return null;
        }

        /// <summary>
        /// Altura visivel do modelo ja instanciado, em metros.
        ///
        /// Serve para assentar a pessoa no chao: o pivot destes modelos esta nos pes,
        /// mas as `bounds` de uma malha com skin sao as da pose de bind e nao as do
        /// que se ve — medir e o unico caminho, como sempre neste projecto.
        /// </summary>
        internal static Bounds VisibleBounds(GameObject character)
        {
            var renderers = character.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(character.transform.position, Vector3.one);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
