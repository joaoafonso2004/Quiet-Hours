using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Conteudo que so existe a partir de certo capitulo — e que ate la nao esta la.
    ///
    /// ---
    ///
    /// **Porque e que isto teve de existir.** A caixa de pecas do Dia 4 foi montada
    /// ao pe da porta de entrada e ficou activa desde o arranque da cena. O prologo
    /// tem um passo que diz *"Bring your boxes in"*: o jogador encontrava **uma caixa
    /// a mais**, podia carrega-la para a bancada, e com ela levantava o
    /// `parts_carried` — que destranca a lanterna com fita azul, a prova que faz o
    /// pivo da historia. **No primeiro dia de jogo.**
    ///
    /// Nada disto dava erro. O `FlavourInteractable` e o `ChapterEventRaiser` da
    /// lanterna ja tinham `requiresEvent`, e por isso os *prompts* estavam bem
    /// guardados — o que nao estava guardado era o objecto existir.
    ///
    /// **Uma condicao no prompt nao e uma condicao na coisa.**
    ///
    /// ---
    ///
    /// **O padrao ja existia, feito a mao tres vezes.** O <see cref="ClimaxStage"/>
    /// acorda o `CLIMAX_CONTENT`, o <see cref="DayOneStage"/> o dele, o
    /// <see cref="DayTwoDirector"/> o dele. Cada um com o seu campo `content` e a sua
    /// chamada a `SetActive`. Isto e a mesma ideia sem dono proprio, para conteudo de
    /// capitulo que nao precisa de um director inteiro so para si.
    ///
    /// **Nao vai substituir os tres.** Trocar directores que ja funcionam por isto e
    /// refactorizacao ao lado da tarefa, e este projecto ja paga caro por mudar duas
    /// coisas ao mesmo tempo. Fica como a ferramenta certa para o proximo.
    ///
    /// ---
    ///
    /// **Desliga no Awake e nao no Start.** Um objecto que chegue a ver-se um frame
    /// e um objecto que se viu: a caixa aparecia e desaparecia a frente do jogador na
    /// abertura, o que e pior do que ela ficar la.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChapterContent : MonoBehaviour, IRebindable
    {
        [Tooltip("A partir deste acontecimento, o conteudo passa a existir.\n\n"
               + "Vazio = existe sempre, e este componente nao faz nada.")]
        [SerializeField] private string showsOnEvent;

        [Tooltip("A partir deste, deixa de existir outra vez.\n\n"
               + "Vazio = fica ate ao fim.")]
        [SerializeField] private string hidesOnEvent;

        [Tooltip("O que se liga e desliga. Vazio = este objecto.\n\n"
               + "Se for este proprio, o componente sobrevive na mesma: quem desliga "
               + "e um filho, e nao o objecto que corre o Update.")]
        [SerializeField] private Transform content;

        [SerializeField] private ChapterDirector director;

        private bool shown;

        private void Awake()
        {
            Rebind();

            // Fechado a partida. Se nao houver condicao nenhuma, fica como estava.
            if (!string.IsNullOrWhiteSpace(showsOnEvent)) Apply(false);
        }

        public void Rebind()
        {
            if (director == null) director = FindObjectOfType<ChapterDirector>();
        }

        private void Update()
        {
            director = ChapterDirector.Resolve(director);
            if (director == null) return;
            if (string.IsNullOrWhiteSpace(showsOnEvent)) return;

            if (!shown)
            {
                if (!director.HasSeen(showsOnEvent)) return;
                shown = true;
                Apply(true);
                return;
            }

            if (string.IsNullOrWhiteSpace(hidesOnEvent)) { enabled = false; return; }
            if (!director.HasSeen(hidesOnEvent)) return;

            Apply(false);
            enabled = false;
        }

        /// <summary>
        /// Liga ou desliga os filhos, e nao o proprio objecto.
        ///
        /// Desligar o objecto que corre este `Update` parava o componente, e o
        /// conteudo nunca mais voltava a aparecer — o componente desligava-se a si
        /// proprio e ficava a espera de um frame que nunca chegava.
        /// </summary>
        private void Apply(bool visible)
        {
            var target = content != null ? content : transform;

            if (target != transform) { target.gameObject.SetActive(visible); return; }

            for (int i = 0; i < transform.childCount; i++)
                transform.GetChild(i).gameObject.SetActive(visible);
        }

#if UNITY_EDITOR
        /// <summary>Usado pelas ferramentas de ligacao, que vivem noutra assembly.</summary>
        public void EditorConfigure(string shows, string hides = "", Transform target = null)
        {
            showsOnEvent = shows;
            hidesOnEvent = hides;
            content = target;
        }
#endif
    }
}
