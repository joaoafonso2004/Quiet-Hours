using System.Collections;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Um plano fixo, no fim, em vez de ecra preto.
    ///
    /// ---
    ///
    /// **Como e que o pai aparece.** Nao ha cena, nao ha conversa e nao ha caminhada.
    /// Ha **uma imagem**: ele de pe ao lado do carro, na rua debaixo da varanda, com a
    /// porta do condutor aberta e os farois acesos. A camara nao se mexe, ele nao se
    /// mexe, e as tres frases do epilogo saem por cima.
    ///
    /// A razao de ser assim e a mesma que faz o final funcionar: **um homem parado a
    /// olhar para a porta do predio nao precisa de fazer nada.** Assim que ele andar,
    /// falar ou abracar alguem, aquilo vira uma cena — e uma cena de reencontro no fim
    /// de um thriller domestico e onde o genero morre. Um plano de cinco segundos deixa
    /// o jogador a olhar para uma pessoa e a perceber sozinho ha quanto tempo ela ali
    /// esta.
    ///
    /// Custa: um modelo em pose de descanso, um carro que ja existe, uma luz e uma
    /// camara. Nao ha animacao nenhuma, e e por isso que cabe.
    ///
    /// ---
    ///
    /// **Onde.** Na rua do proprio jogo, a Y = -21,18, debaixo da varanda onde o Tomas
    /// fumou nos cinco dias. Nao e um cenario novo: e o sitio para onde ele esteve
    /// sempre a olhar de cima. O jogador nao vai reconhece-lo conscientemente, e e
    /// exactamente por isso que resulta.
    ///
    /// ---
    ///
    /// **A ordem, e nenhuma parte dela e arbitraria:** o <see cref="EpilogueStage"/> ja
    /// fechou o ecra a preto quando o epilogo abriu. Isto acende o cenario **por tras
    /// do preto**, troca a camara, e so entao reabre. O jogador nunca ve a montagem —
    /// ve o corte, que e a unica coisa que se ve num filme.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EpilogueTableau : MonoBehaviour
    {
        [Tooltip("O final a que este plano pertence. So este.")]
        [SerializeField] private string showsOnEvent = "ending_father";

        [Tooltip("O cenario: o pai, o carro, a luz. Desligado o jogo todo.")]
        [SerializeField] private GameObject set;

        [Tooltip("A camara do plano. Tambem desligada.")]
        [SerializeField] private Camera view;

        [Tooltip("Segundos de preto antes de abrir. Deixa a ultima batida do climax "
               + "assentar — cortar logo le-se como o jogo a mudar de ecra.")]
        [SerializeField, Min(0f)] private float blackBefore = 1.8f;

        [Tooltip("Quanto demora a abrir. Devagar: os olhos do jogador vao ter de se "
               + "habituar a haver imagem outra vez, e esse segundo faz parte.")]
        [SerializeField, Min(0.2f)] private float openSeconds = 2.4f;

        [SerializeField] private ScreenFade fade;
        [SerializeField] private ChapterDirector director;

        private bool shown;

        /// <summary>Ja abriu. Util para inspeccionar e para os testes.</summary>
        public bool Shown => shown;

        private void Awake()
        {
            if (fade == null) fade = FindObjectOfType<ScreenFade>();
            if (set != null) set.SetActive(false);
            if (view != null) view.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (shown) return;

            director = ChapterDirector.Resolve(director);
            if (director == null || !director.HasSeen(showsOnEvent)) return;

            shown = true;
            StartCoroutine(Show());
        }

        private IEnumerator Show()
        {
            float waited = 0f;
            while (waited < blackBefore) { waited += Time.unscaledDeltaTime; yield return null; }

            // Montado atras do preto. A camara do jogador desliga-se **depois** de a
            // nova estar ligada: com as duas desligadas no mesmo frame o ecra fica
            // sem quem o desenhe e o Unity queixa-se alto.
            if (set != null) set.SetActive(true);
            if (view != null) view.gameObject.SetActive(true);

            var player = Camera.main;
            if (player != null && view != null && player != view)
            {
                var listener = player.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
                player.enabled = false;
            }

            yield return null;

            fade?.FadeTo(0f, openSeconds);
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(string endingEvent, GameObject scenery, Camera camera,
            ScreenFade screen)
        {
            showsOnEvent = endingEvent;
            set = scenery;
            view = camera;
            fade = screen;
        }
#endif
    }
}
