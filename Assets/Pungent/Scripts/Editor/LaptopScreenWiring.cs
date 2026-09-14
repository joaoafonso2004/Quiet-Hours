using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Atmosphere;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Cola o anuncio das pecas no ecra do portatil e liga-o a abertura.
    ///
    /// O mesh do `Laptop_Screen` e o portatil inteiro — base e tampa no mesmo
    /// objecto —, portanto nao ha maneira de descobrir o plano da tampa a partir
    /// das bounds. Os cinco numeros que a posicionam estao todos aqui em cima,
    /// nomeados, para se afinarem de uma vez em vez de se andar a cacar valores
    /// pelo Inspector.
    ///
    /// Re-executavel.
    /// </summary>
    public static class LaptopScreenWiring
    {
        private const string TexturePath = "Assets/ThirdParty/parts.png";
        private const string MaterialPath = "Assets/Pungent/Materials/M_Laptop_Parts.mat";
        private const string QuadName = "Laptop_ScreenImage";

        // --- afinacao da tampa -------------------------------------------------
        // Deslocamento em espaco local do Laptop_Screen, a partir do centro do mesh.
        private static readonly Vector3 LocalOffset = new Vector3(0f, 0.036f, -0.075f);
        // Inclinacao da tampa para tras, em graus. 0 = perpendicular ao chao.
        private const float LidTilt = 14f;
        private const float ScreenWidth = 0.300f;
        private const float ScreenHeight = 0.172f;
        // -----------------------------------------------------------------------

        [MenuItem("Pungent/Blockout/Wire Laptop Screen", false, 35)]
        public static void Wire()
        {
            var host = GameObject.Find("Laptop_Screen");
            if (host == null) { Debug.LogError("[Laptop] Laptop_Screen nao encontrado."); return; }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (texture == null) { Debug.LogError($"[Laptop] Em falta: {TexturePath}"); return; }

            var stale = host.transform.Find(QuadName);
            if (stale != null) Object.DestroyImmediate(stale.gameObject);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = QuadName;
            Undo.RegisterCreatedObjectUndo(quad, "Wire Laptop Screen");
            quad.transform.SetParent(host.transform, false);
            quad.transform.localPosition = LocalOffset;

            // O quad e olhado pelo seu -Z. O host esta rodado 180 em Y, portanto o
            // seu -Z local aponta ao jogador sentado; a tampa inclina para tras.
            quad.transform.localRotation = Quaternion.Euler(-LidTilt, 180f, 0f);
            quad.transform.localScale = new Vector3(ScreenWidth, ScreenHeight, 1f);
            Object.DestroyImmediate(quad.GetComponent<Collider>());

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetMaterial(texture);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var image = host.GetComponent<LaptopScreenImage>();
            if (image == null) image = Undo.AddComponent<LaptopScreenImage>(host);

            var so = new SerializedObject(image);
            so.FindProperty("image").objectReferenceValue = renderer;
            so.ApplyModifiedProperties();

            // Liga a abertura, que e quem o apaga quando a ligacao cai.
            var desk = Object.FindObjectOfType<PlayerDeskOpening>(true);
            if (desk != null)
            {
                var dso = new SerializedObject(desk);
                dso.FindProperty("laptopImage").objectReferenceValue = image;
                dso.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Laptop] Anuncio colado: {ScreenWidth:F3} x {ScreenHeight:F3} m, " +
                      $"inclinacao {LidTilt} graus, offset {LocalOffset:F3}. " +
                      (desk != null ? "Ligado a abertura." : "[PlayerDeskOpening NAO encontrado]"));
        }

        /// <summary>
        /// Material Unlit: um portatil aceso e uma fonte de luz. Com um material
        /// iluminado o anuncio ficava escuro num quarto sem candeeiro aceso — que e
        /// exactamente a situacao da abertura.
        /// </summary>
        private static Material GetMaterial(Texture2D texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.mainTexture = texture;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
