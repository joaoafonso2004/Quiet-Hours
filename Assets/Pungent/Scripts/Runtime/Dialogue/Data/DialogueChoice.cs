using System;
using UnityEngine;

namespace Pungent.Dialogue
{
    /// <summary>
    /// O tom de uma resposta. E isto — nao o texto — que alimenta a ameaca oculta,
    /// para que reescrever uma fala nunca mude por acidente o que ela provoca no Rui.
    ///
    /// Os valores vem da seccao 13.1 do plano mestre. Calm/Honest nao agravam a
    /// relacao; Evasive e Silent deixam uma marca; Edgy pesa mais.
    /// </summary>
    public enum DialogueTone
    {
        Calm,
        Edgy,
        Honest,
        Evasive,
        Silent
    }

    /// <summary>
    /// Uma resposta possivel do jogador, com o que o NPC diz de volta.
    ///
    /// A resposta vive junto da escolha de proposito: no formato antigo o texto da
    /// opcao e a replica correspondente estavam em campos separados (`ChoiceEdgy` e
    /// `ReplyToEdgy`), e mexer numa sem mexer na outra desemparelhava-as em silencio.
    /// </summary>
    [Serializable]
    public sealed class DialogueChoice
    {
        [TextArea] public string Text;

        [Tooltip("O que o NPC responde a esta escolha. Vazio = segue direto para a fala seguinte.")]
        [TextArea] public string Reply;

        [Tooltip("Alimenta a ameaca oculta. Nao depende do texto.")]
        public DialogueTone Tone = DialogueTone.Calm;

        [Tooltip("Acontecimento levantado por escolher ESTA opcao. Vazio = nenhum.\n\n"
               + "E o unico sitio por onde uma escolha do jogador pode mudar a "
               + "historia, e nao so o tom da conversa. Ate existir, uma decisao "
               + "tomada no telemovel nao deixava rasto nenhum: o `EvidenceShared` "
               + "nao tinha quem o alimentasse porque o servico de mensagens "
               + "**consome** acontecimentos e nunca os levantou.")]
        public string RaisesEvent;

        public bool HasReply => !string.IsNullOrWhiteSpace(Reply);

        public bool RaisesAnything => !string.IsNullOrWhiteSpace(RaisesEvent);
    }
}
