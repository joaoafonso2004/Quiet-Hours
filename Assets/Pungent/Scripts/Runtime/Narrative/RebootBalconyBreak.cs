using System.Collections;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.NPC;
using Pungent.Player;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// A pausa na varanda, e o Rui atrás.
    ///
    /// **Fumar é opcional, olhar é obrigatório** — é o que a história pede, e a
    /// distinção não é cosmética. O cigarro é um gesto que o jogador pode escolher
    /// ter; o que o beat precisa mesmo é que ele fique parado, virado para fora,
    /// costas voltadas para a casa. É essa pose que o susto usa.
    ///
    /// Por isso a condição não é "entrou na varanda" mas "está na varanda **e** a
    /// olhar para a rua durante alguns segundos". Sem o segundo pedaço, o Rui
    /// falava-lhe às costas com o jogador virado para ele — que é a mesma frase a
    /// dizer o contrário do que quer dizer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RebootBalconyBreak : MonoBehaviour
    {
        [Header("Cena")]
        [Tooltip("Volume sobre a laje. Trigger; só se lhe pergunta se contém o jogador.")]
        [SerializeField] private BoxCollider balconyVolume;

        [SerializeField] private Transform player;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PrototypeHUD hud;
        [SerializeField] private WorldDialogueController dialogue;

        [Header("Olhar para fora")]
        [Tooltip("Para onde é a rua, em graus. 0 = +Z. A varanda desta casa dá para "
               + "norte, por isso zero serve.")]
        [SerializeField, Range(-180f, 180f)] private float outwardYaw;

        [Tooltip("Quanto pode desviar da rua e ainda contar como estar a olhar lá "
               + "para fora. Largo de propósito: ninguém fica de bússola na mão.")]
        [SerializeField, Range(15f, 90f)] private float lookAngle = 55f;

        [Tooltip("Quanto tempo tem de lá ficar. Curto de mais e o susto apanha-o a "
               + "passar; longo de mais e ele desiste e volta para dentro.")]
        [SerializeField, Min(0.5f)] private float lookSeconds = 3f;

        [Header("Texto")]
        [SerializeField] private string objective = "OBJECTIVE: Take a break on the balcony";
        [SerializeField] private string objectiveWhenDone = string.Empty;
        [SerializeField, TextArea] private string firstLine = "Careful with that rail.";
        [SerializeField, TextArea] private string secondLine = "I can hear it move from my room.";
        [SerializeField] private string speaker = "RUI";
        [SerializeField, Min(0.5f)] private float lineSeconds = 2.4f;

        [Header("O Rui")]
        [SerializeField] private PrototypeNpcRoutine rui;

        [Tooltip("Onde ele fica, do lado de dentro do vão da varanda. A rotação "
               + "conta: tem de ficar virado para a varanda.")]
        [SerializeField] private Transform ruiDoorwayAnchor;

        [Tooltip("Entre a primeira frase e o jogador se virar. É o tempo de perceber "
               + "que há alguém atrás — se a câmara virasse ao mesmo tempo que a voz, "
               + "não havia susto nenhum, havia um corte.")]
        [SerializeField, Min(0f)] private float beforeTurnSeconds = 0.9f;

        [SerializeField, Min(0.1f)] private float cameraLockSeconds = 1.2f;

        [Header("Som")]
        [SerializeField] private AudioSource sound;

        [Tooltip("Sting médio. Vazio por agora — o dono ainda o vai escolher.")]
        [SerializeField] private AudioClip stingClip;
        [SerializeField, Range(0f, 1f)] private float stingVolume = 0.9f;

        private bool armed;
        private bool done;
        private float looking;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            if (input == null) input = FindObjectOfType<PlayerInputReader>();
            if (hud == null) hud = FindObjectOfType<PrototypeHUD>();
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
            if (player == null && input != null) player = input.transform;

            if (balconyVolume == null || rui == null || ruiDoorwayAnchor == null)
            {
                Debug.LogError("[RebootBalconyBreak] Faltam o volume da varanda, o Rui ou a âncora do vão.", this);
                enabled = false;
            }
        }

        /// <summary>Liga o passo. Chamado quando a exportação é vista.</summary>
        public void SetArmed(bool value)
        {
            armed = value;
            if (value && !done && !string.IsNullOrEmpty(objective))
                hud.SetObjective(objective);
        }

        private void Update()
        {
            if (!armed || done || player == null || playerCamera == null) return;

            if (!OnBalcony() || !LookingOut())
            {
                // Zera em vez de descontar: entrar, virar-se para dentro e voltar
                // a virar-se não deve somar. O que se quer é uma pausa inteira.
                looking = 0f;
                return;
            }

            looking += Time.deltaTime;
            if (looking < lookSeconds) return;

            done = true;
            StartCoroutine(RuiBehind());
        }

        private bool OnBalcony() => balconyVolume.bounds.Contains(player.position);

        private bool LookingOut()
        {
            Vector3 outward = Quaternion.Euler(0f, outwardYaw, 0f) * Vector3.forward;
            Vector3 flat = playerCamera.transform.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return false;
            return Vector3.Angle(flat.normalized, outward) <= lookAngle;
        }

        /// <summary>
        /// Ele aparece atrás, e o vão está fora do enquadramento — por construção.
        ///
        /// O beat só dispara com o jogador virado para a rua, e o vão fica-lhe às
        /// costas. É a única encenação deste jogo em que pousar o Rui num sítio é
        /// seguro sem verificar nada: a própria condição de entrada garante que
        /// ninguém está a olhar para lá. Confirma-se na mesma, porque uma condição
        /// que se assume é uma condição que um dia muda.
        /// </summary>
        private IEnumerator RuiBehind()
        {
            if (!AnchorHidden())
            {
                // Não devia acontecer. Se acontecer, esperar é melhor do que o
                // fazer nascer à frente do jogador.
                float espera = 0f;
                while (!AnchorHidden() && espera < 4f) { espera += Time.deltaTime; yield return null; }
            }

            rui.SetHoldFacing(true);      // aqui ele **encara** — está a falar contigo
            rui.HoldAt(ruiDoorwayAnchor);

            yield return Say(firstLine);
            yield return new WaitForSeconds(beforeTurnSeconds);

            if (sound != null && stingClip != null) sound.PlayOneShot(stingClip, stingVolume);

            // A câmara vira-se e é reafirmada todos os frames: um ForceLookAt escreve
            // uma vez e a câmara volta ao corpo no frame seguinte.
            var look = FindObjectOfType<CameraPhysics>();
            Vector3 head = ruiDoorwayAnchor.position + Vector3.up * 1.55f;
            float t = 0f;
            if (input != null) input.SetLookSuppressed(true);
            while (t < cameraLockSeconds)
            {
                t += Time.deltaTime;
                if (look != null) look.ForceLookAt(head);
                yield return null;
            }
            if (input != null) input.SetLookSuppressed(false);

            yield return Say(secondLine);

            rui.enabled = true;
            rui.ResumeRoutine();

            hud.SetObjective(objectiveWhenDone);
            NarrativeBlackboard.Instance?.OnEvent("day1_balcony");
            Debug.Log("[RebootBalconyBreak] Pausa na varanda cumprida.", this);
        }

        private bool AnchorHidden()
        {
            if (playerCamera == null) return true;
            Vector3 chest = ruiDoorwayAnchor.position + Vector3.up * 1.2f;
            Vector3 view = playerCamera.WorldToViewportPoint(chest);
            if (view.z <= 0f || view.x < -0.05f || view.x > 1.05f || view.y < -0.05f || view.y > 1.05f)
                return true;
            Vector3 delta = chest - playerCamera.transform.position;
            return Physics.Raycast(playerCamera.transform.position, delta.normalized,
                delta.magnitude - 0.05f, ~0, QueryTriggerInteraction.Ignore);
        }

        private IEnumerator Say(string line)
        {
            if (dialogue == null || string.IsNullOrWhiteSpace(line)) yield break;
            dialogue.ShowReaction(speaker, line, null, lineSeconds, null, autoClose: true);
            while (dialogue.IsBusy) yield return null;
        }
    }
}
