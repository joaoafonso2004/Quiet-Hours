using UnityEngine;

namespace Pungent.Dialogue
{
    /// <summary>
    /// Os dois sons da escolha: passar por cima de uma resposta, e escolhe-la.
    ///
    /// A escolha era a unica coisa no jogo que se fazia inteiramente a vista. O
    /// realce mudava de cor e aparecia um `›` — e isso chega para *ver* qual e a
    /// linha activa, mas nao para a *sentir*. Sem som, mover o rato pelas tres
    /// respostas nao tem peso nenhum, e o clique que fecha a conversa acontece em
    /// silencio no momento em que o jogador acaba de decidir uma coisa.
    ///
    /// Sao dois sons e nao um: o de passar por cima e uma confirmacao de que o
    /// cursor esta mesmo em cima de alguma coisa, e o de escolher e o unico
    /// momento em que o jogador afirma qualquer coisa nesta cena. Se fossem
    /// iguais, escolher soaria a hesitar.
    ///
    /// **Discretos de proposito.** Isto e um jogo onde se ouve o frigorifico. Um
    /// bip de menu por cima de uma conversa a um metro da cara de alguem arranca a
    /// cena do sitio onde ela esta a acontecer. O de passar por cima e quase um
    /// toque seco; o de escolher e mais grave e um bocado mais cheio, para fechar
    /// a frase em vez de a picar.
    ///
    /// Como no <see cref="NpcMumbleVoice"/>, os clips autorais mandam se estiverem
    /// preenchidos e ha uma versao gerada por codigo para nao depender de
    /// nenhum ficheiro existir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueChoiceAudio : MonoBehaviour
    {
        [Tooltip("Toque de passar por cima de uma resposta. Vazio = gerado por codigo.")]
        [SerializeField] private AudioClip hoverClip;
        [Tooltip("Toque de escolher uma resposta. Vazio = gerado por codigo.")]
        [SerializeField] private AudioClip confirmClip;

        [SerializeField, Range(0f, 1f)] private float hoverVolume = 0.16f;
        [SerializeField, Range(0f, 1f)] private float confirmVolume = 0.3f;

        [Tooltip("Variacao de tom do toque de passar por cima. Sem ela, correr as "
               + "tres respostas de cima a baixo soa a uma maquina, e nao a uma mao.")]
        [SerializeField] private Vector2 hoverPitchRange = new Vector2(0.96f, 1.06f);

        private AudioSource source;
        private AudioClip proceduralHover;
        private AudioClip proceduralConfirm;

        private void Awake() => EnsureSource();

        /// <summary>
        /// Uma fonte propria, num filho.
        ///
        /// **Nao** se apanha a que ja esteja no jogador com um `GetComponent`: o
        /// `PlayerRoot` tem as passadas e a voz do Tomas, e roubar-lhes a fonte
        /// para tocar um toque de interface corta a passada a meio. E som de
        /// interface, portanto `spatialBlend` a zero — nao vem de um sitio do
        /// mundo, vem do proprio acto de escolher.
        /// </summary>
        private void EnsureSource()
        {
            if (source != null) return;

            Transform existing = transform.Find("DialogueChoiceAudio");
            GameObject holder = existing != null
                ? existing.gameObject
                : new GameObject("DialogueChoiceAudio");

            if (existing == null) holder.transform.SetParent(transform, false);

            source = holder.GetComponent<AudioSource>();
            if (source == null) source = holder.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            // Nao e voz nem ambiente: e a interface. Fica no seu proprio grupo de
            // prioridade para nao ser roubado quando a cena tem muita coisa a tocar.
            source.priority = 120;
        }

        /// <summary>O cursor entrou numa resposta. Nunca toca ao sair dela.</summary>
        public void PlayHover()
        {
            EnsureSource();
            AudioClip clip = hoverClip != null ? hoverClip : GetProceduralHover();
            if (clip == null) return;

            source.pitch = Random.Range(hoverPitchRange.x, hoverPitchRange.y);
            source.PlayOneShot(clip, hoverVolume);
        }

        /// <summary>A resposta foi escolhida — por rato ou por tecla.</summary>
        public void PlayConfirm()
        {
            EnsureSource();
            AudioClip clip = confirmClip != null ? confirmClip : GetProceduralConfirm();
            if (clip == null) return;

            // Sem variacao de tom: escolher e um gesto so, e duas escolhas seguidas
            // com tons diferentes soam a duas coisas diferentes terem acontecido.
            source.pitch = 1f;
            source.PlayOneShot(clip, confirmVolume);
        }

        private AudioClip GetProceduralHover()
        {
            if (proceduralHover == null)
                proceduralHover = CreateTick("Dialogue_Hover_Procedural",
                    duration: 0.042f, fundamental: 620f, decay: 78f, body: 0.18f, noise: 0.05f);

            return proceduralHover;
        }

        private AudioClip GetProceduralConfirm()
        {
            if (proceduralConfirm == null)
                proceduralConfirm = CreateTick("Dialogue_Confirm_Procedural",
                    duration: 0.13f, fundamental: 288f, decay: 24f, body: 0.42f, noise: 0.03f);

            return proceduralConfirm;
        }

        /// <summary>
        /// Um toque curto: fundamental, um pouco de segunda harmonica para lhe dar
        /// corpo, um sopro de ruido no ataque, e uma queda exponencial.
        ///
        /// O passa-baixo de um polo no fim nao e um enfeite. Sem ele, uma queda
        /// rapida a 22 kHz deixa um `click` digital no ataque — o mesmo estalo que
        /// se ouve ao cortar uma onda a meio — e isso soa a defeito, nao a
        /// interface. Com ele, o ataque fica redondo e o som senta-se por baixo da
        /// conversa em vez de saltar por cima dela.
        /// </summary>
        private static AudioClip CreateTick(string name, float duration, float fundamental,
            float decay, float body, float noise)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float previous = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Exp(-time * decay);

                // Ataque suavizado nos primeiros milissegundos, pela mesma razao do
                // passa-baixo: uma onda que comeca em amplitude cheia estala.
                envelope *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time * 900f));

                float wave =
                    Mathf.Sin(Mathf.PI * 2f * fundamental * time) +
                    Mathf.Sin(Mathf.PI * 2f * fundamental * 2f * time) * body;

                float breath = (Random.value - 0.5f) * 2f * noise * Mathf.Exp(-time * decay * 3f);

                float value = (wave + breath) * envelope * 0.5f;

                // Passa-baixo de um polo.
                previous = Mathf.Lerp(previous, value, 0.42f);
                samples[i] = previous;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
