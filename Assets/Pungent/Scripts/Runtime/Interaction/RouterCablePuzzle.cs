using System;
using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>
    /// Voltar a ligar o router as 02:47, cabo a cabo.
    ///
    /// ---
    ///
    /// **Porque e que isto existe.** O router era um clique. O passo do capitulo
    /// dizia "reinicia o router", o jogador atravessava o corredor as escuras,
    /// carregava uma vez e estava feito — e o corredor, que e a parte que interessa,
    /// durava o tempo de o atravessar. A tarefa nao pedia nada ao jogador excepto
    /// deslocar-se.
    ///
    /// **Porque e que e tao pequeno.** Nao e um puzzle: e uma coisa chata que se faz
    /// a uma hora a que ninguem quer estar acordado, e e nisso que esta o trabalho.
    /// Quatro cabos, quatro portas, cores. Ninguem falha isto; o que se ganha sao os
    /// vinte segundos em que o Tomas esta de cocoras no corredor **de costas para a
    /// casa**, que e a condicao de que este jogo vive. Um puzzle a serio seria pior:
    /// a atencao ia para o puzzle e sairia da casa.
    ///
    /// **Nunca pode prender.** A quest das 02:47 espera pelo router, portanto uma
    /// combinacao impossivel seria o jogo acabado ali. Cores iguais de um lado e do
    /// outro: nao ha estado sem saida, e nao ha maneira de falhar excepto insistir.
    ///
    /// Desenhado em `OnGUI` como o resto da interface deste protótipo — ver o
    /// <see cref="PrototypeHUD"/>. Quando a interface do jogo for a serio, isto muda
    /// de pele sem mudar de regras.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RouterCablePuzzle : MonoBehaviour
    {
        [Serializable]
        private struct Cable
        {
            public string Name;
            public Color Tint;
        }

        [Tooltip("Os cabos. A ordem aqui e a ordem em que aparecem a esquerda; as "
               + "portas a direita sao baralhadas em cada abertura.")]
        [SerializeField]
        private Cable[] cables =
        {
            new Cable { Name = "Power",    Tint = new Color(0.86f, 0.24f, 0.22f) },
            new Cable { Name = "Line",     Tint = new Color(0.36f, 0.62f, 0.90f) },
            new Cable { Name = "LAN 1",    Tint = new Color(0.45f, 0.78f, 0.42f) },
            new Cable { Name = "LAN 2",    Tint = new Color(0.92f, 0.78f, 0.30f) },
        };

        [SerializeField] private string title = "02:47 — plug it back in";
        [SerializeField] private string hint = "Match each cable to its port.";

        [Tooltip("Quanto tempo a porta errada fica a piscar.")]
        [SerializeField, Min(0.1f)] private float errorSeconds = 0.45f;

        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip plugClip;
        [SerializeField] private AudioClip wrongClip;
        [SerializeField] private AudioClip doneClip;

        [SerializeField] private Pungent.Player.PlayerInputReader input;
        [SerializeField] private PlayerInteractor interactor;

        private int[] portOrder;        // portOrder[slot] = indice do cabo daquela porta
        private bool[] connected;       // por indice de cabo
        private int held = -1;          // cabo na mao, ou -1
        private int wrongSlot = -1;
        private float wrongUntil;
        private Action onSolved;

        private GUIStyle titleStyle, hintStyle, labelStyle;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (input == null) input = FindObjectOfType<Pungent.Player.PlayerInputReader>();
            if (interactor == null) interactor = FindObjectOfType<PlayerInteractor>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        /// <summary>
        /// Abre o painel. O <paramref name="finished"/> so e chamado se for mesmo
        /// resolvido — nao ha maneira de sair a meio, porque nao ha nada a decidir.
        /// </summary>
        public void Open(Action finished)
        {
            if (IsOpen || cables == null || cables.Length == 0) return;

            onSolved = finished;
            connected = new bool[cables.Length];
            portOrder = new int[cables.Length];
            for (int i = 0; i < portOrder.Length; i++) portOrder[i] = i;

            // Baralhadas de cada vez: a segunda noite em que isto acontece nao pode
            // ser resolvida de memoria muscular.
            for (int i = portOrder.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int swap = portOrder[i]; portOrder[i] = portOrder[j]; portOrder[j] = swap;
            }

            held = -1;
            wrongSlot = -1;
            IsOpen = true;

            input?.SetUiPointerActive(true);
            input?.SetMoveSuppressed(true);
            interactor?.SetInteractionBlocked(true);
        }

        private void Close(bool solved)
        {
            IsOpen = false;
            input?.SetUiPointerActive(false);
            input?.SetMoveSuppressed(false);
            interactor?.SetInteractionBlocked(false);

            if (!solved) return;
            Play(doneClip, 0.7f);

            // Guardado e limpo **antes** de chamar: quem responde a isto pode abrir
            // outro painel, e um callback ainda pendurado aqui dava dois donos.
            Action callback = onSolved;
            onSolved = null;
            callback?.Invoke();
        }

        private void Play(AudioClip clip, float volume)
        {
            if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            BuildStyles();

            // **Mais largo, mais alto, mais espaçado.**
            //
            // Reportado a jogar: os cabos liam-se mal e estavam colados uns aos
            // outros. As cores existiam — sao o unico sinal que este puzzle da — mas
            // numa mancha de 34 px com 12 px entre linhas nao se distinguia nada, e o
            // jogador acabava a carregar por tentativa.
            //
            // O puzzle **nao pode ficar dificil**: sao vinte segundos de costas para a
            // casa, e a atencao tem de poder voltar la para tras. Ler mal nao e
            // dificuldade, e atrito.
            float w = Mathf.Min(680f, Screen.width * 0.8f);
            float rowHeight = 68f;
            float h = 128f + cables.Length * rowHeight;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            GUI.color = new Color(0f, 0f, 0f, 0.86f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(panel.x + 24f, panel.y + 18f, panel.width - 48f, 26f), title, titleStyle);
            GUI.Label(new Rect(panel.x + 24f, panel.y + 46f, panel.width - 48f, 22f),
                held >= 0 ? $"Holding: {cables[held].Name}" : hint, hintStyle);

            float top = panel.y + 84f;
            float colWidth = (panel.width - 72f) * 0.5f;

            for (int i = 0; i < cables.Length; i++)
            {
                float y = top + i * rowHeight;

                // Esquerda: os cabos. Um cabo ja ligado deixa de responder.
                // 48 px de altura em 68 de linha: vinte de folga entre cabos, contra os
                // doze de antes. A mancha de cor passa a ler-se de relance.
                var cableRect = new Rect(panel.x + 24f, y, colWidth, 48f);
                DrawSwatch(cableRect, cables[i].Tint, connected[i] ? 0.25f : 1f);
                if (GUI.Button(cableRect, connected[i] ? $"{cables[i].Name}  —  in" : cables[i].Name, labelStyle)
                    && !connected[i])
                {
                    held = held == i ? -1 : i;
                }

                // Direita: as portas, na ordem baralhada.
                int wants = portOrder[i];
                var portRect = new Rect(panel.x + 48f + colWidth, y, colWidth, 48f);
                bool flashing = wrongSlot == i && Time.unscaledTime < wrongUntil;
                DrawSwatch(portRect, flashing ? Color.red : cables[wants].Tint,
                    connected[wants] ? 0.25f : 1f);

                if (GUI.Button(portRect, connected[wants] ? "connected" : "port", labelStyle))
                    TryPlug(i, wants);
            }
        }

        private void TryPlug(int slot, int wants)
        {
            if (held < 0 || connected[wants]) return;

            if (held == wants)
            {
                connected[wants] = true;
                held = -1;
                Play(plugClip, 0.6f);

                for (int i = 0; i < connected.Length; i++)
                    if (!connected[i]) return;

                Close(true);
                return;
            }

            wrongSlot = slot;
            wrongUntil = Time.unscaledTime + errorSeconds;
            held = -1;
            Play(wrongClip, 0.5f);
        }

        private static void DrawSwatch(Rect rect, Color tint, float alpha)
        {
            // **A cor era uma tira de 8 px.** Num painel escuro, a 34 px de altura,
            // quatro riscas finas de cores diferentes lem-se todas como cinzento.
            // Passa a 22 px, e o corpo da linha leva a mesma cor por baixo — fraca o
            // suficiente para o texto continuar legivel, forte o suficiente para se
            // saber qual e o cabo sem ler o nome.
            //
            // A cor e o unico sinal que este puzzle da. Se ela nao se ve, o puzzle nao
            // tem sinal nenhum e joga-se por tentativa.
            var bar = new Rect(rect.x, rect.y, 22f, rect.height);
            GUI.color = new Color(tint.r, tint.g, tint.b, alpha);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);

            GUI.color = new Color(tint.r, tint.g, tint.b, 0.22f * alpha);
            GUI.DrawTexture(new Rect(rect.x + 22f, rect.y, rect.width - 22f, rect.height),
                Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private void BuildStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.88f, 0.9f, 0.86f) }
            };
            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.72f, 0.74f, 0.70f, 0.85f) }
            };
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(20, 8, 0, 0),
                normal = { textColor = new Color(0.88f, 0.9f, 0.86f) },
                hover = { textColor = Color.white }
            };
        }

        /// <summary>
        /// A cena pode ser descarregada com o painel aberto — trocar de dia, por
        /// exemplo. Sem isto o jogador ficava sem se mexer e com o rato solto numa
        /// cena onde ja nao ha painel nenhum.
        /// </summary>
        private void OnDisable()
        {
            if (!IsOpen) return;
            IsOpen = false;
            onSolved = null;
            input?.SetUiPointerActive(false);
            input?.SetMoveSuppressed(false);
            interactor?.SetInteractionBlocked(false);
        }
    }
}
