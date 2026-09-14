using UnityEditor;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Router no fim do corredor: troca a caixa do graybox pelo modelo e afina os
    /// dois LEDs indicadores.
    /// </summary>
    internal static class ApartmentV2RouterWiring
    {
        private const string RouterModelPath = "Assets/ThirdParty/router/router.fbx";

        /// <summary>
        /// Troca a caixa do router pelo modelo e corrige os indicadores: o LED
        /// vermelho era uma esfera grande com um ponto de luz de raio 0.5, que
        /// pintava um circulo vermelho na parede.
        /// </summary>
        internal static void AttachModel()
        {
            var router = GameObject.Find("Router");
            if (router == null) { Debug.LogWarning("[WireV2] Router nao encontrado."); return; }

            var box = router.GetComponent<MeshRenderer>();
            if (box != null) box.enabled = false;

            // O root do router era um cubo com escala nao uniforme (0.5, 0.16, 0.14):
            // um modelo filho herdava essa deformacao. Passa a escala 1 e o colisor
            // recebe as dimensoes explicitamente.
            router.transform.localScale = Vector3.one;
            var collider = router.GetComponent<BoxCollider>();
            if (collider != null) collider.size = new Vector3(0.24f, 0.06f, 0.20f);

            var stale = router.transform.Find("Router_Model");
            if (stale != null) Object.DestroyImmediate(stale.gameObject);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(RouterModelPath);
            if (source == null)
            {
                Debug.LogWarning($"[WireV2] Modelo de router em falta: {RouterModelPath}");
                return;
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            model.name = "Router_Model";
            model.transform.SetParent(router.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * 0.08f;

            var body = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pungent/Materials/M_Router_Body.mat");
            if (body != null)
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
                    r.sharedMaterial = body;

            // Assenta o modelo na prateleira e centra-o no root.
            Bounds bounds = new Bounds();
            bool first = true;
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); }
            if (!first)
            {
                Vector3 offset = router.transform.position - bounds.center;
                offset.y = router.transform.position.y - bounds.min.y;
                model.transform.position += offset;
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
                { bounds.Encapsulate(r.bounds); }
            }

            PlaceIndicators(router, model, bounds);
            TuneIndicator(router, "Router_Red", "Router_RedLight", new Color(1f, 0.06f, 0.02f));
            TuneIndicator(router, "Router_Green", "Router_GreenLight", new Color(0.1f, 1f, 0.2f));
        }

        /// <summary>
        /// Assenta os dois LEDs na face do router virada para a divisao, derivando a
        /// posicao das bounds reais do modelo em vez de coordenadas magicas.
        /// </summary>
        private static void PlaceIndicators(GameObject router, GameObject model, Bounds bounds)
        {
            // O router esta rodado 90 em Y, pelo que o seu forward e +X (a parede) e
            // a face virada para a divisao e -forward. Usar `right` mandava os LEDs
            // para o lado errado.
            Vector3 front = -router.transform.forward;
            Vector3 side = router.transform.right;
            float halfDepth = Vector3.Scale(bounds.extents, Abs(front)).magnitude;
            float halfWidth = Vector3.Scale(bounds.extents, Abs(side)).magnitude;

            Vector3 faceCentre = bounds.center + front * (halfDepth + 0.004f);
            faceCentre.y = bounds.min.y + Mathf.Min(0.035f, bounds.size.y * 0.35f);

            Place(router, "Router_Red", faceCentre - side * (halfWidth * 0.32f));
            Place(router, "Router_Green", faceCentre + side * (halfWidth * 0.32f));
            Place(router, "Router_RedLight", faceCentre - side * (halfWidth * 0.32f) + front * 0.012f);
            Place(router, "Router_GreenLight", faceCentre + side * (halfWidth * 0.32f) + front * 0.012f);
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private static void Place(GameObject parent, string childName, Vector3 worldPosition)
        {
            var child = parent.transform.Find(childName);
            if (child != null) child.position = worldPosition;
        }

        private static void TuneIndicator(GameObject router, string indicatorName, string lightName, Color colour)
        {
            var indicator = router.transform.Find(indicatorName);
            if (indicator != null)
            {
                // LED pequeno e emissivo em vez de uma esfera grande e baça.
                indicator.localScale = new Vector3(0.010f, 0.008f, 0.008f);
                var renderer = indicator.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    var material = renderer.sharedMaterial;
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    material.SetColor("_EmissionColor", colour * 4.5f);
                    material.SetColor("_BaseColor", colour * 0.7f);
                    EditorUtility.SetDirty(material);
                }
            }

            var lightTransform = router.transform.Find(lightName);
            if (lightTransform == null) return;
            var light = lightTransform.GetComponent<Light>();
            if (light == null) return;

            // O LED deve ler-se como um ponto, nao iluminar a prateleira toda.
            light.range = 0.07f;
            light.intensity = 0.08f;
            light.color = colour;
            light.shadows = LightShadows.None;
            EditorUtility.SetDirty(light);
        }
    }
}
