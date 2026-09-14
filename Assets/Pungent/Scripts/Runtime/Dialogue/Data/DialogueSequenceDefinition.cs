using System;
using UnityEngine;

namespace Pungent.Dialogue
{
    /// <summary>
    /// Uma conversa guionada, guardada como asset em vez de estar dentro de um
    /// componente da cena.
    ///
    /// A razao e pratica: no formato antigo as falas viviam num `[SerializeField]`
    /// do `RuiStoryConversation`, o que significa que estavam dentro do ficheiro
    /// .unity. Reescrever uma fala sujava a cena inteira, duas pessoas nao podiam
    /// tocar em texto ao mesmo tempo, e nao havia maneira de ver todo o guiao do
    /// jogo sem abrir a cena e clicar no NPC.
    ///
    /// Corresponde a `DialogueSequenceDefinition` da seccao 13.1 do plano mestre.
    /// </summary>
    [CreateAssetMenu(menuName = "Pungent/Dialogue/Sequence", fileName = "DLG_NewSequence")]
    public sealed class DialogueSequenceDefinition : ScriptableObject
    {
        /// <summary>
        /// Uma fala do NPC e, opcionalmente, as respostas que ela abre.
        /// Sem escolhas, e fala corrida e a conversa avanca sozinha.
        /// </summary>
        [Serializable]
        public sealed class Beat
        {
            [TextArea] public string Line;

            [Tooltip("O interlocutor segura o telemovel enquanto apresenta esta fala.")]
            public bool UsePhone;

            [Tooltip("Vazio = fala corrida. Duas entradas = escolha binaria.")]
            public DialogueChoice[] Choices = Array.Empty<DialogueChoice>();

            public bool HasChoices => Choices != null && Choices.Length >= 2;
        }

        [Tooltip("Nome mostrado no dialogo. Vazio = pensamento do protagonista, sem interlocutor.")]
        [SerializeField] private string speaker = "Rui";

        [SerializeField] private Beat[] beats = Array.Empty<Beat>();

        [Header("Ritmo")]
        [Tooltip("Segundos ate a escolha decidir sozinha. 0 = espera indefinidamente.")]
        [SerializeField, Min(0f)] private float choiceSeconds = 9f;

        [Tooltip("Quanto tempo cada fala fica no ecra depois de escrita.")]
        [SerializeField, Min(0.8f)] private float lineHold = 2.6f;

        public string Speaker => speaker;
        public float ChoiceSeconds => choiceSeconds;
        public float LineHold => lineHold;
        public int BeatCount => beats != null ? beats.Length : 0;

        public Beat BeatAt(int index)
        {
            if (beats == null || index < 0 || index >= beats.Length) return null;
            return beats[index];
        }

#if UNITY_EDITOR
        /// <summary>
        /// Usado pela migracao do formato antigo. So existe no editor: em runtime o
        /// conteudo e so de leitura.
        ///
        /// Publico e nao internal porque o codigo de editor vive noutra assembly
        /// (Assembly-CSharp-Editor) e internal nao a atravessa.
        /// </summary>
        public void EditorPopulate(string speakerName, Beat[] newBeats, float choiceTimeout, float hold)
        {
            speaker = speakerName;
            beats = newBeats;
            choiceSeconds = choiceTimeout;
            lineHold = hold;
        }
#endif
    }
}
