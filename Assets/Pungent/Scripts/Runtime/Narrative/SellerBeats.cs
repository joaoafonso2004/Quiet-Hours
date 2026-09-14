using Pungent.Dialogue;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O Vitor: o que diz, onde esta, e a maneira como nao te deixa ir embora.
    ///
    /// Duas conversas e uma aproximacao, sem escolhas nenhumas — como o estranho da
    /// estrada. O jogador nao responde a isto por palavras; responde carregando as
    /// pecas depressa ou devagar, e entrando no carro.
    ///
    /// **A segunda conversa e o aviso.** Ate ai ele e um homem a vender pecas
    /// baratas; a partir do momento em que o Tomas comeca a carregar, ele muda de
    /// sitio e comeca a fazer perguntas cuja resposta ja sabe. Nao ameaca nem toca
    /// em ninguem: fica entre o Tomas e o carro, e fala.
    ///
    /// A §5 diz que "o vendedor tenta atrasar a saida". Atrasar, e nao impedir —
    /// nada aqui bloqueia o jogador. Se ele quiser ir-se embora a meio de uma frase,
    /// vai-se embora, e o Vitor fica a falar sozinho no patio. Isso e melhor do que
    /// qualquer parede invisivel.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SellerBeats : MonoBehaviour
    {
        [SerializeField] private ChapterDirector director;
        [SerializeField] private WorldDialogueController dialogue;
        [SerializeField] private string speakerName = "Vitor";

        [Header("Ao encontrar")]
        [SerializeField] private string meetEvent = "seller_met";
        [SerializeField, TextArea] private string[] meetLines =
        {
            "You found it. Nobody finds it first time.",
            "It is all under the sheet. Take a look, I am not going to stand over you.",
            "Cash when you are done. No rush."
        };

        [Header("A atrasar a saida")]
        [Tooltip("A partir daqui ele muda-se para o pe do carro e comeca a falar.")]
        [SerializeField] private string delayEvent = "parts_loaded";

        [SerializeField, TextArea] private string[] delayLines =
        {
            "That is the lot. Careful with the radiator, the fins bend if you look at them.",
            "You drove down on your own, then.",
            "Long way back at this hour. The nationals are empty after ten.",
            "You are at the old block on Rua das Flores, right? Third floor.",
            "Somebody said. I forget who."
        };

        [Header("Onde ele fica")]
        [Tooltip("Distancia a que ele para do carro. Perto que baste para incomodar, "
               + "longe que baste para nao parecer que o vai agarrar.")]
        [SerializeField, Min(1f)] private float standOff = 2.6f;

        [SerializeField, Min(1f)] private float walkSpeed = 1.4f;

        [SerializeField] private Transform car;

        [SerializeField, Min(1f)] private float holdSeconds = 3.4f;
        [SerializeField, Min(0f)] private float gapSeconds = 1.0f;

        private string[] lines;
        private int index;
        private float nextAt;
        private bool speaking;
        private bool metDone;
        private bool delayStarted;
        private Vector3 target;
        private Animator animator;

        /// <summary>Ja disse tudo o que tinha a dizer junto ao carro.</summary>
        public bool DelayFinished { get; private set; }

        private void Awake()
        {
            if (director == null) director = FindObjectOfType<ChapterDirector>();
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
            if (car == null)
            {
                var driver = FindObjectOfType<Pungent.Driving.CarDriver>();
                if (driver != null) car = driver.transform;
            }
            animator = GetComponentInChildren<Animator>(true);
        }

        private void Update()
        {
            if (director == null) return;

            if (!metDone && director.HasSeen(meetEvent)) { metDone = true; Say(meetLines); }

            if (!delayStarted && director.HasSeen(delayEvent))
            {
                delayStarted = true;
                if (car != null)
                {
                    // Junto ao carro, do lado do condutor, entre o Tomas e a porta.
                    target = car.position - car.right * standOff;
                    target.y = transform.position.y;
                }
                Say(delayLines);
            }

            if (delayStarted && target != Vector3.zero) Approach();
            UpdateSpeech();
        }

        /// <summary>
        /// Ele desloca-se a andar, ao contrario do Rui no prologo.
        ///
        /// A diferenca e intencional: o Rui muda de sitio quando ninguem o ve, e e
        /// isso que o torna estranho. O Vitor atravessa o patio a andar, a vista, e
        /// e isso que o torna so um homem a aproximar-se — que e outro tipo de
        /// desconforto.
        /// </summary>
        private void Approach()
        {
            float step = walkSpeed * Time.deltaTime;
            Vector3 flat = new Vector3(target.x, transform.position.y, target.z);
            if ((flat - transform.position).sqrMagnitude < 0.04f)
            {
                SetWalking(false);
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, flat, step);
            Vector3 look = flat - transform.position;
            if (look.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(look, Vector3.up);
            SetWalking(true);
        }

        private void SetWalking(bool walking)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetBool("Walking", walking);
        }

        private void Say(string[] script)
        {
            lines = script;
            index = 0;
            nextAt = Time.time;
            speaking = true;
        }

        private void UpdateSpeech()
        {
            if (!speaking || Time.time < nextAt) return;
            if (dialogue != null && dialogue.IsBusy) return;

            if (lines == null || index >= lines.Length)
            {
                speaking = false;
                if (delayStarted) DelayFinished = true;
                return;
            }

            string line = lines[index++];
            nextAt = Time.time + holdSeconds + gapSeconds;
            dialogue?.ShowReaction(speakerName, line, null, holdSeconds, null, true);
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(Transform carTransform) => car = carTransform;
#endif
    }
}
