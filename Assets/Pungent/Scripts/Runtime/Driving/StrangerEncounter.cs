using Pungent.Dialogue;
using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// O que o estranho diz na berma, e o que o Tomas faz enquanto ele diz.
    ///
    /// A §5 pede que ele "ofereça ajuda e faca perguntas que revelam conhecimento
    /// indevido". A regra de escrita deste projecto diz o resto: **o detalhe que se
    /// repete tem de ser banal**. Ele nao ameaca, nao sabe nada de dramatico e nunca
    /// diz o nome do Rui. Diz a hora a que o Tomas saiu da oficina, e diz mal —
    /// corrige-se sozinho, como quem confirma uma coisa que leu.
    ///
    /// Dramatico lia-se como truque de guiao. Assim le-se como vigilancia: alguem
    /// que sabe a que horas ele passou num sitio onde nao havia mais ninguem.
    ///
    /// **Continua a nao haver botao nenhum.** E a unica conversa do jogo em que o
    /// jogador nao escolhe uma linha — responde com o corpo, entrando no carro ou
    /// ficando ali. O que mudou e que agora as duas respostas nao sao a mesma coisa:
    ///
    /// - ficar ate ao fim = <c>stranger_helped</c>. Ele carrega as pecas, pergunta
    ///   para onde, e o Tomas diz-lhe a morada em voz alta;
    /// - entrar no carro antes disso = <c>stranger_refused</c>.
    ///
    /// Ate isto existir, o `road_resolved` fechava o capitulo para os dois casos e o
    /// `TrustStranger` nao tinha quem o alimentasse: o final *"falsa confianca"*
    /// estava escrito, tinha epilogo, e era inalcancavel por construcao.
    ///
    /// **A oferta e dita antes de ser aceite, e demora.** Sem a segunda tanda de
    /// falas, quem ficasse ali por educacao ou por nao saber que podia entrar levava
    /// com a variavel sem perceber que tinha decidido alguma coisa — que e
    /// exactamente a "escolha obscura" que a §6.4 proibe. Com ela, a ultima coisa
    /// que acontece antes de a variavel subir e o Tomas a dizer onde mora, e o
    /// jogador tem a mala aberta e a porta do carro atras dele durante a oferta toda.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StrangerEncounter : MonoBehaviour
    {
        /// <summary>Como e que o encontro acabou.</summary>
        public enum Outcome
        {
            /// <summary>Ainda esta a acontecer.</summary>
            Pending,
            /// <summary>Ficou ali. Deixou-o carregar as pecas e disse-lhe onde mora.</summary>
            Helped,
            /// <summary>Entrou no carro. Nao chegou a haver oferta nenhuma.</summary>
            Refused
        }

        [SerializeField] private WorldDialogueController dialogue;
        [SerializeField] private string speakerName = "Stranger";

        [Tooltip("Ditas por esta ordem, com pausas. A ultima abre a oferta.")]
        [SerializeField, TextArea] private string[] lines =
        {
            // Quem ele e, em duas frases: alguem que ia a passar e parou. O jogador
            // nunca chega a perguntar "quem e este gajo?" — sabe exactamente o que
            // ele e. O que nao sabe e como e que ele sabe o resto, e essa e a
            // pergunta que o capitulo quer.
            "Saw your lights stop. Car trouble?",
            "I am just up the road myself. Give you a hand if you want.",

            // A partir daqui, tudo o que ele diz e uma coisa que o jogador leu num
            // SMS do vendedor, horas antes: o alternador, o deposito de agua, a
            // hora, o vir sozinho. Nada disto e ameaca. Sao factos banais ditos por
            // quem nao devia te-los.
            "These roads eat alternators. That what you were down there for?",
            "The industrial park, right. The one past the water tower.",
            "You left about ten. Half ten.",
            "No — quarter to. It was quarter to.",
            "Long way to come on your own."
        };

        [Tooltip("So sao ditas a quem ficou. E aqui que a ajuda deixa de ser uma "
               + "frase e passa a ser uma coisa que aconteceu.")]
        [SerializeField, TextArea] private string[] offerLines =
        {
            // A oferta, dita a direito. O jogador ouve isto com a porta do carro
            // aberta atras dele: a partir daqui, ficar e uma decisao e nao uma
            // distraccao.
            "Those boxes are heavy on your own. Pop the boot, I will put them across for you.",
            "Right. Where am I taking them?"
        };

        [Tooltip("A resposta do Tomas. E este o gesto: nao e aceitar a ajuda, e dizer "
               + "onde mora a alguem que ja sabia.")]
        [SerializeField, TextArea] private string playerAnswer =
            "Fourteen, off the square. Third floor.";

        [Tooltip("O que ele responde a morada. O arrepio todo do capitulo esta em nao "
               + "ser surpresa nenhuma para ele.")]
        [SerializeField, TextArea] private string closingLine =
            "I know the one. The street door downstairs does not lock.";

        [Tooltip("O nome por que o Tomas aparece a falar. E a unica fala dita em voz "
               + "alta por ele no jogo inteiro, e e para ser notada.")]
        [SerializeField] private string playerName = "Tomas";

        [Tooltip("Quanto tempo cada fala fica no ecra.")]
        [SerializeField, Min(1f)] private float holdSeconds = 3.4f;

        [Tooltip("Silencio entre falas. Ele nao tem pressa nenhuma, e e isso que "
               + "incomoda.")]
        [SerializeField, Min(0f)] private float gapSeconds = 1.1f;

        [Tooltip("Silencio depois da ultima pergunta e antes de o Tomas responder. E "
               + "a janela em que o jogador ainda pode virar costas e entrar no "
               + "carro — a decisao tem de caber num silencio, senao nao e decisao.")]
        [SerializeField, Min(0f)] private float answerDelaySeconds = 4.5f;

        [Header("Vozes")]
        [Tooltip("A voz dele. **Espacial**, e no corpo dele: durante metade da cena o "
               + "jogador nao lhe ve a cara, so a silhueta em contraluz, e ouvir de "
               + "que lado vem a voz e a unica coisa que diz onde ele esta.")]
        [SerializeField] private NpcMumbleVoice strangerVoice;

        [Tooltip("A voz do Tomas. **Nao espacial**: nao vem de um sitio, vem de dentro "
               + "da cabeca dele. Janela de pitch mais baixa do que a do estranho, "
               + "porque as silabas geradas sao as mesmas para toda a gente e duas "
               + "vozes iguais sao uma pessoa a falar sozinha.")]
        [SerializeField] private NpcMumbleVoice playerVoice;

        [Header("Ligacoes")]
        [Tooltip("Para saber se ele entrou no carro a meio. Nao ha botao de recusa: "
               + "a recusa e o corpo dele a sair dali.")]
        [SerializeField] private CarSeat seat;

        [SerializeField] private ChapterDirector director;

        [SerializeField] private string helpedEvent = "stranger_helped";
        [SerializeField] private string refusedEvent = "stranger_refused";

        /// <summary>
        /// `Talking` sao as sete falas do principio, ditas em todos os casos. Tudo o
        /// que vem a seguir so acontece a quem ainda estiver de pe na berma.
        /// </summary>
        private enum Phase { Idle, Talking, Offering, Answering, Closing, Over }

        private Phase phase = Phase.Idle;
        private int index;
        private float nextAt = -1f;

        /// <summary>Disse tudo o que tinha a dizer.</summary>
        public bool Finished => phase == Phase.Over;

        /// <summary>Como acabou. Lido pelo capitulo e pelos finais.</summary>
        public Outcome Result { get; private set; } = Outcome.Pending;

        private void Awake()
        {
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
            if (seat == null) seat = FindObjectOfType<CarSeat>(true);
        }

        /// <summary>Comeca a falar. Chamado quando ele chega ao pe do carro.</summary>
        public void Begin()
        {
            if (phase != Phase.Idle) return;
            phase = Phase.Talking;
            index = 0;
            nextAt = Time.time;
        }

        private void Update()
        {
            if (phase == Phase.Idle || phase == Phase.Over) return;

            // A recusa ganha a tudo o resto e e verificada primeiro: ele pode entrar
            // no carro a meio de uma frase, e e o momento mais provavel para o fazer.
            // Sem isto, quem fugisse enquanto ele falava continuava a apanhar a
            // oferta pelas costas — o encontro corria ate ao fim com o jogador ja
            // fechado la dentro, e a variavel subia a quem tinha feito o contrario.
            if (seat != null && seat.Seated) { Decide(Outcome.Refused); return; }

            if (Time.time < nextAt) return;
            if (dialogue != null && dialogue.IsBusy) return;

            switch (phase)
            {
                case Phase.Talking:
                    if (Speak(lines)) return;
                    phase = Phase.Offering;
                    index = 0;
                    return;

                case Phase.Offering:
                    if (Speak(offerLines)) return;
                    phase = Phase.Answering;
                    nextAt = Time.time + answerDelaySeconds;
                    return;

                case Phase.Answering:
                    Say(playerName, playerAnswer, playerVoice);
                    phase = Phase.Closing;
                    return;

                case Phase.Closing:
                    Say(speakerName, closingLine, strangerVoice);
                    Decide(Outcome.Helped);
                    return;
            }
        }

        /// <summary>
        /// Diz a proxima da lista. Falso quando a lista acabou.
        /// </summary>
        private bool Speak(string[] block)
        {
            if (block == null || index >= block.Length) return false;

            Say(speakerName, block[index++], strangerVoice);
            return true;
        }

        /// <summary>
        /// Uma fala, sem tirar as maos ao jogador.
        ///
        /// O `blocksInteraction` era o que vem por omissao — verdadeiro, como numa
        /// conversa — e isso **tornava a recusa impossivel**: enquanto o estranho
        /// falava, o Tomas nao conseguia abrir a porta do proprio carro, e as unicas
        /// janelas para o fazer eram os 1,1 s de silencio entre falas. Uma decisao
        /// que so cabe entre duas frases nao e uma decisao.
        ///
        /// E a mesma razao do `RuiCarRemark`: isto nao e uma conversa, e uma coisa
        /// dita ao lado de alguem que esta a decidir se fica.
        /// </summary>
        private void Say(string who, string line, NpcMumbleVoice voice)
        {
            nextAt = Time.time + holdSeconds + gapSeconds;
            if (string.IsNullOrWhiteSpace(line) || dialogue == null) return;

            dialogue.ShowReaction(who, line, voice, holdSeconds, null,
                autoClose: true, blocksInteraction: false);
        }

        /// <summary>
        /// Fecha o encontro, uma vez so.
        ///
        /// O director e resolvido **na leitura**: esta cena e trocada pela do
        /// apartamento assim que o `road_resolved` sair, e um director guardado no
        /// `Awake` e o que se vai desactivar.
        /// </summary>
        private void Decide(Outcome outcome)
        {
            if (phase == Phase.Over) return;
            phase = Phase.Over;
            Result = outcome;

            string id = outcome == Outcome.Helped ? helpedEvent : refusedEvent;
            if (string.IsNullOrWhiteSpace(id)) return;

            director = ChapterDirector.Resolve(director);
            director?.Notify(id);
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorSetLines(string[] spoken) => lines = spoken;

        /// <summary>As duas vozes. Ver os tooltips: uma e do mundo, a outra nao.</summary>
        public void EditorSetVoices(NpcMumbleVoice him, NpcMumbleVoice me)
        {
            strangerVoice = him;
            playerVoice = me;
        }
#endif
    }
}
