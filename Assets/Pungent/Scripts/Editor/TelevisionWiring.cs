using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Atmosphere;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Poe a televisao a dar e corrige o gatilho da casa de banho.
    ///
    /// Os dois andam juntos porque sao as duas coisas da sala que existiam na cena
    /// sem nunca acontecerem: a TV era um mesh chapado e a anomalia da casa de
    /// banho estava dentro da casa de banho, onde o jogador nunca entra a caminho
    /// do quarto.
    ///
    /// Re-executavel.
    /// </summary>
    public static class TelevisionWiring
    {
        private const string GlowName = "TV_Glow";
        private const string SpillName = "TV_Spill";

        [MenuItem("Pungent/Blockout/Wire Television + Bathroom Trigger", false, 33)]
        public static void Wire()
        {
            WireTelevision();
            FixBathroomTrigger();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private static void WireTelevision()
        {
            var tv = GameObject.Find("TV");
            if (tv == null) { Debug.LogError("[TV] Objecto 'TV' nao encontrado."); return; }

            var renderer = tv.GetComponent<Renderer>();
            if (renderer == null) { Debug.LogError("[TV] TV sem Renderer."); return; }
            Bounds bounds = renderer.bounds;

            // O sofa esta a oeste (x = 2.25) e a TV a este (x = 5.49): o ecra e a
            // face -X. Derivado das bounds e nao escrito a mao, para aguentar o
            // aparelho ser trocado por outro modelo.
            Vector3 faceNormal = Vector3.left;

            // As bounds do modelo incluem o pe. Dimensionar o quad por elas punha
            // metade dele em cima do suporte e desalinhado do painel — que foi
            // exactamente o que aconteceu. O painel ocupa a parte de cima.
            const float panelHeightFraction = 0.76f;
            const float panelWidthFraction = 0.90f;
            const float panelCentreFraction = 0.60f;   // 0 = base das bounds, 1 = topo

            float panelHeight = bounds.size.y * panelHeightFraction;
            float panelWidth = bounds.size.z * panelWidthFraction;
            float panelCentreY = bounds.min.y + bounds.size.y * panelCentreFraction;

            Vector3 faceCentre = new Vector3(
                bounds.center.x + faceNormal.x * (bounds.extents.x + 0.008f),
                panelCentreY,
                bounds.center.z);

            // --- o quad emissivo ---
            var stale = tv.transform.Find(GlowName);
            if (stale != null) Object.DestroyImmediate(stale.gameObject);

            var glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glow.name = GlowName;
            Undo.RegisterCreatedObjectUndo(glow, "Wire Television");
            glow.transform.SetParent(tv.transform, true);
            glow.transform.position = faceCentre;
            // O quad e olhado pelo seu -Z; virado para -X quer dizer yaw -90.
            glow.transform.rotation = Quaternion.LookRotation(-faceNormal, Vector3.up);
            glow.transform.localScale = new Vector3(panelWidth, panelHeight, 1f);
            Object.DestroyImmediate(glow.GetComponent<Collider>());

            var glowRenderer = glow.GetComponent<MeshRenderer>();
            glowRenderer.sharedMaterial = GetScreenMaterial();
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;

            // --- a luz que pinta a sala ---
            var staleLight = tv.transform.Find(SpillName);
            if (staleLight != null) Object.DestroyImmediate(staleLight.gameObject);

            var spillObject = new GameObject(SpillName);
            Undo.RegisterCreatedObjectUndo(spillObject, "Wire Television");
            spillObject.transform.SetParent(tv.transform, true);
            spillObject.transform.position = faceCentre + faceNormal * 0.35f;

            var spill = spillObject.AddComponent<Light>();
            spill.type = LightType.Point;
            spill.shadows = LightShadows.None;   // uma luz que muda 2x por segundo com sombras custa caro
            spill.range = 6f;
            spill.intensity = 0.55f;

            // --- o componente ---
            var screen = tv.GetComponent<TelevisionScreen>();
            if (screen == null) screen = Undo.AddComponent<TelevisionScreen>(tv);

            var audio = tv.GetComponent<AudioSource>();
            if (audio != null)
            {
                audio.loop = true;
                audio.playOnAwake = true;
                audio.spatialBlend = 1f;
                audio.rolloffMode = AudioRolloffMode.Linear;
                audio.minDistance = 1.2f;
                audio.maxDistance = 8f;
                audio.volume = Mathf.Min(audio.volume, 0.35f);
            }

            var so = new SerializedObject(screen);
            so.FindProperty("glow").objectReferenceValue = glowRenderer;
            so.FindProperty("spill").objectReferenceValue = spill;
            so.FindProperty("audioSource").objectReferenceValue = audio;
            so.ApplyModifiedProperties();

            // O prompt dizia "Turn on TV" e por tras estava um FlavourInteractable,
            // que so devolve um pensamento. O TelevisionScreen passa a ser ele
            // proprio o interactavel, senao o jogo convida a ligar a televisao e
            // depois nao a liga.
            var flavour = tv.GetComponent<Pungent.Interaction.FlavourInteractable>();
            if (flavour != null) Undo.DestroyObjectImmediate(flavour);

            Debug.Log($"[TV] Ligada. Ecra {glow.transform.localScale.x:F2} x " +
                      $"{glow.transform.localScale.y:F2} na face -X, luz a {spill.range} m." +
                      (audio != null && audio.clip == null
                          ? "  Sem clip de som: falta arrastar um para o AudioSource da TV."
                          : string.Empty));
        }

        private static Material GetScreenMaterial()
        {
            const string path = "Assets/Pungent/Materials/M_TV_Screen.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }

            // Unlit: a imagem da televisao nao deve ser sombreada pela luz da sala.
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// O gatilho da anomalia estava em (0.50, 1.00, -1.50) com 2 x 2 x 2, ou
        /// seja inteiramente dentro da casa de banho. So disparava se o jogador
        /// entrasse la durante o passo ReturnToRoom — e o caminho da cozinha para o
        /// quarto e pelo corredor, a passar em frente a porta sem nunca entrar.
        ///
        /// Passa a apanhar a faixa do corredor em frente a porta.
        /// </summary>
        private static void FixBathroomTrigger()
        {
            var trigger = GameObject.Find("Bathroom_Anomaly_Trigger");
            if (trigger == null) { Debug.LogWarning("[TV] Bathroom_Anomaly_Trigger nao encontrado."); return; }

            var box = trigger.GetComponent<BoxCollider>();
            if (box == null) { Debug.LogWarning("[TV] Trigger sem BoxCollider."); return; }

            Vector3 before = trigger.transform.position;
            Vector3 beforeSize = box.size;

            // A porta da casa de banho esta em x = 0.45, z = -0.80; o corredor corre
            // em z = -0.05. A caixa passa a ir do interior ate ao corredor.
            trigger.transform.position = new Vector3(0.90f, 1.0f, -0.75f);
            box.size = new Vector3(2.60f, 2.0f, 2.20f);
            box.isTrigger = true;

            Debug.Log($"[TV] Gatilho da casa de banho: {before:F2} tam {beforeSize:F2}  ->  " +
                      $"{trigger.transform.position:F2} tam {box.size:F2} " +
                      $"(cobre z de {trigger.transform.position.z - box.size.z * 0.5f:F2} " +
                      $"a {trigger.transform.position.z + box.size.z * 0.5f:F2}, corredor em -0.05)");
        }
    }
}
