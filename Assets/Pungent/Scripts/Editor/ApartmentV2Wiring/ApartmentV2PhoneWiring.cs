using UnityEditor;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>Telemovel diegetico que o jogador traz na mao.</summary>
    internal static class ApartmentV2PhoneWiring
    {
        private const string PhoneModelPath = "Assets/ThirdParty/phone.fbx";

        /// <summary>
        /// Troca a caixa do HeldPhone pelo modelo real, sem lhe mexer no
        /// GameObject: o PhoneLightController, o AudioSource e a lanterna vivem la
        /// e reparenta-los partiria as referencias da cena.
        /// </summary>
        internal static void AttachModel()
        {
            var held = GameObject.Find("HeldPhone");
            if (held == null) { Debug.LogWarning("[WireV2] HeldPhone nao encontrado."); return; }

            // O root tinha escala (0.13, 0.23, 0.025) a fazer de aparelho.
            // Com escala 1 o modelo entra sem deformacao.
            held.transform.localScale = Vector3.one;
            var placeholder = held.GetComponent<MeshRenderer>();
            if (placeholder != null) placeholder.enabled = false;
            var collider = held.GetComponent<BoxCollider>();
            if (collider != null) collider.size = new Vector3(0.075f, 0.146f, 0.012f);

            var stale = held.transform.Find("PhoneModel");
            if (stale != null) Object.DestroyImmediate(stale.gameObject);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PhoneModelPath);
            if (source == null) { Debug.LogWarning($"[WireV2] {PhoneModelPath} em falta."); return; }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = "PhoneModel";
            model.transform.SetParent(held.transform, false);
            model.transform.localPosition = Vector3.zero;
            // O telemovel fica a frente da camara, pelo que o jogador ve a face -Z
            // do HeldPhone. O ecra do modelo vinha em +Z: 180 poe-o do lado certo.
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            model.transform.localScale = Vector3.one;

            var body = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pungent/Materials/M_Phone_Body.mat");
            if (body != null)
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
                {
                    // O FBX foi importado sem materiais e a malha tem varios
                    // submeshes: preencher so o slot 0 deixava os restantes com o
                    // material "Lit" por omissao do URP.
                    var slots = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < slots.Length; i++) slots[i] = body;
                    r.sharedMaterials = slots;
                }

            // O quad do brilho era o "ecra" do prototipo. Agora o ecra e o Canvas,
            // e este quad so tapava a frente do aparelho: fica so como referencia
            // do PhoneLightController, sem renderizar.
            var glow = held.transform.Find("PhoneScreen");
            if (glow != null)
            {
                glow.localPosition = new Vector3(0f, 0.073f, -0.0060f);
                glow.localScale = new Vector3(0.067f, 0.133f, 0.001f);
                var glowRenderer = glow.GetComponent<MeshRenderer>();
                if (glowRenderer != null) glowRenderer.enabled = false;
            }

            // Só a posição: a rotação da lanterna já estava afinada e mexer nela
            // mudaria a direção do foco.
            var flashlight = held.transform.Find("PhoneFlashlight");
            if (flashlight != null)
                flashlight.localPosition = new Vector3(0f, 0.130f, -0.008f);
        }
    }
}
