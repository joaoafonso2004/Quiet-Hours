using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O fim do jogo. Ate aqui nao havia nenhum.
    ///
    /// ---
    ///
    /// **O que estava a acontecer.** O `game_end` era levantado pelo ultimo passo do
    /// epilogo e **ninguem o ouvia** — o unico sitio do projecto que lhe tocava era
    /// o menu de pausa, e so para se impedir de abrir. Depois da ultima linha, o
    /// capitulo fechava, o objectivo desaparecia do canto, e o jogador ficava de pe
    /// no apartamento a poder andar e a abrir gavetas, com a historia acabada. Nao
    /// era um bug visivel: era o jogo a nao ter fim nenhum.
    ///
    /// ---
    ///
    /// **Fica no preto, e nao volta.** O texto de fecho usa a mesma tela do
    /// <see cref="IntroTextSequence"/> com o `holdOnBlack` ligado. A abertura
    /// desvanece para o jogo comecar; se o fecho fizesse o mesmo devolvia o jogador
    /// ao apartamento depois da ultima linha, que e o problema de onde se partiu.
    ///
    /// ---
    ///
    /// **Porque e que sai do jogo em vez de voltar a um menu.** Porque nao ha menu:
    /// o build tem tres cenas e sao as tres de jogo. Sair e a unica saida honesta
    /// que existe hoje — e e a mesma que o `PauseMenu` ja usa, portanto quando
    /// houver ecra de titulo muda-se num sitio so.
    ///
    /// **Primeira versao.** O texto esta no componente e nao num asset de dialogo,
    /// como o resto do jogo faz; quando os quatro finais tiverem cada um o seu
    /// fecho, isto passa a escolher pelo `ending_*` que o
    /// <see cref="EndingSelector"/> levantou.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameEnding : MonoBehaviour, IRebindable
    {
        [SerializeField] private ChapterDirector director;

        [Tooltip("O acontecimento que fecha o jogo. E o que o ultimo passo de cada "
               + "epilogo levanta.")]
        [SerializeField] private string endsOn = "game_end";

        [Tooltip("A tela de texto do fecho. Poe-lhe o `holdOnBlack` ligado e o "
               + "`playOnStart` desligado.\n\n**Por preencher de propósito.** Os "
               + "quatro epilogos ja dizem o que ha para dizer, e o que falta "
               + "escrever e o cartao que fecha o enquadramento que a abertura "
               + "abriu — texto de autor, nao meu. Sem ele isto ainda funciona: "
               + "fecha a preto e sai.")]
        [SerializeField] private IntroTextSequence closingText;

        [Tooltip("Usado quando nao ha tela de texto: o ecra tem de fechar antes de "
               + "o jogo sair, senao a ultima coisa que o jogador ve e o corredor "
               + "do apartamento a desaparecer de repente.")]
        [SerializeField] private ScreenFade fade;

        [Tooltip("Quanto tempo o ecra demora a fechar quando nao ha texto.")]
        [SerializeField, Min(0.2f)] private float fadeSeconds = 2.5f;

        [Tooltip("Quanto tempo fica no preto depois da ultima linha sair, antes de "
               + "sair do jogo. Nao encurtar: e o silencio que faz o fim ser um fim "
               + "e nao um corte.")]
        [SerializeField, Min(0f)] private float silenceAfterSeconds = 3.5f;

        [Tooltip("Sair do jogo no fim. Desligado, fica no preto para sempre — util "
               + "para ver o fecho sem o editor parar a meio.")]
        [SerializeField] private bool quitAtEnd = true;

        private bool started;
        private bool textFinished;
        private float silenceTimer;

        private void Awake() => Rebind();

        public void Rebind()
        {
            if (director == null) director = FindObjectOfType<ChapterDirector>();
        }

        private void Update()
        {
            director = ChapterDirector.Resolve(director);

            if (!started)
            {
                if (director == null || string.IsNullOrWhiteSpace(endsOn)) return;
                if (!director.HasSeen(endsOn)) return;

                started = true;
                if (closingText != null)
                {
                    closingText.Play();
                }
                else
                {
                    // Sem texto de fecho, fecha-se a preto na mesma. O `Blink` serve
                    // com um `hold` longo: o que interessa e a primeira metade, e o
                    // jogo sai antes de ele chegar a reabrir.
                    if (fade != null) fade.Blink(fadeSeconds, 999f, 0.1f, null);
                    textFinished = true;

                    // O silencio conta-se **depois** de o ecra estar fechado, e nao
                    // a partir do momento em que comeca a fechar.
                    silenceTimer = -fadeSeconds;
                }
                return;
            }

            if (!textFinished)
            {
                if (closingText != null && closingText.IsRunning) return;
                textFinished = true;
                silenceTimer = 0f;
                return;
            }

            silenceTimer += Time.unscaledDeltaTime;
            if (silenceTimer < silenceAfterSeconds) return;

            enabled = false;
            if (quitAtEnd) Quit();
        }

        /// <summary>A mesma saida que o menu de pausa usa.</summary>
        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(ChapterDirector chapters, IntroTextSequence text, string endEvent)
        {
            director = chapters;
            closingText = text;
            endsOn = endEvent;
        }
#endif
    }
}
