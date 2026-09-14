using System;
using System.Collections.Generic;
using Pungent.Dialogue;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// As variaveis da seccao 6.2. O que o jogo sabe sobre o que o jogador fez.
    ///
    /// Ate aqui a unica coisa acumulada era o `ThreatLevel` do
    /// <see cref="Pungent.NPC.PrototypeNpcRoutine"/>, um inteiro de 0 a 4 que vive no
    /// Rui e morre com a cena. Serve o comportamento dele — repara em ti mais
    /// depressa, pousa menos tempo em cada sitio — e nao serve mais nada. Com isso e
    /// so isso, **os quatro finais da seccao 6.4 sao impossiveis**: nao ha onde
    /// guardar se a prova foi enviada, se a ajuda da estrada foi aceite, ou quantas
    /// portas ficaram trancadas.
    ///
    /// Isto e o sitio. Vive no `GAME_SYSTEMS`, atravessa as cenas com ele, e e
    /// lido no fim pelo <see cref="EndingSelector"/>.
    ///
    /// **Alimentado por acontecimentos, nao por chamadas.** O projecto inteiro ja
    /// fala uma lingua so — `director.Notify("evidence_found")` — e cada sistema que
    /// quisesse mexer aqui teria de conhecer esta classe e lembrar-se de o fazer. E
    /// exactamente assim que uma variavel oculta fica meio ligada: metade dos sitios
    /// mexe nela, a outra metade esqueceu-se, e nao ha erro nenhum a dizer qual.
    ///
    /// Aqui a ligacao e uma tabela. Acrescentar consequencia a um acontecimento e
    /// acrescentar uma linha, e um acontecimento sem linha simplesmente nao conta —
    /// que e o comportamento certo e nao um esquecimento.
    ///
    /// A seccao 6.1 e categorica: **nada disto aparece no ecra.** Nao ha barra, nao
    /// ha numero e nao ha "Rui +10 agressividade". O efeito e apresentado por
    /// comportamento, e o jogador que chegar ao fim deve saber que a casa mudou sem
    /// conseguir dizer que numero e que subiu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NarrativeBlackboard : MonoBehaviour, IRebindable
    {
        /// <summary>Os nomes da seccao 6.2, e mais nenhum.</summary>
        public enum Variable
        {
            /// <summary>Quanto o Rui acredita que o Tomas descobriu.</summary>
            RuiSuspicion,
            /// <summary>Predisposicao dele para controlar fisicamente.</summary>
            RuiAggression,
            /// <summary>Padrao de confronto do jogador.</summary>
            PlayerDefiance,
            /// <summary>Provas descobertas.</summary>
            EvidenceFound,
            /// <summary>Provas enviadas a terceiro.</summary>
            EvidenceShared,
            /// <summary>Aceitacao de ajuda.</summary>
            TrustStranger,
            /// <summary>Preparacao do apartamento: portas trancadas, luzes apagadas.</summary>
            DoorsSecured,
            /// <summary>Historial de contacto visual hostil.</summary>
            StarePressure,

            /// <summary>
            /// Quantas vezes o Tomas reparou em alguma coisa e **nao disse nada**.
            ///
            /// Nao e o contrario da <see cref="PlayerDefiance"/> e nao mede medo: mede
            /// o que o Rui aprendeu sobre o que pode fazer sem consequencia. Um homem
            /// que mexe numa gaveta e ouve uma pergunta a seguir sabe onde e o limite;
            /// um que nao ouve nada nao ficou tranquilizado — ficou informado.
            ///
            /// Existe separada de propósito. Ver a nota do <see cref="OnChoice"/>: ja
            /// houve uma versao em que os dois ramos de uma escolha somavam na
            /// **mesma** variavel, e a ameaca subia jogasse o jogador como jogasse.
            /// Duas variaveis diferentes nao sao esse erro — sao dois Ruis diferentes,
            /// que e o que a §6.1 pede. O que se paga por calar-se nao e ele ficar
            /// mais perigoso; e ele ficar mais a vontade.
            /// </summary>
            RuiEmboldened,
            /// <summary>Variavel narrativa simples, nao recurso de manutencao constante.</summary>
            PhoneBattery,

            /// <summary>
            /// Quantas vezes o Tomas disse ao pai a verdade sobre o que se passa em
            /// casa.
            ///
            /// **Nao mede coragem e nao mede paranoia: mede ter contado.** O fio do
            /// PHN_Dad e um homem a ler tudo o que o filho lhe diz pelo lado banal, e
            /// as respostas honestas sao precisamente as que fazem o Tomas parecer
            /// ridiculo a frente da unica pessoa que se importa com ele. Escolher a
            /// resposta segura — *"esta tudo bem"* — nao custa nada no momento e custa
            /// o final.
            ///
            /// Acrescentada **no fim do enum de propósito**: os valores anteriores sao
            /// serializados por indice nas tabelas de regras que ja existem em cena, e
            /// meter uma entrada no meio renumerava-as todas em silencio.
            /// </summary>
            FatherWarned
        }

        [Serializable]
        public sealed class Rule
        {
            [Tooltip("Acontecimento de historia. O mesmo vocabulario do resto do jogo.")]
            public string EventId;

            public Variable Target;

            [Tooltip("Somado quando o acontecimento passar. Negativo tambem serve.")]
            public int Amount = 1;

            [Tooltip("Uma vez so. Quase sempre verdadeiro: um acontecimento de "
                   + "historia acontece uma vez, mas a tabela pode ser reaplicada "
                   + "por um checkpoint e nao se quer que a captura pague duas vezes.")]
            public bool Once = true;
        }

        [Tooltip("Que acontecimento mexe em que variavel. Ver a nota da classe: e "
               + "isto, e nao chamadas espalhadas pelo codigo, que mantem as nove "
               + "variaveis ligadas ao jogo.")]
        [SerializeField] private Rule[] rules = Array.Empty<Rule>();

        [Tooltip("Quanto uma resposta cortante custa. O tom, nao o texto, e o que "
               + "alimenta isto — reescrever uma fala nunca deve mudar por acidente "
               + "o que ela provoca.")]
        [SerializeField, Min(0)] private int defianceForEdgy = 1;
        [SerializeField, Min(0)] private int suspicionForEdgy = 1;

        [SerializeField] private ChapterDirector director;

        private readonly Dictionary<Variable, int> values = new Dictionary<Variable, int>();
        private readonly HashSet<string> applied = new HashSet<string>();

        public static NarrativeBlackboard Instance { get; private set; }

        private void Awake()
        {
            // O `GameSystemsRoot` ja garante um so exemplar vivo, mas quem le isto
            // le-o por atalho estatico e um atalho a apontar para o exemplar que se
            // vai desactivar era pior do que nao haver atalho nenhum.
            if (Instance == null) Instance = this;
            Rebind();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Rebind()
        {
            if (Instance == null) Instance = this;
            if (director == null) director = FindObjectOfType<ChapterDirector>();
        }

        public int Get(Variable variable) => values.TryGetValue(variable, out int v) ? v : 0;

        public bool Any(Variable variable) => Get(variable) > 0;

        /// <summary>
        /// Soma. Publico porque nem tudo cabe numa tabela de acontecimentos: o tom
        /// de uma resposta e uma qualidade da escolha e nao um acontecimento, e
        /// contactos visuais nao tem nome proprio.
        /// </summary>
        public void Add(Variable variable, int amount)
        {
            if (amount == 0) return;
            values[variable] = Mathf.Max(0, Get(variable) + amount);
        }

        /// <summary>
        /// Aplica a tabela a um acontecimento. Chamado pelo
        /// <see cref="ChapterDirector"/> a cada `Notify`, que e o unico sitio por
        /// onde os acontecimentos deste jogo passam todos.
        /// </summary>
        public void OnEvent(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || rules == null) return;

            foreach (var rule in rules)
            {
                if (rule == null || rule.EventId != id) continue;

                if (rule.Once)
                {
                    string key = id + "#" + rule.Target;
                    if (!applied.Add(key)) continue;
                }

                Add(rule.Target, rule.Amount);
            }
        }

        /// <summary>
        /// Uma escolha do jogador, presencial ou por mensagem.
        ///
        /// So o tom cortante conta. Ser educado nao sobe nada — e a licao que o
        /// `ApplyDialogueChoice` do Rui ja tinha aprendido: a somar nas duas, a
        /// ameaca subia jogasse o jogador como jogasse, e o estado "oculto" deixava
        /// de medir seja o que for.
        /// </summary>
        public void OnChoice(DialogueTone tone)
        {
            if (tone != DialogueTone.Edgy) return;
            Add(Variable.PlayerDefiance, defianceForEdgy);
            Add(Variable.RuiSuspicion, suspicionForEdgy);
        }

        /// <summary>
        /// O jogador reparou em alguma coisa e deixou passar.
        ///
        /// **Nao passa pelo <see cref="OnChoice"/> de proposito.** Aquele metodo mede
        /// confronto e tem escrito porque e que so conta o `Edgy`; meter o silencio la
        /// dentro era exactamente o erro que ele descreve. Isto e outra pergunta, com
        /// outra variavel e outra consequencia — ver <see cref="Variable.RuiEmboldened"/>.
        ///
        /// Chamado por quem ofereceu ao jogador uma oportunidade real de dizer alguma
        /// coisa e viu-o nao a usar. Nao serve para silencio por distraccao: se o
        /// jogador nunca chegou a ouvir a pergunta, nao escolheu nada.
        /// </summary>
        public void OnLetItGo() => Add(Variable.RuiEmboldened, 1);

        /// <summary>Contacto visual mantido. Chamado pelo stare-down.</summary>
        public void OnStare() => Add(Variable.StarePressure, 1);

        /// <summary>Para inspeccionar em desenvolvimento. Nunca no ecra do jogador.</summary>
        public string Dump()
        {
            var text = new System.Text.StringBuilder("[Blackboard]");
            foreach (Variable variable in Enum.GetValues(typeof(Variable)))
                text.Append(' ').Append(variable).Append('=').Append(Get(variable));
            return text.ToString();
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(Rule[] newRules) => rules = newRules;
#endif
    }
}
