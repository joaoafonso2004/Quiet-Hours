using UnityEngine;

namespace Pungent.Atmosphere
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class UnstableLight : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float noiseSpeed = 7f;
        [SerializeField, Range(0f, 0.5f)] private float noiseAmount = 0.08f;
        [SerializeField, Min(1f)] private float rareFlickerInterval = 8f;
        [SerializeField, Range(0f, 1f)] private float rareFlickerChance = 0.35f;

        [Header("Zumbido de fluorescente")]
        [Tooltip("Gera e toca o zumbido do balastro, sincronizado com a falha.")]
        [SerializeField] private bool generateBuzz = true;
        [SerializeField, Range(0f, 1f)] private float buzzVolume = 0.10f;
        [SerializeField, Min(1f)] private float buzzMinDistance = 1.6f;
        [SerializeField, Min(2f)] private float buzzMaxDistance = 6.5f;

        private Light source;
        private AudioSource buzz;
        private float baseIntensity;
        private float nextFlickerCheck;
        private float blackoutUntil;
        private float seed;

        private void Awake()
        {
            source = GetComponent<Light>();
            baseIntensity = source.intensity;
            seed = Random.value * 100f;
            nextFlickerCheck = Time.time + rareFlickerInterval;

            if (generateBuzz) SetUpBuzz();
        }

        /// <summary>
        /// Zumbido gerado em memória: 100 Hz (o dobro da rede, que é como soa um
        /// balastro) com harmónicas ásperas por cima. Nasce aqui e não num clip
        /// para ficar garantidamente em fase com o flicker.
        /// </summary>
        private void SetUpBuzz()
        {
            buzz = GetComponent<AudioSource>();
            if (buzz == null) buzz = gameObject.AddComponent<AudioSource>();

            const int rate = 44100;
            const int length = rate;   // 1 s em loop
            var data = new float[length];
            var random = new System.Random(GetInstanceID());

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float fundamental = Mathf.Sin(t * Mathf.PI * 2f * 100f);
                float harmonic = Mathf.Sin(t * Mathf.PI * 2f * 300f) * 0.35f;
                float grit = (float)(random.NextDouble() * 2.0 - 1.0) * 0.12f;
                data[i] = (fundamental + harmonic + grit) * 0.30f;
            }

            // Cruza as pontas para o loop não estalar.
            int fade = 2048;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[length - fade + i] = Mathf.Lerp(data[length - fade + i], data[i], k);
            }

            var clip = AudioClip.Create("FluorescentBuzz", length, 1, rate, false);
            clip.SetData(data, 0);

            buzz.clip = clip;
            buzz.loop = true;
            buzz.playOnAwake = true;
            buzz.spatialBlend = 1f;
            buzz.rolloffMode = AudioRolloffMode.Linear;
            buzz.minDistance = buzzMinDistance;
            buzz.maxDistance = buzzMaxDistance;
            buzz.volume = buzzVolume;
            buzz.dopplerLevel = 0f;
            buzz.Play();
        }

        private void Update()
        {
            // Luz apagada, zumbido calado: o balastro só zumbe com corrente.
            if (!source.enabled)
            {
                if (buzz != null && buzz.isPlaying) buzz.Pause();
                return;
            }
            if (buzz != null && !buzz.isPlaying) buzz.UnPause();

            if (Time.time >= nextFlickerCheck)
            {
                nextFlickerCheck = Time.time + rareFlickerInterval + Random.Range(-2f, 3f);
                if (Random.value <= rareFlickerChance)
                    blackoutUntil = Time.time + Random.Range(0.035f, 0.12f);
            }

            if (Time.time < blackoutUntil)
            {
                source.intensity = baseIntensity * 0.08f;
                // Na falha o zumbido estala em vez de desaparecer.
                if (buzz != null)
                {
                    buzz.volume = buzzVolume * 1.8f;
                    buzz.pitch = 0.72f;
                }
                return;
            }

            float noise = Mathf.PerlinNoise(seed, Time.time * noiseSpeed);
            source.intensity = baseIntensity * (1f - noiseAmount + noise * noiseAmount * 2f);

            if (buzz == null) return;
            buzz.volume = buzzVolume * (0.85f + noise * 0.3f);
            buzz.pitch = 0.99f + noise * 0.02f;
        }

        private void OnDisable()
        {
            if (source != null) source.intensity = baseIntensity;
        }
    }
}
