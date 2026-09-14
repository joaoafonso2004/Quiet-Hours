using UnityEngine;
using Pungent.Interaction;
using Pungent.NPC;

namespace Pungent.Triggers
{
    [RequireComponent(typeof(BoxCollider))]
    public class DialogueTriggerZone : MonoBehaviour
    {
        public NpcDialogueInteractable targetDialogue;
        public bool triggerOnce = true;
        private bool hasTriggered = false;

        private void OnTriggerEnter(Collider other)
        {
            if (hasTriggered && triggerOnce) return;

            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerInteractor>() != null)
            {
                // Um trigger antigo da noite do router ainda existe na cena e
                // aponta para a conversa binaria de prototipo. Essa conversa foi
                // substituida e o componente esta desativado, mas chamadas diretas
                // a metodos de um MonoBehaviour ignoram `enabled`. Respeitar aqui o
                // estado do alvo impede que dialogos retirados voltem a aparecer.
                if (targetDialogue != null && targetDialogue.isActiveAndEnabled)
                {
                    targetDialogue.TriggerDialogue();
                    hasTriggered = true;
                }
            }
        }
    }
}
