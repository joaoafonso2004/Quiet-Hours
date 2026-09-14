
using UnityEngine.AI;
using UnityEngine;
using Pungent.Dialogue;

namespace Pungent.NPC
{
    public enum PrototypeNpcState { Routine, StareDown, Watching, Pursuing, Evicting, Returning, Acting }

    [DisallowMultipleComponent]
    public sealed class PrototypeNpcRoutine : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private Camera playerCamera;
        
        [SerializeField] private NavMeshAgent chaseAgent;
[SerializeField] private Transform player;
        [SerializeField, Min(0.1f)] private float routineSpeed = 0.85f;
        [SerializeField, Min(0f)] private float waypointPause = 2.5f;
        [SerializeField, Min(0.1f)] private float turnSpeed = 150f;
        [SerializeField, Min(0.1f)] private float stareSeconds = 1.8f;

        [Header("Memoria de olhares")]
        [Tooltip("Quantos olhares sustentados ele tolera antes de comecar a reparar "
               + "mais depressa. Os primeiros nao contam de propósito: um jogador "
               + "olha para o NPC porque e a unica pessoa da casa, e isso nao e um "
               + "padrao — e curiosidade.")]
        [SerializeField, Min(0)] private int staresBeforeNoticing = 2;

        [Tooltip("Quanto cada olhar acima do limite encurta o tempo dele.")]
        [SerializeField, Range(0f, 0.3f)] private float stareMemoryStep = 0.08f;

        [Tooltip("Ate onde isto pode chegar. **Nunca a zero.** A ameaca continua a "
               + "ser quem manda no ritmo do stare-down; isto e uma segunda cor por "
               + "cima dela e nao um segundo motor.")]
        [SerializeField, Range(0.4f, 1f)] private float stareMemoryFloor = 0.6f;
        [SerializeField, Range(0.5f, 12f)] private float gazeAngle = 3.5f;
        [SerializeField, Min(1f)] private float gazeDistance = 10f;
        [SerializeField] private bool dangerEscalationUnlocked;

        [Header("Territorio")]
        [Tooltip("Velocidade a que o NPC acorre ao quarto quando o jogador entra.")]
        [SerializeField, Min(0.5f)] private float evictSpeed = 2.4f;
        [Tooltip("Distancia a que para de avancar e encara o intruso.")]
        [SerializeField, Min(0.5f)] private float evictStopDistance = 1.8f;
        [Tooltip("Velocidade de regresso ao circuito depois de o intruso sair.")]
        [SerializeField, Min(0.3f)] private float returnSpeed = 1.1f;

        [Header("Animacao")]
        [SerializeField] private Animator animator;
        [Tooltip("Parametro float do blend tree de locomocao, em m/s.")]
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField, Min(1f)] private float speedSmoothing = 8f;

        [Header("Passos")]
        [Tooltip("Vazio = o filho `Rui_Footsteps`, que ja existe e ja tem o "
               + "passa-baixo das paredes por cima.")]
        [SerializeField] private AudioSource footsteps;
        [SerializeField] private AudioClip[] footstepClips = new AudioClip[0];
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.34f;

        [Header("Olhar")]
        [SerializeField] private HumanoidHeadLook headLook;
        [Tooltip("Distancia a que olha de relance para o jogador sem parar o que esta a fazer.")]
        [SerializeField, Min(0f)] private float idleGlanceDistance = 4.5f;
        [SerializeField, Range(0f, 1f)] private float idleGlanceWeight = 0.55f;

        private bool intruderPresent;
        private Transform intruder;
        private Vector3 lastPosition;
        private float smoothedSpeed;
        private int speedHash;
        private float actionEndsAt;
        private Quaternion actionFacing;

        private Transform leftFoot;
        private Transform rightFoot;
        private float previousLeftFootHeight;
        private float previousRightFootHeight;
        private float previousLeftFootVelocity;
        private float previousRightFootVelocity;
        private float lastStepAt;
        private bool footSamplesReady;
        private RuiHunt hunt;

        private int waypointIndex;
        private float waitRemaining;
        private float gazeTime;
        private float lostGazeTime;
        private float watchingUntil;
        private bool conversationActive;
        private bool holdFacesPlayer = true;
        private RoutineActionAnchor currentActionAnchor;
        private Vector3 actionAnchorPosition;
        private Vector3 actionOffsetPosition;
        private float actionSettle;

        /// <summary>
        /// Uma accao tem tres tempos: sentar-se, estar sentado, levantar-se. As
        /// transicoes sao opcionais — a maioria dos anchors so tem o do meio.
        /// </summary>
        private enum ActionPhase { Entering, Main, Exiting }

        private ActionPhase actionPhase;
        private float actionPhaseEndsAt;
        private bool actionPhaseMeasured;

        
        public int ThreatLevel { get; private set; }

        /// <summary>
        /// A ameaca de 0 a 1, para escalar comportamento.
        ///
        /// O plano (seccao 6.1) quer a ameaca sentida e nao mostrada: nao ha barra
        /// nem numero. O que muda e o Rui — repara em ti mais depressa, pousa menos
        /// tempo em cada sitio, e fica mais tempo a olhar depois de o fazeres. Quem
        /// esta em 1 nota que a casa mudou sem saber dizer o que.
        /// </summary>
        private float ThreatFactor => ThreatLevel / 4f;

        /// <summary>Quanto tempo aguenta antes de reparar que estas a olhar.</summary>
        private float CurrentStareSeconds =>
            Mathf.Lerp(stareSeconds, stareSeconds * 0.35f, ThreatFactor) * StareMemoryFactor;

        /// <summary>
        /// O que ele aprendeu com os olhares anteriores, e o unico sitio do jogo que
        /// le o `StarePressure`.
        ///
        /// ---
        ///
        /// **A variavel existia e nunca era lida.** O `OnStare` alimenta-a desde o
        /// prologo, o `EndingSelector` nao lhe toca, e nenhum outro sistema a
        /// consultava — estava escrita na §6.2 e no backlog como divida por pagar.
        /// Um estado oculto que ninguem le nao e um estado oculto: e um contador.
        ///
        /// ---
        ///
        /// **O que devolve nao e ameaca.** Ele nao fica mais perigoso por o teres
        /// olhado; fica mais **atento a ti**. Repara mais depressa, e mais nada. A
        /// diferenca importa e e a mesma que o `NarrativeBlackboard` ja tem escrita
        /// entre a `PlayerDefiance` e o `RuiEmboldened`: dois Ruis diferentes, e nao
        /// o mesmo Rui com o volume mais alto.
        ///
        /// **Multiplica, nao substitui.** A ameaca continua a decidir o ritmo — o
        /// `Lerp` acima e quem manda. Isto e uma segunda cor por cima, com chao,
        /// para que um jogador que olhe muito no Dia 1 nao chegue ao Dia 3 com um
        /// Rui que repara nele instantaneamente e sem explicacao possivel.
        /// </summary>
        private float StareMemoryFactor
        {
            get
            {
                var blackboard = Pungent.Narrative.NarrativeBlackboard.Instance;
                if (blackboard == null) return 1f;

                int stares = blackboard.Get(
                    Pungent.Narrative.NarrativeBlackboard.Variable.StarePressure);
                if (stares <= staresBeforeNoticing) return 1f;

                return Mathf.Max(stareMemoryFloor,
                    1f - (stares - staresBeforeNoticing) * stareMemoryStep);
            }
        }

        /// <summary>Quanto tempo pousa em cada ponto do circuito.</summary>
        private float CurrentWaypointPause =>
            Mathf.Lerp(waypointPause, waypointPause * 0.25f, ThreatFactor);

        /// <summary>Quanto tempo fica a olhar depois de desviares o olhar.</summary>
        private float CurrentWatchSeconds => Mathf.Lerp(1.5f, 4.5f, ThreatFactor);

        /// <summary>
        /// Esta a falar com o jogador agora. Lido por quem nao deve sobrepor-se a
        /// isso — os sons de vida dele, por exemplo.
        /// </summary>
        public bool InConversation => conversationActive;

        public void SetConversationActive(bool active)
        {
            conversationActive = active;
            if (active && chaseAgent != null && chaseAgent.enabled && chaseAgent.isOnNavMesh)
                chaseAgent.isStopped = true;
        }

        /// <summary>
        /// Enquanto ele esta seguro, o corpo acompanha o jogador ou fica quieto?
        ///
        /// **So a cabeca, e a diferenca nao e cosmetica.** Segurar o Rui poe-lhe o
        /// olhar em ti a peso inteiro e roda-lhe o corpo devagar atras de ti. Nas
        /// encenacoes em que ele esta a falar contigo isso e o que se quer. Parado
        /// ao fundo do corredor, a rodar sozinho para te acompanhar pela casa, ja
        /// nao: le-se como um homem que te esta a caçar, e o plano da historia diz
        /// o contrario para o Dia 1 — "o Rui comeca credivel e prestavel, nao o
        /// escrever como psicopata obvio cedo".
        ///
        /// Com o corpo quieto a mesma pose fica negavel: ele esta ali, e olha. A
        /// cabeca chega aos <c>maxYaw</c> do `HumanoidHeadLook` — 76 graus — e
        /// desiste aos 115. **Por isso a rotacao da ancora conta**: um Rui posto de
        /// costas para a divisao onde tu estas nao te segue, fica a olhar para a
        /// parede.
        ///
        /// Reposto a `true` pelo <see cref="ResumeRoutine"/>, para nao vazar de uma
        /// encenacao para a seguinte.
        /// </summary>
        public void SetHoldFacing(bool value) => holdFacesPlayer = value;

        /// <summary>
        /// Places Rui at an authored story anchor and lends control of his body to
        /// a scripted presentation. The normal routine stays enabled so its head
        /// look and player-collision rules remain active, but navigation is held
        /// until <see cref="ResumeRoutine"/> is called.
        /// </summary>
        public bool HoldAt(Transform anchor)
        {
            if (anchor == null)
            {
                Debug.LogError("[RuiRoutine] Cannot stage Rui without an anchor.", this);
                return false;
            }

            if (currentActionAnchor != null)
            {
                currentActionAnchor.onActionEnded?.Invoke(currentActionAnchor.AnimatorState);
                currentActionAnchor = null;
            }

            conversationActive = true;
            intruderPresent = false;
            intruder = null;
            State = PrototypeNpcState.Routine;
            waitRemaining = 0f;
            gazeTime = 0f;
            lostGazeTime = 0f;
            DisableAgent();

            transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            lastPosition = transform.position;
            smoothedSpeed = 0f;

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat(speedHash, 0f);
                animator.CrossFadeInFixedTime("Locomotion", 0.15f);
            }

            headLook?.SetLookWeight(1f);
            return true;
        }

        /// <summary>
        /// Devolve o corpo a rotina normal depois de uma encenacao o ter parado.
        ///
        /// Ligar apenas o componente nao chega: ele pode voltar ainda em
        /// `Evicting`, `Returning` ou `Acting`, e o NavMeshAgent pode ter ficado
        /// ligado fora da NavMesh. A rotina normal tambem usa o agente: mover o
        /// transform em linha reta fazia-o atravessar paredes ao sair do dialogo.
        /// </summary>
        public void ResumeRoutine()
        {
            if (currentActionAnchor != null)
            {
                currentActionAnchor.onActionEnded?.Invoke(currentActionAnchor.AnimatorState);
                currentActionAnchor = null;
            }

            conversationActive = false;
            holdFacesPlayer = true;   // ver `SetHoldFacing`: nao vaza para a proxima encenacao
            intruderPresent = false;
            intruder = null;
            State = PrototypeNpcState.Routine;
            waitRemaining = 0f;
            gazeTime = 0f;
            lostGazeTime = 0f;
            lastPosition = transform.position;

            if (animator != null && animator.runtimeAnimatorController != null)
                animator.CrossFadeInFixedTime("Locomotion", 0.2f);

            enabled = true;
            EnableAgent(routineSpeed);
        }

        /// <summary>
        /// Uma resposta ao Rui.
        ///
        /// Respostas calmas ou honestas nao sobem nada. Fugir a pergunta deixa uma
        /// marca pequena; ser cortante ou ignorar deliberadamente o Rui pesa mais.
        /// Assim a conversa do prologo ja constroi a relacao, mas ser educado nao
        /// enche uma barra invisivel aconteca o que acontecer.
        /// </summary>
        public void ApplyDialogueChoice(DialogueTone tone)
        {
            int amount;
            switch (tone)
            {
                case DialogueTone.Edgy:
                    amount = 2;
                    break;
                case DialogueTone.Silent:
                case DialogueTone.Evasive:
                    amount = 1;
                    break;
                default:
                    amount = 0;
                    break;
            }

            RaiseThreat(amount);

            // O blackboard decide por si quais tons contam para os finais. Aqui
            // passa-se o tom real, em vez de transformar tudo num booleano.
            Pungent.Narrative.NarrativeBlackboard.Instance?.OnChoice(tone);
        }

        /// <summary>Compatibilidade com conversas antigas que ainda so distinguem dois tons.</summary>
        public void ApplyDialogueChoice(bool aggressive)
        {
            ApplyDialogueChoice(aggressive ? DialogueTone.Edgy : DialogueTone.Calm);
        }

        /// <summary>
        /// Ignorar uma pergunta presencial faz o Rui parar e encarar o Tomas durante
        /// a propria resposta. Nao muda o estado da rotina: no prologo ela esta
        /// suspensa, mas o Animator continua disponivel.
        /// </summary>
        public void BeginDialogueStare()
        {
            PlayStare();
            headLook?.SetLookWeight(1f);
        }

        public void EndDialogueStare()
        {
            LeaveStare();
        }

        /// <summary>
        /// Sobe a ameaca oculta. Nao e so o dialogo que a alimenta: insistir em
        /// estar onde nao se deve conta tanto como responder mal.
        /// </summary>
        public void RaiseThreat(int amount)
        {
            if (amount <= 0) return;
            ThreatLevel = Mathf.Clamp(ThreatLevel + amount, 0, 4);
            dangerEscalationUnlocked = ThreatLevel >= 3;
        }

        /// <summary>
        /// Sinaliza que o jogador entrou ou saiu de um espaco que este NPC considera seu.
        /// Enquanto o intruso la estiver, o NPC acorre e nao retoma o circuito.
        /// </summary>
        public void SetIntruderPresent(bool present, Transform who)
        {
            if (who != null) intruder = who;
            if (present == intruderPresent) return;
            intruderPresent = present;

            if (present)
            {
                if (State == PrototypeNpcState.Pursuing) return; // a perseguicao tem prioridade

                // Se estava a meio de uma acao contextual, tem de sair dela: senao
                // acorreria ao quarto ainda com a pose de sentado no sofa.
                if (State == PrototypeNpcState.Acting && animator != null
                    && animator.runtimeAnimatorController != null)
                {
                    animator.CrossFadeInFixedTime("Locomotion", 0.2f);
                    if (currentActionAnchor != null)
                    {
                        currentActionAnchor.onActionEnded?.Invoke(currentActionAnchor.AnimatorState);
                        currentActionAnchor = null;
                    }
                }

                State = PrototypeNpcState.Evicting;
                EnableAgent(evictSpeed);
            }
            else if (State == PrototypeNpcState.Evicting)
            {
                State = EnableAgent(returnSpeed) ? PrototypeNpcState.Returning : PrototypeNpcState.Routine;
            }
        }

        /// <summary>
        /// A distancia a que ele para do intruso **agora**.
        ///
        /// Sao duas distancias e nao uma, e confundi-las era o murro no ar.
        ///
        /// A de encarar — o `evictStopDistance`, 1,8 m — e a certa enquanto ele so
        /// esta a avisar: ele entra no quarto, para a dois metros e olha. Ficar mais
        /// perto do que isso seria ele a tocar no jogador antes de haver motivo, e
        /// por isso o <see cref="FaceIntruder"/> ate **recua** se a distancia
        /// encolher.
        ///
        /// A de bater e outra. Um clip de murro tocado a 1,8 m de distancia e um
        /// homem a esmurrar o ar a dois passos de alguem, e o `NpcRoomTerritory`
        /// media a chegada com `evictStopDistance + 0,35` = 2,15 m — ainda mais
        /// longe do que a de encarar. **A expulsao nunca chegou a ter contacto
        /// nenhum.**
        ///
        /// Quem manda na troca e a encenacao, e nao a rotina: o territorio abre a
        /// distancia de bater quando decide expulsar, e fecha-a a seguir.
        /// </summary>
        private float EffectiveStopDistance => strikeDistance > 0f ? strikeDistance : evictStopDistance;

        private float strikeDistance = -1f;

        /// <summary>
        /// Deixa-o fechar ate a distancia de bater. Chamado pela encenacao da
        /// expulsao, e desfeito por <see cref="EndStrikeApproach"/>.
        /// </summary>
        public void BeginStrikeApproach(float distance) => strikeDistance = Mathf.Max(0.4f, distance);

        public void EndStrikeApproach() => strikeDistance = -1f;

        /// <summary>Distancia ao intruso, para quem precise de saber se ja o alcancou.</summary>
        public bool HasReachedIntruder()
        {
            Transform target = intruder != null ? intruder : player;
            if (target == null) return false;

            // A folga larga so serve a fase de encarar, onde meio metro nao muda
            // nada. A fechar para bater, meio metro e a diferenca entre acertar e
            // nao acertar.
            float slack = strikeDistance > 0f ? 0.15f : 0.35f;
            return Vector3.Distance(transform.position, target.position) <= EffectiveStopDistance + slack;
        }

        /// <summary>
        /// Mede o deslocamento real e alimenta o blend tree. Assim funciona igual
        /// quer o NPC seja movido pela rotina (MoveTowards) quer pelo NavMeshAgent.
        /// </summary>
        private void UpdateLocomotionSpeed()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;

            float instant = delta.magnitude / dt;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, instant, 1f - Mathf.Exp(-speedSmoothing * dt));

            if (animator != null && animator.runtimeAnimatorController != null)
                animator.SetFloat(speedHash, smoothedSpeed);
        }

        /// <summary>
        /// As passadas dele a viver a casa.
        ///
        /// A fonte (`Rui_Footsteps`), os dez clips e o passa-baixo das paredes ja
        /// existiam todos — mas so o `RuiHunt` os tocava, e o `RuiHunt` so acorda no
        /// climax do Dia 5. Nos quatro dias antes disso o Rui atravessava o
        /// apartamento em silencio absoluto: via-se uma pessoa a andar e nao se ouvia
        /// nada. Nenhum room tone substitui isto — saber que ele esta a andar pela
        /// casa, sem o ver, e a tensao toda deste jogo.
        ///
        /// **O dono e um so.** Enquanto a caca corre, os passos sao dela: acrescentar
        /// aqui um segundo dono do mesmo som e o erro que este projecto ja pagou uma
        /// vez em audio — dois passos por cada passo — e que esta escrito por extenso
        /// no `RuiPresenceAudio`. Este toca pela velocidade medida, exactamente como o
        /// The old version used a timer, so it could keep the right cadence while
        /// landing between the visible foot contacts. This version detects the
        /// bottom of each foot's real animated descent in LateUpdate.
        /// </summary>
        private void UpdateFootsteps()
        {
            if (footsteps == null || footstepClips.Length == 0 ||
                leftFoot == null || rightFoot == null)
                return;

            bool huntOwnsSteps = hunt != null && hunt.enabled &&
                                 hunt.Current != RuiHunt.State.Hidden;
            bool walking = !huntOwnsSteps && smoothedSpeed >= 0.35f &&
                           animator != null && animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion");

            float leftHeight = leftFoot.position.y - transform.position.y;
            float rightHeight = rightFoot.position.y - transform.position.y;
            float dt = Time.deltaTime;

            if (!walking || dt <= 0f)
            {
                footSamplesReady = false;
                return;
            }

            if (!footSamplesReady)
            {
                previousLeftFootHeight = leftHeight;
                previousRightFootHeight = rightHeight;
                previousLeftFootVelocity = 0f;
                previousRightFootVelocity = 0f;
                footSamplesReady = true;
                return;
            }

            float leftVelocity = (leftHeight - previousLeftFootHeight) / dt;
            float rightVelocity = (rightHeight - previousRightFootHeight) / dt;

            bool leftLanded = previousLeftFootVelocity < -0.035f && leftVelocity >= -0.005f;
            bool rightLanded = previousRightFootVelocity < -0.035f && rightVelocity >= -0.005f;

            if ((leftLanded || rightLanded) && Time.time - lastStepAt >= 0.18f)
            {
                PlayFootstep();
                lastStepAt = Time.time;
            }

            previousLeftFootHeight = leftHeight;
            previousRightFootHeight = rightHeight;
            previousLeftFootVelocity = leftVelocity;
            previousRightFootVelocity = rightVelocity;
        }

        private void PlayFootstep()
        {
            AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
            if (clip == null) return;

            footsteps.pitch = Random.Range(0.94f, 1.05f);
            footsteps.PlayOneShot(clip, footstepVolume);
        }

        private void LateUpdate() => UpdateFootsteps();

        /// <summary>
        /// Durante conversa, stare-down, expulsao e perseguicao a cabeca fixa o jogador.
        /// Na rotina normal so o olha de relance quando ele esta perto, para o
        /// comportamento comecar por parecer banal.
        /// </summary>
        private void UpdateHeadLook()
        {
            if (headLook == null) return;

            float weight;
            if (conversationActive ||
                State == PrototypeNpcState.StareDown ||
                State == PrototypeNpcState.Watching ||
                State == PrototypeNpcState.Evicting ||
                State == PrototypeNpcState.Pursuing)
            {
                weight = 1f;
            }
            else if (player != null && idleGlanceDistance > 0f &&
                     Vector3.Distance(transform.position, player.position) <= idleGlanceDistance)
            {
                weight = idleGlanceWeight;
            }
            else
            {
                weight = 0f;
            }

            headLook.SetLookWeight(weight);
        }

        /// <summary>
        /// Olhar forcado durante alguns segundos, para acompanhar falas do genero
        /// "he started looking at you" sem mudar de estado.
        /// </summary>
        /// <summary>
        /// O Animator do modelo, para sequencias encenadas que precisam de tocar um
        /// estado fora da rotina — a expulsao do quarto, por exemplo.
        /// </summary>
        public Animator Animator => animator;

        [Tooltip("Estado do Animator para o stare-down. Ver RuiAnimatorSetup.Actions.")]
        [SerializeField] private string stareState = "Stare";

        [Header("Olhar para tras")]
        [Tooltip("Ele olha por cima do ombro a meio do circuito, sem o jogador ter "
               + "feito nada. So a partir da ameaca indicada: em zero seria um tique "
               + "sem causa, e o que se quer e que aparece quando a relacao muda.")]
        [SerializeField, Range(0, 4)] private int glanceFromThreat = 2;
        [SerializeField] private string glanceState = "LookOverShoulder";
        [Tooltip("Intervalo medio entre olhares, em segundos.")]
        [SerializeField, Min(4f)] private float glanceInterval = 26f;

        private float nextGlanceAt;

        /// <summary>Entra na pose de quem reparou que estas a olhar para ele.</summary>
        private void PlayStare()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (string.IsNullOrEmpty(stareState)) return;
            animator.CrossFadeInFixedTime(stareState, 0.25f);
        }

        /// <summary>Volta a locomocao depois do stare.</summary>
        private void LeaveStare()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.CrossFadeInFixedTime("Locomotion", 0.25f);
        }

        /// <summary>
        /// De vez em quando olha para tras enquanto anda.
        ///
        /// Nao reage a nada — e esse o ponto. Uma reaccao a estares a olhar e
        /// legivel como mecanica; um olhar sem causa, apanhado de canto de olho,
        /// nao tem explicacao e fica.
        /// </summary>
        private void UpdateGlance()
        {
            if (ThreatLevel < glanceFromThreat) return;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (string.IsNullOrEmpty(glanceState)) return;

            if (nextGlanceAt <= 0f)
            {
                nextGlanceAt = Time.time + Random.Range(glanceInterval * 0.5f, glanceInterval * 1.5f);
                return;
            }

            if (Time.time < nextGlanceAt) return;
            nextGlanceAt = Time.time + Random.Range(glanceInterval * 0.5f, glanceInterval * 1.5f);

            // Nao interrompe accoes nem conversas: so enquanto percorre a casa.
            if (State != PrototypeNpcState.Routine || conversationActive) return;

            animator.CrossFadeInFixedTime(glanceState, 0.18f);
            StartCoroutine(ReturnFromGlance());
        }

        /// <summary>
        /// Olha por cima do ombro — **parado**.
        ///
        /// ---
        ///
        /// **Ele fazia isto a andar, e o que se via era um homem a deslizar.** O
        /// clip poe-lhe o tronco torcido para tras e o agente continuava a
        /// atravessar a casa com essa pose colada: os pes nao andam na animacao, mas
        /// o corpo anda na mesma. O relatorio de teste chama-lhe "uma animacao
        /// estranha de confusao que o faz deslizar", e e exactamente isso.
        ///
        /// Uma pessoa que ouve alguma coisa atras de si **para** para olhar. Parar e
        /// que faz o gesto ler-se: um segundo e meio de imobilidade no meio de um
        /// circuito e o que diz ao jogador que aquilo nao foi rotina.
        ///
        /// Para tirar isto do jogo em vez de o corrigir, basta esvaziar o
        /// `glanceState` no `NPC_Rui` — nao ha mais nada preso a ele.
        /// </summary>
        private System.Collections.IEnumerator ReturnFromGlance()
        {
            yield return null;
            float length = animator.GetCurrentAnimatorStateInfo(0).length;
            if (length <= 0.05f || float.IsInfinity(length)) length = 1.5f;

            // **Reafirmado a cada frame, e nao uma vez.**
            //
            // Por `isStopped = true` uma so vez nao serve para nada aqui: o proprio
            // `Update` desta rotina volta a pedir destino e a religar o agente no
            // frame seguinte. Medido com o tempo a 6%: o clip a tocar em
            // `normalizedTime` 0,15 e o agente a 0,80 m/s, na mesma. E a mesma linha
            // que o `PlayerSleep`, o `HidingSpot` e o `RuiOnTheFloor` ja tem — quem
            // pede um corpo emprestado a outro Update tem de o pedir todos os frames.
            float until = Time.time + length;
            while (Time.time < until)
            {
                // Se a rotina mudou de estado a meio — uma conversa, uma expulsao —
                // quem manda no agente passou a ser outro. Larga-se e nao se toca
                // mais.
                if (State != PrototypeNpcState.Routine) break;

                if (chaseAgent != null && chaseAgent.enabled && chaseAgent.isOnNavMesh)
                    chaseAgent.isStopped = true;

                yield return null;
            }

            if (chaseAgent != null && chaseAgent.enabled && chaseAgent.isOnNavMesh
                && State == PrototypeNpcState.Routine)
                chaseAgent.isStopped = false;

            // Mesma licao do pontape e do stare: o clip nao tem loop e sem isto ele
            // continuava a andar preso na ultima pose.
            if (State == PrototypeNpcState.Routine)
                animator.CrossFadeInFixedTime("Locomotion", 0.2f);
        }

        public void GlanceAtPlayer(float seconds)
        {
            if (headLook != null) headLook.Glance(seconds);
        }

        /// <summary>Entra numa ação contextual do anchor onde acabou de chegar.</summary>
        private bool BeginAction(RoutineActionAnchor action, Transform anchor)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            if (string.IsNullOrEmpty(action.AnimatorState)) return false;

            State = PrototypeNpcState.Acting;
            actionEndsAt = Time.time + action.PickDuration();
            actionFacing = action.AlignToAnchor ? anchor.rotation : transform.rotation;
            
            currentActionAnchor = action;
            currentActionAnchor.onActionStarted?.Invoke(currentActionAnchor.AnimatorState);

            // A accao manda na rotacao e, nalguns casos, nos ultimos centimetros
            // da posicao. O agente fica sempre parado enquanto ela decorre para
            // nao puxar o corpo de lado diante do frigorifico ou do sofa.
            DisableAgent();

            // O sitio onde se faz a coisa nem sempre e sitio onde se pode chegar.
            // O agente fica desligado enquanto o corpo entra no assento, senao
            // puxava-o logo de volta para a NavMesh.
            actionAnchorPosition = transform.position;
            actionOffsetPosition = action.HasOffset ? action.ActionPosition : transform.position;
            actionSettle = 0f;
            if (action.HasEnter)
            {
                animator.CrossFadeInFixedTime(action.EnterState, 0.2f);
                actionPhase = ActionPhase.Entering;
                actionPhaseMeasured = false;
                // O relogio da accao so arranca depois de ele se ter sentado.
                actionEndsAt = float.MaxValue;
            }
            else
            {
                animator.CrossFadeInFixedTime(action.AnimatorState, 0.28f);
                actionPhase = ActionPhase.Main;
            }
            return true;
        }

        /// <summary>
        /// Espera pelo fim do estado que esta a tocar. A duracao vem do proprio
        /// Animator, medida um frame depois do CrossFade — os clips do Mixamo
        /// chamam-se todos "mixamo.com", por isso nao ha como os identificar pelo
        /// nome, e escrever as duracoes a mao obrigaria a reafina-las a cada troca.
        /// </summary>
        private bool PhaseFinished()
        {
            if (!actionPhaseMeasured)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                float length = info.length;
                if (length <= 0.05f || float.IsInfinity(length)) length = 1.2f;
                actionPhaseEndsAt = Time.time + length;
                actionPhaseMeasured = true;
                return false;
            }
            return Time.time >= actionPhaseEndsAt;
        }

        private void UpdateActing()
        {
            // Vira-se devagar para a pose do anchor enquanto executa.
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, actionFacing, turnSpeed * 0.6f * Time.deltaTime);

            // Entra no deslocamento: os ultimos centimetros ate ao assento, ao balcao
            // ou ao frigorifico.
            if (currentActionAnchor != null && currentActionAnchor.HasOffset)
            {
                actionSettle = Mathf.Min(1f, actionSettle +
                    Time.deltaTime / currentActionAnchor.SettleSeconds);
                float eased = actionSettle * actionSettle * (3f - 2f * actionSettle);
                transform.position = Vector3.Lerp(actionAnchorPosition, actionOffsetPosition, eased);
            }

            // A sentar-se: so quando acabar e que a accao propriamente dita comeca.
            if (actionPhase == ActionPhase.Entering)
            {
                if (!PhaseFinished()) return;
                animator.CrossFadeInFixedTime(currentActionAnchor.AnimatorState, 0.15f);
                actionPhase = ActionPhase.Main;
                actionEndsAt = Time.time + currentActionAnchor.PickDuration();
                return;
            }

            // A levantar-se: fica ate o clip acabar e so depois retoma o circuito.
            if (actionPhase == ActionPhase.Exiting)
            {
                if (!PhaseFinished()) return;
                FinishAction();
                return;
            }

            if (Time.time < actionEndsAt) return;

            // Acabou o tempo sentado. Se houver clip de levantar, toca-o primeiro.
            if (currentActionAnchor != null && currentActionAnchor.HasExit)
            {
                animator.CrossFadeInFixedTime(currentActionAnchor.ExitState, 0.15f);
                actionPhase = ActionPhase.Exiting;
                actionPhaseMeasured = false;
                return;
            }

            FinishAction();
        }

        private void FinishAction()
        {
            if (currentActionAnchor != null)
            {
                // Volta ao ponto de onde veio antes de o agente pegar nele outra vez.
                if (currentActionAnchor.HasOffset)
                    transform.position = actionAnchorPosition;

                currentActionAnchor.onActionEnded?.Invoke(currentActionAnchor.AnimatorState);
                currentActionAnchor = null;
            }

            State = PrototypeNpcState.Routine;
            waypointIndex = (waypointIndex + 1) % waypoints.Length;
            waitRemaining = 0.35f;
            if (animator != null && animator.runtimeAnimatorController != null)
                animator.CrossFadeInFixedTime("Locomotion", 0.3f);
        }

        private bool EnableAgent(float speed)
        {
            if (chaseAgent == null) return false;

            // A encenacao do prologo pode terminar alguns centimetros fora da
            // malha. Projecta o ponto para a NavMesh antes de ligar o agente; o
            // desvio e pequeno e, a partir daqui, todo o percurso respeita paredes
            // e portas em vez de cortar em linha reta ate ao primeiro waypoint.
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit start,
                    1.25f, NavMesh.AllAreas))
                return false;

            if ((start.position - transform.position).sqrMagnitude > 0.0001f)
                transform.position = start.position;

            chaseAgent.enabled = true;
            if (!chaseAgent.isOnNavMesh)
            {
                chaseAgent.enabled = false;
                return false;
            }

            chaseAgent.speed = speed;
            chaseAgent.stoppingDistance = 0.08f;
            chaseAgent.isStopped = false;
            return true;
        }

        private void DisableAgent()
        {
            if (chaseAgent == null) return;
            if (chaseAgent.enabled && chaseAgent.isOnNavMesh) chaseAgent.isStopped = true;
            chaseAgent.enabled = false;
        }

        private void UpdateEvicting()
        {
            Transform target = intruder != null ? intruder : player;
            if (target == null) return;

            float distance = Vector3.Distance(transform.position, target.position);
            bool agentReady = chaseAgent != null && chaseAgent.enabled && chaseAgent.isOnNavMesh;

            if (distance > EffectiveStopDistance)
            {
                if (agentReady)
                {
                    chaseAgent.isStopped = false;
                    chaseAgent.SetDestination(target.position);
                }
                else
                {
                    // Sem NavMesh valida, aproxima-se na mesma para nao ficar parado.
                    Vector3 flat = target.position;
                    flat.y = transform.position.y;
                    transform.position = Vector3.MoveTowards(transform.position, flat, evictSpeed * Time.deltaTime);
                }
            }
            else if (distance < EffectiveStopDistance - 0.6f)
            {
                if (agentReady)
                {
                    chaseAgent.isStopped = false;
                    Vector3 away = (transform.position - target.position).normalized;
                    away.y = 0f;
                    chaseAgent.SetDestination(transform.position + away * 1.5f);
                }
                else
                {
                    Vector3 flat = transform.position + (transform.position - target.position).normalized * 0.5f;
                    flat.y = transform.position.y;
                    transform.position = Vector3.MoveTowards(transform.position, flat, (evictSpeed * 0.5f) * Time.deltaTime);
                }
            }
            else if (agentReady)
            {
                chaseAgent.isStopped = true;
            }

            FaceTransform(target, turnSpeed * 1.4f);
        }

        private void UpdateReturning()
        {
            bool agentReady = chaseAgent != null && chaseAgent.enabled && chaseAgent.isOnNavMesh;
            Transform target = waypoints != null && waypoints.Length > 0 ? waypoints[waypointIndex] : null;

            if (!agentReady || target == null)
            {
                DisableAgent();
                State = PrototypeNpcState.Routine;
                return;
            }

            chaseAgent.SetDestination(target.position);
            if (!chaseAgent.pathPending && chaseAgent.remainingDistance <= 0.35f)
            {
                DisableAgent();
                waitRemaining = CurrentWaypointPause;
                State = PrototypeNpcState.Routine;
            }
        }
public PrototypeNpcState State { get; private set; } = PrototypeNpcState.Routine;

        private void Update()
        {
            UpdateLocomotionSpeed();
            UpdateHeadLook();

            if (conversationActive)
            {
                if (holdFacesPlayer) FacePlayer(turnSpeed * 0.65f);
                return;
            }

            // O territorio sobrepoe-se ao circuito e ao stare-down.
            if (State == PrototypeNpcState.Evicting) { UpdateEvicting(); return; }
            if (State == PrototypeNpcState.Returning) { UpdateReturning(); return; }

            // A acao contextual nao bloqueia o stare-down: ele continua a poder
            // reparar em ti enquanto cozinha, que e o ponto do slow-burn.
            if (State == PrototypeNpcState.Acting)
            {
                UpdateActing();
                if (State == PrototypeNpcState.Acting) return;
            }

            bool playerLooking = IsPlayerLookingAtMe();
            if (State == PrototypeNpcState.Routine)
            {
                UpdateRoutine();
                UpdateGlance();
                gazeTime = playerLooking ? gazeTime + Time.deltaTime : Mathf.Max(0f, gazeTime - Time.deltaTime * 2f);
                if (gazeTime >= CurrentStareSeconds)
                {
                    DisableAgent();
                    State = PrototypeNpcState.StareDown;
                    gazeTime = 0f;
                    lostGazeTime = 0f;
                    // A pose de quem reparou. Ate agora o stare-down era o Rui
                    // parado em pose de andar, virado para ti.
                    PlayStare();

                    // Manter o olhar e uma escolha (§6.1) e acumula como tal.
                    Pungent.Narrative.NarrativeBlackboard.Instance?.OnStare();
                }
            }
            else if (State == PrototypeNpcState.StareDown)
            {
                FacePlayer(turnSpeed);
                lostGazeTime = playerLooking ? 0f : lostGazeTime + Time.deltaTime;
                if (lostGazeTime >= 0.3f)
                {
                    // Sair da pose do stare. O clip nao tem loop e nada o
                    // substituia: sem isto ele voltava ao circuito preso nele, como
                    // aconteceu com o pontape.
                    LeaveStare();

                    if (dangerEscalationUnlocked)
                    {
                        State = PrototypeNpcState.Pursuing;
                        BeginPursuit();
                    }
                    else
                    {
                        State = PrototypeNpcState.Watching;
                        watchingUntil = Time.time + CurrentWatchSeconds;
                    }
                }
            }
            else if (State == PrototypeNpcState.Watching)
            {
                FacePlayer(turnSpeed * 0.65f);
                if (Time.time >= watchingUntil)
                    State = PrototypeNpcState.Routine;
            }
            else if (State == PrototypeNpcState.Pursuing)
            {
                if (player == null) return;
                if (chaseAgent != null && chaseAgent.enabled && chaseAgent.isOnNavMesh)
                {
                    chaseAgent.SetDestination(player.position);
                }
                else
                {
                    Vector3 target = player.position;
                    target.y = transform.position.y;
                    transform.position = Vector3.MoveTowards(transform.position, target, 3.8f * Time.deltaTime);
                    FacePlayer(turnSpeed * 1.6f);
                }
            }
        }

        private void UpdateRoutine()
        {
            if (waypoints == null || waypoints.Length == 0) return;
            Transform target = waypoints[waypointIndex];
            if (target == null) return;

            if (waitRemaining > 0f)
            {
                waitRemaining -= Time.deltaTime;
                return;
            }

            Vector3 destination = target.position;
            destination.y = transform.position.y;
            Vector3 difference = destination - transform.position;
            bool agentReady = chaseAgent != null && chaseAgent.enabled && chaseAgent.isOnNavMesh;
            bool arrived = difference.magnitude <= 0.12f ||
                (agentReady && !chaseAgent.pathPending && chaseAgent.hasPath &&
                 chaseAgent.remainingDistance <= 0.12f);
            if (arrived)
            {
                DisableAgent();

                // Chegou. Se o anchor pedir uma acao, executa-a em vez de esperar parado.
                var action = target.GetComponent<RoutineActionAnchor>();
                if (action != null && BeginAction(action, target))
                    return;

                waypointIndex = (waypointIndex + 1) % waypoints.Length;
                waitRemaining = CurrentWaypointPause;
                return;
            }

            if (!agentReady) agentReady = EnableAgent(routineSpeed);
            if (!agentReady) return; // sem NavMesh e preferivel parar a atravessar uma parede

            chaseAgent.speed = routineSpeed;
            chaseAgent.isStopped = false;
            chaseAgent.SetDestination(target.position);
        }

        private bool IsPlayerLookingAtMe()
        {
            if (playerCamera == null) return false;
            Vector3 toNpc = transform.position + Vector3.up * 1.35f - playerCamera.transform.position;
            if (toNpc.magnitude > gazeDistance || Vector3.Angle(playerCamera.transform.forward, toNpc) > gazeAngle)
                return false;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            return Physics.Raycast(ray, out RaycastHit hit, gazeDistance, ~0, QueryTriggerInteraction.Collide) &&
                   hit.transform.IsChildOf(transform);
        }

        private void FacePlayer(float speed) => FaceTransform(player, speed);

        private void FaceTransform(Transform target, float speed)
        {
            if (target == null) return;
            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            RotateTowards(direction, speed);
        }

        private void RotateTowards(Vector3 direction, float speed)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, speed * Time.deltaTime);
        }
    

private void BeginPursuit()
        {
            if (chaseAgent == null) return;
            chaseAgent.enabled = true;
            if (!chaseAgent.isOnNavMesh)
            {
                chaseAgent.enabled = false;
                return;
            }

            chaseAgent.isStopped = false;
            chaseAgent.SetDestination(player.position);
        }


private void Awake()
        {
            if (chaseAgent == null) chaseAgent = GetComponent<NavMeshAgent>();
            if (chaseAgent != null) chaseAgent.enabled = false;
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
            hunt = GetComponent<RuiHunt>();
            if (hunt != null)
                hunt.ShareFootstepAudio(ref footsteps, ref footstepClips);
            speedHash = Animator.StringToHash(speedParameter);
            lastPosition = transform.position;
            if (headLook == null) headLook = GetComponentInChildren<HumanoidHeadLook>(true);
            if (headLook != null && playerCamera != null) headLook.SetTarget(playerCamera.transform);
        }

        /// <summary>
        /// Nos dias normais o corpo dele **nao entala o jogador**.
        ///
        /// A capsula do Rui tem 64 cm num apartamento com um corredor de um metro,
        /// e esta na mesma layer que o `CharacterController`. Basta ele ir a caminho
        /// da cozinha e o jogador vir em sentido contrario para o jogador ficar
        /// encostado a parede sem saida — nao ha empurrao entre um corpo cinematico
        /// e um controlador, so uma parede que anda. Foi isso o "anda atras de mim e
        /// encrava-me"; ele nao estava a perseguir ninguem, estava a passar.
        ///
        /// **So nos dias normais.** Esta rotina e desligada pelo `ClimaxStage` na
        /// noite em que quem manda no corpo dele e o `RuiHunt` — e nessa noite ser
        /// encurralado por ele e o capitulo, e nao um bug. Ligar isto ao ciclo de
        /// vida da rotina faz a regra seguir sozinha essa fronteira, sem ninguem ter
        /// de se lembrar de a repor.
        /// </summary>
        private void OnEnable() => SetBodyBlocksPlayer(false);

        private void OnDisable() => SetBodyBlocksPlayer(true);

        private void SetBodyBlocksPlayer(bool blocks)
        {
            var body = GetComponent<Collider>();
            if (body == null) return;

            if (playerBody == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor != null) playerBody = motor.GetComponent<CharacterController>();
            }
            if (playerBody == null) return;

            Physics.IgnoreCollision(body, playerBody, !blocks);
        }

        private CharacterController playerBody;
}
}
