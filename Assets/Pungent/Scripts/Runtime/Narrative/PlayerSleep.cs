using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Deitar-se e fechar os olhos. Fecha o dia.
    ///
    /// Segue o molde do <see cref="PlayerDeskOpening"/> — nao ha um unico clip de
    /// animacao no projecto, por isso o corpo do jogador e a camara: baixa-se o
    /// pivo, inclina-se o olhar para o tecto e desliza-se ate a cama. Feito com as
    /// mesmas curvas suaves de levantar da secretaria, para os dois momentos
    /// parecerem a mesma pessoa.
    ///
    /// Nao deixa dormir com a porta destrancada nem com a luz acesa. O aviso e um
    /// pensamento e nao um bloqueio mudo: o jogador tem de perceber o que falta,
    /// mas tem mesmo de o fazer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSleep : MonoBehaviour, IPlayerInteractable
    {
        [Header("Jogador")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Transform tiltTransform;
        [SerializeField] private MonoBehaviour[] suppressed;
        [SerializeField] private GameObject heldPhone;

        [Header("Ligacoes")]
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private PrototypeHUD hud;
        [SerializeField] private OpeningQuestDirector quest;

        [Header("Condicoes")]
        [Tooltip("Tem de estar trancada. Vazio = nao exige porta.")]
        [SerializeField] private DoorDragInteractable bedroomDoor;
        [Tooltip("Tem de estar todas apagadas.")]
        [SerializeField] private Light[] lightsThatMustBeOff;

        [Header("Deitar")]
        [Tooltip("Onde o corpo fica, em cima da cama.")]
        [SerializeField] private Vector3 lyingPosition = new Vector3(-5.90f, 0.05f, -3.70f);
        [SerializeField] private float lyingYaw = 90f;
        [Tooltip("Altura do olhar deitado. De pe anda pelos 1.6.")]
        [SerializeField, Min(0.1f)] private float lyingHeight = 0.62f;
        [Tooltip("Olhar para o tecto.")]
        [SerializeField] private Vector3 lyingEuler = new Vector3(-62f, 0f, 0f);
        [SerializeField, Min(0.3f)] private float lieDownSeconds = 3.4f;

        [Header("Fechar os olhos")]
        [SerializeField, Min(0.2f)] private float eyesCloseSeconds = 2.6f;
        [Tooltip("Pausa de ecra preto antes de o dia acabar.")]
        [SerializeField, Min(0f)] private float blackHoldSeconds = 1.8f;

        private enum Phase { Idle, LyingDown, ClosingEyes, Black, Done }

        private Phase phase = Phase.Idle;
        private float phaseTime;
        private Vector3 standingLocalPosition;
        private Quaternion standingLocalRotation;

        /// <summary>De onde o corpo partiu. Ver <see cref="UpdateLyingDown"/>.</summary>
        private Vector3 startPosition;
        private Quaternion startRotation;

        /// <summary>
        /// Os dois que continuavam a trabalhar com ele deitado.
        ///
        /// Nao estao no `suppressed` da cena e nao vale a pena la os por a mao: sao
        /// sempre os mesmos dois e sao sempre no jogador. Ver `Begin`.
        /// </summary>
        private Behaviour footsteps;
        private Behaviour cameraPhysics;
        private bool footstepsWas;
        private bool cameraPhysicsWas;

        /// <summary>Tem o corpo do jogador emprestado. Ver <see cref="ReleaseHold"/>.</summary>
        private bool held;

        private CharacterController body;
        private bool bodyWas;

        [Tooltip("As palpebras. Partilhadas com o acordar do dia seguinte.")]
        [SerializeField] private ScreenEyelid eyelid;

        /// <summary>Disparado quando o dia fecha mesmo. E aqui que o dia seguinte pega.</summary>
        public event System.Action DaySlept;

        public bool HasSlept { get; private set; }

        /// <summary>
        /// As condicoes de dormir — quest no passo certo, porta trancada, luz
        /// apagada — valem para a noite das 02:47. No prologo o Tomas acabou de se
        /// mudar: a quest ainda nao existe, a fechadura nunca foi armada, e obrigar
        /// a trancar a porta na primeira noite dizia ao jogador que tivesse medo
        /// antes de haver do que ter.
        /// </summary>
        [SerializeField] private bool gatesEnabled = true;

        public void SetGatesEnabled(bool value) => gatesEnabled = value;

        /// <summary>
        /// Prepara a mesma cama para a noite seguinte.
        ///
        /// O prologo e a noite das 02:47 usam o mesmo <see cref="PlayerSleep"/>.
        /// Depois do primeiro sono o componente ficava em <c>Done</c>, sem prompt e
        /// sem maneira de voltar a arrancar. O jogador chegava ao segundo "Sleep"
        /// com o ecrã e a cama aparentemente mortos.
        /// </summary>
        public void ResetForNextNight()
        {
            phase = Phase.Idle;
            phaseTime = 0f;
            HasSlept = false;
            ReleaseHold();
        }

        public string Prompt
        {
            get
            {
                if (phase != Phase.Idle) return string.Empty;

                // **Um porteiro desligado nao tranca nada.**
                //
                // Este portao esperava que o `OpeningQuestDirector` chegasse ao
                // passo `LockDoor`. So que o director e **desligado no arranque do
                // Play** — quem manda nos objectivos passou a ser o
                // `ChapterDirector` — e um componente desligado nunca avanca de
                // passo. `Current` ficava em `WakeUp` para sempre.
                //
                // O resultado a jogar: o jogo diz "apaga a luz e vai dormir", e a
                // cama so oferece "Look at the bed". Nao havia maneira nenhuma de
                // passar dali, e o capitulo acabava ali para sempre.
                //
                // Deixa de ser um portao e passa a ser uma parede no momento em que
                // ninguem esta a tratar dele. Entao so conta enquanto ele estiver
                // mesmo a correr: se alguem voltar a ligar o director, o portao
                // volta a funcionar como antes e nada disto muda.
                if (gatesEnabled && quest != null && quest.isActiveAndEnabled &&
                    quest.Current < OpeningQuestDirector.Step.LockDoor)
                    return string.Empty;

                return "Sleep";
            }
        }

        public bool HoldToInteract => false;

        private void Awake()
        {
            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor != null) player = motor.transform;
            }
            if (quest == null) quest = FindObjectOfType<OpeningQuestDirector>();
            if (hud == null) hud = FindObjectOfType<PrototypeHUD>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            if (eyelid == null) eyelid = FindObjectOfType<ScreenEyelid>();

            if (player != null)
            {
                footsteps = player.GetComponent<Pungent.Audio.PlayerFootstepAudio>();
                cameraPhysics = player.GetComponent<Pungent.Player.CameraPhysics>();
            }
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (phase != Phase.Idle) return;

            if (!gatesEnabled) { Begin(); return; }

            if (quest != null && quest.isActiveAndEnabled &&
                quest.Current < OpeningQuestDirector.Step.LockDoor) return;

            // As duas condicoes sao obrigatorias. O pensamento diz o que falta em
            // vez de o clique simplesmente nao fazer nada.
            if (bedroomDoor != null && !bedroomDoor.IsLocked)
            {
                thoughts?.Think("sleep_door", "Not with the door like that. Lock it first.",
                    5, false, 3.0f);
                return;
            }

            var lit = FirstLightOn();
            if (lit != null)
            {
                // Nomeia a luz. "I am not sleeping with the light on" com duas luzes
                // no quarto e uma frase que nao diz qual — e o jogador apaga a que
                // ve primeiro e volta a levar com a mesma frase.
                thoughts?.Think("sleep_light",
                    lit.name == "Laptop_Glow"
                        ? "Not with the laptop still on."
                        : "I am not sleeping with the light on.",
                    5, false, 3.0f);
                return;
            }

            // **Deixa rasto quando deixa passar.**
            //
            // Isto ja passou uma vez com a luz do tecto acesa, e nao houve maneira
            // de o reproduzir a seguir — nem a olho, nem por injeccao de estado. Um
            // portao que nao deixa rasto quando cede so se investiga por adivinhacao,
            // e ja se gastou uma sessao inteira nisso. Uma linha por noite nao custa
            // nada e da a resposta a primeira vez que voltar a acontecer.
            Debug.Log("[Dormir] Autorizado. Porta trancada=" +
                      (bedroomDoor == null ? "sem porta" : bedroomDoor.IsLocked.ToString()) +
                      ", luzes acesas no raio de " + roomRadius.ToString("F1") + " m: nenhuma. " +
                      "Lista explicita: " + (lightsThatMustBeOff == null ? 0 : lightsThatMustBeOff.Length) +
                      " luzes.", this);

            Begin();
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

        /// <summary>
        /// Alguma luz acesa no quarto?
        ///
        /// ---
        ///
        /// **Deixou de ser uma lista a mao.** Era uma so referencia —
        /// `LIGHTING/Bedroom_Light` — e o quarto do Tomas tem **duas** luzes: a do
        /// tecto e o brilho do portatil em cima da secretaria, com 1,6 m de alcance.
        /// Apagar a de cima deixava o quarto iluminado pela outra e o jogo deixava
        /// deitar. Uma lista escrita a mao esquece-se da segunda lampada que alguem
        /// acrescentar, e o sintoma e este: uma regra que o jogador ve a ser
        /// desmentida pelos proprios olhos.
        ///
        /// Passa a varrer o que estiver **dentro do quarto**, com a lista a servir de
        /// acrescento para o que estiver fora dele. Auto-suficiente: quem puser uma
        /// terceira luz ali dentro nao tem de saber que este componente existe.
        ///
        /// **A lanterna do telemovel nao conta.** Vive no jogador, anda com ele, e
        /// exigir que a apague para se deitar seria exigir uma coisa que ele nao
        /// associa a "a luz do quarto".
        /// </summary>
        private bool AnyLightOn() => FirstLightOn() != null;

        private Light FirstLightOn()
        {
            if (lightsThatMustBeOff != null)
                foreach (var light in lightsThatMustBeOff)
                    if (light != null && light.enabled && light.gameObject.activeInHierarchy)
                        return light;

            if (roomRadius <= 0f) return null;

            Vector3 centre = lyingPosition;
            foreach (var light in FindObjectsOfType<Light>())
            {
                if (light.type == LightType.Directional) continue;
                if (!light.enabled || !light.gameObject.activeInHierarchy) continue;

                // A lanterna do telemovel e tudo o que ande no jogador.
                if (player != null && light.transform.IsChildOf(player)) continue;

                if (Vector3.Distance(light.transform.position, centre) > roomRadius) continue;
                return light;
            }

            return null;
        }

        [Tooltip("Raio a volta da cama onde uma luz acesa impede dormir. Zero = so "
               + "conta a lista acima.\n\nTres metros e meio cobrem o quarto e nao "
               + "chegam ao corredor: apagar a casa toda para se deitar seria uma "
               + "tarefa, e as tarefas deste jogo tem prompt.")]
        [SerializeField, Min(0f)] private float roomRadius = 3.5f;

        private void Begin()
        {
            if (cameraPivot == null || player == null) return;

            standingLocalPosition = cameraPivot.localPosition;
            standingLocalRotation = tiltTransform != null
                ? tiltTransform.localRotation
                : cameraPivot.localRotation;

            // De onde ele parte, guardado uma vez. Ver `UpdateLyingDown`.
            startPosition = player.position;
            startRotation = player.rotation;
            held = true;

            player.GetComponent<Pungent.Player.PlayerMotor>()?.StopImmediately();

            // **O `CharacterController` sobrepoe-se a escritas directas no
            // transform**, e e por isso que ele nunca chegava a cama.
            //
            // Medido: com ele ligado, o corpo parava em (-4,45 / -4,00) a caminho de
            // (-5,90 / -3,70) — travado pela propria cama, a metro e meio do
            // travesseiro. O resto da encenacao continuava na mesma, e a camara
            // acabava a olhar para o tecto **de ao lado da cama**. Era isso que se
            // via, e nao uma curva mal escolhida.
            //
            // E a mesma linha que o `ClimaxStage`, o `HidingSpot` e o `RuiHunt` ja
            // tinham: desligar antes de escrever, ligar depois.
            body = player.GetComponent<CharacterController>();
            if (body != null) { bodyWas = body.enabled; body.enabled = false; }

            SetSuppressed(true);

            // **Os passos e o abanao da cabeca, que ninguem estava a calar.**
            //
            // O `suppressed` da cena tem o motor, o interactor e o telemovel — e
            // mais nenhum. Sao os dois que restam que fazem o defeito: ambos medem
            // o **deslocamento** do jogador, e este componente esta a deslocar o
            // jogador ate a cama. Quem carregasse em Sleep a andar ficava deitado
            // com a camara a abanar e os proprios passos a tocar por baixo.
            //
            // Apanhados pelo componente e nao pela lista da cena: sao sempre os
            // mesmos dois, sao sempre no jogador, e uma lista a mao esquece-se.
            if (footsteps != null) { footstepsWas = footsteps.enabled; footsteps.enabled = false; }
            if (cameraPhysics != null) { cameraPhysicsWas = cameraPhysics.enabled; cameraPhysics.enabled = false; }

            if (heldPhone != null) heldPhone.SetActive(false);
            hud?.SetPrompt(string.Empty);
            hud?.SetObjective(string.Empty);
            hud?.SetSideObjective(string.Empty);

            phase = Phase.LyingDown;
            phaseTime = 0f;
        }

        private void Update()
        {
            if (phase == Phase.Idle || phase == Phase.Done) return;

            // Reafirmado como no PlayerDeskOpening: outros directores voltam a ligar
            // estes componentes a meio dos seus proprios Updates.
            SetSuppressed(true);
            phaseTime += Time.deltaTime;

            switch (phase)
            {
                case Phase.LyingDown: UpdateLyingDown(); break;
                case Phase.ClosingEyes: UpdateClosingEyes(); break;
                case Phase.Black: UpdateBlack(); break;
            }
        }

        private void LateUpdate()
        {
            if (phase != Phase.Idle && phase != Phase.Done) SetSuppressed(true);
        }

        private void UpdateLyingDown()
        {
            float t = Mathf.Clamp01(phaseTime / lieDownSeconds);
            float eased = t * t * (3f - 2f * t);

            Vector3 lying = standingLocalPosition;
            lying.y = lyingHeight;
            cameraPivot.localPosition = Vector3.Lerp(standingLocalPosition, lying, eased);

            if (tiltTransform != null)
                tiltTransform.localRotation = Quaternion.Slerp(
                    standingLocalRotation, Quaternion.Euler(lyingEuler), eased);

            // **Do sitio onde ele estava ate a cama, e chega la.**
            //
            // Isto era `Lerp(player.position, alvo, eased * 0.35f)` — interpolar a
            // partir da **posicao actual** todos os frames. Sao dois defeitos num:
            // depende da cadencia de frames, e nunca chega ao destino, so se
            // aproxima dele. O corpo acabava ao lado da cama, a escorregar, com uma
            // curva que muda conforme a maquina. Guardando de onde ele partiu, o
            // `eased` faz o trabalho todo e a chegada e exacta.
            player.position = Vector3.Lerp(startPosition, lyingPosition, eased);
            player.rotation = Quaternion.Slerp(startRotation,
                Quaternion.Euler(0f, lyingYaw, 0f), eased);

            if (t < 1f) return;

            phase = Phase.ClosingEyes;
            phaseTime = 0f;
            thoughts?.Think("sleep_done", "That is enough for one night.", 5, true, 2.6f);
        }

        /// <summary>
        /// As palpebras. Duas piscadelas antes de fechar de vez: fechar de uma so
        /// vez le-se como um corte de camara, e o que se quer e cansaco.
        /// </summary>
        private void UpdateClosingEyes()
        {
            float t = Mathf.Clamp01(phaseTime / eyesCloseSeconds);
            float blink = Mathf.Sin(t * Mathf.PI * 3f) * 0.18f * (1f - t);
            if (eyelid != null) eyelid.Amount = t * t + Mathf.Max(0f, blink);

            if (t < 1f) return;

            if (eyelid != null) eyelid.Amount = 1f;
            phase = Phase.Black;
            phaseTime = 0f;
        }

        private void UpdateBlack()
        {
            if (phaseTime < blackHoldSeconds) return;

            phase = Phase.Done;
            HasSlept = true;

            // **Devolve o que pediu emprestado, e devolve ANTES de passar o dia.**
            //
            // Ate aqui nao devolvia nada: o pivo da camara ficava a altura de
            // deitado, os componentes do jogador ficavam suprimidos e o telemovel
            // desactivado. Quem limpava isso era o `PrologueStage.ResetForNextNight`
            // — que so corre no fim do prologo. Depois do sono das 02:47 nao havia
            // ninguem, e o Dia 3 herdava a casa toda torta.
            //
            // O pior era o pivo. O `DayTwoDirector` comeca por **guardar a altura
            // actual como sendo a de pe** para poder voltar a ela no fim; comecando
            // com o pivo ja baixado, ele guardava 0,62 e era a 0,62 que repunha o
            // jogador. O Dia 3 inteiro corria com o Tomas a olhar da altura de uma
            // crianca, e nada na consola dizia porque.
            //
            // Feito **antes** do `DaySlept` de proposito: quem pegar no dia a seguir
            // encontra o jogador de pe e monta o que quiser por cima — e e
            // exactamente o que o acordar faz, que volta a baixar o pivo com as suas
            // proprias maos.
            ReleaseHold();

            quest?.NotifySlept();
            DaySlept?.Invoke();
        }

        /// <summary>
        /// Repoe o jogador como estava antes de se deitar.
        ///
        /// Nao mexe na `phase` nem no <see cref="HasSlept"/>: isto e devolver o
        /// corpo, nao rearmar a cama. Quem rearma e o
        /// <see cref="ResetForNextNight"/>.
        /// </summary>
        private void ReleaseHold()
        {
            // **So devolve o que chegou a pedir.**
            //
            // O `ResetForNextNight` e chamado pelo fim do prologo, e o fim do
            // prologo pode acontecer sem ninguem se ter deitado — a jogar nao, mas a
            // testar sim, e um dia por alguma encenacao nova. Sem esta guarda, o
            // `standingLocalPosition` ainda era (0,0,0) e isto punha a cabeca do
            // jogador nos pes dele.
            if (!held) return;
            held = false;

            if (cameraPivot != null) cameraPivot.localPosition = standingLocalPosition;
            if (tiltTransform != null) tiltTransform.localRotation = standingLocalRotation;
            else if (cameraPivot != null) cameraPivot.localRotation = standingLocalRotation;

            if (footsteps != null) footsteps.enabled = footstepsWas;
            if (cameraPhysics != null) cameraPhysics.enabled = cameraPhysicsWas;
            if (body != null) body.enabled = bodyWas;

            SetSuppressed(false);
            if (heldPhone != null) heldPhone.SetActive(true);
        }

        private void SetSuppressed(bool value)
        {
            if (suppressed == null) return;
            foreach (var component in suppressed)
                if (component != null) component.enabled = !value;
        }
    }
}
