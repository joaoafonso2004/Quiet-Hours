using Pungent.Narrative;
using UnityEngine;

namespace Pungent.NPC
{
    /// <summary>
    /// Os ruidos de casa que o Rui faz na noite do Dia 5: uma gaveta, um puxador,
    /// uma cadeira arrastada.
    ///
    /// ---
    ///
    /// **O que isto NAO faz, e porque.**
    ///
    /// A primeira versao deste componente tambem fazia passos, escrita com a ideia
    /// de que o Rui atravessava a casa em silencio absoluto. **Estava errado, e a
    /// jogada provou-o:** o `RuiHunt` ja tinha um sistema de passos completo —
    /// fonte propria (`Rui_Footsteps`), dez clips gravados, e o mesmo
    /// <see cref="Pungent.Audio.MuffledThroughWalls"/> por cima. Nunca faltou nada
    /// ali.
    ///
    /// O que se ouvia era silencio por outra razao: **a caca estava encravada.** O
    /// `UpdateFootsteps` do `RuiHunt` so toca acima de 0,35 m/s, e o Rui nao andava
    /// um centimetro — parava a oitenta centimetros do primeiro ponto de busca e
    /// ficava la. Corrigida a paragem, os passos que ja existiam voltaram sozinhos.
    ///
    /// Medido depois, com os dois sistemas ligados: **duas em vinte e quatro
    /// amostras em cada um**, ou seja, dois passos por cada passo. Este perdeu os
    /// dele, e os que ficam sao os gravados, que sao melhores do que sintese.
    ///
    /// A licao vale mais do que o codigo que sobrou: *diagnosticar o sintoma sem
    /// procurar o dono existente e a maneira de acrescentar um segundo dono ao
    /// mesmo som.* E a mesma armadilha dos eventos com dois donos que este projecto
    /// ja pagou seis vezes, desta vez em audio.
    ///
    /// ---
    ///
    /// **O que faz, e que nao existia mesmo.**
    ///
    /// - **Ruidos de casa enquanto ele procura.** Dizem **em que divisao** ele esta
    ///   sem o jogador ter de o ver. Os passos dizem que ha alguem a andar; isto diz
    ///   que alguem abriu uma gaveta na cozinha — e e a diferenca entre saber que
    ///   ele existe e saber onde ele esta.
    /// - **Quase nada enquanto espera.** O `ClimaxStage` deixa-o na lavandaria,
    ///   atras de duas portas fechadas, e diz na propria nota que *"da para o ouvir
    ///   se houver silencio"*. Passa a dar mesmo: um som raro e muito baixo, o unico
    ///   aviso de que a casa nao esta vazia. Ha-de passar despercebido a metade dos
    ///   jogadores, que e o efeito certo.
    ///
    /// Enquanto persegue, nao faz nada: nessa altura os passos ja dizem tudo, e
    /// ruido por cima so torna a informacao mais dificil de ler no momento em que
    /// conta mais.
    ///
    /// **Sem clips, funciona na mesma.** Sintetizados no `Awake`, como o
    /// `NpcMumbleVoice` e o `ProceduralRoomTone` ja fazem. Havendo clips, ganham
    /// eles.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class RuiPresenceAudio : MonoBehaviour
    {
        [Header("Quem")]
        [Tooltip("Vazio = procurado no pai. E ele que diz em que estado a caca esta.")]
        [SerializeField] private RuiHunt hunt;

        [Header("Ruidos de casa")]
        [Tooltip("Intervalo entre ruidos enquanto ele procura. Aleatorio dentro do "
               + "par: uma cadencia certa le-se como metronomo e deixa de assustar.")]
        [SerializeField] private Vector2 searchNoiseGap = new Vector2(4.5f, 9f);
        [SerializeField, Range(0f, 1f)] private float noiseVolume = 0.5f;
        [SerializeField] private AudioClip[] noiseClips = new AudioClip[0];

        [Header("A espera")]
        [Tooltip("Intervalo entre ruidos enquanto ele espera na lavandaria. Longo: "
               + "isto e um aviso, nao uma presenca.")]
        [SerializeField] private Vector2 hiddenNoiseGap = new Vector2(14f, 26f);
        [SerializeField, Range(0f, 1f)] private float hiddenVolume = 0.16f;

        [Header("Quando cala de vez")]
        [Tooltip("O jogador saiu. A partir daqui nao ha mais nada para ouvir.")]
        [SerializeField] private string endEvent = "escaped";

        [SerializeField] private ChapterDirector director;

        private AudioSource source;
        private float nextNoiseAt;
        private bool finished;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.2f;
            source.maxDistance = 18f;
            source.dopplerLevel = 0f;

            if (hunt == null) hunt = GetComponentInParent<RuiHunt>();
            if (noiseClips == null || noiseClips.Length == 0) noiseClips = BuildNoises();

            nextNoiseAt = Time.time + Random.Range(hiddenNoiseGap.x, hiddenNoiseGap.y);
        }

        private void Update()
        {
            if (finished) return;

            if (!string.IsNullOrWhiteSpace(endEvent))
            {
                director = ChapterDirector.Resolve(director);
                if (director != null && director.HasSeen(endEvent))
                {
                    finished = true;
                    source.Stop();
                    return;
                }
            }

            // **So na noite do Dia 5.**
            //
            // O `RuiHunt` vive no Rui em todas as noites e so e ligado pelo
            // `ClimaxStage`. Sem esta condicao, o `Current` de um componente
            // desligado devolve na mesma `Hidden` — que e o estado inicial — e o Rui
            // passava o Dia 1 inteiro a fazer barulhos de gaveta enquanto cozinha
            // tranquilamente na cozinha, a dois metros do jogador.
            if (hunt == null || !hunt.isActiveAndEnabled) return;

            UpdateHouseNoise();
        }

        private void UpdateHouseNoise()
        {
            if (Time.time < nextNoiseAt) return;

            var state = hunt.Current;
            bool hidden = state == RuiHunt.State.Hidden;
            bool searching = state == RuiHunt.State.Searching || state == RuiHunt.State.Lost;

            if (!hidden && !searching)
            {
                // Volta a marcar para nao disparar em rajada assim que ele
                // regressar a procura.
                nextNoiseAt = Time.time + searchNoiseGap.x;
                return;
            }

            Vector2 gap = hidden ? hiddenNoiseGap : searchNoiseGap;
            nextNoiseAt = Time.time + Random.Range(gap.x, gap.y);

            var clip = Pick(noiseClips);
            if (clip == null) return;

            source.pitch = Random.Range(0.9f, 1.12f);
            source.PlayOneShot(clip, hidden ? hiddenVolume : noiseVolume);
        }

        private static AudioClip Pick(AudioClip[] set) =>
            set == null || set.Length == 0 ? null : set[Random.Range(0, set.Length)];

        // ------------------------------------------------------------------
        // Sintese
        //
        // Nada disto quer soar bem. Quer soar **localizavel**: um transiente seco
        // com energia em baixo, que e o que o passa-baixo do `MuffledThroughWalls`
        // deixa passar por uma parede. Um som bonito e cheio de agudos desaparece
        // por completo quando ha uma porta pelo meio, e a informacao perde-se.
        // ------------------------------------------------------------------

        private const int SampleRate = 44100;

        /// <summary>Gaveta, puxador, cadeira.</summary>
        private static AudioClip[] BuildNoises()
        {
            var clips = new AudioClip[3];
            // Frequencia de ressonancia e quanto dura cada um.
            float[] resonance = { 190f, 320f, 135f };
            float[] seconds = { 0.42f, 0.26f, 0.55f };

            for (int i = 0; i < clips.Length; i++)
            {
                int length = (int)(SampleRate * seconds[i]);
                var data = new float[length];
                var random = new System.Random(4400 + i);

                for (int s = 0; s < length; s++)
                {
                    float t = (float)s / SampleRate;

                    // O clique de contacto: curto e largo em frequencia.
                    float click = (float)(random.NextDouble() * 2.0 - 1.0)
                                  * Mathf.Exp(-120f * t) * 0.5f;

                    // A madeira a responder, com um segundo modo por cima para nao
                    // soar a apito.
                    float wood = (Mathf.Sin(2f * Mathf.PI * resonance[i] * t)
                                  + Mathf.Sin(2f * Mathf.PI * resonance[i] * 2.4f * t) * 0.4f)
                                 * Mathf.Exp(-11f * t) * 0.34f;

                    // Arrasto: so no terceiro, que e a cadeira.
                    float drag = i == 2
                        ? (float)(random.NextDouble() * 2.0 - 1.0)
                          * Mathf.Exp(-4.5f * t) * 0.16f
                        : 0f;

                    data[s] = Mathf.Clamp(click + wood + drag, -1f, 1f);
                }

                clips[i] = AudioClip.Create($"Rui_Noise_{i}", length, 1, SampleRate, false);
                clips[i].SetData(data, 0);
            }
            return clips;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(RuiHunt owner) => hunt = owner;
#endif
    }
}
