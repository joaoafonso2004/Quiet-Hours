using Pungent.NPC;
using UnityEngine;

namespace Pungent.Audio
{
    /// <summary>
    /// O som da caca: o que aperta o peito enquanto ele anda a procura, e o que
    /// desaparece no instante em que ele te perde.
    ///
    /// **A regra que decide tudo o resto: isto e informacao, nao e musica.** O
    /// climax e vivido dentro de um roupeiro, sem ver nada — o §12.1 diz que o som
    /// e o principal canal de tensao, e o `RuiHunt` ja fala pelos ruidos de casa
    /// para dizer em que divisao ele esta. Uma camada que sobe e desce ao acaso
    /// tapava esses ruidos e nao dizia nada. Esta sobe quando ele te ve e **cai a
    /// pique quando ele te perde**, e essa queda e a unica coisa que diz a quem esta
    /// escondido que ja pode sair. E o premio por se ter escondido bem.
    ///
    /// Tres camadas, e cada uma faz um trabalho diferente:
    ///
    /// | | |
    /// |---|---|
    /// | **Bordao** | Grave continuo. Existe desde que ele acorda. Diz "ele esta em casa". |
    /// | **Pulso** | Batida surda que **acelera com a proximidade**. Diz o quao perto. |
    /// | **Agulha** | Tom agudo que **sobe de altura enquanto a perseguicao durar** e nunca resolve. E este que agonia. |
    ///
    /// A agulha e a peca que faz o efeito. Um som de perseguicao que se repete em
    /// ciclo vira musica e o ouvido habitua-se em vinte segundos; um que sobe sem
    /// nunca chegar a lado nenhum nao deixa. Volta ao principio quando ele te perde,
    /// portanto **fugir bem tambem se ouve**.
    ///
    /// **Gerado em memoria, sem depender de ficheiros.** E o mesmo caminho do
    /// <see cref="ProceduralRoomTone"/> e do `NpcMumbleVoice`: o jogo tem de soar
    /// bem sem esperar por assets. Havendo gravacoes, preenchem-se os tres campos de
    /// clip e elas ganham — sem mexer numa linha de codigo.
    ///
    /// Nao e espacial. Isto nao sai de um sitio da casa: e o que o Tomas sente, e o
    /// alcance de audicao dele nao muda por ele virar a cabeca.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChaseAudio : MonoBehaviour
    {
        [Header("Quem seguir")]
        [SerializeField] private RuiHunt hunt;
        [SerializeField] private Transform player;

        [Header("Gravacoes (opcionais)")]
        [Tooltip("Se vazio, e gerado em memoria. Tem de ser um loop.")]
        [SerializeField] private AudioClip droneClip;
        [SerializeField] private AudioClip pulseClip;
        [SerializeField] private AudioClip needleClip;

        [Header("Volumes no maximo")]
        [SerializeField, Range(0f, 1f)] private float droneVolume = 0.22f;
        [SerializeField, Range(0f, 1f)] private float pulseVolume = 0.30f;
        [SerializeField, Range(0f, 1f)] private float needleVolume = 0.16f;

        [Header("Distancia")]
        [Tooltip("A partir daqui o pulso e a distancia maxima: lento e baixo.")]
        [SerializeField, Min(1f)] private float farDistance = 14f;
        [Tooltip("A esta distancia o pulso esta no maximo. Nao e zero: ele nunca "
               + "chega a estar em cima sem apanhar.")]
        [SerializeField, Min(0.5f)] private float nearDistance = 2.5f;

        [Header("Tempos")]
        [Tooltip("Quanto demora a subir quando ele te ve. Curto: o susto e agora.")]
        [SerializeField, Min(0.05f)] private float attackSeconds = 0.35f;

        [Tooltip("Quanto demora a cair quando ele te perde. **Longo de proposito.** "
               + "Cair a pique no instante em que ele desiste dava ao jogador uma "
               + "certeza que ele nao devia ter; assim ha alguns segundos em que ja "
               + "esta safo e ainda nao sabe.")]
        [SerializeField, Min(0.05f)] private float releaseSeconds = 2.6f;

        [Tooltip("Quanto a agulha sobe por segundo de perseguicao, em semitons.")]
        [SerializeField, Min(0f)] private float needleClimbPerSecond = 0.22f;

        [Tooltip("Tecto da subida, em semitons. Sem tecto, ao fim de dois minutos "
               + "era um assobio de chaleira e deixava de ser ameaca.")]
        [SerializeField, Min(1f)] private float needleClimbCeiling = 14f;

        private AudioSource drone;
        private AudioSource pulse;
        private AudioSource needle;

        private float presence;    // 0..1 — ele esta em casa e a procurar
        private float alarm;       // 0..1 — ele viu-te
        private float climb;       // semitons acumulados da perseguicao

        private const int SampleRate = 44100;

        private void Awake()
        {
            if (hunt == null) hunt = FindObjectOfType<RuiHunt>(true);
            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>(true);
                if (motor != null) player = motor.transform;
            }

            drone = Layer("CHASE_Drone", droneClip != null ? droneClip : BuildDrone());
            pulse = Layer("CHASE_Pulse", pulseClip != null ? pulseClip : BuildPulse());
            needle = Layer("CHASE_Needle", needleClip != null ? needleClip : BuildNeedle());
        }

        /// <summary>
        /// Uma camada: fonte propria, em loop, a tocar sempre a volume zero.
        ///
        /// A tocar desde o inicio e nao ligada quando faz falta: comecar um
        /// `AudioSource` no instante do susto da um estalo no arranque do buffer, e
        /// o estalo chega antes do som — o jogador reage ao clique e nao a ameaca.
        /// </summary>
        private AudioSource Layer(string name, AudioClip clip)
        {
            var host = new GameObject(name);
            host.transform.SetParent(transform, false);

            var source = host.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;   // e o que ele sente, nao um sitio da casa
            source.volume = 0f;
            source.dopplerLevel = 0f;
            source.Play();
            return source;
        }

        private void Update()
        {
            if (hunt == null) { Silence(); return; }

            RuiHunt.State state = hunt.Current;

            // Antes de ele sair da lavandaria nao ha caca nenhuma, e a casa tem de
            // estar em silencio para os ruidos dele valerem alguma coisa.
            bool awake = state != RuiHunt.State.Hidden;
            bool seen = state == RuiHunt.State.Pursuing;

            // O `Caught` e estado de passagem com o ecra ja a ir a preto: calar tudo
            // depressa, senao a camada continua a tocar por cima do corte.
            if (state == RuiHunt.State.Caught) { Silence(); return; }

            presence = Approach(presence, awake ? 1f : 0f, attackSeconds, releaseSeconds);
            alarm = Approach(alarm, seen ? 1f : 0f, attackSeconds, releaseSeconds);

            // A agulha sobe enquanto ele te ve, e volta atras quando te perde. Nao
            // instantaneamente: descer mais devagar do que subiu deixa a sensacao de
            // que aquilo ainda nao acabou, que e verdade — ele volta a procurar.
            climb = seen
                ? Mathf.Min(needleClimbCeiling, climb + needleClimbPerSecond * Time.deltaTime)
                : Mathf.Max(0f, climb - needleClimbPerSecond * 0.45f * Time.deltaTime);

            float closeness = Closeness();

            drone.volume = droneVolume * presence;

            // O pulso vive da proximidade, e nao do estado: ele a passar do outro
            // lado da parede sem te ver tambem conta, e e assim que quem esta
            // escondido percebe que ele esta a chegar sem o ouvir nem o ver.
            pulse.volume = pulseVolume * presence * Mathf.Max(closeness, alarm);
            // Batida mais rapida ao perto. 1x longe, 2,1x colado.
            pulse.pitch = Mathf.Lerp(1f, 2.1f, Mathf.Max(closeness, alarm));

            needle.volume = needleVolume * alarm;
            needle.pitch = Mathf.Pow(2f, climb / 12f);
        }

        /// <summary>
        /// 0 longe, 1 colado. Em linha recta e nao pelo caminho que ele tem de
        /// fazer: o que se quer aqui e a parede do lado, e nao a volta que ele dá.
        /// </summary>
        private float Closeness()
        {
            if (player == null || hunt == null) return 0f;

            float distance = Vector3.Distance(player.position, hunt.transform.position);
            return 1f - Mathf.Clamp01(Mathf.InverseLerp(nearDistance, farDistance, distance));
        }

        private static float Approach(float current, float target, float up, float down)
        {
            float seconds = target > current ? up : down;
            return Mathf.MoveTowards(current, target, Time.deltaTime / Mathf.Max(0.01f, seconds));
        }

        private void Silence()
        {
            presence = 0f;
            alarm = 0f;
            climb = 0f;
            if (drone != null) drone.volume = 0f;
            if (pulse != null) pulse.volume = 0f;
            if (needle != null) needle.volume = 0f;
        }

        // ------------------------------------------------------------------
        // As tres camadas, geradas
        // ------------------------------------------------------------------

        /// <summary>
        /// O bordao: duas fundamentais graves ligeiramente desafinadas uma da outra.
        ///
        /// O desafinamento e a peca toda. Duas ondas exactamente a mesma frequencia
        /// somam-se numa so e soam a teste de audio; a 0,5 Hz de diferenca batem uma
        /// contra a outra e o resultado respira devagar, como uma coisa viva que nao
        /// se ve. Nenhuma nota, nenhuma harmonia: nao pode soar a musica.
        /// </summary>
        private static AudioClip BuildDrone()
        {
            const float seconds = 8f;
            int length = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float a = Mathf.Sin(2f * Mathf.PI * 47.5f * t);
                float b = Mathf.Sin(2f * Mathf.PI * 48f * t);
                // Um harmonico fraco: sem ele o grave desaparece em colunas pequenas
                // e portateis, e o jogo passa a nao ter camada nenhuma para metade
                // das pessoas.
                float c = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.18f;
                data[i] = (a + b) * 0.34f + c;
            }

            LoopFade(data, 0.25f);
            Normalise(data, 0.5f);
            return Clip("Chase_Drone", data);
        }

        /// <summary>
        /// O pulso: uma batida surda, uma por segundo, com cauda curta.
        ///
        /// Um segundo exacto de propósito, para o `pitch` a acelerar ser lido como
        /// ritmo a subir e nao como som a mudar de cor. Nao e um coracao — um
        /// batimento cardiaco pertence ao corpo do jogador e este som pertence a
        /// **ele**.
        /// </summary>
        private static AudioClip BuildPulse()
        {
            const float seconds = 1f;
            int length = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                // Cai depressa: o que fica no ar e o silencio a seguir a batida.
                float envelope = Mathf.Exp(-t * 9f);
                // A altura tambem cai — e o que faz soar a pancada em madeira e nao
                // a nota de baixo.
                float frequency = Mathf.Lerp(78f, 41f, Mathf.Clamp01(t * 6f));
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
            }

            Normalise(data, 0.85f);
            return Clip("Chase_Pulse", data);
        }

        /// <summary>
        /// A agulha: o tom que aperta.
        ///
        /// Duas ondas com sete Hz de diferenca. Sete Hz e o intervalo em que o
        /// ouvido deixa de ouvir duas notas e comeca a ouvir uma a tremer, e e
        /// desconfortavel sem ser estridente — o §7.1 nao deixa tapar informacao de
        /// jogo, e um agudo limpo e alto fazia exactamente isso aos ruidos da casa.
        /// A subida de altura nao esta aqui: vem do `pitch`, para poder nao resolver.
        /// </summary>
        private static AudioClip BuildNeedle()
        {
            const float seconds = 4f;
            int length = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[length];

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float a = Mathf.Sin(2f * Mathf.PI * 523f * t);
                float b = Mathf.Sin(2f * Mathf.PI * 530f * t);
                data[i] = (a + b) * 0.5f;
            }

            LoopFade(data, 0.15f);
            Normalise(data, 0.4f);
            return Clip("Chase_Needle", data);
        }

        /// <summary>
        /// Cose o fim ao principio. Sem isto, cada volta do loop da um estalo, e um
        /// estalo de segundo a segundo e a coisa mais barata de notar num jogo.
        /// </summary>
        private static void LoopFade(float[] data, float seconds)
        {
            int fade = Mathf.Min(data.Length / 2, Mathf.RoundToInt(SampleRate * seconds));
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                int tail = data.Length - fade + i;
                data[tail] = Mathf.Lerp(data[tail], data[i], k);
            }
        }

        private static void Normalise(float[] data, float peak)
        {
            float loudest = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float magnitude = Mathf.Abs(data[i]);
                if (magnitude > loudest) loudest = magnitude;
            }

            if (loudest <= 0.0001f) return;

            float scale = peak / loudest;
            for (int i = 0; i < data.Length; i++) data[i] *= scale;
        }

        private static AudioClip Clip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(RuiHunt theHunt, Transform thePlayer)
        {
            hunt = theHunt;
            player = thePlayer;
        }
#endif
    }
}
