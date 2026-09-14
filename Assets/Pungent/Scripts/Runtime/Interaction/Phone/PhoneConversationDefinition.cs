using System;
using Pungent.Dialogue;
using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>
    /// Um fio de mensagens com um contacto, guardado como asset.
    ///
    /// Substitui os cinco campos `[SerializeField]` do `PrototypePhoneUI`
    /// (`incomingMessage`, `neutralResponse`, `defiantResponse`, `ruiFollowUp`,
    /// `ruiFollowUpDefiant`), que so davam para um contacto e uma troca. Um fio
    /// que corre ao longo do jogo — o do Pai — precisa de lista, historico e
    /// mensagens que chegam quando a historia manda, nao quando a cena carrega.
    ///
    /// Corresponde a `PhoneConversationDefinition` da seccao 13.1.1 do plano.
    /// </summary>
    [CreateAssetMenu(menuName = "Pungent/Phone/Conversation", fileName = "PHN_NewThread")]
    public sealed class PhoneConversationDefinition : ScriptableObject
    {
        /// <summary>
        /// Um passo do fio: uma mensagem que chega e, se houver, as respostas que abre.
        ///
        /// Reutiliza <see cref="DialogueChoice"/> do dialogo presencial de propósito.
        /// A escolha ja traz texto, replica e <see cref="DialogueTone"/>, e o tom ja
        /// alimenta a ameaca oculta — nao ha razao para o telemovel ter um tipo
        /// paralelo que faz o mesmo e diverge com o tempo.
        /// </summary>
        [Serializable]
        public sealed class Step
        {
            [TextArea] public string Line;

            [Tooltip("SILENCIO antes de o contacto sequer comecar a escrever, contado a "
                   + "partir do momento em que o passo fica desbloqueado. E aqui que "
                   + "vao os tempos longos: um pai as 02:47 pode demorar minutos a "
                   + "pegar no telemovel.")]
            [Min(0f)] public float DelaySeconds = 20f;

            [Tooltip("Se preenchido, o passo so comeca a contar depois deste evento de "
                   + "historia. Vazio = segue logo a seguir ao passo anterior.")]
            public string WaitForEvent;

            [Tooltip("Se este evento ja tiver acontecido quando chegar a vez desta "
                   + "mensagem, ela e saltada.\n\nVazio = chega sempre, por mais tarde "
                   + "que seja.\n\n**Os atrasos sao absolutos e os dias nao sao.** Uma "
                   + "mensagem do Pai a dizer 'Morning' presa ao Dia 1 com 120 s de "
                   + "atraso chega a meio da noite das 02:47 se o jogador se deitar "
                   + "depressa. Poe aqui o evento que fecha o dia dela.")]
            public string StaleAfterEvent;

            [Tooltip("Quanto tempo se ve 'a escrever...' DEPOIS do silencio e antes de a "
                   + "mensagem aparecer. Sao segundos, nao minutos: e o tempo de "
                   + "escrever a frase, nao o de decidir responder. 0 = aparece do nada.")]
            [Min(0f)] public float TypingSeconds = 4f;

            [Tooltip("Vazio = mensagem sem resposta. Duas entradas = o fio espera pelo jogador.")]
            public DialogueChoice[] Choices = Array.Empty<DialogueChoice>();

            [Tooltip("SILENCIO entre a resposta do jogador e o contacto comecar a "
                   + "escrever a replica. Depois deste tempo corre o TypingSeconds.")]
            [Min(0f)] public float ReplyDelaySeconds = 14f;

            [Tooltip("O contacto pode simplesmente nao responder. Nao ficar em silencio "
                   + "e uma decisao de escrita, nao um estado por omissao.")]
            public bool EndsThread;

            public bool HasChoices => Choices != null && Choices.Length >= 2;
        }

        [SerializeField] private string contactName = "Rui";

        [Tooltip("Acontecimento que poe este contacto NO TELEMOVEL. Vazio = ja la "
               + "estava quando o jogo comecou, que e o caso do Pai.\n\n"
               + "Ate ele acontecer o fio nao existe: nao aparece na lista, nao "
               + "conta como por ler e nao comeca a correr.")]
        [SerializeField] private string unlockEvent;

        [Tooltip("Fio que ja comeca entregue quando o jogo arranca. Util para o "
               + "historico anterior ao inicio do jogo.")]
        [SerializeField] private bool startsDelivered;

        [Tooltip("O tom das respostas neste fio alimenta a ameaca oculta do Rui. "
               + "So faz sentido no fio DELE: ser seco com o pai nao torna o colega "
               + "de casa perigoso, e era o que acontecia por o servico aplicar isto "
               + "a todos os fios.")]
        [SerializeField] private bool affectsRuiThreat;

        [SerializeField] private Step[] steps = Array.Empty<Step>();

        public string ContactName => contactName;

        /// <summary>
        /// Vazio = contacto que ja estava gravado. Cheio = so entra na lista quando
        /// a historia o puser la.
        ///
        /// Existe porque a lista de contactos era o indice dos ficheiros: o servico
        /// criava um fio por cada asset, o telemovel mostrava-os todos, e na primeira
        /// noite do jogo o Tomas tinha o numero de um vendedor de pecas que so vai
        /// conhecer no dia seguinte. Acrescentar um fio ao jogo era acrescentar um
        /// contacto ao prologo.
        /// </summary>
        public string UnlockEvent => unlockEvent;

        public bool StartsDelivered => startsDelivered;
        public bool AffectsRuiThreat => affectsRuiThreat;
        public int StepCount => steps != null ? steps.Length : 0;

        public Step StepAt(int index)
        {
            if (steps == null || index < 0 || index >= steps.Length) return null;
            return steps[index];
        }

#if UNITY_EDITOR
        /// <summary>
        /// Usado pela migracao do formato antigo. Publico porque o codigo de editor
        /// vive noutra assembly e internal nao a atravessa.
        /// </summary>
        public void EditorPopulate(string contact, Step[] newSteps)
        {
            contactName = contact;
            steps = newSteps;
        }

        /// <summary>
        /// Quem poe este contacto no telemovel. Separado do `EditorPopulate` porque
        /// quem escreve o texto de um fio nem sempre e quem sabe a que altura da
        /// historia o contacto aparece.
        /// </summary>
        public void EditorSetUnlock(string eventId) => unlockEvent = eventId;
#endif
    }
}
