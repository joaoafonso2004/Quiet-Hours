using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// As coisas que ha para fazer num dia, e o que fecha esse dia.
    ///
    /// **O problema que isto resolve.**
    ///
    /// Cada dia deste jogo era uma linha recta de quatro ou cinco cliques: chega a
    /// cozinha, olha para a rua, ve o telemovel, senta-te a secretaria, dorme. Um
    /// jogador que saiba para onde vai acaba o Dia 1 em noventa segundos, e um dia
    /// de noventa segundos nao tem espaco fisico para tensao nenhuma. Nao e um
    /// problema de escrita — nenhuma frase salva um dia que acaba antes de comecar.
    ///
    /// A seccao 5 pede *"rotina, perturbacao e uma pequena consequencia"* em cada
    /// dia, e a rotina era a parte que faltava por inteiro.
    ///
    /// **A rotina nao e enchimento.** Sao os minutos em que o jogador esta ocupado,
    /// parado, de costas para uma divisao — que e exactamente a condicao de que o
    /// <see cref="HouseChange"/> precisa para existir. Sem tarefas nao ha momento
    /// nenhum em que a casa possa mudar sem ninguem ver, e sem isso este jogo e uma
    /// visita guiada. As duas coisas sao uma so.
    ///
    /// **Nem todas contam.** O conjunto pede `required` de entre as que houver, e
    /// nao todas: obrigar as sete transforma a casa numa lista de afazeres e o
    /// jogador deixa de escolher o que faz. Ele tem de comer e de trabalhar; se
    /// tambem lava a loica ou vai fumar a varanda e com ele — mas gastou o tempo na
    /// mesma, que e o que interessa.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChoreSet : MonoBehaviour
    {
        [Tooltip("Ids das tarefas deste dia, como estao nos `HomeTaskInteractable`.")]
        [SerializeField] private string[] choreIds = new string[0];

        [Tooltip("Quantas e preciso fazer para o dia poder acabar. Menos do que a "
               + "lista de proposito — ver a nota da classe.")]
        [SerializeField, Min(1)] private int required = 3;

        [Tooltip("Levantado quando a conta chega. E este que o passo do capitulo "
               + "espera; sem dono, o dia fica preso — a armadilha de sempre.")]
        [SerializeField] private string completedEvent = "day1_chores";

        [Tooltip("So comeca a contar depois deste acontecimento. Evita que uma "
               + "tarefa feita no dia anterior conte para este.")]
        [SerializeField] private string requiresEvent;

        [Tooltip("Dito quando faltar uma so. Vazio = silencio.\n\n"
               + "Nao e uma barra de progresso: e a unica pista de que o jogo espera "
               + "mais alguma coisa antes de o deixar ir dormir.")]
        [SerializeField, TextArea] private string nearlyDoneThought =
            "One more thing and I can call it a day.";

        [SerializeField] private HomeTaskDirector tasks;
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private ChapterDirector director;
        [SerializeField] private Pungent.Interaction.PrototypeHUD hud;

        private bool sent;
        private bool nearlySaid;
        private string shownSide;
        private HomeTaskInteractable[] cachedTasks;
        private StagedHomeTask[] cachedStaged;

        /// <summary>Quantas ja estao feitas. Util para inspeccionar.</summary>
        public int DoneCount { get; private set; }

        private void Awake()
        {
            if (tasks == null) tasks = FindObjectOfType<HomeTaskDirector>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            if (hud == null) hud = FindObjectOfType<Pungent.Interaction.PrototypeHUD>();
        }

        private void Update()
        {
            if (sent) return;

            // O `HomeTaskDirector` vive no jogador, e o jogador e outro objecto em
            // cada cena carregada.
            if (tasks == null)
            {
                tasks = FindObjectOfType<HomeTaskDirector>();
                if (tasks == null) return;
            }

            if (!string.IsNullOrWhiteSpace(requiresEvent))
            {
                director = ChapterDirector.Resolve(director);
                if (director == null || !director.HasSeen(requiresEvent)) return;
            }

            int done = 0;
            foreach (var id in choreIds)
                if (!string.IsNullOrWhiteSpace(id) && tasks.IsCompleted(id)) done++;

            DoneCount = done;

            if (!nearlySaid && required > 1 && done == required - 1)
            {
                nearlySaid = true;
                if (!string.IsNullOrWhiteSpace(nearlyDoneThought))
                    thoughts?.Think($"chores_{completedEvent}", nearlyDoneThought, 2, true, 3.2f);
            }

            UpdateSideObjective(done);

            if (done < required) return;

            sent = true;

            // **Limpar antes de notificar, e nunca depois.**
            //
            // O `Notify` fecha o passo do capitulo no mesmo frame, e o passo seguinte
            // escreve a sua propria linha secundaria — que no Dia 1 e *"Out on the
            // balcony. Look down at the street"*, a unica indicacao que o jogo da de
            // que ha uma varanda e que e para la ir.
            //
            // Com a limpeza a seguir, esta funcao apagava-a um instante depois de ela
            // aparecer. O jogador ficava com *"OBJECTIVE: Take a breather."* e mais
            // nada, a procurar o que fazer numa casa onde ja tinha feito tudo. O texto
            // estava escrito no capitulo desde sempre e nunca chegou a ser lido por
            // ninguem.
            if (hud != null && shownSide != null) hud.SetSideObjective(string.Empty);

            director = ChapterDirector.Resolve(director);
            director?.Notify(completedEvent);
        }

        /// <summary>
        /// Diz **uma** coisa que ha para fazer, e quantas faltam.
        ///
        /// O passo do capitulo trazia uma linha fixa — "Eat, wash up, unpack, sit
        /// down" — escrita a mao no `DayOneWiring`. Tinha tres problemas ao mesmo
        /// tempo: nao dizia quantas eram precisas (sao tres de cinco, e a lista tem
        /// quatro nomes), nao se apagava a medida que se fazia nenhuma, e nao dizia
        /// **onde**. A jogar isso nao e uma lista de tarefas, e uma frase.
        ///
        /// Isto nao tira a escolha, que e a razao de o conjunto pedir tres de cinco e
        /// nao cinco de cinco — continua a poder fazer-se qualquer uma. So mostra uma
        /// de cada vez, que era o que faltava para se perceber o que o jogo espera:
        /// o proprio prompt da tarefa seguinte por fazer, que ja e uma instrucao
        /// escrita ("Wash the dishes"), mais a conta do que falta.
        /// </summary>
        private void UpdateSideObjective(int done)
        {
            if (hud == null) return;

            int missing = Mathf.Max(0, required - done);
            string side = string.Empty;

            if (missing > 0)
            {
                string next = NextUndoneLabel();
                if (!string.IsNullOrWhiteSpace(next))
                    side = missing > 1 ? $"{next}  ({missing} left)" : $"{next}  (last one)";
            }

            if (side == shownSide) return;
            shownSide = side;
            hud.SetSideObjective(side);
        }

        /// <summary>
        /// O gesto seguinte por fazer, seja de que tipo for a tarefa.
        ///
        /// Sao dois tipos e nao um, e esquecer o segundo dava uma lista com buracos:
        /// tres dos nove afazeres da casa — comer no Dia 1, arrumar a roupa, comer no
        /// Dia 3 — sao `StagedHomeTask` e nao `HomeTaskInteractable`. Sao exactamente
        /// os que tem dois gestos em sitios diferentes, ou seja **os que mais
        /// precisam de ser explicados**, e eram os unicos que nao apareciam.
        /// </summary>
        private string NextUndoneLabel()
        {
            if (cachedTasks == null || cachedTasks.Length == 0)
                cachedTasks = FindObjectsOfType<HomeTaskInteractable>(true);
            if (cachedStaged == null || cachedStaged.Length == 0)
                cachedStaged = FindObjectsOfType<StagedHomeTask>(true);

            for (int i = 0; i < choreIds.Length; i++)
            {
                string id = choreIds[i];
                if (string.IsNullOrWhiteSpace(id) || tasks.IsCompleted(id)) continue;

                for (int t = 0; t < cachedTasks.Length; t++)
                    if (cachedTasks[t] != null && cachedTasks[t].TaskId == id)
                        return cachedTasks[t].ObjectiveLabel;

                for (int t = 0; t < cachedStaged.Length; t++)
                    if (cachedStaged[t] != null && cachedStaged[t].TaskId == id)
                        return cachedStaged[t].ObjectiveLabel;
            }
            return string.Empty;
        }

#if UNITY_EDITOR
        /// <summary>Usado pelas ferramentas de ligacao, que vivem noutra assembly.</summary>
        public void EditorConfigure(string[] ids, int howMany, string raises,
            string requires, string nearly)
        {
            choreIds = ids;
            required = howMany;
            completedEvent = raises;
            requiresEvent = requires;
            nearlyDoneThought = nearly;
        }
#endif
    }
}
