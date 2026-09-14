using Pungent.Dialogue;
using Pungent.Interaction;
using UnityEngine;

namespace Pungent.NPC
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PrototypeNpcRoutine))]
    public sealed class NpcDialogueInteractable : MonoBehaviour
    {
        [SerializeField] private WorldDialogueController dialogueController;
        [SerializeField] private NpcMumbleVoice mumbleVoice;
        [SerializeField, Min(2f)] private float choiceSeconds = 7f;

        private PrototypeNpcRoutine routine;
        private Pungent.Interaction.PrototypePhoneUI phoneUI;
        private bool awaitingChoice;
        private int exchangeIndex;
        private bool hasAskedRouter;

        private void Awake()
        {
            routine = GetComponent<PrototypeNpcRoutine>();
            if (mumbleVoice == null)
                mumbleVoice = GetComponent<NpcMumbleVoice>();
            if (dialogueController == null)
                dialogueController = FindObjectOfType<WorldDialogueController>();
            
            phoneUI = FindObjectOfType<Pungent.Interaction.PrototypePhoneUI>();
        }

        private void OnDisable()
        {
            if (awaitingChoice)
                EndConversation();
        }

        public void TriggerDialogue()
        {
            if (awaitingChoice || dialogueController == null)
                return;

            if (!hasAskedRouter && (phoneUI == null || !phoneUI.PlayerReplied))
            {
                bool startedRouter = dialogueController.BeginChoice(
                    "Rui",
                    "Did you turn the router back on?",
                    mumbleVoice,
                    "Yes.",
                    "No.",
                    choiceSeconds,
                    ResolveRouterChoice);

                if (startedRouter)
                {
                    awaitingChoice = true;
                    routine.SetConversationActive(true);
                }
                return;
            }

            // Ele proprio mandou a mensagem que pos o Tomas de pe: nao pode agora
            // estranhar que ele esteja acordado.
            string line = exchangeIndex == 0
                ? "The floorboards out here always give you away."
                : "Why do you keep staring at me?";
            string firstChoice = exchangeIndex == 0
                ? "I just needed some water."
                : "You're making me uncomfortable.";
            string secondChoice = exchangeIndex == 0
                ? "Don't creep up on me like that."
                : "Look away.";

            bool started = dialogueController.BeginChoice(
                "Rui",
                line,
                mumbleVoice,
                firstChoice,
                secondChoice,
                choiceSeconds,
                ResolveChoice);

            if (!started)
                return;

            awaitingChoice = true;
            routine.SetConversationActive(true);
        }

        private void ResolveRouterChoice(int choiceIndex)
        {
            hasAskedRouter = true;
            
            if (choiceIndex == 0) // Yes
            {
                dialogueController.ShowReaction("Rui", "Thanks.", mumbleVoice, 2f, EndConversation);
            }
            else // No
            {
                dialogueController.ShowReaction("Rui", "Then do it.", mumbleVoice, 2f, () => 
                {
                    dialogueController.ShowReaction("Tomás", "Ok.", null, 2f, EndConversation);
                });
            }
        }

        private void ResolveChoice(int choiceIndex)
        {
            bool aggressive = choiceIndex == 1;
            routine.ApplyDialogueChoice(aggressive);
            exchangeIndex++;

            string reaction = aggressive
                ? "I live here too. You're the one sneaking around."
                : "Water. Right. Just... try not to make so much noise next time.";

            dialogueController.ShowReaction("Rui", reaction, mumbleVoice, 3.2f, EndConversation);
        }

        private void EndConversation()
        {
            awaitingChoice = false;
            if (routine != null)
                routine.SetConversationActive(false);
        }
    }
}
