using UnityEngine;

namespace Pungent.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DoorDragInteractable : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private Rigidbody body;
        [SerializeField] private HingeJoint hinge;
        [SerializeField, Range(-120f, 120f)] private float openAngle = -85f;
        [SerializeField, Min(0.01f)] private float springAcceleration = 1f;
        [SerializeField, Min(0f)] private float damping = 0.9f;
        [SerializeField, Min(0.1f)] private float maximumAcceleration = 100f;
        [SerializeField] private bool locked;

        [Header("Porta estática")]
        [Tooltip("Mantém a folha completamente imóvel e sem interação própria. "
               + "Serve a porta 3B: no clímax a saída pertence ao ClimaxExit e "
               + "termina num corte, portanto esta folha nunca precisa de física.")]
        [SerializeField] private bool staticDoor;

        [Header("Navegacao")]
        [Tooltip("Obstaculo que tapa o vao na NavMesh enquanto a porta esta fechada. "
               + "Sem ele os NPCs atravessam a folha: a NavMesh e feita uma vez com "
               + "os vaos livres e nao sabe que ha ali uma porta.")]
        [SerializeField] private UnityEngine.AI.NavMeshObstacle navObstacle;

        [Header("Colisao")]
        [Tooltip("A folha perde colisao enquanto roda. Uma porta com dobradica e "
               + "mola empurra fisicamente quem estiver no vao: ou atira com o "
               + "jogador, ou fica encravada contra ele a meio do arco. Sem colisao "
               + "durante o movimento ela passa e assenta, e so volta a ter corpo "
               + "quando esta parada.")]
        [SerializeField] private bool disableCollisionWhileMoving = true;

        [Tooltip("Tapa o vao enquanto uma porta trancada ainda se esta a fechar. "
               + "Sem isto ha uma janela de cerca de um segundo em que a fechadura "
               + "ja estalou e o vao ainda esta aberto e sem corpo nenhum: o "
               + "`SetLocked` corta a interaccao com a folha, nao corta a passagem. "
               + "O dono entrou no quarto do Rui por ali, pelo meio da folha, "
               + "depois de o Rui a ter trancado a frente dele. "
               + "Tapa-se com uma copia parada da folha fechada, e nao dando corpo "
               + "a folha a rodar: essa foi a primeira tentativa e encrava a porta "
               + "contra o chao a meio do arco.")]
        [SerializeField] private bool blockDoorwayWhileLocked = true;


        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip[] openingClips;
        [SerializeField] private AudioClip[] closingClips;
        [SerializeField, Range(0f, 1f)] private float interactionVolume = 0.72f;
        [SerializeField] private Vector2 pitchRange = new Vector2(0.96f, 1.03f);

        private bool open;
        private int previousOpeningClip = -1;
        private int previousClosingClip = -1;

        private Collider[] leafColliders;
        private BoxCollider doorwayBlocker;
        private bool blockerOn;
        private bool collisionOn = true;
        private readonly Collider[] overlapBuffer = new Collider[8];

        [Header("Estremecer")]
        [Tooltip("Mola do estremecimento, so durante a batida. Muito mais rija do "
               + "que a normal de proposito: a porta do jogo e lenta porque abre "
               + "devagar, e uma pancada nao e um balanco — e um tique que volta.\n\n"
               + "900 da `raiz(900)` = 30 rad/s, ou seja 4,8 Hz. Nao subir muito "
               + "acima disto: a fisica corre a 50 Hz, e um oscilador precisa de "
               + "uns dez passos por ciclo para ser integrado em vez de amostrado. "
               + "A 30 rad/s sao dezasseis; ao dobro ja se ve o passo da fisica "
               + "e nao a folha.")]
        [SerializeField, Min(1f)] private float rattleSpring = 900f;

        [Tooltip("Travagem do estremecimento. Abaixo do critico (2*raiz(mola) = 60) "
               + "para dar dois ou tres saltos visiveis em vez de um so.\n\n"
               + "15 sao vinte e cinco por cento do critico: cada salto fica a um "
               + "quinto do anterior, portanto ve-se o primeiro com forca, o "
               + "segundo bem, e o terceiro no limite.")]
        [SerializeField, Min(0f)] private float rattleDamping = 15f;

        [Tooltip("Empurrao de cada pancada, em graus por segundo.\n\n"
               + "Com a mola e a travagem acima, 60 dao **1,09 graus** de desvio "
               + "no primeiro salto, e depois 0,43 / 0,17 / 0,07 — medido a "
               + "simular o `FixedUpdate` a 50 Hz com o `angularDrag` da folha, "
               + "e nao estimado. Um grau numa folha de oitenta centimetros sao "
               + "quinze milimetros na ponta: ve-se, e nao chega a ler-se como a "
               + "porta a abrir-se sozinha.")]
        [SerializeField, Min(0f)] private float rattleKick = 60f;

        [Tooltip("Quanto tempo cada pancada manda na folha.\n\n"
               + "Mais longo do que o intervalo entre pancadas (0,28 s) de "
               + "proposito, para as janelas se sobreporem: o que interessa e a "
               + "ultima, e com esta mola a folha so assenta de vez uns quatro "
               + "decimos depois. Se a janela acabasse antes disso, a mola fraca "
               + "do dia-a-dia herdava uma porta ainda a mexer e ficava a arrastar "
               + "o resto durante quase um segundo — com os colliders desligados, "
               + "que e o que isto existe para evitar.\n\n"
               + "Sete decimos e nao quatro e meio, e a diferenca e medida: a "
               + "folha so desce abaixo do limiar de `moving` — meio grau por "
               + "segundo — aos 0,44 s, e o quarto salto ainda existe depois "
               + "disso. Uma janela que acabasse a 0,45 entregava ao controlo "
               + "normal uma porta ainda a mexer, e ele **desliga os colliders "
               + "quando ve movimento**: ficava um buraco de dois decimos com a "
               + "folha atravessavel, invisivel a olho, no unico instante do "
               + "jogo em que o jogador esta encostado a ela. A folga aqui nao "
               + "custa nada — com a porta parada a mola rija nao tem erro "
               + "nenhum para corrigir.")]
        [SerializeField, Min(0.02f)] private float rattleSeconds = 0.7f;

        private float rattleUntil;

        /// <summary>Disparado quando o jogador tranca a porta com as proprias maos.</summary>
        public event System.Action Locked;

        [Header("Trancar por dentro")]
        [Tooltip("Quando armado, a porta esta fechada e o clique TRANCA em vez de abrir. "
               + "E o que se quer quando o jogo pede ao jogador para se fechar por dentro: "
               + "o gesto tem de ser trancar, nao abrir por engano.")]
        [SerializeField] private bool lockPromptArmed;
        [SerializeField] private AudioClip lockClip;
        [SerializeField, Range(0f, 1f)] private float lockVolume = 0.8f;

        [Tooltip("Opcional. Se existir, pode fazer a fechadura falhar uma vez antes "
               + "de pegar. Ver `StickyLock`.")]
        [SerializeField] private StickyLock stickyLock;

        public string Prompt
        {
            get
            {
                if (staticDoor) return string.Empty;
                if (LockAvailable) return "Lock the door";
                if (locked) return "The door is locked";
                return open ? "Close door" : "Open door";
            }
        }
        public bool HoldToInteract => false;

        /// <summary>O clique vai trancar em vez de abrir?</summary>
        private bool LockAvailable => lockPromptArmed && !locked && !open;

        public bool IsLocked => locked;

        /// <summary>
        /// Arma ou desarma o gesto de trancar. Com isto ligado o jogador nao volta a
        /// abrir a porta por acidente quando o que lhe foi pedido foi tranca-la.
        /// </summary>
        public void SetLockPromptArmed(bool value) => lockPromptArmed = value;

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (hinge == null) hinge = GetComponent<HingeJoint>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (hinge != null) hinge.useSpring = false;
            if (body != null)
            {
                body.maxAngularVelocity = 3.2f;
                if (staticDoor) body.isKinematic = true;
            }

            // So os colisores solidos da folha. Os triggers, se algum dia houver,
            // servem deteccao e nao devem desligar-se com o movimento.
            var found = GetComponentsInChildren<Collider>(true);
            var solid = new System.Collections.Generic.List<Collider>(found.Length);
            foreach (var c in found)
                if (c != null && !c.isTrigger) solid.Add(c);
            leafColliders = solid.ToArray();

            ReportRestingPenetration();
            BuildDoorwayBlocker();
            ApplyNavObstacle();
        }

        /// <summary>
        /// Diz alto quando a folha fechada nasce dentro de geometria que nao se
        /// mexe.
        ///
        /// **Foi assim que tres portas desta casa passaram a atravessar-se, e nunca
        /// deu uma linha na consola.** A folha vai de y=0,021 a y=2,079, igual em
        /// todas as portas. As vergas e que nao estao a mesma altura — quatro
        /// foram esticadas na vertical e a base delas desceu para dentro do vao:
        ///
        ///     Int_BathLaundry_head1   base 2,1000   folga +0,021   Laundry   solida
        ///     Int_CorridorSouth_head1 base 2,0756   folga -0,003   Tomas     solida
        ///     Int_HallStorage_head1   base 2,0650   folga -0,014   Storage   atravessa
        ///     Int_CorridorSouth_head5 base 2,0620   folga -0,017   Bathroom  atravessa
        ///     Int_CorridorSouth_head3 base 2,0550   folga -0,024   Rui       atravessa
        ///
        /// O ciclo: a folha recupera corpo, o PhysX ve-a enfiada na verga e empurra-a
        /// para fora; o empurrao afunda a porta na dobradica e inclina-lhe o eixo;
        /// com o eixo fora da vertical a gravidade roda-a para fora do zero; `moving`
        /// volta a ser verdadeiro e os colisores desligam-se outra vez. Recomeca.
        /// Fica sem corpo quase sempre, e solida um frame de onde a onde — que e
        /// exactamente o que se via a jogar.
        ///
        /// **Nao se corrige aqui de proposito.** A verga e geometria do dono, e o
        /// arranjo e levantar-lhe a base. Tapar isto por codigo — ignorando o par
        /// de colisores — deixava a porta solida e continuava a enfiar-lhe o canto
        /// de cima dentro da ombreira, agora sem ninguem dar por isso. O que o
        /// codigo tem de garantir e que deixa de ser silencioso.
        /// </summary>
        private void ReportRestingPenetration()
        {
            if (staticDoor || leafColliders == null) return;

            // As posicoes da cena so entram no PhysX no primeiro passo de fisica,
            // e isto corre antes disso: sem sincronizar, mede-se o sitio errado.
            Physics.SyncTransforms();

            var found = new Collider[32];
            for (int i = 0; i < leafColliders.Length; i++)
            {
                var self = leafColliders[i];
                if (self == null || !self.enabled) continue;

                Bounds box = self.bounds;
                int count = Physics.OverlapBoxNonAlloc(box.center, box.extents, found,
                    Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

                for (int j = 0; j < count; j++)
                {
                    var other = found[j];
                    if (other == null || other == self) continue;
                    if (other.transform.IsChildOf(transform)) continue;

                    // So o que nao cede. Uma caixa solta encostada a folha tambem
                    // penetra, mas essa e empurrada e o assunto morre num frame.
                    var otherBody = other.attachedRigidbody;
                    if (otherBody != null && !otherBody.isKinematic) continue;

                    Vector3 direction;
                    float depth;
                    if (!Physics.ComputePenetration(
                            self, self.transform.position, self.transform.rotation,
                            other, other.transform.position, other.transform.rotation,
                            out direction, out depth))
                        continue;

                    // Abaixo do `contactOffset` o solver nao chega a empurrar. Nao
                    // e teoria: a porta do Tomas nasce 3 mm dentro da verga dela e
                    // e solida na mesma, e as que atravessam nascem a 14 mm ou mais.
                    if (depth <= self.contactOffset) continue;

                    Debug.LogError("[DoorDragInteractable] " + name + ": a folha nasce "
                        + depth.ToString("F4") + " m dentro de '" + other.name
                        + "' (sair por " + direction.ToString("F2") + "). O PhysX empurra-a "
                        + "a cada frame em que ela recupera corpo, e a porta fica "
                        + "atravessavel quase sempre. Afasta a geometria: enquanto isto "
                        + "durar, esta porta nao barra ninguem.", this);
                }
            }
        }

        /// <summary>
        /// Levanta o tapa-vao: uma copia parada da folha fechada, que so entra em
        /// servico enquanto uma porta trancada ainda esta a fechar-se.
        ///
        /// **Porque nao bastava dar corpo a folha.** A primeira tentativa foi
        /// manter os colisores da folha ligados enquanto ela roda trancada. Nao
        /// funciona, e esta medido: a folha raspa no chao e na verga — o
        /// `SomeoneInDoorway` ja o dizia por outras palavras — e uma folha solida
        /// a rodar contra geometria estatica encrava. Nos ensaios ficou a 27 e a
        /// 34 graus e nunca mais fechou, o que e pior do que o buraco que se
        /// queria tapar.
        ///
        /// Este objecto nao roda, por isso nao raspa em nada. E filho da raiz da
        /// porta e nao da folha, fica exactamente onde a folha fechada esta, e
        /// vive desligado — o unico instante em que existe e entre a fechadura
        /// estalar e a folha assentar. Depois disso e a propria folha que tapa o
        /// vao, como sempre tapou.
        /// </summary>
        private void BuildDoorwayBlocker()
        {
            if (staticDoor || !blockDoorwayWhileLocked) return;

            BoxCollider source = null;
            for (int i = 0; i < leafColliders.Length; i++)
            {
                source = leafColliders[i] as BoxCollider;
                if (source != null) break;
            }
            if (source == null) return;

            // A pose de referencia e a folha **fechada**. Se a porta comecar
            // aberta na cena, a copia ficava a tapar o sitio errado e o jogador
            // batia num muro invisivel a meio do quarto. Isso e um erro de cena,
            // e tem de se ouvir: nao ha fallback que o esconda.
            if (open)
            {
                Debug.LogError("[DoorDragInteractable] " + name + " comeca aberta: "
                    + "nao ha pose fechada de onde tirar o tapa-vao. Fecha-a na cena "
                    + "ou desliga blockDoorwayWhileLocked.", this);
                return;
            }

            // **Filho do pai da porta, e nao da porta.** O Rigidbody e o
            // HingeJoint vivem na raiz `Door_X`, por isso e a raiz que roda e
            // tudo o que la esteja pendurado roda com ela. Pendurado na porta,
            // este objecto rodava junto, entrava na conta do corpo dinamico e
            // encravava a folha contra o chao a 31 graus — que e exactamente o
            // sintoma que se estava a tentar evitar, agora por outro caminho.
            // Medido, nao suposto.
            Transform holder = transform.parent;
            var go = new GameObject("__DoorwayBlocker");
            go.layer = source.gameObject.layer;
            go.transform.SetParent(holder, false);
            go.transform.SetPositionAndRotation(source.transform.position,
                                                source.transform.rotation);

            Vector3 want = source.transform.lossyScale;
            Vector3 parentScale = holder == null ? Vector3.one : holder.lossyScale;
            go.transform.localScale = new Vector3(
                Mathf.Approximately(parentScale.x, 0f) ? want.x : want.x / parentScale.x,
                Mathf.Approximately(parentScale.y, 0f) ? want.y : want.y / parentScale.y,
                Mathf.Approximately(parentScale.z, 0f) ? want.z : want.z / parentScale.z);

            var box = go.AddComponent<BoxCollider>();
            box.center = source.center;
            box.size = source.size;
            box.enabled = false;

            doorwayBlocker = box;
        }

        /// <summary>
        /// Tapa ou destapa o vao.
        ///
        /// Chamado todos os frames a partir do `FixedUpdate`, e por isso a
        /// condicao vive num sitio so: o vao esta tapado exactamente enquanto a
        /// porta estiver trancada **e** a folha ainda em movimento. Nao ha estado
        /// a manter e nao ha caminho por onde isto fique ligado depois de a folha
        /// assentar.
        /// </summary>
        private void SetDoorwayBlocked(bool value)
        {
            if (doorwayBlocker == null || blockerOn == value) return;
            blockerOn = value;
            doorwayBlocker.enabled = value;
        }

        /// <summary>
        /// So tapa o vao quando a porta esta mesmo fechada. A meio do movimento fica
        /// desligado de proposito: um obstaculo a carvar enquanto roda obriga a
        /// NavMesh a refazer-se todos os frames e faz os agentes tremer.
        /// </summary>
        private void ApplyNavObstacle()
        {
            if (navObstacle == null) return;
            navObstacle.carveOnlyStationary = true;
            navObstacle.enabled = !open;
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (staticDoor) return;

            // O gesto de trancar tem precedencia sobre o de abrir.
            if (LockAvailable)
            {
                // A fechadura pode nao pegar. Ver `StickyLock`: uma vez so, num dia
                // marcado, e a porta continua a ser quem manda no booleano — o outro
                // componente so responde se desta vez pegou ou nao.
                if (stickyLock != null && stickyLock.ConsumeJam()) return;

                locked = true;
                lockPromptArmed = false;
                ApplyNavObstacle();
                if (audioSource != null && lockClip != null)
                {
                    audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
                    audioSource.PlayOneShot(lockClip, lockVolume);
                }
                Locked?.Invoke();
                return;
            }

            if (locked || hinge == null || body == null) return;
            open = !open;
            body.WakeUp();
            ApplyNavObstacle();
            PlayInteractionSound(open ? openingClips : closingClips, open);
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }

        public void EndInteraction(PlayerInteractor interactor) { }

        public void SetLocked(bool value) => locked = value;

        /// <summary>
        /// Poe a porta aberta ou fechada sem ninguem lhe tocar.
        ///
        /// **Calada por omissao, e e esse o ponto.** Quem chama isto sao as
        /// encenacoes que montam um dia — o `DayThreeStage` deixa a porta do Rui
        /// aberta antes de o jogador acordar, e volta a fecha-la quando ele nao esta
        /// a olhar. Uma porta que se ouve a mexer sozinha e um susto barato; uma
        /// porta que **ja estava** assim quando se chegou e o jogo inteiro.
        ///
        /// Nao mexe no trinco: uma porta trancada nao se abre por encenacao, senao
        /// perde-se a unica coisa que o trancar tem para dar.
        /// </summary>
        public void SetOpen(bool value, bool audible = false)
        {
            if (staticDoor || hinge == null || body == null) return;
            if (locked || open == value) return;

            open = value;
            body.WakeUp();
            ApplyNavObstacle();
            if (audible) PlayInteractionSound(open ? openingClips : closingClips, open);
        }

        /// <summary>
        /// Fecha a porta sem passar pelo jogador nem tocar o som de interacao.
        /// Serve os momentos encenados em que alguem a fecha por ti.
        /// </summary>
        public void ForceClosed()
        {
            open = false;
            ApplyNavObstacle();
            if (staticDoor) return;
            if (body == null) return;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }

        public bool IsOpen => open;

        /// <summary>
        /// Abre a porta sem o jogador. Serve os NPCs: com o vao tapado na NavMesh
        /// enquanto a porta esta fechada, um NPC que precise de entrar tem de a
        /// abrir primeiro — senao fica parado do outro lado sem caminho nenhum.
        /// </summary>
        public void ForceOpen()
        {
            if (locked || open) return;
            open = true;
            ApplyNavObstacle();
            if (staticDoor) return;
            if (body != null) body.WakeUp();
            PlayInteractionSound(openingClips, true);
        }

        /// <summary>
        /// Uma pancada na folha, do lado de fora.
        ///
        /// **Nao roda o transform.** A folha e um `Rigidbody` num `HingeJoint`, e
        /// mexer-lhe a rotacao a mao poe o motor de fisica e este codigo a puxar
        /// para lados diferentes no mesmo frame. Um empurrao angular e o que uma
        /// pancada e de verdade, e a mola trata do regresso sozinha.
        ///
        /// O empurrao vai **no sentido de abrir** porque e o unico lado com espaco:
        /// para o outro esta o aro. Sai de `openAngle`, e nao de um sinal escrito
        /// a mao, para funcionar nas portas que abrem ao contrario.
        ///
        /// Uma porta aberta nao estremece — baloica, que e outra coisa e fica mal.
        /// </summary>
        public void Rattle()
        {
            if (staticDoor || hinge == null || body == null || open) return;

            rattleUntil = Time.time + rattleSeconds;
            body.WakeUp();

            Vector3 axisWorld = transform.TransformDirection(hinge.axis).normalized;
            body.AddTorque(
                axisWorld * (rattleKick * Mathf.Sign(openAngle) * Mathf.Deg2Rad),
                ForceMode.VelocityChange);
        }

        private void FixedUpdate()
        {
            if (staticDoor) return;
            if (hinge == null || body == null) return;

            Vector3 axisWorld = transform.TransformDirection(hinge.axis).normalized;
            float angularSpeedDegrees = Vector3.Dot(body.angularVelocity, axisWorld) * Mathf.Rad2Deg;

            // Enquanto estremece, manda a mola rija — e **a colisao fica ligada**.
            //
            // Esta e a parte que interessa. O controlo normal desliga os colliders
            // da folha assim que ela se mexe, para ninguem ficar preso no vao a
            // atravessar uma porta a abrir. Uma batida tambem a poe a mexer, e sem
            // isto a porta do quarto ficava atravessavel durante o segundo e meio
            // em que o Rui bate — que e exactamente o instante em que o jogador
            // esta encostado a ela.
            //
            // O `!open` nao e defensiva a mais: ouvir bater e abrir a porta e a
            // reaccao normal de quem esta do lado de dentro. Sem ele, a mola rija
            // ficava a puxar para fechado durante quase um segundo e meio e a
            // porta recusava-se a abrir no unico momento em que alguem a quer
            // abrir. Assim que o jogador manda abrir, o estremecimento deixa de
            // ter voto e o controlo normal assume.
            if (!open && Time.time < rattleUntil)
            {
                // **Convertido para radianos, e e a linha de que tudo isto depende.**
                //
                // O erro e a velocidade vem em graus, e o `ForceMode.Acceleration`
                // le radianos por segundo ao quadrado. Sem o `Deg2Rad`, a mola que
                // chega ao motor de fisica e 57 vezes a que esta escrita aqui: os
                // 900 viravam 51 600, a frequencia saltava de 4,8 Hz para 36, e o
                // amortecimento — que parece um quarto do critico — passava a tres
                // vezes o critico. A folha nao dava salto nenhum: desviava-se tres
                // centesimas de grau e voltava. Meio milimetro na ponta.
                //
                // E aos 36 Hz nem sequer chegava a ser isso. A fisica corre a 50 Hz
                // e um ciclo de 36 Hz nao cabe em dois passos; o que se integrava
                // deixava de ser uma mola e passava a ser o resto da divisao.
                //
                // O `Deg2Rad` vem **antes** do `Clamp` de proposito. Ao contrario,
                // o limite de 1120 ficava em graus e valia 19,5 rad/s² depois da
                // conversao — abaixo dos 24 que o proprio estremecimento precisa,
                // e a rede de seguranca passava a ser o tecto.
                float rattleError = Mathf.DeltaAngle(hinge.angle, 0f);
                float rattleAcceleration =
                    (rattleError * rattleSpring - angularSpeedDegrees * rattleDamping)
                    * Mathf.Deg2Rad;

                SetLeafCollision(true);
                body.AddTorque(axisWorld * Mathf.Clamp(rattleAcceleration,
                        -maximumAcceleration * 8f, maximumAcceleration * 8f),
                    ForceMode.Acceleration);
                return;
            }

            float targetAngle = open ? openAngle : 0f;
            float angleError = Mathf.DeltaAngle(hinge.angle, targetAngle);
            float acceleration = angleError * springAcceleration - angularSpeedDegrees * damping;
            acceleration = Mathf.Clamp(acceleration, -maximumAcceleration, maximumAcceleration);

            bool moving = Mathf.Abs(angleError) > 0.15f || Mathf.Abs(angularSpeedDegrees) > 0.5f;
            SetLeafCollision(!moving);
            SetDoorwayBlocked(locked && moving);

            if (moving)
                body.AddTorque(axisWorld * acceleration, ForceMode.Acceleration);
        }

        /// <summary>
        /// Liga e desliga o corpo solido da folha.
        ///
        /// A voltar a ligar ha um cuidado: se alguem estiver dentro do volume da
        /// folha nesse instante, fica preso la. Nesse caso adia-se — a porta so
        /// recupera colisao depois de o vao estar livre.
        /// </summary>
        private void SetLeafCollision(bool value)
        {
            if (!disableCollisionWhileMoving || leafColliders == null) return;
            if (collisionOn == value) return;
            if (value && SomeoneInDoorway()) return;

            collisionOn = value;
            for (int i = 0; i < leafColliders.Length; i++)
                if (leafColliders[i] != null) leafColliders[i].enabled = value;
        }

        /// <summary>
        /// Ha uma pessoa dentro do volume da folha?
        ///
        /// Procura-se so por gente — jogador ou NPC — e nao por qualquer colisor.
        /// A folha fechada encosta ao aro e ao chao, por isso um teste generico
        /// dava sempre positivo e a porta nunca mais recuperava corpo.
        ///
        /// A caixa e derivada do transform e nao de `collider.bounds`, porque os
        /// colisores estao desligados neste momento e as bounds de um colisor
        /// desligado nao sao de confianca.
        /// </summary>
        private bool SomeoneInDoorway()
        {
            for (int i = 0; i < leafColliders.Length; i++)
            {
                var box = leafColliders[i] as BoxCollider;
                if (box == null) continue;

                Transform t = box.transform;
                Vector3 centre = t.TransformPoint(box.center);
                Vector3 half = Vector3.Scale(box.size * 0.5f, t.lossyScale);

                int count = Physics.OverlapBoxNonAlloc(centre, half, overlapBuffer,
                    t.rotation, ~0, QueryTriggerInteraction.Ignore);

                for (int j = 0; j < count; j++)
                {
                    var other = overlapBuffer[j];
                    if (other == null) continue;
                    if (other.transform.IsChildOf(transform)) continue;

                    if (other.GetComponentInParent<CharacterController>() != null) return true;
                    if (other.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null) return true;

                    // E qualquer corpo solto. Procurava-se so por gente, e uma caixa
                    // do prologo deixada no vao dos arrumos ficava de fora da conta:
                    // a folha recuperava corpo por cima dela, empurrava-a, era
                    // empurrada de volta, e a porta passava a noite a bater sozinha.
                    // Visto a jogar, e nao ha erro nenhum que o apanhe.
                    var rigidbody = other.attachedRigidbody;
                    if (rigidbody != null && !rigidbody.isKinematic) return true;
                }
            }
            return false;
        }

        private void PlayInteractionSound(AudioClip[] clips, bool opening)
        {
            if (audioSource == null || clips == null || clips.Length == 0)
                return;

            int previousIndex = opening ? previousOpeningClip : previousClosingClip;
            int clipIndex = Random.Range(0, clips.Length);
            if (clips.Length > 1 && clipIndex == previousIndex)
                clipIndex = (clipIndex + 1) % clips.Length;

            AudioClip clip = clips[clipIndex];
            if (clip == null)
                return;

            if (opening)
                previousOpeningClip = clipIndex;
            else
                previousClosingClip = clipIndex;

            audioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
            audioSource.PlayOneShot(clip, interactionVolume);
        }
    }
}
