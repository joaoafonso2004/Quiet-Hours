using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O Rui caido no chao da cozinha, e a levantar-se quando o encontram.
    ///
    /// ---
    ///
    /// **O que isto e.** O Tomas entra na cozinha e ha um corpo no chao, numa poca
    /// escura, com uma garrafa partida ao lado. Fica la o tempo que o jogador
    /// levar a perceber o que esta a ver. Depois o Rui levanta-se — devagar, com
    /// dificuldade — e diz uma coisa banal.
    ///
    /// ---
    ///
    /// **Um clip so, e e a peca inteira do desenho.**
    ///
    /// O corpo no chao e o frame zero do <c>floor_stand_up</c>; o levantar e o
    /// mesmo clip a correr. Nao ha pose estatica de um lado e animacao do outro,
    /// portanto nao ha salto no instante em que a cena se decide — que era o unico
    /// sitio onde isto se podia estragar de forma visivel.
    ///
    /// O clip tambem ja traz tres segundos de imobilidade antes do primeiro
    /// movimento. Nao foram pedidos e sao o melhor que ele tem: o jogador ve o
    /// corpo, olha, e passa-se um tempo desconfortavel antes de aquilo mexer.
    ///
    /// ---
    ///
    /// **Nao e um susto, e a regra que este componente existe para cumprir.**
    ///
    /// Ele nao se levanta de rompante nem reage a ser encontrado. Levanta-se como
    /// se levanta uma pessoa que adormeceu no chao — porque foi isso que
    /// aconteceu, e a garrafa partida esta ali a dize-lo. A seccao 3.4 do plano
    /// proibe o jogo de viver de jumpscares, e um corpo que salta para cima e
    /// exactamente o que ela proibe.
    ///
    /// O medo chega depois. Ao fim da tarde o Tomas ainda esta a pensar que
    /// encontrou o homem deitado no escuro numa poca de vinho e que ele nao pareceu
    /// nada envergonhado. E o registo pedido: **nada acontece, e mesmo assim fica
    /// mal.**
    ///
    /// ---
    ///
    /// **Ser visto e a condicao, nao entrar na divisao.** Disparar a entrada da
    /// cozinha punha-o a levantar-se de costas para quem chegou, ou fora do ecra,
    /// e a cena acontecia sem ninguem. A condicao e a mesma que o
    /// <see cref="WitnessedCue"/> ja usa para o <c>MustSee</c>: linha de vista a
    /// serio, mantida algum tempo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuiOnTheFloor : MonoBehaviour
    {
        private enum Phase { Waiting, Down, Rising, Done }

        [Header("Quem")]
        [Tooltip("A raiz do NPC. E ela que e posta no chao.")]
        [SerializeField] private Transform rui;

        [Tooltip("O Animator do modelo. Costuma estar num filho.")]
        [SerializeField] private Animator animator;

        [Tooltip("Estado do AC_Rui com o clip de levantar do chao.")]
        [SerializeField] private string floorState = "FloorStandUp";

        [Tooltip("Estado para onde ele volta depois de estar de pe.")]
        [SerializeField] private string exitState = "Locomotion";

        [Tooltip("Tudo o que conduz o Rui e nao pode correr enquanto ele esta no "
               + "chao. Sem isto a rotina domestica tenta leva-lo ao fogao e o "
               + "corpo desliza pela cozinha fora, deitado.")]
        [SerializeField] private MonoBehaviour[] suppressed;

        [Tooltip("O NavMeshAgent, e o que mais precise de ser desligado sem ser "
               + "MonoBehaviour.")]
        [SerializeField] private Behaviour[] alsoDisabled;

        [Header("Onde cai")]
        [SerializeField] private Vector3 lyingPosition = new Vector3(-5.30f, 0f, 3.30f);
        [SerializeField] private float lyingYaw = 90f;

        [Tooltip("A poca e os vidros. **Escondidos ate ele cair.**\n\n"
               + "Estavam visiveis desde o carregamento da cena, ou seja desde o "
               + "**prologo**: o jogador entrava em casa pela primeira vez e havia "
               + "uma poca de vinho com uma garrafa partida no chao da cozinha, tres "
               + "dias antes de alguem a partir. Nao dava erro — a encenacao so "
               + "mandava no corpo do Rui e ninguem mandava nos objectos.\n\n"
               + "Ficam depois de ele se levantar, e e de proposito: sao a "
               + "explicacao. Uma poca que desaparece com ele deixa a noite sem "
               + "nada que a justifique de manha.")]
        [SerializeField] private GameObject mess;

        [Header("Quando se levanta")]
        [Tooltip("O jogador tem de estar mais perto do que isto.")]
        [SerializeField, Min(0.5f)] private float maximumDistance = 5f;

        [Tooltip("E tem de estar a olhar para ele durante este tempo seguido. "
               + "Curto de mais e ele levanta-se enquanto o jogador ainda esta a "
               + "perceber o que tem a frente.")]
        [SerializeField, Min(0.1f)] private float dwellSeconds = 1.6f;

        [Tooltip("Tempo a partir do qual ele se levanta so por o jogador estar "
               + "perto, mesmo que o teste de vista nunca passe. E a rede que "
               + "impede a noite de ficar trancada — ver a nota no `Update`.")]
        [SerializeField, Min(1f)] private float patienceSeconds = 14f;

        [SerializeField] private LayerMask sightBlockers = ~0;

        [Tooltip("Altura a que o corpo e procurado pelos testes de vista. Baixa, "
               + "que e onde ele esta.")]
        [SerializeField, Min(0.05f)] private float bodyHeight = 0.35f;

        [Header("Janela")]
        [Tooltip("A noite das 02:47. Ele cai no passo `TalkToRui` — o instante em "
               + "que o router fica reiniciado e o objectivo manda o Tomas procura-"
               + "-lo. Nesse momento o jogador esta na outra ponta da casa, junto a "
               + "porta da rua, e a cozinha esta fora de vista: da tempo de ele "
               + "estar no chao antes de alguem entrar.\n\n"
               + "**Nao serve para o Dia 3.** Esse dia inteiro assenta na ausencia "
               + "do Rui — ver o `DayThreeStage` — e um corpo na cozinha responde "
               + "logo de manha a pergunta que o dia existe para deixar em aberto.")]
        [SerializeField] private OpeningQuestDirector quest;

        [SerializeField] private OpeningQuestDirector.Step lieDownOn =
            OpeningQuestDirector.Step.TalkToRui;

        [Tooltip("Alternativa por acontecimento, para quando nao ha quest director. "
               + "Vazio = manda o passo acima.")]
        [SerializeField] private string requiresEvent;

        [Tooltip("Deixa de poder acontecer depois deste.")]
        [SerializeField] private string silencedByEvent;

        [Header("Som")]
        [Tooltip("**A garrafa a partir, e e o som que faz a cena existir.**\n\n"
               + "Toca no momento em que ele cai — com o Tomas na outra ponta da "
               + "casa, junto ao router. Nao e um efeito: e o convite. Sem ele o "
               + "jogador vai a cozinha porque um objectivo lhe disse, e encontra "
               + "aquilo por ordem; com ele, ouve partir-se qualquer coisa no "
               + "escuro e vai la ver por vontade propria. E a diferenca entre "
               + "cumprir um passo e ir a procura de uma coisa.")]
        [SerializeField] private AudioSource breakSound;
        [SerializeField] private AudioClip breakClip;
        [SerializeField, Range(0f, 1f)] private float breakVolume = 0.85f;

        [Tooltip("O vidro a mexer quando ele se apoia para se levantar. Baixo e "
               + "curto.")]
        [SerializeField] private AudioSource sound;
        [SerializeField] private AudioClip stirClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.5f;

        [Header("Depois")]
        [Tooltip("Quase sempre vazio. O Tomas nao precisa de dizer ao jogador o "
               + "que ele acabou de ver.")]
        [SerializeField, TextArea] private string thought;

        [SerializeField] private string raisesEvent = "day3_found_rui";

        [SerializeField] private ChapterDirector director;
        [SerializeField] private PlayerThoughtDirector thoughts;

        private Phase phase = Phase.Waiting;
        private Transform player;
        private Camera eye;
        private float heldSince = -1f;
        private float nearSince = -1f;
        private int floorHash;

        /// <summary>Ja se levantou. Util para inspeccionar e para os testes.</summary>
        public bool HasRisen => phase == Phase.Done;

        /// <summary>Esta caido agora.</summary>
        public bool IsDown => phase == Phase.Down;

        private void Awake()
        {
            if (rui == null) rui = transform;
            if (animator == null && rui != null) animator = rui.GetComponentInChildren<Animator>(true);
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            floorHash = Animator.StringToHash(floorState);

            // No `Awake` e nao no `Start`: a cena abre no prologo e o jogador entra
            // pela porta a olhar para a cozinha. Um frame com a poca la e um frame a
            // mais.
            if (mess != null) mess.SetActive(false);
        }

        private void Update()
        {
            switch (phase)
            {
                case Phase.Waiting:
                    if (Allowed()) LieDown();
                    break;

                case Phase.Down:
                    // **Reafirmado a cada frame, e nao so ao deitar.**
                    //
                    // O relatorio de teste diz que ele "rasteja" depois do router:
                    // anda pela cozinha na pose do chao. A pose esta certa e o corpo
                    // e que nao devia mexer-se — ou seja, alguem volta a ligar o que
                    // isto desligou, a meio do proprio Update dele. Nao e novidade
                    // neste projecto: o `PlayerSleep` e o `HidingSpot` ja tem esta
                    // mesma linha, com a mesma nota.
                    //
                    // Nao vale a pena descobrir **quem** — ha uma duzia de
                    // componentes com uma referencia ao NPC_Rui e qualquer um deles
                    // pode repor um `enabled` guardado antes desta cena comecar. O
                    // que vale e nao deixar que isso importe.
                    SetControlled(false);

                    // E o corpo tambem. Se alguma coisa o mover por fora dos
                    // componentes que eu conheco — uma encenacao de outro capitulo,
                    // uma corrotina a meio — ele volta ao chao no mesmo frame. Um
                    // homem caido no chao esta caido no chao.
                    if (rui != null)
                    {
                        rui.position = lyingPosition;
                        rui.rotation = Quaternion.Euler(0f, lyingYaw, 0f);
                    }

                    if (!ResolvePlayer()) return;

                    bool near = rui != null &&
                                Vector3.Distance(player.position, rui.position) <= maximumDistance;
                    if (near) { if (nearSince < 0f) nearSince = Time.time; }
                    else nearSince = -1f;

                    if (Seen())
                    {
                        if (heldSince < 0f) heldSince = Time.time;
                        if (Time.time - heldSince >= dwellSeconds) { Rise(); break; }
                    }
                    else heldSince = -1f;

                    // **A rede de seguranca, e nao e um extra.** A conversa do Rui
                    // esta suprimida enquanto ele esta no chao, e o passo da noite
                    // so avanca depois dela. Um jogador que fique na cozinha sem
                    // que o teste de vista alguma vez passe — de costas, atras da
                    // bancada, com o angulo errado — ficava com a noite trancada e
                    // sem nada que lhe dissesse porque. Ao fim deste tempo ele
                    // levanta-se de qualquer maneira.
                    if (nearSince >= 0f && Time.time - nearSince >= patienceSeconds) Rise();
                    break;

                case Phase.Rising:
                    // Enquanto se levanta continua a ser a encenacao a mandar. So o
                    // Animator e que corre — o `speed` ja voltou a um no `Rise` — e o
                    // corpo fica onde esta: o clip levanta-o do sitio, e um agente
                    // ligado a meio disto punha-o a andar deitado.
                    SetControlled(false);

                    if (Standing()) Finish();
                    break;
            }
        }

        /// <summary>
        /// Poe o corpo no chao e cala tudo o resto.
        ///
        /// O `Play` seguido de `Update(0)` forca a pose no mesmo frame; sem ele
        /// ficava um frame com o Rui de pe antes de a animacao pegar, e um frame
        /// e quanto basta para o jogador ver a pessoa de pe onde devia estar um
        /// corpo. So depois e que o `speed` vai a zero — a ordem importa.
        /// </summary>
        private void LieDown()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            if (!animator.HasState(0, floorHash))
            {
                Debug.LogWarning($"[RuiOnTheFloor] O AC_Rui nao tem o estado '{floorState}'.");
                enabled = false;
                return;
            }

            SetControlled(false);

            // A poca e os vidros aparecem **com** ele e nao antes. O jogador esta na
            // outra ponta da casa, junto ao router — nao ha ninguem a olhar para a
            // cozinha neste instante, que e a mesma razao por que a garrafa se parte
            // agora e nao quando ele chega.
            if (mess != null) mess.SetActive(true);

            if (rui != null)
            {
                rui.position = lyingPosition;
                rui.rotation = Quaternion.Euler(0f, lyingYaw, 0f);
            }

            animator.applyRootMotion = false;
            animator.Play(floorHash, 0, 0f);
            animator.Update(0f);
            animator.speed = 0f;

            // A garrafa parte-se agora, e nao quando o jogador chega. E o unico
            // instante em que ele nao esta a olhar para a cozinha — ver a nota do
            // campo.
            if (breakSound != null && breakClip != null)
                breakSound.PlayOneShot(breakClip, breakVolume);

            phase = Phase.Down;
        }

        private void Rise()
        {
            animator.speed = 1f;
            if (sound != null && stirClip != null) sound.PlayOneShot(stirClip, volume);
            phase = Phase.Rising;
        }

        private bool Standing()
        {
            if (animator == null) return true;
            if (animator.IsInTransition(0)) return false;

            var info = animator.GetCurrentAnimatorStateInfo(0);
            return info.shortNameHash == floorHash && info.normalizedTime >= 0.999f;
        }

        /// <summary>
        /// Devolve o Rui a si proprio.
        ///
        /// O `speed` volta a um antes de o controlo voltar: uma rotina a comecar
        /// com o Animator parado deixava-o a deslizar pela cozinha na pose em que
        /// ficou, que e o mesmo defeito de nao ter suprimido nada.
        /// </summary>
        private void Finish()
        {
            phase = Phase.Done;

            animator.speed = 1f;
            if (animator.HasState(0, Animator.StringToHash(exitState)))
                animator.CrossFadeInFixedTime(exitState, 0.25f);

            SetControlled(true);

            if (!string.IsNullOrWhiteSpace(thought))
                thoughts?.Think($"floor_{GetInstanceID()}", thought, 3, true, 3.6f);

            if (!string.IsNullOrWhiteSpace(raisesEvent))
            {
                director = ChapterDirector.Resolve(director);
                director?.Notify(raisesEvent);
            }
        }

        private void SetControlled(bool on)
        {
            if (suppressed != null)
                foreach (var m in suppressed)
                    if (m != null) m.enabled = on;

            if (alsoDisabled != null)
                foreach (var b in alsoDisabled)
                    if (b != null)
                    {
                        b.enabled = on;
                        if (on) Replant(b);
                    }
        }

        /// <summary>
        /// Devolve o agente ao sitio onde o corpo ficou.
        ///
        /// ---
        ///
        /// **Isto faltava, e era o que o deixava preso.**
        ///
        /// O <see cref="LieDown"/> teleporta o `rui.position` para o chao da cozinha e
        /// desliga o `NavMeshAgent`. Um agente desligado **nao acompanha o transform**:
        /// quando volta a ser ligado, continua a achar que esta onde estava antes de
        /// tudo isto — e a partir dai puxa o corpo para uma posicao que ja nao existe,
        /// ou desiste e fica parado no sitio.
        ///
        /// Era exactamente o que se via a jogar: ele levantava-se do vinho e ficava
        /// especado, e ao mexer-se atravessava o chao.
        ///
        /// `Warp` e a unica maneira certa de mover um agente. Escrever no transform
        /// com o agente ligado e o que produz o deslizar e o atravessar — a mesma
        /// licao que o `CharacterController` do jogador ja tinha ensinado a este
        /// projecto.
        ///
        /// **E amostra-se a NavMesh primeiro.** O sitio onde ele adormeceu pode nao
        /// estar em cima da malha — o chao da cozinha tem moveis por cima, e a poca de
        /// vinho foi colocada a olho. Um `Warp` para fora da malha nao falha alto:
        /// devolve `false` e deixa o agente inerte, que e o mesmo sintoma outra vez.
        /// </summary>
        private void Replant(Behaviour behaviour)
        {
            var agent = behaviour as UnityEngine.AI.NavMeshAgent;
            if (agent == null || rui == null) return;

            if (UnityEngine.AI.NavMesh.SamplePosition(rui.position, out var hit, 1.5f,
                    UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                return;
            }

            Debug.LogWarning("[RuiOnTheFloor] Nao ha NavMesh a menos de 1,5 m de onde ele "
                           + "se levantou (" + rui.position.ToString("F2") + "). O agente fica "
                           + "inerte e ele nao volta a andar.");
        }

        /// <summary>
        /// Perto e a olhar mesmo para aquilo. O teste completo — enquadramento e
        /// linha de vista — e o mesmo que o <see cref="WitnessedCue"/> usa.
        /// </summary>
        private bool Seen()
        {
            if (rui == null) return false;

            float distance = Vector3.Distance(player.position, rui.position);
            if (distance > maximumDistance) return false;
            if (eye == null) return true;

            return Pungent.NPC.PlayerSight.CanPlayerSee(
                eye, rui, bodyHeight, sightBlockers, 0.04f, maximumDistance + 10f);
        }

        private bool ResolvePlayer()
        {
            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor == null) return false;
                player = motor.transform;
            }

            if (eye == null && player != null) eye = player.GetComponentInChildren<Camera>(true);
            if (eye == null) eye = Camera.main;

            return true;
        }

        /// <summary>Na duvida cala-se, como o resto do jogo.</summary>
        private bool Allowed()
        {
            // O passo da noite manda, quando ha um. E a janela exacta: abre quando
            // o objectivo passa a "Talk to Rui" e fecha quando ele avanca, portanto
            // nao ha maneira de o corpo aparecer numa noite que ja seguiu em frente.
            if (quest != null) return quest.Current == lieDownOn;

            bool gated = !string.IsNullOrWhiteSpace(requiresEvent)
                      || !string.IsNullOrWhiteSpace(silencedByEvent);
            if (!gated) return true;

            director = ChapterDirector.Resolve(director);
            if (director == null) return false;

            if (!string.IsNullOrWhiteSpace(requiresEvent) && !director.HasSeen(requiresEvent))
                return false;
            if (!string.IsNullOrWhiteSpace(silencedByEvent) && director.HasSeen(silencedByEvent))
                return false;

            return true;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(Transform who, Animator anim, Vector3 where, float yaw,
            MonoBehaviour[] toSuppress, Behaviour[] toDisable, string requires, string silencedBy,
            AudioSource source = null, AudioClip stir = null, string raises = null,
            float maxDistance = 5f, float dwell = 1.6f)
        {
            rui = who;
            animator = anim;
            lyingPosition = where;
            lyingYaw = yaw;
            suppressed = toSuppress;
            alsoDisabled = toDisable;
            requiresEvent = requires;
            silencedByEvent = silencedBy;
            sound = source;
            stirClip = stir;
            if (raises != null) raisesEvent = raises;
            maximumDistance = maxDistance;
            dwellSeconds = dwell;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.75f, 0.1f, 0.15f, 0.9f);
            Gizmos.DrawWireCube(lyingPosition + Vector3.up * 0.1f, new Vector3(1.8f, 0.2f, 0.6f));
            Gizmos.color = new Color(0.75f, 0.1f, 0.15f, 0.18f);
            Gizmos.DrawWireSphere(lyingPosition, maximumDistance);
        }
    }
}
