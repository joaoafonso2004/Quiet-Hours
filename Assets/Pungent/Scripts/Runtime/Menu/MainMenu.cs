using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace Pungent.Menu
{
    /// <summary>
    /// O menu inicial.
    ///
    /// ---
    ///
    /// **Em OnGUI como o resto.** O HUD, o cartao de capitulo, as palpebras e o menu
    /// de pausa vivem todos aqui; abrir um Canvas so para isto punha o jogo com dois
    /// sistemas de interface e dois sitios onde mudar um tipo de letra. Quando o HUD
    /// sair do OnGUI, isto sai com ele.
    ///
    /// ---
    ///
    /// **Sem "Continuar".** Nao ha sistema de gravacao. Um menu que oferece continuar
    /// e nao continua nada e a primeira mentira que o jogo conta ao jogador, e conta-a
    /// antes de comecar.
    ///
    /// ---
    ///
    /// **A casa comeca a ouvir-se.**
    ///
    /// O menu abre em silencio absoluto. Ao fim de <see cref="quietSeconds"/> parado,
    /// o ambiente do apartamento entra por baixo, muito baixo, e depois uma tabua
    /// estala uma vez. Quem carrega logo em *New* nunca ouve nada; quem fica a olhar
    /// para a imagem ouve a casa a comecar sem ele.
    ///
    /// Nao ha musica em lado nenhum deste jogo e nao ha aqui. O que o menu tem para
    /// oferecer e o mesmo que o resto: silencio, e alguma coisa a acontecer dentro
    /// dele.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenu : MonoBehaviour
    {
        [Header("Identidade")]
        [Tooltip("O fundo em movimento. Ganha a imagem parada quando esta preenchido.\n\n"
               + "Um ciclo com respiracao — a imagem a mexer-se de leve — diz uma coisa "
               + "que uma imagem parada nao consegue dizer: que aquilo esta a acontecer "
               + "agora, e nao e uma capa.")]
        [SerializeField] private VideoClip backgroundVideo;

        [Tooltip("A imagem de fundo, usada quando nao ha video. Vazio = preto, e o "
               + "titulo e escrito por cima.")]
        [SerializeField] private Texture2D background;

        [Tooltip("Ligar quando a imagem de fundo **ja tem o titulo desenhado**. Com "
               + "isto ligado o menu nao escreve texto nenhum por cima — dois titulos "
               + "no mesmo ecra, um deles em Arial, e o que faz um menu parecer "
               + "temporario.")]
        [SerializeField] private bool artHasTitle = true;

        [Tooltip("Escrito so quando nao ha arte nenhuma — nem video nem imagem. Ver "
               + "`artHasTitle`.\n\nUma linha e nao duas: houve aqui um `3B` por cima, "
               + "o numero do apartamento. Estava a imitar o cartao de capitulo, cuja "
               + "linha pequena e **sempre uma data** — portanto imitava-o mal — e so "
               + "queria dizer alguma coisa a quem ja tivesse jogado. Num menu, isso e "
               + "decoracao a preencher um espaco que existia por acaso.")]
        [SerializeField] private string title = "QUIET HOURS";

        [Header("Respiracao do fundo")]
        [Tooltip("Quantos pixeis o fundo anda para cada lado. Poucos: a partir de uns "
               + "dez le-se como o ecra a abanar, e o que se quer e nao se dar conta "
               + "de que anda.")]
        [SerializeField, Range(0f, 24f)] private float driftPixels = 6f;

        [Tooltip("Segundos de um ciclo completo. Lento — uma respiracao, nao um "
               + "pendulo.")]
        [SerializeField, Min(1f)] private float driftSeconds = 11f;

        [Tooltip("Quanto o fundo e ampliado para nao mostrar borda preta enquanto "
               + "anda.\n\nTem de ser maior do que o deslocamento: a 1.02 num ecra de "
               + "1920 sobram 19 pixeis de folga de cada lado, o dobro do que os seis "
               + "de deriva gastam.")]
        [SerializeField, Range(1f, 1.15f)] private float overscale = 1.02f;

        [Tooltip("Encher o ecra e cortar o que sobra, ou caber inteira e sobrar preto?\n\n"
               + "**Cabe inteira, por omissao, e nao e timidez.** `ScaleAndCrop` num "
               + "ecra que nao seja 16:9 corta pelos lados: num 16:10 sao 5% de cada "
               + "lado, e o titulo da arte comeca aos 5,4% da largura. A diferenca "
               + "entre ver `QUIET HOURS` e ver `UIET HOURS` e a proporcao do monitor "
               + "de quem abrir o jogo.\n\n"
               + "E nao se paga nada por isso **nesta arte**: as quatro bordas "
               + "desvanecem para preto, e o menu ja tem preto por baixo. A barra que "
               + "sobra e da mesma cor do que lhe fica ao lado, portanto nao ha barra "
               + "nenhuma para ver.\n\n"
               + "Passa para `ScaleAndCrop` se um dia entrar uma imagem com detalhe "
               + "ate a borda — ai o corte custa menos do que a moldura.")]
        [SerializeField] private ScaleMode fitMode = ScaleMode.ScaleToFit;

        [Header("Espaco dos botoes")]
        [Tooltip("O escurecimento no centro, por baixo dos botoes. Sem ele, o texto "
               + "cai por cima do que a arte tiver no meio e deixa de se ler quando o "
               + "fundo mexe.")]
        [SerializeField, Range(0f, 1f)] private float scrimStrength = 0.34f;
        [SerializeField, Range(0.1f, 1f)] private float scrimWidth = 0.34f;
        [SerializeField, Range(0.1f, 1f)] private float scrimHeight = 0.42f;

        [Tooltip("Altura do centro da coluna de botoes, de cima para baixo.\n\n"
               + "Um botao para os equilibrar contra a arte sem mexer em codigo. A "
               + "meio funciona quando a imagem tem o meio vazio; se o titulo da "
               + "arte estiver a mesma altura e passarem a competir, desce-se isto "
               + "para uns 0,68 e a coluna cai por baixo da linha de base dele.\n\n"
               + "So mexe nos botoes e no escurecimento, que anda com eles. O "
               + "fundo nao le isto.")]
        [SerializeField, Range(0.2f, 0.9f)] private float itemsCentreY = 0.52f;

        [Tooltip("Onde a coluna vive na largura do ecra.\n\n"
               + "**Ao lado da arte e nao por cima dela.** Uma imagem de menu "
               + "desenhada com o titulo a um lado deixou o outro lado vazio de "
               + "proposito, e e la que a interface pertence — ao centro, ela "
               + "assenta no meio da composicao e passa a ser uma coisa colada "
               + "por cima.\n\n"
               + "Nesta arte o titulo ocupa ate aos 35% e o Rui comeca aos 78%. "
               + "0,66 poe a coluna entre os dois, no vao escuro do corredor, sem "
               + "tocar em nenhum.")]
        [SerializeField, Range(0f, 1f)] private float itemsCentreX = 0.66f;

        [Tooltip("Texto encostado a esquerda da coluna, em vez de centrado nela.\n\n"
               + "E o que separa um menu de jogo de uma caixa de dialogo. Centrado, "
               + "cada linha comeca num sitio diferente conforme o comprimento da "
               + "palavra e a coluna fica com a forma de um losango; encostado, as "
               + "iniciais alinham e leem-se como uma lista de cima para baixo — "
               + "que e o que e.")]
        [SerializeField] private bool itemsAlignLeft = true;

        [Tooltip("Tamanho das opcoes. Grande: sao cinco palavras num ecra inteiro, "
               + "e nao ha nada a competir com elas.")]
        [SerializeField, Range(12, 48)] private int itemsFontSize = 26;

        [Tooltip("Distancia entre linhas. Folgada de proposito — um menu apertado "
               + "le-se como uma lista de definicoes.")]
        [SerializeField, Range(24f, 80f)] private float itemsRowHeight = 52f;

        [Tooltip("Escrito a canto inferior esquerdo, pequeno e apagado.\n\n"
               + "Nao e decoracao: e a primeira coisa que alguem cita quando "
               + "descreve um erro, e sem ela a resposta e sempre \"qual versao?\". "
               + "Vazio nao desenha nada.")]
        [SerializeField] private string versionLine = "v0.1";

        [Header("Sem arte: a luz por baixo da porta")]
        [Tooltip("Desenhada so quando nao ha video nem imagem.\n\n"
               + "E o fundo por omissao, e nao um remendo a espera de arte. Um "
               + "menu preto com o titulo ao meio le-se como uma cena por acabar; "
               + "isto le-se como estar sentado no escuro de um quarto a olhar "
               + "para a porta, que e o jogo inteiro numa imagem e nao custa "
               + "ficheiro nenhum. Se um dia entrar arte, desliga-se sozinha.")]
        [SerializeField] private bool doorLightWhenBare = true;

        [SerializeField] private Color doorLightColour = new Color(0.99f, 0.87f, 0.66f);
        [SerializeField, Range(0f, 1f)] private float doorLightStrength = 0.55f;

        [Tooltip("Largura da fresta, em fraccao do ecra. Uma porta e mais estreita "
               + "do que um ecra: a chegar as bordas deixa de ser uma porta e passa "
               + "a ser uma barra de interface.")]
        [SerializeField, Range(0.1f, 1f)] private float doorLightWidth = 0.44f;

        [SerializeField, Range(0.01f, 0.3f)] private float doorLightHeight = 0.07f;

        [Tooltip("Onde acaba a luz, de cima para baixo. Abaixo de 1 fica uma nesga "
               + "escura por baixo — chao na sombra. Encostada ao fundo do ecra "
               + "parecia um elemento de interface e nao uma coisa da casa.")]
        [SerializeField, Range(0.5f, 1f)] private float doorLightBottom = 0.94f;

        [Tooltip("Um ciclo da respiracao da luz. Nada no corredor esta a piscar — "
               + "e a lampada a nao estar completamente parada.")]
        [SerializeField, Min(1f)] private float breathSeconds = 7f;

        [Tooltip("Intervalo, minimo e maximo, entre passagens. Alguem atravessa o "
               + "corredor e a luz apaga-se por baixo durante um instante.\n\n"
               + "Raro de proposito. A primeira vez le-se como um defeito do ecra; "
               + "e so a segunda que se percebe o que foi, e ja se esta a esperar "
               + "pela terceira. Frequente, virava um efeito.")]
        [SerializeField] private Vector2 passEvery = new Vector2(38f, 85f);

        [SerializeField, Min(0.2f)] private float passSeconds = 1.5f;

        [Header("Para onde vai")]
        [SerializeField] private string firstScene = "Apartment_Blockout_V2";

        [Tooltip("Corte a preto antes de entrar. Sem ele o menu desaparece e o "
               + "apartamento aparece no mesmo frame, e isso le-se como um erro.")]
        [SerializeField, Min(0f)] private float fadeOutSeconds = 1.6f;

        [Header("A casa a comecar")]
        [Tooltip("Segundos de silencio antes de o apartamento se comecar a ouvir.")]
        [SerializeField, Min(0f)] private float quietSeconds = 40f;

        [SerializeField] private AudioSource ambience;
        [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.14f;
        [SerializeField, Min(1f)] private float ambienceFadeSeconds = 22f;

        [Tooltip("A tabua. Toca uma vez, depois de o ambiente ja estar la.")]
        [SerializeField] private AudioSource creakSource;
        [SerializeField] private AudioClip creakClip;
        [SerializeField, Min(0f)] private float creakAfterQuiet = 26f;

        private GUIStyle titleStyle, itemStyle;
        private Texture2D black;

        private int hovered = -1;
        private float openedAt;
        private bool creaked;
        private bool leaving;
        private float fade;

        // Em maiusculas para acompanhar o titulo da arte.
        //
        // E a unica maneira barata de os botoes pertencerem a imagem. `New game`
        // em Arial ao lado de um `QUIET HOURS` gasto e pintado le-se como interface
        // de teste encostada a uma capa; a mesma palavra em maiusculas le-se como
        // tendo sido decidida. Nao ha ficheiro de tipo de letra nisto — so a caixa.
        private static readonly string[] Items = { "NEW GAME", "SETTINGS", "QUIT" };
        private bool inSettings;

        private VideoPlayer player;
        private RenderTexture videoTarget;

        private Texture2D doorGlow;
        private float nextPassAt = -1f;
        private float passStartedAt = -1f;
        private float passLeftToRight = 1f;

        /// <summary>Nao ha video nem imagem: o menu tem de se desenhar a si proprio.</summary>
        private bool Bare => backgroundVideo == null && background == null;

        /// <summary>
        /// Monta o ciclo de video, se houver um.
        ///
        /// ---
        ///
        /// **Sem som.** O `audioOutputMode` fica em `None` de propósito: o ficheiro
        /// pode trazer uma faixa e este jogo nao tem musica em lado nenhum. O que se
        /// ouve no menu e a casa a comecar — ver `quietSeconds` — e uma banda sonora
        /// por baixo disso desfazia o unico efeito que este ecra tem.
        ///
        /// **Numa RenderTexture do tamanho do proprio video**, e nao do ecra: assim
        /// nao ha reamostragem antes do `ScaleAndCrop`, e mudar de resolucao em jogo
        /// nao obriga a recriar nada.
        ///
        /// **`waitForFirstFrame`** para o menu nao abrir num frame preto ou, pior, num
        /// frame de lixo do buffer. Ate estar pronto desenha-se a imagem parada, que e
        /// para isso que ela continua a existir ao lado do video.
        /// </summary>
        private void BuildVideo()
        {
            if (backgroundVideo == null) return;

            int width = Mathf.Max(16, (int)backgroundVideo.width);
            int height = Mathf.Max(16, (int)backgroundVideo.height);
            videoTarget = new RenderTexture(width, height, 0) { name = "MenuLoop" };

            player = gameObject.AddComponent<VideoPlayer>();
            player.clip = backgroundVideo;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = videoTarget;
            player.isLooping = true;
            player.playOnAwake = true;
            player.waitForFirstFrame = true;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.skipOnDrop = true;
            player.Play();
        }

        private void Awake()
        {
            openedAt = Time.unscaledTime;
            BuildVideo();

            if (ambience != null)
            {
                ambience.loop = true;
                ambience.volume = 0f;
                ambience.spatialBlend = 0f;
                ambience.Play();
            }

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void Update()
        {
            float quiet = Time.unscaledTime - openedAt - quietSeconds;

            // A casa entra por baixo, devagar. Vinte e dois segundos a subir: quem
            // esta a olhar para a imagem nao da pelo momento em que comecou.
            if (ambience != null && quiet > 0f)
                ambience.volume = Mathf.Min(ambienceVolume,
                    ambienceVolume * (quiet / ambienceFadeSeconds));

            // A tabua chega depois de o ambiente ja estar la — nunca com ele. Um som
            // seco sobre silencio absoluto e um susto; sobre uma casa que ja se ouve e
            // uma casa a mexer-se.
            if (!creaked && Time.unscaledTime - openedAt > quietSeconds + creakAfterQuiet)
            {
                creaked = true;
                if (creakSource != null && creakClip != null)
                    creakSource.PlayOneShot(creakClip, 0.5f);
            }

            // Quem passa no corredor. So conta quando o ecra e mesmo o da luz —
            // com arte por baixo nao ha fresta nenhuma para atravessar.
            if (doorLightWhenBare && Bare) TickCorridorPass();

            if (leaving) fade = Mathf.MoveTowards(fade, 1f,
                Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeOutSeconds));
        }

        /// <summary>
        /// Agenda e corre as passagens no corredor.
        ///
        /// A primeira nunca cai no arranque: o intervalo comeca a contar quando o
        /// menu abre, e o minimo sao trinta e oito segundos. Quem carrega em *New*
        /// nao ve nada — como o ambiente e a tabua, isto e so para quem fica.
        /// </summary>
        private void TickCorridorPass()
        {
            float now = Time.unscaledTime;

            if (nextPassAt < 0f)
            {
                nextPassAt = now + Random.Range(passEvery.x, passEvery.y);
                return;
            }

            if (passStartedAt < 0f)
            {
                if (now < nextPassAt) return;
                passStartedAt = now;
                passLeftToRight = Random.value < 0.5f ? -1f : 1f;
                return;
            }

            if (now - passStartedAt < passSeconds) return;

            passStartedAt = -1f;
            nextPassAt = now + Random.Range(passEvery.x, passEvery.y);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            titleStyle = new GUIStyle
            {
                font = font,
                fontSize = 64,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.93f, 0.90f, 0.82f) }
            };
            itemStyle = new GUIStyle(titleStyle) { fontSize = 24, fontStyle = FontStyle.Normal };

            black = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            black.SetPixel(0, 0, Color.white);
            black.Apply();
        }

        private void OnGUI()
        {
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            // A ordem **e** a arquitectura das camadas: o fundo respira, o
            // escurecimento assenta por cima dele sempre no mesmo sitio, e a interface
            // vem por ultimo. Quem desenha depois fica em cima, e nada do que vem
            // depois le a deriva.
            DrawBackground(w, h);
            DrawDoorLight(w, h);
            DrawScrim(w, h);

            // O titulo so e escrito quando a arte nao o traz. Ver `artHasTitle`.
            if (!artHasTitle)
                GUI.Label(new Rect(0f, h * 0.25f, w, 80f), title, titleStyle);

            if (inSettings) DrawSettings(w, h);
            else DrawItems(w, h);

            DrawVersion(w, h);

            if (fade > 0.001f)
            {
                GUI.color = new Color(0f, 0f, 0f, fade);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), black);
                GUI.color = Color.white;
            }
        }

        /// <summary>
        /// A camada de fundo: a arte, ampliada e a respirar.
        ///
        /// ---
        ///
        /// **As duas camadas sao separadas por construcao e nao por convencao.** A
        /// deriva vive **so** neste rectangulo. Os botoes sao desenhados noutro metodo,
        /// a partir do centro do ecra, e nao ha maneira de este calculo lhes chegar —
        /// nao ha um transform partilhado que alguem se possa esquecer de compensar.
        ///
        /// **A folga tem de ser maior do que a deriva.** A ampliacao empurra a imagem
        /// para fora do ecra por todos os lados; o deslocamento come essa folga de um
        /// lado de cada vez. A 1.02 num ecra de 1920 sao 19 pixeis de cada lado, e a
        /// deriva gasta 6 — se alguem subir a deriva sem subir a ampliacao, aparece
        /// preto na borda, e e a unica maneira de isto se partir.
        ///
        /// **Dois seios de periodos diferentes**, e nao um: um so em X le-se como um
        /// carrossel. Com um segundo mais lento e mais curto em Y, o movimento deixa
        /// de ter um ritmo que se consiga contar — que e o que separa respirar de
        /// oscilar.
        ///
        /// Nunca `StretchToFill`, seja qual for o `fitMode`: uma imagem de menu
        /// esticada num monitor de outra proporcao e a coisa mais barata que um jogo
        /// pode parecer, e acontece na primeira coisa que o jogador ve.
        /// </summary>
        private void DrawBackground(float w, float h)
        {
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, w, h), black);
            GUI.color = Color.white;

            float t = Time.unscaledTime;
            float driftX = Mathf.Sin(t * Mathf.PI * 2f / driftSeconds) * driftPixels;
            float driftY = Mathf.Sin(t * Mathf.PI * 2f / (driftSeconds * 1.7f)) * driftPixels * 0.35f;

            float overW = w * overscale, overH = h * overscale;
            var drifting = new Rect(
                -(overW - w) * 0.5f + driftX,
                -(overH - h) * 0.5f + driftY,
                overW, overH);

            // O video ganha, mas so depois de ter mesmo um frame. Ate la a imagem
            // parada segura o ecra — e por isso que as duas coexistem em vez de uma
            // substituir a outra.
            bool videoReady = player != null && player.isPrepared && videoTarget != null;

            if (videoReady)
                GUI.DrawTexture(drifting, videoTarget, fitMode, false);
            else if (background != null)
                GUI.DrawTexture(drifting, background, fitMode, false);
        }

        /// <summary>
        /// O escurecimento no centro, por baixo dos botoes.
        ///
        /// Sem ele o menu depende da arte: qualquer imagem com uma zona clara no meio
        /// engole o texto, e a deriva faz com que engula **de forma intermitente**, que
        /// e pior do que engolir sempre.
        ///
        /// Gerado por codigo em vez de ser mais um ficheiro: e um gradiente radial de
        /// 64 pixeis, esticado, e a mesma tecnica que o `PrototypeHUD` ja usa para o
        /// reticulo. Um asset a mais e um asset que alguem tem de manter.
        ///
        /// **Nao anda com o fundo.** E desenhado depois dele e antes dos botoes, sempre
        /// no centro do ecra: e ele que faz a zona morta ser uma zona morta em vez de
        /// uma mancha que passeia.
        /// </summary>
        private void DrawScrim(float w, float h)
        {
            // **Nas opcoes e uma cortina, nao uma mancha.** Um gradiente radial
            // escurece o centro e desvanece nas bordas, que e o que os tres botoes
            // precisam; o painel de opcoes ocupa o ecra de lado a lado e a coluna
            // dos nomes cai justamente onde o gradiente ja nao chega — em cima da
            // arte do titulo. Nove linhas de texto pequeno sobre um logotipo nao se
            // leem. Uniforme, a arte recua toda ao mesmo tempo e continua la.
            if (inSettings)
            {
                GUI.color = new Color(0f, 0f, 0f, 0.78f);
                GUI.DrawTexture(new Rect(0f, 0f, w, h), black);
                GUI.color = Color.white;
                return;
            }

            if (scrimStrength <= 0.001f) return;

            if (scrim == null) scrim = BuildScrim();

            // **Centrado na coluna e nao no ecra.** Com os botoes ao meio dava no
            // mesmo; encostados a um lado, um escurecimento no centro escurece o
            // sitio onde nao esta nada e deixa o texto sem nada por baixo. Continua
            // a nao ler a deriva do fundo — anda com a interface, que esta parada.
            float sw = w * scrimWidth;
            float sh = h * scrimHeight;
            var rect = new Rect(w * itemsCentreX - sw * 0.5f, h * itemsCentreY - sh * 0.5f, sw, sh);

            GUI.color = new Color(0f, 0f, 0f, scrimStrength);
            GUI.DrawTexture(rect, scrim, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
        }

        /// <summary>
        /// A luz por baixo de uma porta fechada — o fundo de quando nao ha arte.
        ///
        /// ---
        ///
        /// **Uma fresta e o derrame por baixo dela**, e nao uma barra. A textura tem
        /// as pontas a esbater porque uma porta e mais estreita do que um ecra: a
        /// chegar as bordas deixava de ser uma porta e passava a ser interface.
        ///
        /// **A respiracao e da lampada e nao do corredor.** Um ciclo de sete segundos
        /// e um desvio de catorze por cento: chega para o ecra nao estar morto e nao
        /// chega para alguem dizer que esta a piscar.
        ///
        /// **E de vez em quando alguem passa.** A sombra atravessa a fresta e a luz
        /// apaga-se por baixo durante um segundo e meio. E a unica coisa que acontece
        /// neste ecra, e acontece de trinta e oito em trinta e oito segundos no
        /// minimo — a primeira vez le-se como um defeito do monitor, e e so a segunda
        /// que se percebe o que foi.
        ///
        /// A sombra reaproveita o disco do escurecimento em preto. Um gradiente ja
        /// gerado serve os dois sitios, e um asset a mais e um asset para manter.
        /// </summary>
        private void DrawDoorLight(float w, float h)
        {
            if (!doorLightWhenBare || !Bare || doorLightStrength <= 0.001f) return;

            if (doorGlow == null) doorGlow = BuildDoorGlow();
            if (scrim == null) scrim = BuildScrim();

            float t = Time.unscaledTime;
            float breath = 0.86f + 0.14f * Mathf.Sin(t * Mathf.PI * 2f / breathSeconds);

            float glowW = w * doorLightWidth, glowH = h * doorLightHeight;
            var rect = new Rect((w - glowW) * 0.5f,
                h * doorLightBottom - glowH, glowW, glowH);

            var tint = doorLightColour;
            tint.a = doorLightStrength * breath;
            GUI.color = tint;
            GUI.DrawTexture(rect, doorGlow, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;

            // Alguem no corredor. Mais alta e mais larga do que a fresta para a
            // apagar de vez em vez de lhe morder um bocado.
            if (passStartedAt < 0f) return;

            float p = Mathf.Clamp01((t - passStartedAt) / Mathf.Max(0.05f, passSeconds));
            float centre = Mathf.Lerp(-0.2f, 1.2f, passLeftToRight > 0f ? p : 1f - p);
            float bodyW = glowW * 0.38f;

            GUI.color = new Color(0f, 0f, 0f, 0.95f);
            GUI.DrawTexture(new Rect(
                rect.x + centre * glowW - bodyW * 0.5f,
                rect.y - glowH * 0.5f,
                bodyW, glowH * 2f), scrim, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
        }

        /// <summary>
        /// A fresta e o derrame, gerados.
        ///
        /// A linha da fresta esta em cima e o derrame cai dela para baixo, que e o
        /// que a luz faz quando vem de uma folha por cima. Se sair ao contrario no
        /// jogo, e porque o IMGUI virou a textura: troca `v` por `1f - v`.
        /// </summary>
        private static Texture2D BuildDoorGlow()
        {
            const int Width = 128, Height = 64;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[Width * Height];

            for (int y = 0; y < Height; y++)
            {
                float v = y / (float)(Height - 1);

                // O derrame, e depois a propria fresta por cima dele.
                float spill = Mathf.Pow(v, 3f) * 0.7f;
                float slit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.88f, 1f, v));

                for (int x = 0; x < Width; x++)
                {
                    float u = x / (float)(Width - 1);
                    float ends = Mathf.SmoothStep(0f, 1f,
                        Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.22f));

                    float alpha = Mathf.Clamp01((spill + slit * 0.9f) * ends);
                    pixels[y * Width + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Disco suave, opaco no centro e transparente na borda.</summary>
        private static Texture2D BuildScrim()
        {
            const int Size = 64;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            float centre = (Size - 1) * 0.5f;
            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // Curva ao quadrado: cai devagar no meio e depressa na borda, para
                    // nao se ver onde e que o escurecimento acaba.
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;

                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private Texture2D scrim;

        /// <summary>
        /// A coluna de botoes: ao centro, empilhada, e **parada**.
        ///
        /// ---
        ///
        /// **Nao se mexe porque nao ha por onde.** As posicoes saem do centro do ecra
        /// e nada aqui le a deriva do fundo. Nao e uma compensacao que possa
        /// dessincronizar-se — e a ausencia de qualquer ligacao entre as duas camadas.
        ///
        /// A arte tem o titulo a esquerda e uma cara a direita; o meio e um quarto
        /// escuro. E ai que a coluna vive, com o escurecimento do `DrawScrim` a
        /// garantir que continua legivel seja qual for a imagem que la esteja.
        /// </summary>
        private void DrawItems(float w, float h)
        {
            const float RowWidth = 320f;

            float top = h * itemsCentreY - (Items.Length * itemsRowHeight) * 0.5f;
            float left = w * itemsCentreX - RowWidth * 0.5f;

            for (int i = 0; i < Items.Length; i++)
            {
                var rect = new Rect(left, top + i * itemsRowHeight, RowWidth, itemsRowHeight);

                // A zona de clique e a linha inteira, mas so a partir de onde o
                // texto comeca: encostado a esquerda, metade do rectangulo fica
                // vazia a direita da palavra, e um clique nesse vazio a acender a
                // opcao le-se como um botao invisivel.
                var hitbox = itemsAlignLeft
                    ? new Rect(rect.x, rect.y, RowWidth * 0.62f, rect.height)
                    : rect;

                bool over = hitbox.Contains(Event.current.mousePosition);
                if (over) hovered = i;

                var style = new GUIStyle(itemStyle)
                {
                    fontSize = itemsFontSize,
                    alignment = itemsAlignLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter
                };
                style.normal.textColor = over
                    ? new Color(1f, 1f, 0.98f)
                    : new Color(0.90f, 0.89f, 0.85f, 0.95f);

                Shadowed(rect, Items[i], style);

                if (over && Event.current.type == EventType.MouseDown && !leaving)
                {
                    Event.current.Use();
                    Choose(i);
                }
            }
        }

        /// <summary>
        /// A versao, ao canto. Pequena, apagada, e fora do caminho de tudo.
        ///
        /// Encostada ao canto inferior esquerdo porque e o unico sitio de um menu
        /// que nunca tem nada: o titulo esta em cima, a coluna esta a direita, e
        /// ninguem procura ali por acidente.
        /// </summary>
        private void DrawVersion(float w, float h)
        {
            if (string.IsNullOrWhiteSpace(versionLine)) return;

            var style = new GUIStyle(itemStyle)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            style.normal.textColor = new Color(0.68f, 0.67f, 0.63f, 0.55f);

            Shadowed(new Rect(w * 0.035f, h - 46f, 280f, 24f), versionLine, style);
        }

        /// <summary>
        /// As opcoes.
        ///
        /// **Ao centro e nao na coluna.** Os botoes vivem encostados a direita
        /// para nao taparem a arte; nove linhas com sliders precisam da largura
        /// toda, e ai o que estorva e a arte e nao o contrario. Sai da coluna e
        /// assume o ecra enquanto esta aberto.
        /// </summary>
        private void DrawSettings(float w, float h)
        {
            float rowHeight = Mathf.Clamp(h * 0.062f, 34f, 58f);
            float panelHeight = SettingsPanel.HeightFor(rowHeight);
            float panelWidth = Mathf.Min(w * 0.62f, 760f);

            var area = new Rect((w - panelWidth) * 0.5f,
                                h * 0.5f - panelHeight * 0.5f - 20f,
                                panelWidth, panelHeight);

            var label = new GUIStyle(itemStyle) { fontSize = Mathf.RoundToInt(rowHeight * 0.46f) };
            label.normal.textColor = new Color(0.90f, 0.89f, 0.85f, 0.95f);

            SettingsPanel.Draw(area, label, rowHeight);

            if (Button(new Rect(w * 0.5f - 150f, area.yMax + 26f, 300f, 40f), "Back"))
            {
                SettingsPanel.Commit();
                inSettings = false;
            }
        }

        private bool Button(Rect rect, string text)
        {
            bool over = rect.Contains(Event.current.mousePosition);
            var style = new GUIStyle(itemStyle) { alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = over ? new Color(1f, 1f, 0.98f)
                                          : new Color(0.90f, 0.89f, 0.85f, 0.95f);
            Shadowed(rect, text, style);

            if (over && Event.current.type == EventType.MouseDown)
            {
                Event.current.Use();
                return true;
            }
            return false;
        }

        private static void Shadowed(Rect rect, string text, GUIStyle style)
        {
            Color original = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, style);
            style.normal.textColor = original;
            GUI.Label(rect, text, style);
        }

        private void Choose(int index)
        {
            switch (index)
            {
                case 0: StartCoroutine(Begin()); break;
                case 1: inSettings = true; break;
                case 2: Quit(); break;
            }
        }

        private IEnumerator Begin()
        {
            leaving = true;

            // O ambiente vai-se embora com o menu: sem isto, o apartamento carregava
            // com dois ambientes por cima um do outro.
            float from = ambience != null ? ambience.volume : 0f;
            float elapsed = 0f;
            while (elapsed < fadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                if (ambience != null)
                    ambience.volume = Mathf.Lerp(from, 0f, elapsed / fadeOutSeconds);
                yield return null;
            }

            SceneManager.LoadScene(firstScene);
        }

        /// <summary>A mesma saida que o menu de pausa e o `GameEnding` usam.</summary>
        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (black != null) Destroy(black);
            if (scrim != null) Destroy(scrim);

            // A RenderTexture e memoria de GPU e nao e recolhida sozinha. Sem isto,
            // voltar ao menu vezes suficientes deixava uma fila delas por libertar.
            if (videoTarget != null)
            {
                videoTarget.Release();
                Destroy(videoTarget);
            }
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(Texture2D art, bool titleInArt, string scene,
            AudioSource room, AudioSource creaks, AudioClip creak, VideoClip loop = null)
        {
            background = art;
            backgroundVideo = loop;
            artHasTitle = titleInArt;
            firstScene = scene;
            ambience = room;
            creakSource = creaks;
            creakClip = creak;
        }
#endif
    }
}
