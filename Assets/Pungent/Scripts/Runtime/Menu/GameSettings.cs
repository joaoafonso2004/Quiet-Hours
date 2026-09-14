using UnityEngine;

namespace Pungent.Menu
{
    /// <summary>
    /// As definicoes do jogo, guardadas entre sessoes.
    ///
    /// ---
    ///
    /// **Estatica e sem objecto na cena, de proposito.**
    ///
    /// Um componente obrigava a que cada cena o tivesse, ou a mais um prefab
    /// persistente a arrastar entre elas; e o motor do jogador teria de o
    /// procurar para saber a sensibilidade do rato. Assim quem precisa de uma
    /// definicao le-a onde esta — `GameSettings.Sensitivity` — e nao ha
    /// referencia nenhuma por ligar no Inspector, que e a maneira mais comum
    /// de isto se partir em silencio.
    ///
    /// ---
    ///
    /// **Aplicar e guardar sao coisas separadas.**
    ///
    /// Mexer no cursor de um slider aplica logo: quem esta a afinar o brilho
    /// tem de o ver a mudar enquanto arrasta. Escrever no disco a cada frame
    /// de arrasto e que nao — o `Save` e chamado quando o painel fecha.
    ///
    /// ---
    ///
    /// **O que cada uma faz mesmo**, porque metade dos menus de opcoes de jogos
    /// pequenos tem interruptores que nao estao ligados a nada:
    ///
    /// - `Volume` e `AudioListener.volume`.
    /// - `Fullscreen` e `VSync` sao do ecra e da placa grafica.
    /// - `Brightness` e um veu desenhado por cima de tudo — ver `BrightnessVeil`.
    /// - `TextureFiltering` liga o filtro anisotropico.
    /// - `RawInput` decide se o rato passa por suavizacao antes de virar a cabeca.
    /// - `InvertY`, `Sensitivity` e `HeadBobbing` sao lidas pelo jogador.
    /// </summary>
    public static class GameSettings
    {
        private const string KeyVolume = "qh_volume";
        private const string KeyFullscreen = "qh_fullscreen";
        private const string KeyVSync = "qh_vsync";
        private const string KeyBrightness = "qh_brightness";
        private const string KeyFiltering = "qh_filtering";
        private const string KeyRawInput = "qh_rawinput";
        private const string KeyInvertY = "qh_inverty";
        private const string KeySensitivity = "qh_sensitivity";
        private const string KeyHeadBob = "qh_headbob";

        /// <summary>
        /// Sensibilidade do rato, de 0 a 1, e o que 0 e 1 querem dizer.
        ///
        /// O valor guardado e normalizado para o cursor do slider poder ser um
        /// cursor; quem move a camara multiplica por <see cref="SensitivityScale"/>.
        /// Meio da barra = o valor com que o jogo foi afinado.
        /// </summary>
        public const float MinSensitivity = 0.25f;
        public const float MaxSensitivity = 3f;

        public static float Volume = 1f;
        public static bool Fullscreen = true;
        public static bool VSync = true;

        /// <summary>0 = escuro, 0,5 = como o jogo foi iluminado, 1 = claro.</summary>
        public static float Brightness = 0.5f;

        public static bool TextureFiltering = true;
        public static bool RawInput = true;
        public static bool InvertY;
        public static float Sensitivity = 0.5f;
        public static bool HeadBobbing = true;

        private static bool loaded;

        /// <summary>
        /// Multiplicador a aplicar ao delta do rato. Meio da barra da 1 — o jogo
        /// nao fica mais rapido nem mais lento por alguem abrir as opcoes e nao
        /// mexer em nada.
        /// </summary>
        public static float SensitivityScale =>
            Sensitivity <= 0.5f
                ? Mathf.Lerp(MinSensitivity, 1f, Sensitivity * 2f)
                : Mathf.Lerp(1f, MaxSensitivity, (Sensitivity - 0.5f) * 2f);

        /// <summary>
        /// Carrega e aplica antes de a primeira cena correr.
        ///
        /// `BeforeSceneLoad` e nao `AfterSceneLoad`: o jogador ja pode estar a
        /// mexer o rato no primeiro frame do apartamento, e uma sensibilidade
        /// que so chega no segundo le-se como o jogo a "engatar".
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            Load();
            Apply();
            BrightnessVeil.Ensure();
        }

        public static void Load()
        {
            if (loaded) return;
            loaded = true;

            Volume = PlayerPrefs.GetFloat(KeyVolume, 1f);
            Fullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) != 0;
            VSync = PlayerPrefs.GetInt(KeyVSync, 1) != 0;
            Brightness = PlayerPrefs.GetFloat(KeyBrightness, 0.5f);
            TextureFiltering = PlayerPrefs.GetInt(KeyFiltering, 1) != 0;
            RawInput = PlayerPrefs.GetInt(KeyRawInput, 1) != 0;
            InvertY = PlayerPrefs.GetInt(KeyInvertY, 0) != 0;
            Sensitivity = PlayerPrefs.GetFloat(KeySensitivity, 0.5f);
            HeadBobbing = PlayerPrefs.GetInt(KeyHeadBob, 1) != 0;
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(KeyVolume, Volume);
            PlayerPrefs.SetInt(KeyFullscreen, Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(KeyVSync, VSync ? 1 : 0);
            PlayerPrefs.SetFloat(KeyBrightness, Brightness);
            PlayerPrefs.SetInt(KeyFiltering, TextureFiltering ? 1 : 0);
            PlayerPrefs.SetInt(KeyRawInput, RawInput ? 1 : 0);
            PlayerPrefs.SetInt(KeyInvertY, InvertY ? 1 : 0);
            PlayerPrefs.SetFloat(KeySensitivity, Sensitivity);
            PlayerPrefs.SetInt(KeyHeadBob, HeadBobbing ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Poe no sistema o que so o sistema sabe fazer. As restantes definicoes
        /// nao aparecem aqui porque nao ha nada para "aplicar": sao lidas no
        /// momento em que fazem falta.
        ///
        /// **No editor nao se mexe no modo de ecra.** `Screen.fullScreen` no
        /// editor mete a janela do Unity em ecra inteiro a meio de uma sessao de
        /// trabalho, e nao e isso que alguem quer ao carregar num interruptor
        /// para testar.
        /// </summary>
        public static void Apply()
        {
            AudioListener.volume = Mathf.Clamp01(Volume);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            QualitySettings.anisotropicFiltering = TextureFiltering
                ? AnisotropicFiltering.ForceEnable
                : AnisotropicFiltering.Disable;

#if !UNITY_EDITOR
            if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
#endif
        }
    }

    /// <summary>
    /// O veu do brilho: um rectangulo por cima de tudo o resto.
    ///
    /// ---
    ///
    /// **Nao mexe na iluminacao da cena, e e essa a razao de existir.** Subir a
    /// luz ambiente para "clarear" desfazia o trabalho de iluminacao de um jogo
    /// que se passa quase todo no escuro — as sombras do corredor sao a peca,
    /// nao um acidente. Um veu por cima muda o que se ve sem mexer no que esta
    /// la.
    ///
    /// **Cria-se sozinho e sobrevive as cenas.** Uma definicao que so funciona
    /// se alguem se lembrar de arrastar um objecto para cada cena e uma definicao
    /// que vai estar desligada em metade do jogo.
    ///
    /// **Ordem 1000 no `OnGUI`**: tem de ficar por cima do HUD e do menu de
    /// pausa. Um brilho que nao afecta a interface deixa a interface a boiar
    /// sobre uma imagem escurecida.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class BrightnessVeil : MonoBehaviour
    {
        private static BrightnessVeil instance;
        private Texture2D pixel;

        internal static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("BRIGHTNESS_VEIL") { hideFlags = HideFlags.HideAndDontSave };
            instance = go.AddComponent<BrightnessVeil>();
            DontDestroyOnLoad(go);
        }

        private void OnGUI()
        {
            float b = GameSettings.Brightness;
            if (Mathf.Abs(b - 0.5f) < 0.005f) return;

            if (pixel == null)
            {
                pixel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                pixel.SetPixel(0, 0, Color.white);
                pixel.Apply();
            }

            // Escurecer aguenta muito mais do que clarear: um veu branco a 30%
            // ja lava a imagem toda e faz o preto virar cinzento, e este jogo
            // vive do preto. Por isso os dois lados da barra nao tem a mesma
            // forca — tem a mesma **utilidade**.
            Color tint = b < 0.5f
                ? new Color(0f, 0f, 0f, (0.5f - b) * 1.4f)
                : new Color(1f, 1f, 1f, (b - 0.5f) * 0.30f);

            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), pixel);
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
            if (instance == this) instance = null;
        }
    }
}
