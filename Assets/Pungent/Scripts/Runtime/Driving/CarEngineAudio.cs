using Pungent.Player;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// O motor, gerado em memoria como o room tone do apartamento.
    ///
    /// Metade da sensacao de conduzir e o ouvido: sem motor, o carro e um carrinho
    /// de supermercado com farois. O clip e um ronco curto em loop — fundamental
    /// grave mais harmonicas e um resto de ruido — e a velocidade vive toda no
    /// pitch e no volume, nao no buffer.
    ///
    /// Quando o motor morre, isto morre com ele — e o silencio que sobra e o do
    /// capitulo, so com o radio a tocar de dentro do carro.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class CarEngineAudio : MonoBehaviour
    {
        [SerializeField] private CarDriver car;
        [SerializeField] private PlayerInputReader input;

        [SerializeField, Range(0f, 1f)] private float idleVolume = 0.10f;
        [SerializeField, Range(0f, 1f)] private float throttleVolume = 0.22f;

        [Tooltip("Pitch parado e no batente. O meio vem da velocidade.")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.72f, 1.75f);

        private const int SampleRate = 44100;
        private const float Seconds = 2f;

        private AudioSource source;
        private float volume;

        private void Awake()
        {
            if (car == null) car = GetComponentInParent<CarDriver>();
            if (input == null) input = FindObjectOfType<PlayerInputReader>(true);

            source = GetComponent<AudioSource>();
            source.clip = BuildClip();
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.6f;
            source.maxDistance = 26f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
        }

        private void Update()
        {
            if (car == null) return;

            bool running = car.EngineRunning;
            if (running && !source.isPlaying) source.Play();

            float throttle = input != null && running ? Mathf.Clamp01(Mathf.Abs(input.Move.y)) : 0f;
            float speed01 = Mathf.Clamp01(Mathf.Abs(car.Speed) / car.MaxSpeed);

            float targetVolume = running ? idleVolume + throttleVolume * Mathf.Max(throttle, speed01 * 0.55f) : 0f;
            volume = Mathf.Lerp(volume, targetVolume, 1f - Mathf.Exp(-3.5f * Time.deltaTime));
            source.volume = volume;

            // O grosso do regime vem da velocidade; o acelerador da o resto — pisar
            // a fundo parado ja se ouve a subir antes de o carro mexer.
            source.pitch = Mathf.Lerp(pitchRange.x, pitchRange.y, speed01 * 0.82f + throttle * 0.18f);

            if (!running && volume < 0.005f && source.isPlaying) source.Stop();
        }

        private AudioClip BuildClip()
        {
            int length = (int)(SampleRate * Seconds);
            var data = new float[length];
            var rng = new System.Random(1988);

            // Fundamental de quatro cilindros cansados, com harmonicas cada vez
            // mais timidas e um resto de ruido mecanico.
            float[] partials = { 55f, 110f, 165f, 220f, 330f };
            float[] weights = { 1.00f, 0.55f, 0.30f, 0.18f, 0.08f };

            float noise = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / SampleRate;
                float sample = 0f;
                for (int p = 0; p < partials.Length; p++)
                    sample += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * weights[p];

                // Ruido de baixa frequencia (alisado) para nao soar a orgao.
                noise = Mathf.Lerp(noise, (float)rng.NextDouble() * 2f - 1f, 0.045f);
                sample += noise * 0.55f;

                data[i] = sample;
            }

            // Normalizar e fechar o loop sem estalo.
            float peak = 0f;
            foreach (var s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
            float gain = peak > 0f ? 0.30f / peak : 1f;
            int fade = SampleRate / 25;
            for (int i = 0; i < length; i++)
            {
                float envelope = 1f;
                if (i < fade) envelope = i / (float)fade;
                else if (i > length - fade) envelope = (length - i) / (float)fade;
                data[i] *= gain * envelope;
            }

            var clip = AudioClip.Create("EngineLoop", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
