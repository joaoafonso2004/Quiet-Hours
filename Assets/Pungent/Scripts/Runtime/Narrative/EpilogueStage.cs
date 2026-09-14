using System;
using System.Collections;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O epilogo: ecra preto, tres frases, e **um som**.
    ///
    /// ---
    ///
    /// **O que estava errado.** Os quatro finais existem, a conta do
    /// <see cref="EndingSelector"/> esta certa, e cada um tem tres linhas escritas. E
    /// as tres linhas sao ditas com o jogador de pe no sitio onde o climax o largou —
    /// a olhar para um corredor, com o rato ainda a funcionar, livre de andar para a
    /// cozinha enquanto o jogo lhe conta como e que a historia acabou.
    ///
    /// Nao e um problema de escrita. Um epilogo dito por cima de jogabilidade nao e um
    /// epilogo: e legendas. O jogador esta ocupado a perceber se ainda tem controlo, e
    /// e nisso que pensa enquanto passa a melhor frase do guiao.
    ///
    /// ---
    ///
    /// **O que isto faz, e e tudo o que faz:**
    ///
    /// 1. Fecha o ecra a preto e **nao o reabre**. As tres frases saem sobre nada.
    /// 2. Tira o controlo, para nao haver duvida nenhuma de que acabou.
    /// 3. Toca um som — um so — algures a meio.
    ///
    /// ---
    ///
    /// **O terceiro ponto e o epilogo inteiro.**
    ///
    /// Tres frases de texto sao um resumo, e um resumo nao assusta ninguem. O que fica
    /// e o som que vem depois delas, sobre preto, sem imagem que o explique — porque
    /// sobre preto o jogador nao pode verificar nada e tem de decidir sozinho o que
    /// aquilo era.
    ///
    /// E o mesmo som pode ser bom ou terrivel conforme o final:
    ///
    /// | final | o som | o que ele quer dizer |
    /// |---|---|---|
    /// | prova enviada | uma notificacao do telemovel | alguem leu, e respondeu |
    /// | sem prova | **nada** | ninguem perguntou nada. Ninguem vai perguntar |
    /// | falsa confianca | uma porta de carro a fechar, perto | ele sabe onde tu moras agora |
    /// | apanhado | uma fechadura a abrir, do lado de fora | nao e o teu andar. E o teu andar |
    ///
    /// O final sem prova ser o unico **sem som nenhum** nao e falta de conteudo: e a
    /// unica maneira de o dizer. Os outros tres deixam alguma coisa a acontecer depois
    /// do fim; aquele deixa silencio, e o silencio ali e a resposta.
    ///
    /// ---
    ///
    /// Nao mexe no <see cref="GameEnding"/>, que continua dono do `game_end`, do
    /// texto final e de fechar o jogo. Isto e so os segundos antes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EpilogueStage : MonoBehaviour, IRebindable
    {
        /// <summary>Um final e o som que o fecha.</summary>
        [Serializable]
        public sealed class Ending
        {
            [Tooltip("O acontecimento que o EndingSelector levanta.")]
            public string EventId;

            [Tooltip("O som, sobre preto. Vazio = silencio, e isso e uma escolha "
                   + "legitima e nao um esquecimento — ver a nota da classe.")]
            public AudioClip Sound;

            [Tooltip("Segundos depois de o final ser decidido. Tarde: tem de cair "
                   + "**depois** da ultima frase, quando o jogador ja pensa que "
                   + "acabou.")]
            [Min(0f)] public float AfterSeconds = 14f;

            [Range(0f, 1f)] public float Volume = 0.8f;
        }

        [Tooltip("O acontecimento que abre o epilogo. E o mesmo por que o "
               + "EndingSelector espera.")]
        [SerializeField] private string epilogueEvent = "epilogue";

        [Header("O corte")]
        [SerializeField] private ScreenFade fade;

        [Tooltip("Quanto tempo a fechar. Devagar: um corte seco le-se como fim de "
               + "nivel, e isto e o fim da historia.")]
        [SerializeField, Min(0.2f)] private float fadeSeconds = 2.6f;

        [Tooltip("Componentes suspensos durante o epilogo. Vazios = descobre sozinho "
               + "o motor, a interaccao e o telemovel do jogador da cena.")]
        [SerializeField] private MonoBehaviour[] suppressed = new MonoBehaviour[0];

        [Header("Os quatro")]
        [SerializeField] private Ending[] endings = new Ending[0];

        [SerializeField] private ChapterDirector director;

        private AudioSource source;
        private bool opened;
        private bool chosen;

        /// <summary>O epilogo ja comecou. Util para inspeccionar e para os testes.</summary>
        public bool Opened => opened;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;

            // Plano e nao espacial: sobre preto nao ha sitio nenhum onde o som possa
            // estar, e essa e precisamente a ideia. Um som posicionado convidava o
            // jogador a virar-se para ele, e nao ha para onde virar.
            source.spatialBlend = 0f;

            Rebind();
        }

        public void Rebind()
        {
            if (director == null) director = FindObjectOfType<ChapterDirector>();
            if (fade == null) fade = FindObjectOfType<ScreenFade>();
        }

        private void Update()
        {
            director = ChapterDirector.Resolve(director);
            if (director == null) return;

            if (!opened && director.HasSeen(epilogueEvent)) Open();
            if (opened && !chosen) TryChoose();
        }

        private void Open()
        {
            opened = true;

            CollectSuppressed();
            foreach (var component in suppressed)
                if (component != null) component.enabled = false;

            fade?.FadeTo(1f, fadeSeconds);
        }

        /// <summary>
        /// Descobre o que suspender, se ninguem lho disse.
        ///
        /// Feito no momento e nao na montagem, pela mesma razao que o
        /// `ChapterDirector` reconstroi a lista dele: o jogador de cada cena e outro
        /// objecto, e uma lista serializada de componentes de uma cena anterior chega
        /// aqui cheia de destruidos — que comparam como nulos e nao suspendem coisa
        /// nenhuma. O epilogo corria com o jogador a andar por tras do preto.
        /// </summary>
        private void CollectSuppressed()
        {
            var keep = new System.Collections.Generic.List<MonoBehaviour>();
            foreach (var component in suppressed)
                if (component != null) keep.Add(component);

            Add(keep, FindObjectOfType<Pungent.Player.PlayerMotor>());
            Add(keep, FindObjectOfType<Pungent.Interaction.PlayerInteractor>());
            Add(keep, FindObjectOfType<Pungent.Interaction.PrototypePhoneUI>());

            suppressed = keep.ToArray();
        }

        private static void Add(System.Collections.Generic.List<MonoBehaviour> list, MonoBehaviour item)
        {
            if (item != null && !list.Contains(item)) list.Add(item);
        }

        private void TryChoose()
        {
            if (endings == null) return;

            foreach (var ending in endings)
            {
                if (ending == null || string.IsNullOrWhiteSpace(ending.EventId)) continue;
                if (!director.HasSeen(ending.EventId)) continue;

                chosen = true;
                if (ending.Sound != null) StartCoroutine(PlayLate(ending));
                return;
            }
        }

        private IEnumerator PlayLate(Ending ending)
        {
            float waited = 0f;
            while (waited < ending.AfterSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (source != null && ending.Sound != null)
                source.PlayOneShot(ending.Sound, ending.Volume);
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(string opensOn, ScreenFade screen, Ending[] four)
        {
            epilogueEvent = opensOn;
            fade = screen;
            endings = four;
        }
#endif
    }
}
