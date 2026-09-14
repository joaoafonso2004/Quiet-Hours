using UnityEngine;
using V = Pungent.Narrative.NarrativeBlackboard.Variable;

namespace Pungent.Narrative
{
    /// <summary>
    /// Escolhe qual dos quatro finais da seccao 6.4 aconteceu.
    ///
    /// A regra que a propria seccao impoe: *"Nao criar finais baseados numa unica
    /// escolha obscura. O resultado deve reflectir varias decisoes compreensiveis."*
    /// Por isso nenhum destes ramos depende de um so `if`, e nenhum depende de uma
    /// coisa que o jogador possa ter feito sem perceber que a fez.
    ///
    /// Cada um levanta o seu acontecimento, e ha um capitulo de epilogo a espera de
    /// cada um. Assim o texto continua a viver em assets e acrescentar um final e
    /// acrescentar um asset — nao mais um ramo aqui dentro.
    ///
    /// **A ordem importa e nao e arbitraria.** Le-se de cima para baixo, do desfecho
    /// mais especifico para o mais generico: quem foi apanhado foi apanhado, tenha
    /// feito o resto que tiver feito.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndingSelector : MonoBehaviour, IRebindable
    {
        [Tooltip("Quando isto acontecer, decide-se. E o ultimo passo do climax que o "
               + "levanta.")]
        [SerializeField] private string decideOn = "epilogue";

        [Header("Os tres")]
        [Tooltip("Saiu de casa pelo seu pe. Com prova ou sem ela — e o mesmo final.")]
        [SerializeField] private string escaped = "ending_out";

        [Tooltip("Levantado ao lado do `escaped` quando a prova ja tinha saido de "
               + "casa. Nao e um final: e o que faz o mesmo epilogo mostrar outra "
               + "coisa.\n\nVazio = a prova deixa de mudar o que se ve.")]
        [SerializeField] private string evidenceOut = "ending_out_proof";

        [Tooltip("Falhou durante a seccao de perigo.")]
        [SerializeField] private string captured = "ending_taken";

        [Tooltip("Contou ao pai, ao longo dos cinco dias, o que se estava a passar.")]
        [SerializeField] private string fatherCame = "ending_father";

        [Header("Limiares")]
        [Tooltip("Quantas capturas fazem do desfecho uma captura. Uma so nao chega: "
               + "o climax devolve o jogador ao jogo depois de cada uma, e punir a "
               + "primeira falha com o pior final era transformar um checkpoint numa "
               + "sentenca retroactiva.")]
        [SerializeField, Min(1)] private int capturesForTaken = 3;

        [Tooltip("Quantas respostas honestas ao pai fazem dele alguem que sabe.\n\n"
               + "Tres de quatro, e nao quatro de quatro: uma pessoa que contou tres "
               + "vezes contou. Exigir todas transformava um padrao de comportamento "
               + "numa lista de verificacao, e bastava falhar uma mensagem por "
               + "distraccao para o final desaparecer sem o jogador perceber porque.")]
        [SerializeField, Min(1)] private int warningsForFather = 3;

        [SerializeField] private ChapterDirector director;
        [SerializeField] private NarrativeBlackboard blackboard;

        private bool decided;

        private void Awake() => Rebind();

        public void Rebind()
        {
            if (director == null) director = FindObjectOfType<ChapterDirector>();
            if (blackboard == null) blackboard = FindObjectOfType<NarrativeBlackboard>();
        }

        private void Update()
        {
            director = ChapterDirector.Resolve(director);
            if (decided || director == null) return;
            if (!director.HasSeen(decideOn)) return;

            decided = true;

            string ending = Choose();
            director.Notify(ending);

            // **A prova nao muda o final: muda o que se ve nele.**
            //
            // Havia dois finais para a mesma coisa — sair com prova e sair sem ela — e
            // num jogo que acontece todo dentro de uma casa isso sao dois epilogos a
            // manter para uma diferenca que cabe num plano. Passou a ser um final so,
            // com este acontecimento levantado ao lado quando a prova ja tinha saido.
            // O epilogo troca o quadro; o desfecho e o mesmo homem a descer as mesmas
            // escadas.
            if (ending == escaped && !string.IsNullOrWhiteSpace(evidenceOut)
                && blackboard != null && blackboard.Get(V.EvidenceShared) > 0)
                director.Notify(evidenceOut);
        }

        /// <summary>
        /// A conta. Escrita como quatro frases legiveis de propósito: quem vier a
        /// este ficheiro daqui a seis meses tem de conseguir dizer, sem correr o
        /// jogo, o que e preciso fazer para ver cada um.
        /// </summary>
        private string Choose()
        {
            if (blackboard == null) return escaped;

            int captures = blackboard.Get(V.RuiAggression);
            int told = blackboard.Get(V.FatherWarned);

            // Apanhado vezes de mais. Nao ha volta a dar a isto.
            if (captures >= capturesForTaken) return captured;

            // **Contou ao pai, e o pai desta vez ouviu.**
            //
            // Fica acima da falsa confianca e da prova por ser o mais especifico dos
            // tres: qualquer jogador pode enviar um ficheiro a alguem, e qualquer um
            // pode aceitar boleia de um estranho numa estrada escura. Dizer a verdade
            // quatro vezes a um homem que responde sempre pelo lado banal e um padrao
            // que se paga em constrangimento, mensagem a mensagem, durante cinco dias.
            //
            // **Nao e um resgate.** Ele nao chega a tempo de nada e nao salva ninguem —
            // o Tomas sai por si, como em todos os outros finais. O que muda e o que o
            // espera la fora, e isso e uma consequencia do que ele disse e nao do que
            // ele fez.
            if (told >= warningsForFather) return fatherCame;

            // **Saiu de casa pelo seu pe.** Com prova ou sem ela.
            //
            // Eram dois finais e passaram a um, com o corte de 11-08: num jogo que
            // acontece todo dentro de uma casa, "saiu com prova" e "saiu sem prova"
            // sao o mesmo homem a descer as mesmas escadas, e a diferenca entre eles
            // cabe num plano. O `evidenceOut`, levantado ao lado deste, e quem a diz.
            //
            // **A falsa confianca saiu com a estrada.** Dependia do `TrustStranger`,
            // que so o `StrangerEncounter` alimentava, e esse vivia no `Night_Road`.
            // A variavel fica no blackboard e a regra fica na ferramenta: desligar nao
            // e apagar, e se a estrada voltar o final volta com ela.
            return escaped;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(string outEvent, string outWithProof, string taken)
        {
            escaped = outEvent;
            evidenceOut = outWithProof;
            captured = taken;
        }
#endif
    }
}
