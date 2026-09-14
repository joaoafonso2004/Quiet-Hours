using Pungent.Interaction;
using UnityEngine;

namespace Pungent.NPC
{
    /// <summary>
    /// Quando o telemovel do jogador diz "Rui esta a escrever", o Rui esta mesmo a
    /// escrever.
    ///
    /// E uma daquelas coisas que ninguem agradece e toda a gente nota se faltar.
    /// Estar na cozinha a olhar para ele, sentado sem nada nas maos, enquanto o
    /// ecra na tua mao diz que ele esta a escrever-te, e o tipo de contradicao que
    /// desfaz uma casa inteira: num segundo o Rui deixa de ser uma pessoa e passa a
    /// ser um sistema de mensagens com um corpo ao lado.
    ///
    /// Ao contrario, tambem funciona ao vivo: veres o telemovel dele acender-se
    /// **antes** de o teu vibrar diz-te, sem uma linha de texto, que a mensagem
    /// veio mesmo dali.
    ///
    /// So mexe quando ele esta a escrever ao jogador. O resto do tempo o
    /// `PrototypeNpcRoutine` manda nele como sempre — isto nunca lhe toma o
    /// controlo, so pede emprestado.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcPhonePresence : MonoBehaviour
    {
        [Tooltip("Nome do contacto no telemovel do jogador. Tem de bater certo com o "
               + "`ContactName` do fio.")]
        [SerializeField] private string contactName = "Rui";

        [Tooltip("O telemovel na mao dele. Desligado quando nao esta a escrever.")]
        [SerializeField] private GameObject phoneProp;

        [Tooltip("Estado do Animator com ele a mexer no telemovel.")]
        [SerializeField] private string phoneState = "PhoneIdle";

        [Tooltip("Estado para onde volta. Um clip sem loop deixado por sua conta "
               + "prende o NPC na pose — ja aconteceu neste projecto.")]
        [SerializeField] private string returnState = "Locomotion";

        [Tooltip("So pega no telemovel se estiver parado. A escrever e a andar ao "
               + "mesmo tempo fica mal e briga com a rotina dele.")]
        [SerializeField] private bool onlyWhenStill = true;

        [SerializeField] private PhoneMessageService messages;
        [SerializeField] private Animator animator;
        [SerializeField] private PrototypeNpcRoutine routine;

        private bool holding;
        private bool dialogueHold;
        private bool routineHold;

        private void Awake()
        {
            if (messages == null) messages = FindObjectOfType<PhoneMessageService>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (routine == null) routine = GetComponent<PrototypeNpcRoutine>();

            if (phoneProp != null) phoneProp.SetActive(false);
        }

        private void Update()
        {
            bool shouldHold = dialogueHold
                || routineHold
                || (IsTypingToPlayer() && (!onlyWhenStill || IsStill()));
            SetHolding(shouldHold);
        }

        /// <summary>
        /// Empresta ao dialogo presencial a pose e o prop usados quando o Rui
        /// escreve uma mensagem. Aqui ele esta deliberadamente parado a mostrar o
        /// telefone, portanto este override nao depende do filtro de movimento.
        /// </summary>
        public void BeginDialoguePhone()
        {
            dialogueHold = true;
            SetHolding(true);
        }

        public void EndDialoguePhone()
        {
            dialogueHold = false;
            RefreshHolding();
        }

        /// <summary>Mostra o prop durante a acao domestica `PhoneIdle`.</summary>
        public void SetRoutinePhone(bool active)
        {
            routineHold = active;
            RefreshHolding();
        }

        private void RefreshHolding()
        {
            SetHolding(dialogueHold || routineHold
                || (IsTypingToPlayer() && (!onlyWhenStill || IsStill())));
        }

        private void SetHolding(bool shouldHold)
        {
            if (shouldHold == holding) return;

            holding = shouldHold;
            if (phoneProp != null) phoneProp.SetActive(holding);

            if (animator == null) return;
            animator.CrossFadeInFixedTime(holding ? phoneState : returnState, 0.22f);
        }

        /// <summary>
        /// O fio dele esta com o "a escrever" ligado.
        ///
        /// Pelo nome do contacto e nao por uma referencia ao asset: o fio do Rui
        /// pode vir a ser mais do que um, e o que interessa e quem esta do outro
        /// lado, nao qual dos ficheiros e.
        /// </summary>
        private bool IsTypingToPlayer()
        {
            if (messages == null) return false;

            foreach (var thread in messages.Threads)
            {
                if (thread == null || thread.ContactName != contactName) continue;
                if (thread.State == PhoneMessageService.ThreadState.Typing) return true;
            }
            return false;
        }

        private bool IsStill()
        {
            if (routine == null) return true;

            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            return agent == null || agent.velocity.sqrMagnitude < 0.05f;
        }
    }
}
