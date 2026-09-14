using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Recolocacao do conteudo herdado do graybox na planta V2: jogador, router,
    /// secretaria e o que deixa de servir.
    /// </summary>
    internal static class ApartmentV2LayoutWiring
    {
        internal static void MovePlayer()
        {
            var player = GameObject.Find("PlayerRoot");
            if (player == null) { Debug.LogWarning("[WireV2] PlayerRoot nao encontrado."); return; }

            // Ao lado da cama do Tomas, virado para a porta do quarto (norte).
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.position = new Vector3(-4.30f, 0.05f, -3.05f);
            player.transform.rotation = Quaternion.identity;
            if (controller != null) controller.enabled = true;

            var yaw = player.transform.Find("ViewYaw");
            if (yaw != null) yaw.localRotation = Quaternion.identity;
        }

        internal static void MoveRouterAndDesk()
        {
            // Router no fim do corredor, na parede este da entrada.
            // y = 1.07 e o topo da prateleira (centro 1.02 + meia espessura 0.05):
            // a 1.25 o router ficava a flutuar acima dela.
            ApartmentV2WiringUtil.Move("Router_Shelf", new Vector3(5.55f, 1.02f, -0.30f), new Vector3(0f, 90f, 0f));
            // 180 em vez de 90: a 90 o router ficava de lado para quem chega pelo corredor.
            ApartmentV2WiringUtil.Move("Router", new Vector3(5.60f, 1.07f, -0.30f), new Vector3(0f, 180f, 0f));

            // Secretaria e portatil do graybox, sob a janela sul do quarto do Tomas.
            // Posicoes absolutas de proposito: um deslocamento relativo tornava
            // este metodo nao idempotente e afastava a secretaria a cada re-execucao.
            ApartmentV2WiringUtil.Move("Desk_Top", new Vector3(-4.50f, 0.78f, -4.65f), null);
            ApartmentV2WiringUtil.Move("Desk_Leg_L", new Vector3(-5.073f, 0.39f, -4.65f), null);
            ApartmentV2WiringUtil.Move("Desk_Leg_R (1)", new Vector3(-3.914f, 0.39f, -4.65f), null);
            ApartmentV2WiringUtil.Move("Laptop_Glow (1)", new Vector3(-4.392f, 1.17f, -4.50f), null);

            // Ambiente 3D do frigorifico segue o novo frigorifico da cozinha.
            ApartmentV2WiringUtil.Move("AMB_FridgeDrone", new Vector3(-6.64f, 1.15f, 4.53f), null);
        }

        internal static void DisableLegacyDressing(Scene scene)
        {
            // Substituido pelo ART_PASS_V2, mas mantido na cena para poder ser reavaliado.
            var legacy = ApartmentV2WiringUtil.Find(scene, "ART_PASS_QUATERNIUS");
            if (legacy != null) legacy.SetActive(false);
        }
    }
}
