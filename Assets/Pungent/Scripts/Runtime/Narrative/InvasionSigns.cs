using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Os sinais de que alguem esteve no quarto enquanto o Tomas dormia.
    ///
    /// Aplicados atras das palpebras fechadas, como o <see cref="DayTwoDirector"/>
    /// ja faz com o ceu e com o Rui: o jogador fecha os olhos numa casa e abre-os
    /// noutra, sem ver a troca. Ver a mudanca acontecer transformava-a num truque;
    /// encontra-la feita e o que assusta.
    ///
    /// Sao tres, por ordem de forca:
    ///
    /// 1. **A porta destrancada.** So conta se o jogador a tiver trancado — e por
    ///    isso que e a mais forte: ele lembra-se do gesto, foi ele que o fez. Se
    ///    nao trancou, nao ha sinal nenhum, e e melhor assim: inventar uma porta
    ///    destrancada que ele nunca trancou nao prova nada a ninguem.
    /// 2. **A gaveta entreaberta.** O pensamento dela ja estava escrito a preparar
    ///    isto — "Exactly where I left them. I think."
    /// 3. **A janela aberta**, com a cidade a entrar mais alto.
    ///
    /// Nenhum e comentado por ninguem. O peso vem depois, quando o Rui repetir um
    /// detalhe banal que so se sabe la de dentro.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InvasionSigns : MonoBehaviour
    {
        [Tooltip("Evento que abre o dia em que os sinais aparecem.")]
        [SerializeField] private string appliesOnEvent = "day_two";

        [SerializeField] private ChapterDirector director;
        [SerializeField] private PlayerThoughtDirector thoughts;

        [Header("1 - A porta")]
        [SerializeField] private DoorDragInteractable bedroomDoor;
        [Tooltip("Pensamento ao dar por ela. So sai se ele a tiver mesmo trancado.")]
        [SerializeField, TextArea] private string doorThought = "I locked that. I know I locked that.";

        [Header("2 - A gaveta")]
        [Tooltip("Transform da gaveta. Vazio = salta este sinal.")]
        [SerializeField] private Transform drawer;
        [Tooltip("Quanto sai, em metros, na direccao local indicada.")]
        [SerializeField] private float drawerSlide = 0.17f;
        [SerializeField] private Vector3 drawerAxis = Vector3.forward;
        [Tooltip("Substitui o que a comoda diz depois de a gaveta estar aberta. Sem "
               + "isto ela continuava a dizer que esta tudo no sitio com a gaveta "
               + "aberta a frente do jogador.")]
        [SerializeField] private FlavourInteractable dresserFlavour;
        [SerializeField] private Pungent.Dialogue.ThoughtLineSet dresserLinesAfter;

        [Header("3 - A janela")]
        [Tooltip("Folha da janela do quarto. Vazio = salta este sinal.")]
        [SerializeField] private Transform windowSash;
        [SerializeField] private float windowOpenAngle = 34f;
        [SerializeField] private Vector3 windowAxis = Vector3.up;
        [Tooltip("Ambiente da cidade, que passa a ouvir-se mais alto com a janela aberta.")]
        [SerializeField] private AudioSource cityAmbience;
        [SerializeField, Range(0f, 1f)] private float cityVolumeWhenOpen = 0.42f;
        [Tooltip("Alcance novo: com a janela aberta a cidade chega ao quarto.")]
        [SerializeField, Min(1f)] private float cityRangeWhenOpen = 16f;

        private bool applied;

        /// <summary>
        /// Se a porta estava trancada quando a noite acabou. Guardado no momento em
        /// que os sinais sao aplicados, porque a seguir a porta ja esta destrancada
        /// e a pergunta deixa de ter resposta.
        /// </summary>
        public bool DoorWasLocked { get; private set; }

        public bool Applied => applied;

        private void Awake()
        {
            if (director == null) director = FindObjectOfType<ChapterDirector>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
        }

        private void Update()
        {
            if (applied || director == null) return;
            if (!director.HasSeen(appliesOnEvent)) return;
            Apply();
        }

        /// <summary>
        /// Aplica os tres sinais de uma vez. Publico para as ferramentas de editor e
        /// para quem quiser disparar isto sem esperar pelo evento.
        /// </summary>
        public void Apply()
        {
            if (applied) return;
            applied = true;

            ApplyDoor();
            ApplyDrawer();
            ApplyWindow();
        }

        private void ApplyDoor()
        {
            if (bedroomDoor == null) return;

            DoorWasLocked = bedroomDoor.IsLocked;
            if (!DoorWasLocked) return;   // nao trancou: nao ha nada a descobrir

            bedroomDoor.SetLocked(false);
            if (!string.IsNullOrWhiteSpace(doorThought))
                thoughts?.Think("invasion_door", doorThought, 4, true, 3.4f);
        }

        private void ApplyDrawer()
        {
            // As duas metades deste sinal sao independentes de proposito.
            //
            // A comoda do quarto e um "holder + malha" sem gaveta separada: nao ha
            // transform nenhum para puxar. Enquanto as duas metades estavam presas
            // uma a outra, um modelo sem gaveta levava atras as linhas novas, e o
            // sinal desaparecia inteiro sem dar erro — a comoda continuava a dizer
            // que estava tudo no sitio. Perder o movimento e uma pena; perder o que
            // ele pensa sobre isso e perder o sinal.
            if (drawer != null)
            {
                // Entreaberta, nao escancarada. Uma gaveta toda fora le-se como
                // arrombamento; um palmo le-se como alguem que fechou com pressa.
                drawer.localPosition += drawerAxis.normalized * drawerSlide;
            }

            if (dresserFlavour != null && dresserLinesAfter != null)
                dresserFlavour.SetThoughtLines(dresserLinesAfter);
        }

        private void ApplyWindow()
        {
            if (windowSash != null)
                windowSash.localRotation *= Quaternion.AngleAxis(windowOpenAngle, windowAxis.normalized);

            // A cidade entra pela janela. E o unico dos tres sinais que se nota sem
            // se olhar para nada — ouve-se do outro lado do quarto.
            if (cityAmbience != null)
            {
                cityAmbience.volume = cityVolumeWhenOpen;
                cityAmbience.maxDistance = cityRangeWhenOpen;
            }
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorSetEvent(string id) => appliesOnEvent = id;
#endif
    }
}
