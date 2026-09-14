using Pungent.Dialogue;
using UnityEngine;

namespace Pungent.NPC
{
    /// <summary>
    /// Os sons de alguem que existe quando ninguem esta a olhar: uma tosse na sala,
    /// a meio da tarde, sem razao nenhuma.
    ///
    /// **Espacial, e e essa a peca toda.** Uma tosse em 2D era um efeito sonoro; uma
    /// tosse que vem da cozinha diz que ele esta na cozinha, e diz isso a quem esta
    /// no quarto de porta fechada. E a mesma ideia dos ruidos de casa do `RuiHunt` —
    /// o §12.1 poe o som como principal canal de tensao — so que aplicada ao dia a
    /// dia, onde ainda nao ha ameaca nenhuma. E por isso que funciona: quando
    /// **houver**, o jogador ja aprendeu a localiza-lo pelo ouvido.
    ///
    /// **Raro de propósito.** Um som de vida que se repete de vinte em vinte
    /// segundos deixa de ser vida e passa a ser um temporizador; a partir daí o
    /// jogador conta-o em vez de o ouvir. O intervalo por omissao e longo e
    /// aleatorio, e nunca toca duas vezes o mesmo clip de seguida.
    ///
    /// Cala-se durante conversas — pedido explicito, e obvio assim que se ouve uma
    /// tosse por cima da propria fala dele.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcLifeSounds : MonoBehaviour
    {
        [Tooltip("Tosses, pigarros, o que for. Tocados ao acaso, nunca o mesmo duas "
               + "vezes de seguida.")]
        [SerializeField] private AudioClip[] clips = new AudioClip[0];

        [Tooltip("Minimo entre dois sons.")]
        [SerializeField, Min(5f)] private float minimumGap = 55f;

        [Tooltip("Maximo entre dois sons. Longo: isto e vida, nao e um metronomo.")]
        [SerializeField, Min(10f)] private float maximumGap = 150f;

        [SerializeField, Range(0f, 1f)] private float volume = 0.55f;

        [Tooltip("A partir daqui nao se ouve. Generoso — o objectivo e ouvi-lo de "
               + "outra divisao, senao isto nao diz nada a ninguem.")]
        [SerializeField, Min(1f)] private float minimumDistance = 2f;
        [SerializeField, Min(2f)] private float maximumDistance = 18f;

        [Header("Quando se cala")]
        [SerializeField] private PrototypeNpcRoutine routine;
        [SerializeField] private WorldDialogueController dialogue;

        [Header("Movimento da tosse")]
        [SerializeField] private Animator animator;
        [Tooltip("Peito inclinado de forma aditiva enquanto a tosse toca. Vazio = osso Chest do Humanoid.")]
        [SerializeField] private Transform coughPivot;
        [SerializeField, Min(0.25f)] private float coughMotionSeconds = 0.9f;
        [SerializeField, Range(2f, 25f)] private float coughBendDegrees = 13f;

        [Tooltip("Silencio depois de o jogo comecar, antes do primeiro som. Sem "
               + "isto, ele tossia no segundo em que a cena carrega e a primeira "
               + "coisa que o jogo faz e um som sem contexto.")]
        [SerializeField, Min(0f)] private float startupSilence = 25f;

        private AudioSource source;
        private float nextAt;
        private int lastClip = -1;
        private float coughStartedAt = -10f;

        private void Awake()
        {
            if (routine == null) routine = GetComponentInParent<PrototypeNpcRoutine>();
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
            if (animator == null) animator = GetComponentInParent<PrototypeNpcRoutine>()?.Animator;
            if (coughPivot == null && animator != null && animator.isHuman)
            {
                coughPivot = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (coughPivot == null) coughPivot = animator.GetBoneTransform(HumanBodyBones.Chest);
                if (coughPivot == null) coughPivot = animator.GetBoneTransform(HumanBodyBones.Spine);
            }

            source = GetComponent<AudioSource>();
            if (source == null) source = gameObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minimumDistance;
            source.maxDistance = maximumDistance;
            source.dopplerLevel = 0f;

            nextAt = Time.time + startupSilence + Random.Range(0f, minimumGap);
        }

        private void Update()
        {
            if (clips == null || clips.Length == 0) return;
            if (Time.time < nextAt) return;

            // Nao por cima da fala dele, nem por cima de um pensamento do Tomas: o
            // texto no ecra e a coisa mais fragil do jogo e uma tosse rouba-lhe a
            // atencao toda. Adia, nao salta — o som acontece a seguir.
            if (routine != null && routine.InConversation) { Postpone(6f); return; }
            if (dialogue != null && dialogue.IsBusy) { Postpone(6f); return; }
            if (source.isPlaying) { Postpone(3f); return; }

            source.PlayOneShot(Pick(), volume);
            coughStartedAt = Time.time;
            nextAt = Time.time + Random.Range(minimumGap, Mathf.Max(minimumGap, maximumGap));
        }

        private void LateUpdate()
        {
            if (coughPivot == null) return;
            float t = (Time.time - coughStartedAt) / Mathf.Max(0.25f, coughMotionSeconds);
            if (t < 0f || t > 1f) return;

            // Duas contraccoes curtas, aplicadas depois do Animator. Assim continua
            // a funcionar sobre idle, andar ou a pose do telemovel sem precisar de
            // um clip de tosse dedicado.
            float first = Mathf.Sin(Mathf.Clamp01(t / 0.48f) * Mathf.PI);
            float second = t < 0.42f ? 0f :
                Mathf.Sin(Mathf.Clamp01((t - 0.42f) / 0.58f) * Mathf.PI) * 0.68f;
            coughPivot.localRotation *= Quaternion.Euler((first + second) * coughBendDegrees, 0f, 0f);
        }

        private void Postpone(float seconds) => nextAt = Time.time + seconds;

        private AudioClip Pick()
        {
            if (clips.Length == 1) return clips[0];

            int index = Random.Range(0, clips.Length);
            if (index == lastClip) index = (index + 1) % clips.Length;
            lastClip = index;
            return clips[index];
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(AudioClip[] sounds, PrototypeNpcRoutine owner,
            WorldDialogueController dialogueController)
        {
            clips = sounds;
            routine = owner;
            dialogue = dialogueController;
        }
#endif
    }
}
