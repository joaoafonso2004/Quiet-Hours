using System.Collections.Generic;
using Pungent.Dialogue;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Pensamentos falados do protagonista (secção 13.5 do plano mestre).
    ///
    /// Existe para que o jogo consiga dizer coisas ao jogador sem inventar
    /// objetivos artificiais: a fome leva-o à cozinha, o desconforto comenta o
    /// Rui. Tem prioridade, cooldown e `playOnce` porque o risco destes sistemas
    /// é falarem demais e estragarem o silêncio.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerThoughtDirector : MonoBehaviour
    {
        [SerializeField] private WorldDialogueController dialogue;

        [Tooltip("Silêncio mínimo depois de uma fala de história acabar.")]
        [SerializeField, Min(0f)] private float storyCooldown = 5f;

        [Tooltip("Silêncio mínimo antes de uma banalidade poder sair.\n\n"
               + "Grande de propósito. Ver a nota em CooldownFor.")]
        [SerializeField, Min(0f)] private float ambientCooldown = 50f;

        [Tooltip("A partir desta prioridade um pensamento conta como história e usa "
               + "o arrefecimento curto. Abaixo dela é banalidade.")]
        [SerializeField, Range(0, 9)] private int storyPriority = 2;

        [SerializeField, Min(0.8f)] private float defaultHold = 3.0f;

        private readonly Dictionary<string, float> spokenAt = new Dictionary<string, float>();
        private readonly List<Pending> queue = new List<Pending>();
        private float quietFrom;

        /// <summary>
        /// Quanto silêncio este pensamento exige antes de poder sair.
        ///
        /// Era um número só, de seis segundos, e o resultado media-se a jogar: a casa
        /// tem dezenas de `ProximityThought` e cada um traz o seu próprio
        /// arrefecimento, mas nenhum deles sabe dos outros. Atravessar o apartamento
        /// acordava vinte gatilhos diferentes e o Tomás falava de seis em seis
        /// segundos, sem parar, por cima do que estivesse a acontecer.
        ///
        /// **O erro não era a frequência, era haver um número só.** Uma banalidade
        /// sobre uma torneira e a linha que fecha um capítulo não podem estar sujeitas
        /// à mesma regra. Agora a pergunta é outra: uma fala de história sai quase já,
        /// porque é a razão de o sistema existir; uma banalidade só sai se houver
        /// mesmo silêncio há bastante tempo.
        ///
        /// E se não houver, **é descartada e não adiada** — a paciência da fila trata
        /// disso. Uma banalidade guardada para sair daqui a um minuto chega fora do
        /// sítio que a justificava. É isto que as torna raras sem ninguém ter de ir
        /// apagar linhas: elas continuam todas escritas, e só se ouvem quando a casa
        /// esteve calada tempo suficiente para uma delas valer a pena.
        /// </summary>
        private float CooldownFor(int priority)
            => priority >= storyPriority ? storyCooldown : ambientCooldown;

        private struct Pending
        {
            public string Id;
            public string Text;
            public int Priority;
            public float Hold;
            public bool Once;
            public float ExpiresAt;
        }

        private void Awake()
        {
            if (dialogue == null) dialogue = GetComponent<WorldDialogueController>();
        }

        /// <summary>
        /// Pede um pensamento. Se houver algo a falar, entra em fila e o de maior
        /// prioridade ganha; os que esperarem demasiado são descartados em vez de
        /// aparecerem fora de contexto.
        /// </summary>
        public void Think(string id, string text, int priority = 0, bool once = true,
                          float hold = 0f, float patience = 8f)
        {
            if (once && spokenAt.ContainsKey(id)) return;

            for (int i = 0; i < queue.Count; i++)
                if (queue[i].Id == id) return;   // já está em fila

            queue.Add(new Pending
            {
                Id = id,
                Text = text,
                Priority = priority,
                Hold = hold > 0f ? hold : defaultHold,
                Once = once,
                ExpiresAt = Time.time + patience
            });
        }

        /// <summary>Limpa a fila, para o silêncio quando algo mais importante acontece.</summary>
        public void Silence()
        {
            queue.Clear();
            quietFrom = Time.time;
        }

        private void Update()
        {
            if (queue.Count == 0) return;

            for (int i = queue.Count - 1; i >= 0; i--)
                if (Time.time > queue[i].ExpiresAt)
                    queue.RemoveAt(i);

            if (queue.Count == 0) return;
            if (dialogue == null || dialogue.IsBusy) return;

            // O melhor de entre os que **podem sair agora**, e não o melhor da fila.
            // Escolher primeiro e testar o arrefecimento depois calava a fila inteira
            // sempre que a banalidade mais prioritária estivesse em arrefecimento —
            // uma fala de história ficava atrás dela à espera da vez.
            int best = -1;
            for (int i = 0; i < queue.Count; i++)
            {
                if (Time.time - quietFrom < CooldownFor(queue[i].Priority)) continue;
                if (best < 0 || queue[i].Priority > queue[best].Priority) best = i;
            }
            if (best < 0) return;

            Pending chosen = queue[best];
            queue.RemoveAt(best);

            if (!dialogue.ShowThought(chosen.Text, chosen.Hold)) return;

            if (chosen.Once) spokenAt[chosen.Id] = Time.time;

            // O silêncio conta a partir do fim da fala e não do princípio: com um
            // `hold` de três segundos e um arrefecimento de cinco, dois pensamentos
            // seguidos ficavam a dois segundos de distância um do outro.
            quietFrom = Time.time + chosen.Hold;
        }
    }
}
