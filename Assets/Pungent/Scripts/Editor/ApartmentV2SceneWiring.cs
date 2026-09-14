using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Liga o conteudo herdado do graybox a planta V2: portas fisicas, posicao do
    /// jogador, router, secretaria, luzes por divisao e circuito do Rui.
    ///
    /// Re-executavel. Corre depois de "Rebuild Apartment V2" e "Dress Apartment V2".
    /// Nao apaga nada do graybox: o que deixa de servir e apenas desativado.
    ///
    /// Este ficheiro e so a ordem dos passos. Cada passo vive numa classe propria
    /// em ApartmentV2Wiring/, porque a ordem e o unico acoplamento real entre eles:
    /// os interruptores precisam das luzes ja colocadas, o territorio do Rui precisa
    /// do modelo (a voz mudou para a cabeca) e a NavMesh precisa das portas na cena.
    /// </summary>
    public static class ApartmentV2SceneWiring
    {
        [MenuItem("Pungent/Blockout/Wire Apartment V2", false, 30)]
        public static void Wire()
        {
            if (BuildGuard.Blocked("WireV2")) return;

            var scene = EditorSceneManager.GetActiveScene();

            int doorLayer = ApartmentV2WiringUtil.EnsureLayer("Door");
            ApartmentV2DoorWiring.Build(scene, doorLayer);

            ApartmentV2LayoutWiring.MovePlayer();
            ApartmentV2LayoutWiring.MoveRouterAndDesk();
            ApartmentV2RouterWiring.AttachModel();

            ApartmentV2LightingWiring.PlaceLights();

            ApartmentV2RuiWiring.WireRoutine(scene);
            ApartmentV2RuiWiring.WireModel();
            ApartmentV2RuiWiring.WireTerritory(scene);

            ApartmentV2NarrativeWiring.WireIntro(scene);
            ApartmentV2NarrativeWiring.WireOpeningSequence(scene);
            ApartmentV2NarrativeWiring.WireDeskFlavour();

            ApartmentV2PhoneWiring.AttachModel();

            // Depois de PlaceLights: cada interruptor procura as suas Light pelo nome.
            ApartmentV2LightingWiring.BuildSwitches(scene);

            ApartmentV2AudioWiring.BuildRoomTones(scene);
            ApartmentV2AudioWiring.TuneAmbientSources();

            ApartmentV2LayoutWiring.DisableLegacyDressing(scene);

            // Por ultimo: as portas ja existem e a sua layer tem de ficar de fora.
            ApartmentV2NavigationWiring.Configure(scene, doorLayer);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[WireV2] Cena ligada a planta V2.");
        }
    }
}
