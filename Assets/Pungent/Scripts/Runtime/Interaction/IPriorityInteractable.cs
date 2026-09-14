namespace Pungent.Interaction
{
    /// <summary>
    /// "Isto agora e mais importante do que o que este objecto costuma fazer."
    ///
    /// O oposto do <see cref="IAmbientInteractable"/>, e existe pela mesma razao: a
    /// ordem por que alguem arrastou componentes para um GameObject nao pode ser o
    /// que decide o que o jogador consegue fazer.
    ///
    /// O caso que o obrigou a existir e o Rui. Ele ja e tres verbos no mesmo corpo —
    /// falar, a conversa guionada, e a presenca ao telefone — e o confronto e um
    /// quarto que so existe durante dois minutos depois de ele ter feito alguma
    /// coisa. Nesses dois minutos, "Ask him about it" tem de ganhar a "Talk to Rui"
    /// sem depender de estar antes na lista de componentes.
    ///
    /// **Usa-se pouco, e por definicao.** Se metade da casa for prioritaria, isto
    /// deixa de ordenar nada e volta a ser a ordem dos componentes com mais passos.
    /// A regra e: so o que e verdade durante uma janela curta e por causa de uma
    /// coisa que acabou de acontecer.
    /// </summary>
    public interface IPriorityInteractable
    {
    }
}
