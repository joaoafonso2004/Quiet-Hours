using System;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Um capitulo do jogo, guardado como asset.
    ///
    /// O Dia 2 e o Dia 3 foram escritos como classes — `OpeningQuestDirector` e
    /// `DayTwoDirector` — cada uma com a sua ferramenta de ligacao. O plano tem
    /// sete capitulos: por esse caminho seriam sete classes e sete ferramentas, e
    /// mudar a ordem de um passo passava a ser mexer em codigo.
    ///
    /// Aqui um capitulo e um ficheiro. Acrescentar um dia e criar um asset, e
    /// reordenar a manha e arrastar linhas num array.
    ///
    /// Os dois dias que ja existem ficam como estao: funcionam, estao ligados, e
    /// reescreve-los agora era gastar tempo a nao andar para a frente.
    /// </summary>
    [CreateAssetMenu(menuName = "Pungent/Narrative/Chapter", fileName = "CH_NewChapter")]
    public sealed class ChapterDefinition : ScriptableObject
    {
        /// <summary>Como se sabe que um passo acabou.</summary>
        public enum Completion
        {
            /// <summary>Passa ao seguinte assim que o texto e mostrado. Para encadear falas.</summary>
            Immediate,
            /// <summary>Ao fim de <see cref="Step.Seconds"/>.</summary>
            Timer,
            /// <summary>Quando o jogador chega perto de <see cref="Step.Place"/>.</summary>
            ReachArea,
            /// <summary>Quando alguem chamar Notify com o <see cref="Step.EventId"/>.</summary>
            Event
        }

        [Serializable]
        public sealed class Step
        {
            [Tooltip("Linha do objectivo. Vazio = sem objectivo visivel, util para "
                   + "passos que so existem para esperar por alguma coisa.")]
            public string Objective;

            [Tooltip("Linha secundaria, mais apagada. Para o que se pode fazer e nao e preciso.")]
            public string SideObjective;

            [Tooltip("Pensamento do protagonista ao entrar no passo.")]
            [TextArea] public string Thought;

            [Header("Como acaba")]
            public Completion Ends = Completion.Event;

            [Tooltip("Para Timer.")]
            [Min(0f)] public float Seconds = 5f;

            [Tooltip("Para ReachArea: centro e raio, em metros.")]
            public Vector3 Place;
            [Min(0.3f)] public float Radius = 2f;

            [Tooltip("Para Event: o identificador por que este passo espera.")]
            public string EventId;

            [Header("Ao acabar")]
            [Tooltip("Eventos levantados quando o passo fecha. Servem os fios do "
                   + "telemovel e os passos de outros capitulos.")]
            public string[] RaiseOnComplete = Array.Empty<string>();
        }

        [SerializeField] private string title = "Day 3";

        [Header("Cartao de abertura")]
        [Tooltip("Linha de cima do ecra preto. Vazio nas duas = capitulo sem cartao.")]
        [SerializeField] private string cardDate = "TUESDAY";
        [Tooltip("Linha grande. A hora faz trabalho sozinha: 02:47 a terceira vez ja "
               + "nao precisa de explicacao.")]
        [SerializeField] private string cardTime = "02:47";

        [Tooltip("Evento que arranca este capitulo. Vazio = arranca sozinho.")]
        [SerializeField] private string startsOnEvent;

        [SerializeField] private Step[] steps = Array.Empty<Step>();

        public string Title => title;
        public string StartsOnEvent => startsOnEvent;
        public string CardDate => cardDate;
        public string CardTime => cardTime;
        public bool HasCard => !string.IsNullOrWhiteSpace(cardDate) || !string.IsNullOrWhiteSpace(cardTime);
        public int StepCount => steps != null ? steps.Length : 0;

        public Step StepAt(int index)
        {
            if (steps == null || index < 0 || index >= steps.Length) return null;
            return steps[index];
        }

#if UNITY_EDITOR
        /// <summary>Usado pelas ferramentas de editor. Publico porque vivem noutra assembly.</summary>
        public void EditorPopulate(string chapterTitle, string startEvent, Step[] newSteps,
            string date = "", string time = "")
        {
            title = chapterTitle;
            startsOnEvent = startEvent;
            steps = newSteps;
            cardDate = date;
            cardTime = time;
        }
#endif
    }
}
