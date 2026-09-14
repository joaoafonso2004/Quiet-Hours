namespace Pungent.Narrative
{
    /// <summary>
    /// O que se vê nas mãos durante uma tarefa doméstica, e como se gasta.
    ///
    /// A `HomeTaskInteractable` só sabe quanto falta para acabar; não sabe se o que
    /// está a arder é um cigarro ou se o que está a esvaziar é um copo. Cada tarefa
    /// traz o seu próprio objecto e é ele que decide o que fazer com o progresso.
    /// </summary>
    public interface ITaskProgressVisual
    {
        void BeginTask();

        /// <param name="normalised">0 no início da tarefa, 1 no fim.</param>
        void SetTaskProgress(float normalised);

        void EndTask();
    }
}
