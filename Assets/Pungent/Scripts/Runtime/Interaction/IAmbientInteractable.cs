namespace Pungent.Interaction
{
    /// <summary>
    /// "Isto e so para olhar." Um interactavel que existe para a casa parecer
    /// habitada e que **nunca** deve ganhar a mira a um verbo a serio.
    ///
    /// Sem esta distincao, quem decidia era a ordem por que os componentes foram
    /// adicionados ao GameObject: o `PlayerInteractor` escolhe o primeiro
    /// interactavel com prompt, e na cama o `FlavourInteractable` esta antes do
    /// `PlayerSleep`. Medido, isso dava **0 em 11** — de todas as direccoes e
    /// distancias medidas, a cama oferecia "Look at the bed" e nunca "Sleep", e o
    /// dia nao tinha como acabar. A mesma ordem tirava a porta do quarto a quem
    /// apontava a porta do quarto, porque a comoda ao lado tinha uma frase para
    /// dizer.
    ///
    /// Uma frase sobre um movel nao e uma accao. Perde sempre, e deixa de ser
    /// preciso ter cuidado com a ordem dos componentes.
    /// </summary>
    public interface IAmbientInteractable
    {
    }
}
