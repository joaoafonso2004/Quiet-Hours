using Pungent.Player;
using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>
    /// Aponta-se e carrega-se. Nada mais do que isso.
    ///
    /// ---
    ///
    /// **O que estava errado, e nao era o desenho: era uma linha.**
    ///
    /// Este ficheiro tinha tres caminhos de busca, dois criterios de angulo, um
    /// desempate a tres niveis e seiscentas linhas de notas a explicar cada um. Todos
    /// eles foram escritos para compensar o mesmo defeito, e nenhum deles o
    /// encontrou.
    ///
    /// O `SphereCast` devolve os colisores que ja tocam a esfera **no inicio** do
    /// varrimento, com `distance = 0`. Da origem — a camara — o primeiro deles e
    /// sempre o `CharacterController` do proprio jogador. O calculo de oclusao
    /// aceitava-o como parede:
    ///
    ///     occludedAt = 0
    ///     if (hit.distance > occludedAt + 0.02f) continue;
    ///
    /// **Tudo o que estivesse a mais de dois centimetros da cara era deitado fora.**
    /// Medido no lava-loica da cozinha: sete colisores no cone, o lava-loica a 1,20 m
    /// e tres pontos de tarefa a 1,05 m, e nenhum deles chegava a ser considerado.
    ///
    /// O cone de tolerancia nunca funcionou. O que restava era o raio fino do segundo
    /// caminho — que escapa por outra razao, `Raycast` ignora colisores que contenham
    /// a origem — e por isso a interaccao era do tamanho de um pixel. A jogar lia-se
    /// como "so funciona quando ando": a respiracao e o headbob mexem a camara, o raio
    /// varre, e de vez em quando acerta.
    ///
    /// As tres camadas de heuristica que existiam por cima disto eram todas a tentar
    /// devolver a tolerancia que a linha de oclusao estava a comer.
    ///
    /// ---
    ///
    /// **As regras agora, e sao cinco.**
    ///
    /// 1. **O corpo do jogador nao existe para esta pergunta.** O
    ///    `CharacterController`, o telemovel na mao e o seu ecra sao filhos da camara
    ///    ou do jogador e nunca sao alvo nem parede. Era isto que faltava.
    /// 2. **Um colisor que contenha a camara nao tapa nada.** Estar dentro de uma
    ///    coisa nao poe essa coisa a frente do que se ve.
    /// 3. **Ganha o mais perto ao longo da mira.** E o criterio de qualquer jogo com
    ///    interaccao em primeira pessoa, e nao precisa de mais nada quando os volumes
    ///    de ajuda estao postos.
    /// 4. **Empate a menos de 15 cm ganha o mais pequeno.** Uma coisa pousada em cima
    ///    de outra e sempre a que se quer: a roupa em cima da cama, o prato na
    ///    bancada, o portatil na secretaria.
    /// 5. **Nada atraves de paredes.** Um solido pelo meio tapa; um trigger, o proprio
    ///    objecto e um interactavel calado nao tapam.
    ///
    /// O que nao mudou, porque estava certo e paga-se caro por o perder: um prompt
    /// vazio quer dizer "isto nao da agora" e o alvo passa a ser atravessado
    /// (<see cref="Resolve"/>), e a caixa ao colo nunca tapa nem ganha.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PrototypeHUD hud;
        [SerializeField, Min(0.5f)] private float reach = 2.5f;
        [SerializeField] private LayerMask interactionMask = ~0;

        [Header("Tolerancia de mira")]
        [Tooltip("Raio do varrimento. Maior = mais facil apontar a coisas pequenas.\n\n"
               + "Isto so passou a fazer alguma coisa depois de o corpo do jogador "
               + "deixar de contar como parede — ate ai, o cone inteiro era descartado "
               + "e a mira valia um pixel.")]
        [SerializeField, Range(0.02f, 0.35f)] private float probeRadius = 0.14f;

        [Tooltip("Com a camara dentro do volume de um objecto, ainda e preciso estar "
               + "virado para ele.\n\nSem isto, encostar-se a uma coisa dava-lhe o "
               + "clique mesmo de costas: os volumes de ajuda de mira sao colunas de "
               + "quase um metro e meio e o jogador entra dentro delas.")]
        [SerializeField, Range(30f, 180f)] private float insideAngle = 100f;

        [Tooltip("Mantem o alvo durante este tempo depois de o perder, para o prompt "
               + "nao piscar enquanto o jogador respira.")]
        [SerializeField, Range(0f, 0.5f)] private float focusGrace = 0.18f;

        // A cozinha e a entrada chegam a mais de 16 colisores dentro do cone. 64
        // continua sem alocacoes e cobre a cena inteira medida.
        private readonly RaycastHit[] castBuffer = new RaycastHit[64];

        // Separado: a verificacao de linha de vista corre **dentro** do ciclo que
        // percorre o outro, e reutiliza-lo apagava a lista a meio.
        private readonly RaycastHit[] sightBuffer = new RaycastHit[32];

        private IPlayerInteractable stickyFocus;
        private float stickyUntil;

        private IPlayerInteractable focused;
        private IPlayerInteractable active;
        private bool interactionBlocked;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (hud == null) hud = GetComponent<PrototypeHUD>();
        }

        private void Update()
        {
            if (interactionBlocked)
            {
                hud?.SetPrompt(string.Empty);
                return;
            }

            if (active != null)
            {
                if (input.InteractReleased || !input.InteractHeld)
                {
                    active.EndInteraction(this);
                    active = null;
                    input.SetLookSuppressed(false);
                }
                else
                {
                    active.UpdateInteraction(this, input.PointerDelta);
                }

                hud?.SetPrompt(string.Empty);
                return;
            }

            focused = FindFocusedInteractable();

            // Histerese: sem isto, perder o alvo por um frame fazia o prompt e o
            // reticulo piscarem enquanto o jogador se mexe.
            if (focused != null)
            {
                stickyFocus = focused;
                stickyUntil = Time.time + focusGrace;
            }
            else if (stickyFocus != null && Time.time <= stickyUntil)
            {
                focused = stickyFocus;
            }
            else
            {
                stickyFocus = null;
            }

            hud?.SetPrompt(focused != null ? focused.Prompt : string.Empty);

            if (focused == null || !input.InteractPressed) return;

            // BeginInteraction pode bloquear a interacao a meio deste Update
            // (o dialogo presencial chama SetInteractionBlocked, que limpa `focused`).
            // A referencia tem de ser guardada localmente antes da chamada.
            IPlayerInteractable target = focused;
            target.BeginInteraction(this);

            if (interactionBlocked) return;

            if (target.HoldToInteract)
            {
                active = target;
                input.SetLookSuppressed(true);
            }
            else
            {
                target.EndInteraction(this);
            }
        }

        /// <summary>
        /// Um varrimento, e o mais perto ganha.
        ///
        /// Um `SphereCast` so — nao tres caminhos. Ele ja devolve o que um raio fino
        /// devolveria **e** o que estiver a tocar na esfera de partida, que era o caso
        /// para o qual existia o terceiro caminho: encostar-se a uma coisa. A unica
        /// razao por que aquilo tudo era preciso e que esta funcao estava a tratar o
        /// corpo do jogador como uma parede a zero metros.
        /// </summary>
        private IPlayerInteractable FindFocusedInteractable()
        {
            if (playerCamera == null) return null;

            Vector3 origin = playerCamera.transform.position;
            Vector3 direction = playerCamera.transform.forward;

            int count = Physics.SphereCastNonAlloc(origin, probeRadius, direction, castBuffer,
                reach, interactionMask, QueryTriggerInteraction.Collide);

            float occludedAt = OcclusionDistance(count);

            IPlayerInteractable best = null;
            float bestAngle = float.MaxValue, bestDistance = float.MaxValue, bestSize = float.MaxValue;

            IPlayerInteractable ambient = null;
            float ambientAngle = float.MaxValue, ambientDistance = float.MaxValue, ambientSize = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = castBuffer[i];
                Collider collider = hit.collider;
                if (Ignored(collider)) continue;

                // Uma folga pequena: o volume de ajuda de mira de um objecto comeca
                // muitas vezes rente a superficie que o tapa.
                if (hit.distance > occludedAt + 0.02f) continue;

                IPlayerInteractable candidate = Resolve(collider);
                if (candidate == null) continue;

                Bounds body = BodyOf(candidate, collider);
                Vector3 aimPoint = AimPointOf(body, origin, direction);

                // Camara dentro do volume. A distancia nao separa nada aqui — e zero
                // para tudo o que nos envolva — por isso a pergunta passa a ser se o
                // jogador esta virado para a coisa. Ver `insideAngle`.
                if (hit.distance <= 0.0001f)
                {
                    Vector3 toBody = aimPoint - origin;
                    if (toBody.sqrMagnitude > 0.000001f &&
                        Vector3.Angle(direction, toBody) > insideAngle) continue;
                }

                if (Blocked(origin, aimPoint, candidate)) continue;

                // **Quem entra na lista e o volume; quem ganha e o corpo.**
                //
                // A distancia do `hit` e ate ao volume de ajuda de mira, e esses
                // volumes sobrepoem-se: as tres caixas do prologo estao a 60–130 cm
                // umas das outras e cada uma traz uma coluna mais larga do que ela.
                // A apontar a uma, o varrimento entra primeiro na coluna da do lado, e
                // essa ganhava por estar "mais perto" — medido em Play, `Box_Kitchen`
                // roubava a mira cinco vezes, e as tres caixas juntas falhavam 21 de
                // 36 direccoes.
                //
                // Medir ate ao **corpo** desfaz isso sem tocar na tolerancia: as
                // colunas continuam a por os candidatos na lista, e o que decide passa
                // a ser a caixa a que se esta mesmo a apontar. Para um ponto de tarefa
                // sem malha o corpo **e** o volume, e nada muda.
                float distance = Vector3.Distance(origin, aimPoint);

                // **O angulo decide, e nao a distancia.**
                //
                // Medido em Play: com a distancia a decidir, as tres caixas do prologo
                // acertavam 21 de 37 direccoes e roubavam-se umas as outras. Estao a
                // 60–130 cm de distancia e o jogador esta a 90 — a essa geometria,
                // "qual esta mais perto" nao tem resposta util, e nao e a pergunta que
                // ele esta a fazer. A pergunta e qual esta debaixo da mira.
                //
                // Medir ate ao corpo em vez de ate ao volume era preciso e nao
                // chegava: subiu 84% para 85% e nao mexeu nas caixas. O que faltava
                // era o criterio.
                Vector3 toBodyPoint = aimPoint - origin;
                float angle = toBodyPoint.sqrMagnitude > 0.000001f
                    ? Vector3.Angle(direction, toBodyPoint)
                    : 0f;

                float size = body.size.x * body.size.y * body.size.z;

                if (candidate is IAmbientInteractable)
                {
                    if (!Beats(angle, distance, size,
                               ambientAngle, ambientDistance, ambientSize)) continue;
                    ambientAngle = angle;
                    ambientDistance = distance;
                    ambientSize = size;
                    ambient = candidate;
                }
                else
                {
                    if (!Beats(angle, distance, size,
                               bestAngle, bestDistance, bestSize)) continue;
                    bestAngle = angle;
                    bestDistance = distance;
                    bestSize = size;
                    best = candidate;
                }
            }

            // Sem outro verbo a frente, o clique pousa a caixa. Isto torna a accao
            // independente de se conseguir apontar para um volume que ocupa quase todo
            // o ecra e se move com a camara.
            return best ?? ambient ?? CarryableBox.Carried;
        }

        /// <summary>
        /// A que distancia a vista fica tapada.
        ///
        /// **Um colisor que contenha a camara nao tapa nada** — e a correccao inteira
        /// deste ficheiro. O `SphereCast` devolve o que ja toca na esfera de partida
        /// com `distance = 0`, e o corpo do jogador esta sempre nessa lista. Aceitar
        /// esse zero como parede deitava fora todos os alvos do jogo.
        ///
        /// Triggers tambem nao tapam: volumes de historia e zonas de entrega existem
        /// para o `OnTriggerEnter` e nao para servir de parede. E um interactavel
        /// calado nao tapa — foi assim que uma caixa ja entregue impedia que se
        /// pousasse outra em cima dela.
        /// </summary>
        private float OcclusionDistance(int count)
        {
            float occludedAt = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider collider = castBuffer[i].collider;
                if (Ignored(collider)) continue;
                if (collider.isTrigger) continue;
                if (castBuffer[i].distance <= 0.0001f) continue;
                if (HasInteractable(collider)) continue;

                if (castBuffer[i].distance < occludedAt) occludedAt = castBuffer[i].distance;
            }

            return occludedAt;
        }

        /// <summary>
        /// O corpo do jogador e o que ele leva na mao: nunca alvo, nunca parede.
        ///
        /// Sao tres colisores e os tres estavam a estragar a mira: o
        /// `CharacterController` do `PlayerRoot`, e o `HeldPhone` e o `PhoneScreen`,
        /// que sao filhos da camara e portanto estao a centimetros dela.
        ///
        /// Resolvido por hierarquia e nao por layer de propósito: uma layer nova
        /// obrigava a lembrar de a por em cada objecto novo do jogador, e esquece-se
        /// uma vez e o defeito volta sem dizer nada.
        /// </summary>
        private bool Ignored(Collider collider)
        {
            if (collider == null) return true;
            if (collider.transform.IsChildOf(transform)) return true;
            return BelongsToCarriedBox(collider);
        }

        /// <summary>
        /// Entre dois candidatos, qual e que o jogador quis. Tres perguntas, por ordem.
        ///
        /// ---
        ///
        /// **1. Qual esta mais debaixo da mira.** E a pergunta toda, e foi medida
        /// contra a alternativa: com a distancia a decidir, as tres caixas do prologo
        /// — a 60–130 cm umas das outras, com o jogador a 90 — acertavam 21 de 37
        /// direccoes e roubavam-se mutuamente. A essa geometria "qual esta mais perto"
        /// nao tem resposta util, e nao e o que o jogador esta a perguntar.
        ///
        /// **2. Se estao os dois debaixo da mira, ganha o mais perto.** Dentro do
        /// corpo de um objecto o angulo e zero, por isso abaixo de um grau e meio ele
        /// deixa de separar seja o que for e a distancia volta a ter trabalho.
        ///
        /// **3. E se estiverem a mesma distancia, ganha o mais pequeno.** Uma coisa
        /// pousada em cima de outra e sempre a que se quer: a roupa em cima da cama, o
        /// prato na bancada, o portatil na secretaria. A cama levava a mira em 10 de 13
        /// posicoes com o jogador a olhar direito para a pilha de roupa.
        /// </summary>
        private static bool Beats(float angle, float distance, float size,
            float bestAngle, float bestDistance, float bestSize)
        {
            // Um grau e meio: abaixo disto os dois estao debaixo da mira e a diferenca
            // e ruido do ponto que a caixa envolvente devolveu.
            const float UnderTheCrosshair = 1.5f;
            const float SameShelf = 0.15f;

            if (angle < bestAngle - UnderTheCrosshair) return true;
            if (angle > bestAngle + UnderTheCrosshair) return false;

            if (distance < bestDistance - SameShelf) return true;
            if (distance > bestDistance + SameShelf) return false;

            return size < bestSize;
        }

        /// <summary>
        /// O ponto pelo qual se mede um candidato: o ponto do **corpo** dele mais
        /// proximo da linha da mira.
        ///
        /// A linha da mira, e nao o olho. `ClosestPoint(origin)` devolve o ponto mais
        /// proximo da camara, e o ponto mais proximo da camara de um sofa de 2,6 m
        /// esta praticamente sempre a zero graus — media "esta perto de mim" e nao
        /// "esta debaixo da mira".
        ///
        /// Trabalha em caixas envolventes de propósito: `Collider.ClosestPoint` nao
        /// responde em malhas concavas — devolve o ponto que recebeu — e aqui nao se
        /// esta a fazer fisica, esta-se a decidir para onde o jogador olha.
        /// </summary>
        private static Vector3 AimPointOf(Bounds body, Vector3 origin, Vector3 direction)
        {
            float alongRay = Mathf.Max(0f, Vector3.Dot(body.center - origin, direction));
            Vector3 onSightLine = origin + direction * alongRay;
            return body.ClosestPoint(onSightLine);
        }

        /// <summary>
        /// O corpo de um interactavel: o que dele esta **desenhado**, ou, quando nao
        /// ha nada desenhado, o volume solido; e so em ultimo caso o volume que o raio
        /// atravessou.
        ///
        /// A ordem importa e e sempre a mesma pergunta: para onde e que o jogador olha
        /// quando quer esta coisa? Olha para a caixa no chao, nao para a coluna
        /// invisivel por cima dela. Um ponto de tarefa em cima de uma bancada nao tem
        /// nada desenhado nem nada solido — ai o volume **e** o objecto, e serve.
        /// </summary>
        private static Bounds BodyOf(IPlayerInteractable interactable, Collider hit)
        {
            var behaviour = interactable as MonoBehaviour;
            if (behaviour != null)
            {
                Bounds bounds = default;
                bool any = false;

                Renderer[] renderers = behaviour.GetComponentsInChildren<Renderer>(false);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (!renderers[i].enabled) continue;
                    if (!any) { bounds = renderers[i].bounds; any = true; }
                    else bounds.Encapsulate(renderers[i].bounds);
                }
                if (any) return bounds;

                Collider[] colliders = behaviour.GetComponentsInChildren<Collider>(false);
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (!colliders[i].enabled || colliders[i].isTrigger) continue;
                    if (!any) { bounds = colliders[i].bounds; any = true; }
                    else bounds.Encapsulate(colliders[i].bounds);
                }
                if (any) return bounds;
            }

            return hit.bounds;
        }

        /// <summary>
        /// Se ha alguma coisa solida entre o olho e o ponto do objecto que se quer.
        ///
        /// Medido quando isto nao existia: **30 de 136 posicoes tapadas** ainda davam
        /// o alvo ao jogador — quase todas as chaves da entrada, o lava-loica e a
        /// televisao, de dentro da divisao do lado. Um volume de ajuda de mira
        /// atravessa paredes com facilidade: o das chaves tem 48 cm num corredor com
        /// paredes de 10.
        ///
        /// Quatro coisas nao tapam, e cada uma tem o seu motivo:
        ///
        /// - **o corpo do jogador e a caixa ao colo**, pela mesma razao de sempre;
        /// - **o proprio objecto**, em qualquer sentido da arvore. Um ponto de tarefa
        ///   vive dentro do movel a que pertence, e o movel nao pode ser a parede do
        ///   seu proprio ponto de tarefa;
        /// - **um interactavel calado**: uma caixa ja entregue nao pode impedir que se
        ///   pouse outra em cima dela. Uma porta fechada, essa, tem prompt e tapa —
        ///   que e o que se quer de uma porta fechada.
        ///
        /// Os ultimos cinco centimetros nao contam: o ponto de mira esta na superficie
        /// do objecto, e a superficie nao se tapa a si mesma.
        /// </summary>
        private bool Blocked(Vector3 origin, Vector3 point, IPlayerInteractable candidate)
        {
            Vector3 delta = point - origin;
            float length = delta.magnitude;
            if (length < 0.10f) return false;   // praticamente em cima do objecto

            var owner = candidate as MonoBehaviour;
            int count = Physics.RaycastNonAlloc(origin, delta / length, sightBuffer,
                length - 0.05f, interactionMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider blocker = sightBuffer[i].collider;
                if (Ignored(blocker)) continue;

                if (owner != null &&
                    (blocker.transform.IsChildOf(owner.transform) ||
                     owner.transform.IsChildOf(blocker.transform))) continue;

                if (HasInteractable(blocker) && Resolve(blocker) == null) continue;

                return true;
            }

            return false;
        }

        private static bool BelongsToCarriedBox(Collider collider)
        {
            CarryableBox carried = CarryableBox.Carried;
            return carried != null && collider != null &&
                   collider.GetComponentInParent<CarryableBox>() == carried;
        }

        /// <summary>
        /// A disabled component is not an offer.
        ///
        /// The search deliberately walks inactive parents, so a component someone
        /// switched off still answered here. That is how the archived router panel
        /// kept taking the interaction: opening it suppressed movement, but its UI
        /// lives in OnGUI, which a disabled behaviour never runs, so there was no
        /// way left to close it. Anything switched off must be invisible to the
        /// reticle, not merely silent.
        /// </summary>
        private static bool IsLive(MonoBehaviour behaviour)
            => behaviour != null && behaviour.isActiveAndEnabled;

        private static bool HasInteractable(Collider collider)
        {
            if (collider == null) return false;
            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IPlayerInteractable && IsLive(behaviours[i]))
                    return true;
            return false;
        }

        /// <summary>
        /// O interactavel deste colisor, **se tiver alguma coisa a oferecer agora**.
        ///
        /// Um prompt vazio ja queria dizer "nao da para isto neste momento" em quase
        /// todo o projecto — a caixa entregue, o capo aberto, a cama antes de a porta
        /// estar trancada, o Rui antes da hora de falar. O que faltava era o
        /// corolario: um interactavel calado continuava a **ganhar a mira** e a tapar
        /// o que estivesse atras dele, e o clique nao fazia nada.
        ///
        /// **Duas passagens, e nao uma.** A cama tem o `FlavourInteractable` antes do
        /// `PlayerSleep`, e "Look at the bed" nunca esta calado — media-se 0 em 11: a
        /// cama nunca oferecia dormir e o dia nao acabava. A ordem em que alguem
        /// arrastou componentes para um GameObject nao pode decidir isso.
        /// </summary>
        private static IPlayerInteractable Resolve(Collider collider)
        {
            if (collider == null) return null;

            MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);
            IPlayerInteractable ambient = null;
            IPlayerInteractable ordinary = null;

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (!(behaviours[i] is IPlayerInteractable interactable)) continue;
                if (!IsLive(behaviours[i])) continue;
                if (string.IsNullOrEmpty(interactable.Prompt)) continue;

                // Tres escaloes, e a ordem dos componentes so desempata dentro de cada
                // um. Ver `IPriorityInteractable` e `IAmbientInteractable`.
                if (interactable is IPriorityInteractable) return interactable;

                if (interactable is IAmbientInteractable)
                {
                    if (ambient == null) ambient = interactable;
                    continue;
                }

                if (ordinary == null) ordinary = interactable;
            }

            return ordinary ?? ambient;
        }

        public void SetInteractionBlocked(bool blocked)
        {
            interactionBlocked = blocked;
            if (blocked)
            {
                focused = null;
                hud?.SetPrompt(string.Empty);
            }
        }

        private void OnDisable()
        {
            if (active != null) active.EndInteraction(this);
            active = null;
            focused = null;
            interactionBlocked = false;
            input?.SetLookSuppressed(false);
            hud?.SetPrompt(string.Empty);
        }
    }
}
