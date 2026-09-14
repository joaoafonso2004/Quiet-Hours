using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.NPC;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Self-contained arrival conversation for the apartment reboot.
    /// It deliberately avoids the legacy quest, chapter and threat systems.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RebootRuiIntroduction : MonoBehaviour,
        IPlayerInteractable, IPriorityInteractable
    {
        [SerializeField] private DialogueSequenceDefinition conversation;
        [SerializeField] private WorldDialogueController dialogue;
        [SerializeField] private PrototypeHUD hud;
        [SerializeField] private PrototypeNpcRoutine routine;
        [SerializeField] private NpcMumbleVoice voice;
        [SerializeField] private RebootRoomKey roomKey;

        private int beatIndex;
        private bool available;
        private bool running;
        private bool completed;

        public string Prompt => available && !running && !completed ? "Talk to Rui" : string.Empty;
        public bool HoldToInteract => false;

        private void Awake()
        {
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
            if (hud == null) hud = FindObjectOfType<PrototypeHUD>();
            if (routine == null) routine = GetComponent<PrototypeNpcRoutine>();
            if (voice == null) voice = GetComponentInChildren<NpcMumbleVoice>(true);
            if (roomKey == null) roomKey = FindObjectOfType<RebootRoomKey>();
        }

        public void Arm()
        {
            if (completed) return;

            if (conversation == null || conversation.BeatCount == 0 || dialogue == null ||
                routine == null || roomKey == null)
            {
                Debug.LogError("[RebootRuiIntro] Arrival conversation is not fully configured.", this);
                return;
            }

            available = true;
            hud?.SetObjective("OBJECTIVE: Talk to Rui");
            Debug.Log("[RebootRuiIntro] Arrival conversation armed.", this);
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (!available || running || completed || dialogue == null || dialogue.IsBusy)
                return;

            available = false;
            running = true;
            beatIndex = 0;
            hud?.SetObjective(string.Empty);
            routine?.SetConversationActive(true);
            PlayBeat();
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

        private void PlayBeat()
        {
            if (!running || conversation == null || beatIndex >= conversation.BeatCount)
            {
                Finish();
                return;
            }

            DialogueSequenceDefinition.Beat beat = conversation.BeatAt(beatIndex);
            if (beat == null)
            {
                NextBeat();
                return;
            }

            routine?.GlanceAtPlayer(conversation.LineHold + 1.5f);

            if (!beat.HasChoices)
            {
                bool isLast = beatIndex == conversation.BeatCount - 1;
                dialogue.ShowReaction(conversation.Speaker, beat.Line, voice,
                    conversation.LineHold, NextBeat, autoClose: isLast);
                return;
            }

            var choices = new string[beat.Choices.Length];
            for (int i = 0; i < beat.Choices.Length; i++)
                choices[i] = beat.Choices[i].Text;

            if (!dialogue.BeginChoice(conversation.Speaker, beat.Line, voice,
                    choices, conversation.ChoiceSeconds, OnChoice))
            {
                available = true;
                running = false;
                hud?.SetObjective("OBJECTIVE: Talk to Rui");
            }
        }

        private void OnChoice(int index)
        {
            DialogueSequenceDefinition.Beat beat = conversation.BeatAt(beatIndex);
            if (beat == null || beat.Choices == null || beat.Choices.Length == 0)
            {
                NextBeat();
                return;
            }

            DialogueChoice choice = beat.Choices[Mathf.Clamp(index, 0, beat.Choices.Length - 1)];
            if (choice == null || string.IsNullOrWhiteSpace(choice.Reply))
            {
                NextBeat();
                return;
            }

            routine?.GlanceAtPlayer(conversation.LineHold + 1.5f);
            dialogue.ShowReaction(conversation.Speaker, choice.Reply, voice,
                conversation.LineHold, NextBeat);
        }

        private void NextBeat()
        {
            beatIndex++;
            PlayBeat();
        }

        private void Finish()
        {
            if (!running && completed) return;

            available = false;
            running = false;
            completed = true;
            hud?.SetObjective(string.Empty);
            routine?.ResumeRoutine();
            roomKey?.Arm();
            Debug.Log("[RebootRuiIntro] Arrival conversation complete; roaming resumed.", this);
        }

        private void OnDisable()
        {
            if (!running) return;
            dialogue?.Cancel();
            running = false;
            available = !completed;
            routine?.SetConversationActive(true);
        }
    }
}
