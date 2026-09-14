using UnityEngine;

namespace Pungent.Dialogue
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class NpcMumbleVoice : MonoBehaviour
    {
        [Header("Voice")]
        [Tooltip("Gravação corrida de resmungo. Se estiver preenchida, é esta que "
               + "toca em loop enquanto a frase se escreve, e as sílabas soltas "
               + "abaixo deixam de ser usadas.")]
        [SerializeField] private AudioClip continuousMumble;
        [Tooltip("Sílabas soltas, uma por caractere. Só usadas se não houver "
               + "gravação corrida; se também estiverem vazias, geram-se por código.")]
        [SerializeField] private AudioClip[] syllables;
        [SerializeField, Range(0f, 1f)] private float volume = 0.32f;
        [SerializeField] private Vector2 pitchRange = new Vector2(0.92f, 1.07f);
        [SerializeField, Min(0.03f)] private float minimumInterval = 0.085f;

        [Header("Spatial sound")]
        [Tooltip("Sai de um sítio da cena. Verdadeiro para toda a gente — é assim "
               + "que se sabe de que divisão vem a voz do Rui.\n\n"
               + "**Falso só para o Tomás.** A voz dele não vem de um ponto do mundo: "
               + "vem de dentro da cabeça de quem está a jogar, e o ouvinte anda "
               + "colado a ela. Espacial, o volume dela dependia de para onde o "
               + "jogador estivesse virado — alguém a ouvir-se a si próprio mais "
               + "alto por olhar para a esquerda.")]
        [SerializeField] private bool spatial = true;

        [SerializeField, Min(0.1f)] private float minimumDistance = 0.8f;
        [SerializeField, Min(1f)] private float maximumDistance = 9f;

        private AudioSource source;
        private AudioClip[] proceduralSyllables;
        private float nextAllowedTime;
        private int lastIndex = -1;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minimumDistance;
            source.maxDistance = maximumDistance;
            source.dopplerLevel = 0f;
        }

        public void SpeakCharacter(char character, int characterIndex, char previousCharacter)
        {
            // Com uma gravação corrida, o resmungo é uma cama contínua que dura o
            // tempo da frase, e não um disparo por letra. Entra a partir de um
            // ponto aleatório do clip para duas falas seguidas não começarem com
            // exactamente o mesmo som.
            if (continuousMumble != null)
            {
                if (!source.isPlaying)
                {
                    source.clip = continuousMumble;
                    source.loop = true;
                    source.pitch = Random.Range(pitchRange.x, pitchRange.y);
                    source.volume = volume;
                    source.time = Random.Range(0f, Mathf.Max(0.01f, continuousMumble.length - 0.5f));
                    source.Play();
                }
                return;
            }

            if (!char.IsLetterOrDigit(character) || Time.unscaledTime < nextAllowedTime)
                return;

            bool startsWord = characterIndex == 0 || char.IsWhiteSpace(previousCharacter) ||
                              char.IsPunctuation(previousCharacter);
            if (!startsWord && characterIndex % 3 != 0)
                return;

            AudioClip[] available = HasAuthoredSyllables() ? syllables : GetProceduralSyllables();
            if (available == null || available.Length == 0)
                return;

            int index = PickWithoutImmediateRepeat(available.Length);
            for (int attempt = 0; attempt < available.Length && available[index] == null; attempt++)
                index = (index + 1) % available.Length;
            if (available[index] == null)
                return;

            source.pitch = Random.Range(pitchRange.x, pitchRange.y);
            source.PlayOneShot(available[index], volume * Random.Range(0.86f, 1.04f));
            nextAllowedTime = Time.unscaledTime + minimumInterval;
        }

        public void Stop()
        {
            if (source != null)
                source.Stop();
        }

        /// <summary>
        /// Faz desta a voz de dentro da cabeca — a do Tomas.
        ///
        /// O gemeo em tempo de execucao do <see cref="EditorConfigure"/>, que vive
        /// numa assembly de editor e nao serve para uma voz montada em `Awake`.
        ///
        /// Existe porque os pensamentos do protagonista eram **mudos**: o
        /// `ShowThought` passava voz nula, e a unica pessoa do jogo que nunca se
        /// ouvia era aquela de quem sao os pensamentos. O aviso ja estava escrito na
        /// nota do `spatial` deste ficheiro e nunca chegou a ser ligado.
        ///
        /// O `pitch` e o que separa duas pessoas quando nao ha gravacoes — as
        /// silabas geradas sao as mesmas para toda a gente. Mais grave e mais baixo
        /// do que a voz do Rui, senao o Tomas pensa com a voz do vizinho.
        /// </summary>
        public void MakeInnerVoice(Vector2 pitch, float loudness)
        {
            spatial = false;
            pitchRange = pitch;
            volume = Mathf.Clamp01(loudness);

            // O `Awake` pode ja ter corrido e fixado o `spatialBlend` a 1.
            if (source != null) source.spatialBlend = 0f;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Afina a voz. Usado pelas ferramentas de ligacao, que vivem noutra
        /// assembly.
        ///
        /// O `pitch` e o que separa duas pessoas quando nao ha gravacoes: as silabas
        /// sao geradas por codigo e sao as mesmas para toda a gente, portanto duas
        /// vozes com a mesma janela de pitch sao literalmente a mesma pessoa a falar
        /// consigo propria.
        /// </summary>
        public void EditorConfigure(bool isSpatial, Vector2 pitch, float loudness,
            float minDistance, float maxDistance)
        {
            spatial = isSpatial;
            pitchRange = pitch;
            volume = Mathf.Clamp01(loudness);
            minimumDistance = Mathf.Max(0.1f, minDistance);
            maximumDistance = Mathf.Max(1f, maxDistance);
        }
#endif

        private bool HasAuthoredSyllables()
        {
            if (syllables == null || syllables.Length == 0)
                return false;

            for (int i = 0; i < syllables.Length; i++)
            {
                if (syllables[i] != null)
                    return true;
            }

            return false;
        }

        private int PickWithoutImmediateRepeat(int count)
        {
            if (count <= 1)
                return 0;

            int index = Random.Range(0, count);
            if (index == lastIndex)
                index = (index + 1) % count;

            lastIndex = index;
            return index;
        }

        private AudioClip[] GetProceduralSyllables()
        {
            if (proceduralSyllables != null)
                return proceduralSyllables;

            proceduralSyllables = new AudioClip[6];
            for (int i = 0; i < proceduralSyllables.Length; i++)
                proceduralSyllables[i] = CreateProceduralSyllable(i);

            return proceduralSyllables;
        }

        private static AudioClip CreateProceduralSyllable(int variant)
        {
            const int sampleRate = 22050;
            float duration = 0.075f + variant * 0.008f;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            float fundamental = 96f + variant * 7.5f;
            float phaseOffset = variant * 0.71f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float normalized = i / (float)(sampleCount - 1);
                float envelope = Mathf.Sin(normalized * Mathf.PI);
                envelope *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized * 8f));

                float carrier =
                    Mathf.Sin(Mathf.PI * 2f * fundamental * time + phaseOffset) * 0.52f +
                    Mathf.Sin(Mathf.PI * 2f * fundamental * 2f * time + 0.4f) * 0.22f +
                    Mathf.Sin(Mathf.PI * 2f * fundamental * 3f * time + 1.1f) * 0.11f;
                float breath = (Mathf.PerlinNoise(variant * 0.37f, time * 115f) - 0.5f) * 0.12f;
                samples[i] = (carrier + breath) * envelope * 0.42f;
            }

            AudioClip clip = AudioClip.Create($"Rui_Mumble_Procedural_{variant + 1}", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
