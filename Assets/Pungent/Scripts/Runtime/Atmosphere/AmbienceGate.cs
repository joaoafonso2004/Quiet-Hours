using UnityEngine;

namespace Pungent.Atmosphere
{
    /// <summary>
    /// Abafa uma fonte de ambiente exterior conforme o jogador esta dentro ou fora,
    /// e conforme a porta esta aberta ou fechada.
    ///
    /// A atenuacao por distancia do Unity nao resolve isto. A cidade tem de soar
    /// vinda de baixo, portanto a fonte esta seis metros abaixo da varanda — e a
    /// essa profundidade o apartamento inteiro fica praticamente a mesma distancia
    /// dela: a varanda dava 74% e o meio da sala 67%. Nao ha curva de rolloff que
    /// separe o que esta a dois metros de diferenca horizontal e seis de altura.
    ///
    /// O que separa dentro de fora e a parede, e isso e uma decisao e nao uma
    /// distancia. Aqui e tomada explicitamente.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbienceGate : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private Transform player;

        [Header("Fora")]
        [Tooltip("A partir deste Z o jogador conta como estando la fora. A porta da "
               + "varanda esta em z = 5.13.")]
        [SerializeField] private float outsideBeyondZ = 5.35f;

        [Header("A porta")]
        [Tooltip("Com ela aberta ouve-se mais, mesmo de dentro.")]
        [SerializeField] private Pungent.Interaction.DoorDragInteractable door;

        [Header("Volumes")]
        [Tooltip("Na varanda.")]
        [SerializeField, Range(0f, 1f)] private float outside = 0.30f;
        [Tooltip("Dentro, com a porta da varanda aberta.")]
        [SerializeField, Range(0f, 1f)] private float insideDoorOpen = 0.16f;
        [Tooltip("Dentro, com a porta fechada. Nao e zero de proposito: uma janela "
               + "de terceiro andar nao veda uma cidade, so a poe longe.")]
        [SerializeField, Range(0f, 1f)] private float insideDoorClosed = 0.07f;

        [Tooltip("Quanto tempo demora a mudar. Instantaneo denunciava a fronteira e "
               + "o jogador ouvia o volume a saltar ao atravessar a porta.")]
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.9f;

        private float current;

        private void Awake()
        {
            if (source == null) source = GetComponent<AudioSource>();
            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor != null) player = motor.transform;
            }

            current = Target();
            if (source != null) source.volume = current;
        }

        private void Update()
        {
            if (source == null) return;

            current = Mathf.MoveTowards(current, Target(),
                Time.deltaTime / Mathf.Max(0.05f, fadeSeconds));
            source.volume = current;
        }

        private float Target()
        {
            if (player == null) return insideDoorClosed;
            if (player.position.z >= outsideBeyondZ) return outside;
            return door != null && door.IsOpen ? insideDoorOpen : insideDoorClosed;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
            Gizmos.DrawLine(new Vector3(-8f, 0f, outsideBeyondZ), new Vector3(8f, 0f, outsideBeyondZ));
            Gizmos.DrawLine(new Vector3(-8f, 3f, outsideBeyondZ), new Vector3(8f, 3f, outsideBeyondZ));
        }
    }
}
