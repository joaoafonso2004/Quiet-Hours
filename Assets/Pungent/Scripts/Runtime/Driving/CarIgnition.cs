using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// A chave a rodar num carro que nao quer pegar.
    ///
    /// "Entrar, trancar, tentar ligar" (§5). As duas primeiras sao gestos que ele
    /// controla; esta nao. E a unica coisa do capitulo que nao depende do jogador —
    /// carregar mais depressa nao adianta nada — e e por isso que e aqui que a
    /// tensao mora.
    ///
    /// Falha um numero fixo de vezes e a seguir pega. Fixo, e nao aleatorio: um
    /// arranque que pegasse a primeira num jogo e nao no outro tirava o momento a
    /// metade dos jogadores, e uma probabilidade que azarasse cinco vezes seguidas
    /// deixava de ser tensao e passava a ser um bug aos olhos de quem joga.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarIgnition : MonoBehaviour
    {
        [SerializeField] private CarDriver car;
        [SerializeField] private AudioSource ignitionAudio;

        [Tooltip("O motor de arranque a rodar sem pegar.")]
        [SerializeField] private AudioClip crankClip;

        [Tooltip("Quando finalmente pega.")]
        [SerializeField] private AudioClip startClip;

        [Tooltip("Quantas vezes falha antes de pegar.")]
        [SerializeField, Min(0)] private int failedAttempts = 2;

        [Tooltip("Quanto dura cada tentativa falhada.")]
        [SerializeField, Min(0.2f)] private float crankSeconds = 1.6f;

        [Tooltip("Pausa entre tentativas. O silencio entre duas e o pior bocado.")]
        [SerializeField, Min(0.1f)] private float betweenSeconds = 0.9f;

        private int attempts;
        private float nextAt = -1f;
        private bool running;

        /// <summary>Ja pegou.</summary>
        public bool Started { get; private set; }

        private void Awake()
        {
            if (car == null) car = GetComponentInParent<CarDriver>();
            if (ignitionAudio == null) ignitionAudio = GetComponent<AudioSource>();
        }

        /// <summary>Comeca a tentar. Chamado quando ele volta a entrar e tranca.</summary>
        public void Begin()
        {
            if (running || Started) return;
            running = true;
            attempts = 0;
            nextAt = Time.time + 0.35f;   // a mao a chegar a chave
        }

        private void Update()
        {
            if (!running || Started || Time.time < nextAt) return;

            if (attempts < failedAttempts)
            {
                attempts++;
                if (crankClip != null && ignitionAudio != null) ignitionAudio.PlayOneShot(crankClip);
                nextAt = Time.time + crankSeconds + betweenSeconds;
                return;
            }

            Started = true;
            running = false;

            if (startClip != null && ignitionAudio != null) ignitionAudio.PlayOneShot(startClip);
            if (car != null) car.StartEngine();
        }
    }
}
