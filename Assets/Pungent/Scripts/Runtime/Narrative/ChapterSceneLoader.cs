using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pungent.Narrative
{
    /// <summary>
    /// Muda de cena quando a historia chega a um ponto.
    ///
    /// Faltava a ultima ponte. O Dia 3 acaba a porta da rua e levanta `day_four`; o
    /// `CH_Day4_Garage` arranca com esse mesmo acontecimento e o primeiro passo dele
    /// espera por `arrived_garage`, que so o patio da oficina levanta. Sem ninguem a
    /// abrir a cena da oficina, o Dia 4 comecava e ficava parado no primeiro passo
    /// para sempre — outra vez um passo preso a um evento que ninguem podia levantar.
    ///
    /// Ouve como tudo o resto neste projecto ouve: a perguntar ao director se o
    /// acontecimento ja passou, em vez de um evento a que e preciso alguem se ligar.
    /// E uma pergunta barata e nao ha ligacoes por atar que se possam esquecer.
    ///
    /// A troca acontece com o ecra tapado. Ver a cena a mudar era ver o truque.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChapterSceneLoader : MonoBehaviour, IRebindable
    {
        [Tooltip("Acontecimento que manda mudar de cena.")]
        [SerializeField] private string onEvent;

        [Tooltip("Nome da cena, como esta nas Build Settings.")]
        [SerializeField] private string sceneName;

        [SerializeField] private float fadeOutSeconds = 1.1f;
        [SerializeField] private float holdSeconds = 0.6f;
        [SerializeField] private float fadeInSeconds = 1.4f;

        [SerializeField] private ChapterDirector director;
        [SerializeField] private ScreenFade fade;

        private bool sent;

        private void Awake() => Rebind();

        public void Rebind()
        {
            if (director == null) director = FindObjectOfType<ChapterDirector>();
            if (fade == null) fade = FindObjectOfType<ScreenFade>();
        }

        private void Update()
        {
            if (sent || director == null) return;
            if (string.IsNullOrWhiteSpace(onEvent) || string.IsNullOrWhiteSpace(sceneName)) return;
            if (!director.HasSeen(onEvent)) return;

            // Ja la estamos. Acontece a quem abrir a cena de destino directamente no
            // editor para experimentar um capitulo: sem isto, carregava-se a si
            // propria em ciclo.
            if (SceneManager.GetActiveScene().name == sceneName) { sent = true; return; }

            sent = true;

            if (fade != null) fade.Blink(fadeOutSeconds, holdSeconds, fadeInSeconds, Load);
            else Load();
        }

        private void Load() => SceneManager.LoadScene(sceneName);

#if UNITY_EDITOR
        /// <summary>Usado pelas ferramentas de ligacao, que vivem noutra assembly.</summary>
        public void EditorConfigure(string raisedEvent, string scene)
        {
            onEvent = raisedEvent;
            sceneName = scene;
        }
#endif
    }
}
