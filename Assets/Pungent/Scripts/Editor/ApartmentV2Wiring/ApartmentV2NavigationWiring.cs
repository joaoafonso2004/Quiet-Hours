using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pungent.EditorTools
{
    /// <summary>NavMesh do interior domestico. Corre no fim, ja com as portas na cena.</summary>
    internal static class ApartmentV2NavigationWiring
    {
        /// <summary>
        /// As folhas das portas sao dinamicas e nao podem ser cozidas na NavMesh estatica:
        /// se o forem, isolam casa de banho, lavandaria, arrumos e varanda.
        /// </summary>
        internal static void Configure(Scene scene, int doorLayer)
        {
            var nav = ApartmentV2WiringUtil.Find(scene, "NAVIGATION");
            if (nav == null) { Debug.LogWarning("[WireV2] NAVIGATION nao encontrado."); return; }

            var surface = nav.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
            if (surface == null) { Debug.LogWarning("[WireV2] NavMeshSurface nao encontrado."); return; }

            // Exclui portas E cidade. Antes so tirava as portas e, como este metodo
            // reescreve a mascara inteira, apagava a exclusao feita pelo
            // CityExteriorBuilder e a NavMesh voltava a cobrir a rua toda.
            int mask = ~(1 << doorLayer);
            int cityLayer = LayerMask.NameToLayer("CityExterior");
            if (cityLayer >= 0) mask &= ~(1 << cityLayer);
            surface.layerMask = mask;

            // Interior domestico: o voxel de 0.1 m e o minRegionArea de 2 m2 por omissao
            // apagavam casa de banho, lavandaria e arrumos inteiras.
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.06f;
            surface.minRegionArea = 0.4f;

            surface.BuildNavMesh();
            EditorUtility.SetDirty(surface);
        }
    }
}
