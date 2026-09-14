using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O jogo abre com o Tomas sentado a secretaria, em frente ao portatil.
    ///
    /// Antes abria com ele deitado, e a linha da ligacao que cai chegava do nada:
    /// ninguem da pela internet a ir abaixo a dormir. Sentado ao portatil as duas
    /// e quarenta e sete, e ele proprio que ve a ligacao morrer — e so depois e que
    /// se levanta. A mensagem do Rui passa a ser resposta a uma coisa que o jogador
    /// viu acontecer.
    ///
    /// Nao mexe no CharacterController a nao ser para o desligar enquanto ele esta
    /// sentado (senao a cadeira empurra-o) e para o repor de pe no fim.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDeskOpening : MonoBehaviour
    {
        [Tooltip("Pivo que da a altura dos olhos (ViewYaw).")]
        [SerializeField] private Transform cameraPivot;
        [Tooltip("Transform da propria camara. A inclinacao vai aqui porque o "
               + "CameraPhysics reescreve a rotacao de todos os pivos acima dela.")]
        [SerializeField] private Transform tiltTransform;
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private OpeningQuestDirector quest;
        [Tooltip("Texto de abertura. A cena corre por tras do preto, por isso a "
               + "encenacao a secretaria so comeca a contar depois de ele sair.")]
        [SerializeField] private IntroTextSequence intro;
        [Tooltip("Componentes suspensos enquanto ele ainda esta sentado.")]
        [SerializeField] private MonoBehaviour[] suppressed;

        [Header("Portatil")]
        [Tooltip("Luz do ecra do portatil. Apaga-se quando a ligacao cai.")]
        [SerializeField] private Light laptopGlow;
        [Tooltip("O anuncio das pecas no ecra. Desaparece quando a ligacao cai — "
               + "a perda ve-se em vez de ser anunciada.")]
        [SerializeField] private Pungent.Atmosphere.LaptopScreenImage laptopImage;
        [SerializeField] private PrototypeHUD hud;
        [Tooltip("Telemovel na mao. Sentado, a camara esta baixa e inclinada para a "
               + "secretaria, e o telemovel atravessa o tampo. Ele ainda o tem no "
               + "bolso — so o tira quando se levanta.")]
        [SerializeField] private GameObject heldPhone;
        [Tooltip("Anuncio da hora e da queda da ligacao. Aparece no momento em que "
               + "acontece, e nao no arranque da cena por tras do texto de abertura.")]
        [SerializeField] private string dropMessage = "02:47 — Connection lost";
        [SerializeField] private Color connectedGlow = new Color(0.12f, 0.38f, 0.72f);
        [SerializeField] private Color deadGlow = new Color(0.30f, 0.30f, 0.33f);

        [Header("Pose sentada")]
        [Tooltip("Onde o corpo fica enquanto ele esta na cadeira.")]
        [SerializeField] private Vector3 seatedPosition = new Vector3(-4.45f, 0.05f, -4.00f);
        [Tooltip("Para onde ele se afasta ao levantar-se, para nao ficar dentro da secretaria.")]
        [SerializeField] private Vector3 standingPosition = new Vector3(-4.45f, 0.05f, -3.35f);
        [SerializeField] private float seatedYaw = 180f;
        [SerializeField] private float seatedHeight = 1.18f;
        [Tooltip("Olhar caido sobre o ecra do portatil.")]
        [SerializeField] private Vector3 seatedEuler = new Vector3(24f, 0f, 0f);

        [Header("Ritmo")]
        [Tooltip("Quanto tempo ele fica a olhar para o ecra antes de a ligacao cair.")]
        [SerializeField, Min(0f)] private float secondsBeforeDrop = 2.4f;
        [Tooltip("Pausa entre a ligacao cair e o jogo aceitar que ele se levante.")]
        [SerializeField, Min(0f)] private float secondsAfterDrop = 1.4f;
        [SerializeField, Min(0.3f)] private float standSeconds = 2.2f;
        [SerializeField] private bool playOnStart = true;

        private enum Phase { Seated, Standing, Done }

        [Header("Quem se senta")]
        [Tooltip("Corpo do jogador. Vazio = este objecto, que era o caso quando isto "
               + "vivia dentro do PlayerRoot.")]
        [SerializeField] private Transform player;

        private Phase phase = Phase.Done;
        private float phaseTime;
        private Vector3 standingLocalPosition;
        private Quaternion standingLocalRotation;
        private CharacterController body;
        private bool connectionDropped;

        /// <summary>
        /// Onde a encenacao mexe.
        ///
        /// Isto escrevia em `transform` a contar estar no mesmo objecto que o
        /// jogador. Quando o `PlayerRoot` se partiu em jogador e sistemas, esta
        /// encenacao passou para os sistemas do apartamento e `transform` deixou de
        /// ser o corpo do Tomas: a abertura sentava um objecto vazio a secretaria e
        /// deixava o jogador de pe no meio da sala, sem erro nenhum.
        /// </summary>
        private Transform Body => player != null ? player : transform;

        private void Start()
        {
            if (playOnStart) Begin();
        }

        /// <summary>
        /// Arranca a abertura a secretaria.
        ///
        /// Publico porque o prologo tem de correr antes disto: com o playOnStart
        /// ligado, o jogo comecava as 02:47 ao mesmo tempo que o Tomas estava a
        /// desfazer as caixas na noite em que se mudou.
        /// </summary>
        public void Begin()
        {
            if (cameraPivot == null) { enabled = false; return; }

            standingLocalPosition = cameraPivot.localPosition;
            standingLocalRotation = tiltTransform != null
                ? tiltTransform.localRotation
                : cameraPivot.localRotation;

            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor != null) player = motor.transform;
            }

            body = Body.GetComponent<CharacterController>();
            if (intro == null) intro = GetComponentInChildren<IntroTextSequence>(true);
            Body.GetComponent<Pungent.Player.PlayerMotor>()?.StopImmediately();
            SetSuppressed(true);

            // O controller fica ligado. Desliga-lo evitava que ele fosse empurrado,
            // mas o PlayerMotor apanha sempre um frame antes de ser suspenso outra
            // vez e enchia a consola de "Move called on inactive controller". Um
            // CharacterController so se mexe quando lhe chamam Move, e com o motor
            // suspenso ninguem lhe chama.
            Body.position = seatedPosition;
            Body.rotation = Quaternion.Euler(0f, seatedYaw, 0f);

            // **E a camara com ele.** Rodar o corpo nao roda o olhar: o yaw vive no
            // `viewYaw` e quem manda nele e o `CameraPhysics`. Sem esta linha, o
            // Tomas era sentado a secretaria virado a sul — que e onde a secretaria
            // esta — e abria os olhos virado para a cama, porque era para la que
            // estava a olhar quando o Dia 1 acabou.
            Body.GetComponent<Pungent.Player.CameraPhysics>()?.AlignToBody();

            if (laptopGlow != null)
            {
                laptopGlow.enabled = true;
                laptopGlow.color = connectedGlow;
            }

            // A pagina esta aberta desde antes de o jogo comecar.
            laptopImage?.SetOn(true);

            if (heldPhone != null) heldPhone.SetActive(false);

            phase = Phase.Seated;
            phaseTime = 0f;
            ApplySeated();
        }

        private void ApplySeated()
        {
            Vector3 local = standingLocalPosition;
            local.y = seatedHeight;
            cameraPivot.localPosition = local;
            if (tiltTransform != null) tiltTransform.localRotation = Quaternion.Euler(seatedEuler);
        }

        private void Update()
        {
            if (phase == Phase.Done) return;

            // Reafirmado todos os frames de proposito. O texto de abertura tambem
            // suspende estes componentes e volta a liga-los quando acaba — se so
            // desligassemos uma vez no Start, o jogador ficava a andar pela casa
            // ainda sentado a secretaria e com o CharacterController desligado.
            SetSuppressed(true);

            switch (phase)
            {
                case Phase.Seated: UpdateSeated(); break;
                case Phase.Standing: UpdateStanding(); break;
            }
        }

        /// <summary>
        /// O texto de abertura liga os componentes de volta a meio do seu proprio
        /// Update. Reafirmar aqui, depois de todos os Updates, garante que no frame
        /// seguinte ja estao suspensos antes de o motor correr.
        /// </summary>
        private void LateUpdate()
        {
            if (phase != Phase.Done) SetSuppressed(true);
        }

        private void UpdateSeated()
        {
            // Ele ja esta sentado no lugar certo, mas o relogio so arranca quando o
            // jogador puder ver alguma coisa. Sem isto a ligacao caia atras do ecra
            // preto e ele chegava ao jogo ja de pe.
            if (intro != null && intro.IsRunning) return;

            phaseTime += Time.deltaTime;

            if (!connectionDropped && phaseTime >= secondsBeforeDrop)
            {
                connectionDropped = true;
                // O ecra fica aceso mas morto: e o sinal de que caiu a ligacao e
                // nao a luz da casa.
                if (laptopGlow != null) laptopGlow.color = deadGlow;
                // O anuncio desaparece. E o unico momento do jogo em que se perde
                // alguma coisa a vista do jogador em vez de lhe ser contada.
                laptopImage?.SetOn(false);
                if (hud != null && !string.IsNullOrEmpty(dropMessage))
                    hud.ShowMessage(dropMessage, 4f);
                thoughts?.Think("desk_01", "Two forty-seven. And the page just died on me.", 5, true, 3.2f);
            }

            if (phaseTime < secondsBeforeDrop + secondsAfterDrop) return;
            if (!AnyKey()) return;

            phase = Phase.Standing;
            phaseTime = 0f;
        }

        private void UpdateStanding()
        {
            phaseTime += Time.deltaTime;
            float t = Mathf.Clamp01(phaseTime / standSeconds);
            // Devagar a arrancar e a acabar: levantar-se custa.
            float eased = t * t * (3f - 2f * t);

            Vector3 seated = standingLocalPosition;
            seated.y = seatedHeight;

            cameraPivot.localPosition = Vector3.Lerp(seated, standingLocalPosition, eased);
            if (tiltTransform != null)
                tiltTransform.localRotation = Quaternion.Slerp(
                    Quaternion.Euler(seatedEuler), standingLocalRotation, eased);

            // Ao mesmo tempo afasta-se da secretaria, senao levantava-se dentro dela.
            Body.position = Vector3.Lerp(seatedPosition, standingPosition, eased);

            if (t < 1f) return;

            phase = Phase.Done;
            cameraPivot.localPosition = standingLocalPosition;
            if (tiltTransform != null) tiltTransform.localRotation = standingLocalRotation;
            Body.position = standingPosition;
            if (body != null && !body.enabled) body.enabled = true;
            if (heldPhone != null) heldPhone.SetActive(true);

            SetSuppressed(false);
            quest?.NotifyWokeUp();

            thoughts?.Think("desk_02", "The whole connection dropped. Of course it did.", 4, true, 3.2f);
        }

        private static bool AnyKey()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
        }

        private void SetSuppressed(bool value)
        {
            if (suppressed == null) return;
            foreach (var component in suppressed)
                if (component != null)
                    component.enabled = !value;
        }
    }
}
