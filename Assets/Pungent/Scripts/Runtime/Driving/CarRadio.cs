using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// O radio do carro: duas musicas a conduzir, uma terceira depois de o motor
    /// morrer.
    ///
    /// As duas primeiras sao 2m29 e 1m52 — 4m21 ao todo, que e a medida da viagem.
    /// A conducao dura o que elas durarem, e quando a segunda acaba o motor morre.
    /// Nao e enchimento: e o unico relogio que o jogador ouve.
    ///
    /// **A terceira toca com o carro parado.** O motor morre, o radio nao — fica a
    /// dar da bateria. Ele sai para ver o motor e a musica continua atras dele,
    /// abafada, de dentro do carro. E por isso que a fonte e 3D e nao 2D: ao volante
    /// a musica vem de todo o lado porque a cabeca dele esta la dentro, e assim que
    /// ele sai a musica fica onde o carro ficou. Passa a ser um sitio, e nao uma
    /// banda sonora.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class CarRadio : MonoBehaviour
    {
        [Tooltip("As que tocam a conduzir. A viagem dura o que elas durarem.")]
        [SerializeField] private AudioClip[] drivingTracks = new AudioClip[0];

        [Tooltip("A que fica a tocar depois de o motor morrer, de dentro do carro. "
               + "Vazio = o radio cala-se com o motor.")]
        [SerializeField] private AudioClip afterStopTrack;

        [SerializeField] private CarDriver car;

        [Tooltip("Pausa entre faixas, como quem muda de musica.")]
        [SerializeField, Min(0f)] private float gapSeconds = 1.2f;

        [Tooltip("A musica de depois entra mais baixa: o carro esta morto e ela vem "
               + "de dentro dele.")]
        [SerializeField, Range(0f, 1f)] private float afterStopVolume = 0.75f;

        [Header("Abafado, ca fora")]
        [SerializeField] private CarSeat seat;
        [SerializeField] private AudioLowPassFilter lowPass;

        [Tooltip("Corte com a cabeca dentro do carro: som de colunas, aberto.")]
        [SerializeField] private float insideCutoff = 7500f;

        [Tooltip("Corte com ele ca fora. Os agudos ficam presos no vidro; do lado de "
               + "fora sobra o baixo e o corpo da musica.")]
        [SerializeField] private float outsideCutoff = 780f;

        [Tooltip("E tambem mais baixa: a chapa segura parte do volume, nao so o brilho.")]
        [SerializeField, Range(0f, 1f)] private float outsideVolume = 0.62f;

        [Tooltip("Quanto tempo a passagem demora. Instantanea denunciava o truque; "
               + "isto e a porta a abrir-se.")]
        [SerializeField, Min(0.01f)] private float muffleSeconds = 0.45f;

        private AudioSource source;
        private int next;
        private float nextTrackAt = -1f;
        private float drivingVolume;
        private bool switchedToAfter;
        private float cutoff = 7500f;
        private float muffleVolume = 1f;

        /// <summary>Quantas faixas de conducao ja comecaram. 2 = esta na segunda.</summary>
        public int TrackIndex => next;

        /// <summary>
        /// As musicas de conducao acabaram.
        ///
        /// E isto que manda o carro morrer, e nao uma distancia fixa: assim as duas
        /// tocam sempre inteiras, conduza-se depressa ou devagar.
        /// </summary>
        public bool Finished =>
            next >= drivingTracks.Length && !switchedToAfter && !source.isPlaying;

        /// <summary>Soma das faixas de conducao, com as pausas. Em segundos.</summary>
        public float DrivingSeconds
        {
            get
            {
                float total = 0f;
                foreach (var clip in drivingTracks)
                    if (clip != null) total += clip.length + gapSeconds;
                return total;
            }
        }

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            drivingVolume = source.volume;

            if (car == null) car = GetComponentInParent<CarDriver>();
            if (seat == null) seat = FindObjectOfType<CarSeat>();
            if (lowPass == null) lowPass = GetComponent<AudioLowPassFilter>();

            if (lowPass != null) cutoff = insideCutoff;
        }

        private void Start() => PlayNextDriving();

        private void Update()
        {
            UpdateMuffle();

            // O motor morreu: passar a musica de depois, uma vez so.
            if (!switchedToAfter && car != null && !car.EngineRunning)
            {
                switchedToAfter = true;
                PlayAfterStop();
                return;
            }

            if (switchedToAfter || source.isPlaying || drivingTracks.Length == 0) return;

            if (nextTrackAt < 0f) nextTrackAt = Time.time + gapSeconds;
            else if (Time.time >= nextTrackAt) PlayNextDriving();
        }

        /// <summary>
        /// Abafa a musica quando ele sai do carro.
        ///
        /// A fonte ja e 3D, portanto a distancia trata-se sozinha — mas distancia so
        /// nao chega: a dois metros do carro, de fora, a musica continuava a soar
        /// aberta como se as portas nao existissem. O que diz "isto vem de dentro"
        /// sao os agudos a desaparecerem primeiro, que e o que o vidro faz.
        ///
        /// Ele passa o fim do capitulo ca fora, com o capo levantado e a musica atras
        /// das costas. E o som que o poe la, e nao o volume.
        /// </summary>
        private void UpdateMuffle()
        {
            if (lowPass == null) return;

            bool outside = seat != null && !seat.Seated;
            float targetCutoff = outside ? outsideCutoff : insideCutoff;
            float targetVolume = outside ? outsideVolume : 1f;

            // Exponencial e nao linear: em frequencia, 7500 -> 780 a passos iguais
            // ouve-se como um salto no fim. Em oitavas ouve-se como uma porta.
            float blend = 1f - Mathf.Exp(-Time.deltaTime / muffleSeconds);
            cutoff = Mathf.Lerp(Mathf.Log(cutoff), Mathf.Log(targetCutoff), blend);
            cutoff = Mathf.Exp(cutoff);
            lowPass.cutoffFrequency = cutoff;

            muffleVolume = Mathf.Lerp(muffleVolume, targetVolume, blend);
            source.volume = BaseVolume * muffleVolume;
        }

        /// <summary>Volume da faixa a tocar, antes de o abafamento entrar.</summary>
        private float BaseVolume => switchedToAfter ? drivingVolume * afterStopVolume : drivingVolume;

        private void PlayNextDriving()
        {
            nextTrackAt = -1f;
            if (next >= drivingTracks.Length) return;

            var clip = drivingTracks[next++];
            if (clip == null) return;

            source.clip = clip;
            source.volume = BaseVolume * muffleVolume;   // o abafamento manda no volume
            source.Play();
        }

        private void PlayAfterStop()
        {
            if (afterStopTrack == null) { source.Stop(); return; }

            // Em ciclo: ele fica ali o tempo que quiser a olhar para o motor, e o
            // silencio a meio dava a sensacao de que alguem desligou o radio.
            source.Stop();
            source.clip = afterStopTrack;
            source.loop = true;
            source.volume = BaseVolume * muffleVolume;
            source.Play();
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorSetTracks(AudioClip[] driving, AudioClip afterStop)
        {
            drivingTracks = driving;
            afterStopTrack = afterStop;
        }
#endif
    }
}
