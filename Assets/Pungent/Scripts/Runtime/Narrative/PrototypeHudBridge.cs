using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O HUD do prototipo vive em `Pungent.Interaction`; este intermediario evita
    /// que a camada narrativa passe a depender dele so por causa de uma linha de
    /// texto, e permite trocar o HUD sem mexer nas tarefas.
    ///
    /// Vive num ficheiro seu, e nao dentro do `HomeTaskDirector` onde foi escrito.
    /// O Unity so cria o `MonoScript` de um `MonoBehaviour` quando o ficheiro tem o
    /// nome da classe: enquanto isto esteve dentro de outro ficheiro, o componente
    /// deixava-se acrescentar e parecia bem, mas gravava-se sem script nenhum
    /// (`m_Script: {fileID: 0}`) e voltava do disco como "script em falta". A ponte
    /// ficava desligada e as tarefas domesticas nao diziam nada no HUD, sem erro
    /// nenhum a explicar porque.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PrototypeHudBridge : MonoBehaviour
    {
        [SerializeField] private Pungent.Interaction.PrototypeHUD hud;
        [SerializeField, Min(1f)] private float seconds = 2.6f;

        private void Awake()
        {
            if (hud == null) hud = GetComponent<Pungent.Interaction.PrototypeHUD>();
            if (hud == null) hud = FindObjectOfType<Pungent.Interaction.PrototypeHUD>();
        }

        public void Announce(string message)
        {
            if (hud == null || string.IsNullOrWhiteSpace(message)) return;
            hud.ShowMessage(message, seconds);
        }
    }
}
