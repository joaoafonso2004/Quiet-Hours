using System.Collections.Generic;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// As pequenas coisas que se fazem em casa e que ninguem manda fazer.
    ///
    /// A cadeia de objectivos da abertura (`OpeningQuestDirector`) e uma linha
    /// recta: liga o router, fala com o Rui, volta ao quarto. Isso conduz a
    /// historia mas nao faz do apartamento uma casa. Um sitio onde se mora tem
    /// coisas que apetecem: ir a varanda, fumar, beber agua, ver televisao. Nenhuma
    /// delas e obrigatoria, e e precisamente por isso que dao vida ao espaco —
    /// quando o jogador as faz, faz porque quis.
    ///
    /// Este director so guarda o estado. Quem as oferece sao as `HomeTask` pelo
    /// apartamento fora; o que se ganha e o proprio momento, mais um pensamento.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeTaskDirector : MonoBehaviour
    {
        [SerializeField] private PrototypeHudBridge hudBridge;
        [Tooltip("Aviso curto quando se completa uma tarefa opcional. Vazio = silencio.")]
        [SerializeField] private bool announceCompletion = true;

        private readonly HashSet<string> completed = new HashSet<string>();
        private readonly HashSet<string> known = new HashSet<string>();

        public int CompletedCount => completed.Count;
        public int KnownCount => known.Count;

        public bool IsCompleted(string id) => !string.IsNullOrEmpty(id) && completed.Contains(id);

        /// <summary>Uma tarefa deu por si ao jogador (entrou na varanda, viu o macao).</summary>
        public void Discover(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            known.Add(id);
        }

        public void Complete(string id, string label)
        {
            if (string.IsNullOrEmpty(id) || !completed.Add(id)) return;
            known.Add(id);

            if (announceCompletion && hudBridge != null && !string.IsNullOrWhiteSpace(label))
                hudBridge.Announce(label);
        }
    }

}
