using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// Apontar ao radio e desliga-lo.
    ///
    /// A musica e o relogio da viagem e nao se pode perder — mas obrigar alguem a
    /// ouvi-la e outra coisa. Quem quiser conduzir os quatro minutos em silencio
    /// tem o direito, e o botao esta onde estaria num carro: no tablier, a mao
    /// direita de quem conduz.
    ///
    /// Cala, nao para. A faixa continua a correr por baixo, e por isso o motor
    /// morre a hora certa mesmo com o radio desligado — quem o calou nao encurtou a
    /// viagem sem saber, e voltar a liga-lo a meio apanha a musica onde ela ja ia,
    /// como um radio a serio.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RadioToggle : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private string muteText = "Mute radio";
        [SerializeField] private string unmuteText = "Unmute radio";

        public bool Muted { get; private set; }

        public string Prompt => Muted ? unmuteText : muteText;
        public bool HoldToInteract => false;

        private void Awake()
        {
            if (source == null) source = GetComponentInParent<AudioSource>();
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            Muted = !Muted;
            if (source != null) source.mute = Muted;
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }
    }
}
