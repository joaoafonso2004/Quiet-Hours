using System;
using UnityEngine;

namespace Pungent.Dialogue
{
    /// <summary>
    /// Um conjunto de falas soltas sem ramificacao: pensamentos de um objeto do
    /// mundo, beats de uma tarefa domestica, frases de encher de um NPC.
    ///
    /// Nao usa <see cref="DialogueSequenceDefinition"/> de proposito. Uma conversa
    /// tem ordem, escolhas e consequencias; isto e uma lista. Forcar as duas coisas
    /// no mesmo asset daria um inspector cheio de campos vazios em todos os 26
    /// objetos de cenario que so dizem uma linha.
    /// </summary>
    [CreateAssetMenu(menuName = "Pungent/Dialogue/Thought Lines", fileName = "THT_NewLines")]
    public sealed class ThoughtLineSet : ScriptableObject
    {
        public enum Order
        {
            /// <summary>Por ordem; a ultima fica a repetir-se. Insistir num objeto nao gera texto novo para sempre.</summary>
            SequentialLastRepeats,

            /// <summary>Ao acaso, mas nunca a mesma duas vezes seguidas.</summary>
            RandomNoImmediateRepeat
        }

        [SerializeField, TextArea] private string[] lines = Array.Empty<string>();
        [SerializeField] private Order order = Order.SequentialLastRepeats;

        [Tooltip("Quanto tempo a legenda fica no ecra depois de escrita.")]
        [SerializeField, Min(0.8f)] private float holdSeconds = 2.6f;

        public int Count => lines != null ? lines.Length : 0;
        public float HoldSeconds => holdSeconds;

        /// <summary>Valor inicial do cursor que <see cref="Pick"/> espera.</summary>
        public int NewCursor => order == Order.RandomNoImmediateRepeat ? -1 : 0;

        /// <summary>Fala numa posicao fixa, para quem percorre a lista ele proprio.</summary>
        public string LineAt(int index)
        {
            if (Count == 0) return null;
            return lines[Mathf.Clamp(index, 0, lines.Length - 1)];
        }

        /// <summary>
        /// A fala seguinte segundo a <see cref="Order"/> deste conjunto. O cursor
        /// pertence a quem chama, para que dois objetos que partilhem o mesmo asset
        /// avancem cada um ao seu ritmo.
        /// </summary>
        public string Pick(ref int cursor)
        {
            if (Count == 0) return null;

            if (order == Order.RandomNoImmediateRepeat)
            {
                int pick = UnityEngine.Random.Range(0, lines.Length);
                if (lines.Length > 1 && pick == cursor)
                    pick = (pick + 1) % lines.Length;
                cursor = pick;
                return lines[pick];
            }

            int index = Mathf.Clamp(cursor, 0, lines.Length - 1);
            if (cursor < lines.Length - 1) cursor++;
            return lines[index];
        }

#if UNITY_EDITOR
        /// <summary>
        /// Usado pela migracao do formato antigo. So existe no editor.
        /// Publico porque o codigo de editor vive noutra assembly.
        /// </summary>
        public void EditorPopulate(string[] newLines, Order newOrder, float hold)
        {
            lines = newLines;
            order = newOrder;
            holdSeconds = hold;
        }
#endif
    }
}
