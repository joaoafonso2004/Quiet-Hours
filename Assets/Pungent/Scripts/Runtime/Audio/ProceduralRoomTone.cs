using UnityEngine;

namespace Pungent.Audio
{
    /// <summary>
    /// Room tone gerado em memória, sem depender de clips.
    ///
    /// Um apartamento silencioso a 02:47 é o que mais denuncia um protótipo: o
    /// ouvido lê ausência total de ruído como "isto não é um sítio". Cada divisão
    /// recebe um leito de ruído filtrado com carácter próprio — o quarto abafado,
    /// o corredor com um zumbido eléctrico, a cozinha com os electrodomésticos.
    ///
    /// O clip é curto e em loop; a variação vem do filtro e de uma modulação lenta,
    /// não do comprimento do buffer.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class ProceduralRoomTone : MonoBehaviour
    {
        public enum Character { Bedroom, Corridor, Living, Kitchen, Bathroom }

        [SerializeField] private Character character = Character.Bedroom;
        [SerializeField, Range(0f, 1f)] private float volume = 0.03f;
        [Tooltip("Alcance em que se ouve. Room tone é presença, não evento.")]
        [SerializeField, Min(0.5f)] private float minDistance = 1f;
        [SerializeField, Min(2f)] private float maxDistance = 4.5f;
        [SerializeField] private int seed = 0;

        private const int SampleRate = 44100;
        private const int Seconds = 4;

        /// <summary>
        /// Pico a que cada clip é normalizado.
        ///
        /// Estava a 0.9, ou seja praticamente escala cheia antes sequer de chegar ao
        /// volume da fonte. Como o apartamento tem oito destes e os alcances se
        /// sobrepõem, ouviam-se três a cinco ao mesmo tempo e a soma saturava — daí
        /// o ruído de fundo soar estourado em vez de soar a casa.
        /// </summary>
        private const float TargetPeak = 0.30f;

        private void Awake()
        {
            var source = GetComponent<AudioSource>();
            source.clip = BuildClip();
            source.loop = true;
            source.playOnAwake = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.volume = volume;
            source.dopplerLevel = 0f;
            source.Play();
        }

        private AudioClip BuildClip()
        {
            int length = SampleRate * Seconds;
            var data = new float[length];
            var random = new System.Random(seed != 0 ? seed : character.GetHashCode());

            // Parâmetros por divisão: corte do passa-baixo, quanto zumbido de rede
            // eléctrica entra, e profundidade da respiração lenta.
            float lowPass;
            float hum;
            float breath;
            switch (character)
            {
                case Character.Corridor: lowPass = 0.035f; hum = 0.30f; breath = 0.25f; break;
                case Character.Living: lowPass = 0.020f; hum = 0.10f; breath = 0.35f; break;
                case Character.Kitchen: lowPass = 0.055f; hum = 0.45f; breath = 0.15f; break;
                case Character.Bathroom: lowPass = 0.045f; hum = 0.18f; breath = 0.20f; break;
                default: lowPass = 0.014f; hum = 0.06f; breath = 0.40f; break;
            }

            float filtered = 0f;
            for (int i = 0; i < length; i++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                // Passa-baixo de um pólo: transforma ruído branco em leito grave.
                filtered += (white - filtered) * lowPass;

                float t = i / (float)SampleRate;
                // 50 Hz é a frequência da rede na Europa; é o que dá o zumbido de
                // casa. Baixo de propósito: é a parte mais áspera quando várias
                // divisões se ouvem ao mesmo tempo, e já há o buzz das lâmpadas.
                float mains = Mathf.Sin(t * Mathf.PI * 2f * 50f) * 0.008f * hum;
                float slow = 1f + Mathf.Sin(t * Mathf.PI * 2f * 0.07f) * breath * 0.5f;

                data[i] = (filtered * 3.2f + mains) * slow;
            }

            NormaliseAndFadeLoop(data);

            var clip = AudioClip.Create($"RoomTone_{character}", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Normaliza e cruza as pontas, senão o loop dá um estalo audível a cada
        /// volta — que é pior do que não haver room tone nenhum.
        /// </summary>
        private static void NormaliseAndFadeLoop(float[] data)
        {
            float peak = 0f;
            foreach (float sample in data) peak = Mathf.Max(peak, Mathf.Abs(sample));
            if (peak > 0.0001f)
            {
                float gain = TargetPeak / peak;
                for (int i = 0; i < data.Length; i++) data[i] *= gain;
            }

            int fade = Mathf.Min(4096, data.Length / 4);
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                float head = data[i];
                float tail = data[data.Length - fade + i];
                data[data.Length - fade + i] = Mathf.Lerp(tail, head, t);
            }
        }
    }
}
