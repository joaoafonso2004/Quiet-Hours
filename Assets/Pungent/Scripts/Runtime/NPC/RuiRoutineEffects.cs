using UnityEngine;

namespace Pungent.NPC
{
    public class RuiRoutineEffects : MonoBehaviour
    {
        [Header("Phone")]
        [SerializeField] private NpcPhonePresence phonePresence;

        [Header("Stove")]
        public AudioSource stoveAudio;

        [Header("TV")]
        public AudioSource tvAudio;

        [Header("Fridge")]
        public Light fridgeLight;
        public AudioSource fridgeAudio;
        public AudioClip fridgeOpenClip;
        public AudioClip fridgeCloseClip;

        [Tooltip("A porta que abre mesmo. Antes so acendia uma luz e tocava um som "
               + "com o frigorifico fechado — ouvia-se abrir sem nada se mexer.")]
        public Pungent.Interaction.FridgeDoor fridgeDoor;

        private void Awake()
        {
            if (phonePresence == null) phonePresence = GetComponent<NpcPhonePresence>();
        }

        public void OnActionStarted(string actionName)
        {
            if (actionName == "Stove" && stoveAudio != null)
            {
                stoveAudio.Play();
            }
            else if (actionName == "PhoneIdle")
            {
                phonePresence?.SetRoutinePhone(true);
            }
            else if (actionName == "CouchSit" && tvAudio != null)
            {
                tvAudio.Play();
            }
            else if (actionName == "Fridge")
            {
                if (fridgeDoor != null) fridgeDoor.Open();
                // A luz de dentro passa a ser tratada pela propria porta, que a
                // acende ao ritmo da abertura. Este campo fica para quem tiver uma
                // luz separada do modelo.
                if (fridgeDoor == null && fridgeLight != null) fridgeLight.enabled = true;
                if (fridgeAudio != null && fridgeOpenClip != null)
                {
                    fridgeAudio.PlayOneShot(fridgeOpenClip);
                }
            }
        }

        public void OnActionEnded(string actionName)
        {
            if (actionName == "Stove" && stoveAudio != null)
            {
                stoveAudio.Stop();
            }
            else if (actionName == "PhoneIdle")
            {
                phonePresence?.SetRoutinePhone(false);
            }
            else if (actionName == "CouchSit" && tvAudio != null)
            {
                tvAudio.Stop();
            }
            else if (actionName == "Fridge")
            {
                if (fridgeDoor != null) fridgeDoor.Close();
                if (fridgeDoor == null && fridgeLight != null) fridgeLight.enabled = false;
                if (fridgeAudio != null && fridgeCloseClip != null)
                {
                    fridgeAudio.PlayOneShot(fridgeCloseClip);
                }
            }
        }
    }
}
