using Pungent.Player;
using UnityEngine;
using System.Collections;

namespace Pungent.Narrative
{
    [RequireComponent(typeof(Collider))]
    public sealed class CameraLockEvent : MonoBehaviour
    {
        [SerializeField] private OpeningQuestDirector questDirector;
        [SerializeField] private OpeningQuestDirector.Step requiredStep = OpeningQuestDirector.Step.ReturnToRoom;
        
        [SerializeField] private Transform lookTarget;
        [SerializeField] private float lockDuration = 2.5f;
        [SerializeField] private bool triggerOnce = true;
        
        [Header("Optional Door Event")]
        [SerializeField] private Transform doorToOpen;
        [SerializeField] private float doorOpenAngle = 20f;
        [SerializeField] private float doorOpenSpeed = 10f;
        [SerializeField] private AudioSource doorAudio;
        [SerializeField] private AudioClip creakClip;
        
        [Header("Stinger Audio")]
        [SerializeField] private AudioSource stingerSource;
        [SerializeField] private AudioClip stingerClip;

        [Header("Linha de vista")]
        [Tooltip("Se ligado, a camara so e agarrada quando o `lookTarget` esta mesmo "
               + "a vista. Sem isto, o susto disparava com ele do outro lado de uma "
               + "parede: a camara virava-se para madeira e o sting tocava para "
               + "ninguem.")]
        [SerializeField] private bool requireVisibleTarget = true;

        [Tooltip("O que conta como parede.")]
        [SerializeField] private LayerMask sightBlockers = ~0;

        [Tooltip("Altura do alvo, para os raios.")]
        [SerializeField, Min(0.2f)] private float targetHeight = 1.75f;

        private bool triggered;
        private Coroutine lockCoroutine;

        private void OnTriggerEnter(Collider other)
        {
            if (triggered && triggerOnce) return;

            // Only trigger if we are in the correct quest step
            if (questDirector != null && questDirector.Current != requiredStep) return;

            // **Nunca atraves de paredes — mas so isso.**
            //
            // Isto ja esteve a usar o `CanPlayerSee`, que exige tambem que o alvo
            // esteja no enquadramento, e assim o susto do quarto do Rui deixou de
            // disparar: quem entra no quarto ainda nao esta virado para ele, porque
            // e a camara que o vai virar. O teste tem de ser so a linha de vista.
            if (requireVisibleTarget && lookTarget != null)
            {
                var camera = other.GetComponentInChildren<Camera>();
                if (camera == null) camera = Camera.main;
                Vector3 eye = camera != null ? camera.transform.position
                                             : other.transform.position + Vector3.up * 1.6f;
                if (!Pungent.NPC.PlayerSight.HasLineOfSight(eye, lookTarget,
                        targetHeight, sightBlockers))
                    return;
            }

            var input = other.GetComponentInParent<PlayerInputReader>();
            var camPhysics = other.GetComponentInChildren<CameraPhysics>();
            
            if (input != null && camPhysics != null)
            {
                triggered = true;
                if (lockCoroutine != null) StopCoroutine(lockCoroutine);
                lockCoroutine = StartCoroutine(LockSequence(input, camPhysics));
            }
        }

        /// <summary>
        /// Agarra a camara ao alvo **sem mexer ninguem de sitio**.
        ///
        /// Vale a pena ficar escrito, porque e a razao de o plano da imagem
        /// funcionar: isto so chama `ForceLookAt`, que **roda**. Nao ha dolly, nao
        /// ha zoom e nao ha distancia de enquadramento nenhuma. A distancia entre os
        /// dois no momento em que dispara e a distancia que fica — e por isso que
        /// disparar com ele a um metro da cara da um grande plano, e disparar do
        /// outro lado da sala da um plano largo.
        ///
        /// Se alguem alguma vez quiser "enquadrar melhor" aproximando a camara, e
        /// isto que se perde: o plano deixa de dizer onde ele estava.
        /// </summary>
        private IEnumerator LockSequence(PlayerInputReader input, CameraPhysics camPhysics)
        {
            input.SetMoveSuppressed(true);
            input.SetLookSuppressed(true);
            
            if (doorAudio != null && creakClip != null)
            {
                doorAudio.PlayOneShot(creakClip, 0.7f);
            }
            
            if (stingerSource != null && stingerClip != null)
            {
                stingerSource.PlayOneShot(stingerClip, 1f);
            }
            
            float elapsed = 0f;
            Quaternion initialDoorRot = doorToOpen != null ? doorToOpen.localRotation : Quaternion.identity;
            Quaternion targetDoorRot = initialDoorRot;
            if (doorToOpen != null)
            {
                // Most doors swing on the Y axis, adjusting by -doorOpenAngle might be right for opening inwards
                targetDoorRot = initialDoorRot * Quaternion.Euler(0, -doorOpenAngle, 0); 
            }

            while (elapsed < lockDuration)
            {
                if (lookTarget != null)
                {
                    camPhysics.ForceLookAt(lookTarget.position);
                }
                
                if (doorToOpen != null)
                {
                    doorToOpen.localRotation = Quaternion.RotateTowards(
                        doorToOpen.localRotation, 
                        targetDoorRot, 
                        doorOpenSpeed * Time.deltaTime
                    );
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }

            input.SetMoveSuppressed(false);
            input.SetLookSuppressed(false);
        }
    }
}
