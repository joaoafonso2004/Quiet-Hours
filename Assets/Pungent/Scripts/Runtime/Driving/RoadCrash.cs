using System.Collections;
using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// O que acontece a quem bate de frente na estrada.
    ///
    /// ---
    ///
    /// **Nao ha ecra de derrota, e a razao nao e generosidade.**
    ///
    /// Um "tentar outra vez" diz ao jogador que aquilo era um teste de conducao e que
    /// ele reprovou. A partir desse ecra o resto da noite e uma prova, e um
    /// perseguidor deixa de ser um perseguidor e passa a ser um obstaculo com um
    /// contador. E o mesmo erro que transformaria a caca do Dia 5 num jogo de furtar.
    ///
    /// Tambem nao pode ser de graca. Um despiste sem consequencia nenhuma ensina em
    /// dez segundos que a estrada e um corredor onde nada custa nada, e a partir dai
    /// nao ha tensao possivel em cima de quatro rodas.
    ///
    /// **O preco e tempo, e o tempo e a unica coisa que o outro carro nao perdeu.**
    ///
    /// Corte a preto — depressa, porque uma batida e depressa. Alguns segundos de
    /// nada. E a abertura e lenta, muito mais lenta do que o corte, porque nao e um
    /// fade: e alguem a voltar a si. Quando ele volta, o carro esta parado na berma,
    /// virado ao contrario do que estava, com o motor morto — e os farois de tras
    /// estao mais perto do que estavam.
    ///
    /// Ninguem comenta o que aconteceu. Nao ha "estas bem?", nao ha contador de
    /// batidas e nao ha nada no HUD. O jogador sabe que perdeu tempo porque **ve**
    /// quanto e que perdeu, pelo retrovisor.
    ///
    /// ---
    ///
    /// **Empurrado para tras e nao para a frente.** O carro reaparece uns metros aquem
    /// de onde bateu. Isso e o que faz a conta ser sentida em vez de anunciada: ele
    /// perde estrada e o outro nao, e a distancia entre os dois fecha-se pelos dois
    /// lados ao mesmo tempo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadCrash : MonoBehaviour
    {
        [SerializeField] private string crashEvent = "road_crash";

        [Header("Ligacoes")]
        [SerializeField] private CarDriver car;
        [SerializeField] private RoadPath path;
        [SerializeField] private NightRoadDirector roadDirector;
        [SerializeField] private ScreenFade fade;
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private ChapterDirector director;

        [Header("O corte")]
        [Tooltip("A batida e depressa. Um corte lento le-se como desmaio encenado.")]
        [SerializeField, Min(0.05f)] private float blackOutSeconds = 0.28f;

        [Tooltip("Quanto tempo fica preto. E aqui que o carro e reposto.")]
        [SerializeField, Min(0.5f)] private float blackHoldSeconds = 4.5f;

        [Tooltip("A abertura. Lenta de propósito: nao e uma transicao, e alguem a "
               + "voltar a si.")]
        [SerializeField, Min(0.5f)] private float wakeSeconds = 3.4f;

        [Header("Onde acorda")]
        [Tooltip("Metros para o lado do eixo da estrada. Positivo = berma direita.")]
        [SerializeField] private float vergeOffset = 4.6f;

        [Tooltip("Metros perdidos. Ele acorda **aquem** de onde bateu.")]
        [SerializeField, Min(0f)] private float groundLost = 30f;

        [Tooltip("Angulo com que o carro fica em relacao a estrada. Nao alinhado: um "
               + "carro que se despista nao fica direitinho na berma.")]
        [SerializeField] private float askewDegrees = 34f;

        [Header("O preco")]
        [Tooltip("A que distancia quem vem atras passa a andar. Era 48.")]
        [SerializeField, Min(8f)] private float followerGapAfter = 20f;

        [Header("Som e fala")]
        [SerializeField] private AudioSource impactSource;
        [SerializeField] private AudioClip impactClip;

        [Tooltip("Dito ao voltar a si, e so uma vez. Banal: ele nao comenta o "
               + "despiste, comenta o vidro.")]
        [SerializeField, TextArea]
        private string thought = "The wing mirror is gone. I did not hear it go.";

        private bool crashing;
        private bool spent;
        private int hint;

        /// <summary>Ja houve um despiste. Util para inspeccionar e para os testes.</summary>
        public bool HasCrashed => spent;

        private void Awake()
        {
            if (path == null) path = FindObjectOfType<RoadPath>();
            if (fade == null) fade = FindObjectOfType<ScreenFade>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            if (roadDirector == null) roadDirector = FindObjectOfType<NightRoadDirector>();
        }

        private void Update()
        {
            if (crashing || spent) return;

            director = ChapterDirector.Resolve(director);
            if (director == null || !director.HasSeen(crashEvent)) return;

            crashing = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            if (impactSource != null && impactClip != null)
                impactSource.PlayOneShot(impactClip, 1f);

            fade?.FadeTo(1f, blackOutSeconds);

            float waited = 0f;
            while (waited < blackOutSeconds + blackHoldSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            PutOnVerge();
            roadDirector?.CloseFollowerGap(followerGapAfter);

            fade?.FadeTo(0f, wakeSeconds);

            // A fala sai depois de haver imagem outra vez. Dita no escuro, seria uma
            // voz sem corpo a explicar o que o jogador ainda nao viu.
            waited = 0f;
            while (waited < wakeSeconds) { waited += Time.unscaledDeltaTime; yield return null; }

            if (!string.IsNullOrWhiteSpace(thought))
                thoughts?.Think("road_crash", thought, 4, true, 3.6f);

            crashing = false;
            spent = true;
        }

        /// <summary>
        /// Poe o carro na berma, parado, torto e com o motor morto.
        ///
        /// O motor morto e importante e nao e castigo: e a unica maneira de o jogador
        /// perceber que **passou tempo**. Um carro que continua a trabalhar nunca
        /// esteve parado. Ele tem de rodar a chave outra vez, e e nesse segundo — com
        /// o motor a pegar — que ele olha pelo retrovisor.
        /// </summary>
        private void PutOnVerge()
        {
            if (car == null) return;

            car.StopEngine();

            if (path == null) return;

            float here = path.DistanceOf(car.transform.position, ref hint);
            float at = Mathf.Max(0f, here - groundLost);

            Vector3 forward = path.ForwardAt(at);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 place = path.PointAt(at) + right * vergeOffset + Vector3.up * 0.15f;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up)
                                * Quaternion.Euler(0f, askewDegrees, 0f);

            car.PlaceStopped(place, rotation);
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(CarDriver playerCar, RoadPath road,
            NightRoadDirector night, ScreenFade screen, AudioSource source, AudioClip clip)
        {
            car = playerCar;
            path = road;
            roadDirector = night;
            fade = screen;
            impactSource = source;
            impactClip = clip;
        }
#endif
    }
}
