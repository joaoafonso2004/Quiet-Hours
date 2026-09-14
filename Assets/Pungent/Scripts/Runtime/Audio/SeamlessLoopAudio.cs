using UnityEngine;

namespace Pungent.Audio
{
    /// <summary>
    /// Ambiente em loop sem se ouvir a emenda.
    ///
    /// Pôr `loop = true` num AudioSource com um clip comprimido dá quase sempre um
    /// salto audível na volta: o MP3/Vorbis traz silêncio de codificação nas pontas,
    /// e mesmo sem isso o fim e o princípio da gravação não têm o mesmo nível. Numa
    /// cama de ruído que toca a noite inteira, essa emenda ouve-se de minuto a
    /// minuto e denuncia o truque.
    ///
    /// A solução são duas fontes a alternar: enquanto uma acaba, a outra já começou,
    /// e o cruzamento tapa a junta. Custa um AudioSource a mais e resolve o problema
    /// sem precisar de reeditar o ficheiro.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeamlessLoopAudio : MonoBehaviour
    {
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.16f;
        [Tooltip("Duração do cruzamento. Longo de mais come a gravação; curto de "
               + "mais volta a deixar ouvir a emenda.")]
        [SerializeField, Min(0.2f)] private float crossfadeSeconds = 2.5f;
        [Tooltip("0 = igual em toda a parte (cama de ambiente). 1 = posicional.")]
        [SerializeField, Range(0f, 1f)] private float spatialBlend;
        [SerializeField] private bool playOnAwake = true;

        private AudioSource a;
        private AudioSource b;
        private AudioSource current;
        private double nextSwapTime;

        private void Awake()
        {
            a = gameObject.AddComponent<AudioSource>();
            b = gameObject.AddComponent<AudioSource>();
            Configure(a);
            Configure(b);
            if (playOnAwake) Play();
        }

        private void Configure(AudioSource source)
        {
            source.clip = clip;
            source.loop = false;          // o encadeamento é feito à mão
            source.playOnAwake = false;
            source.volume = 0f;
            source.spatialBlend = spatialBlend;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
        }

        public void Play()
        {
            if (clip == null) { enabled = false; return; }

            current = a;
            current.volume = volume;
            current.Play();
            // A troca começa uma passagem antes do fim do clip.
            nextSwapTime = AudioSettings.dspTime + Mathf.Max(0.1f, clip.length - crossfadeSeconds);
        }

        private void Update()
        {
            if (clip == null || current == null) return;

            if (AudioSettings.dspTime >= nextSwapTime)
            {
                AudioSource incoming = current == a ? b : a;
                incoming.volume = 0f;
                incoming.Play();
                current = incoming;
                nextSwapTime = AudioSettings.dspTime + Mathf.Max(0.1f, clip.length - crossfadeSeconds);
            }

            // Quem está a entrar sobe, quem está a sair desce. Fora do cruzamento
            // isto assenta em volume e zero e não faz nada.
            float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, crossfadeSeconds) * volume;
            AudioSource outgoing = current == a ? b : a;
            current.volume = Mathf.MoveTowards(current.volume, volume, step);
            outgoing.volume = Mathf.MoveTowards(outgoing.volume, 0f, step);
            if (outgoing.volume <= 0.0001f && outgoing.isPlaying && current.time > crossfadeSeconds)
                outgoing.Stop();
        }

        public void SetVolume(float value)
        {
            volume = Mathf.Clamp01(value);
        }
    }
}
