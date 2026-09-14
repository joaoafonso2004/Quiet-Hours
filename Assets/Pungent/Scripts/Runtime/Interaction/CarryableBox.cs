using System.Collections;
using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>
    /// Uma caixa que se pega, se leva ao colo e se pousa noutro sitio.
    ///
    /// E o unico verbo novo do prologo, e existe por uma razao que nao e mecanica:
    /// **a casa so fica conhecida a quem a atravessa com as maos ocupadas.** Levar
    /// tres caixas da entrada ao quarto sao seis viagens pelo mesmo corredor, e
    /// nesse corredor ha uma porta entreaberta que nao e a do jogador. Ninguem
    /// precisa de dizer nada sobre ela; o jogador passa-lhe ao lado seis vezes.
    ///
    /// Devagar de proposito. Com a caixa a frente ve-se menos chao, anda-se pior, e
    /// o corredor demora mais a acabar — que e exactamente o que se quer de um
    /// corredor onde esta alguem parado.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CarryableBox : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private string pickUpPrompt = "Pick up";
        [SerializeField] private string putDownPrompt = "Put it down";

        [Tooltip("Dito quando ela e pousada fora da zona. Diz onde, sem dizer como.")]
        [SerializeField, TextArea] private string wrongPlaceThought =
            "Not here. These go in my room.";

        [Tooltip("Onde a caixa fica enquanto e levada, em metros a frente da camara.")]
        [SerializeField] private Vector3 carryOffset = new Vector3(0f, -0.32f, 0.62f);

        [Tooltip("Quanto o jogador abranda com uma caixa ao colo, em fraccao.")]
        [SerializeField, Range(0.3f, 1f)] private float carrySpeedFactor = 0.62f;

        [Tooltip("Zona onde esta caixa conta como arrumada. Vazio = pousa-se onde se quiser.")]
        [SerializeField] private BoxDropZone dropZone;

        [Tooltip("Levantado quando esta caixa chega ao destino.")]
        [SerializeField] private string deliveredEvent;

        [SerializeField, TextArea] private string firstLiftThought;

        [SerializeField] private ChapterDirector director;
        [SerializeField] private PlayerThoughtDirector thoughts;

        private Transform carrier;
        private Rigidbody body;
        private BoxCollider box;
        private Pungent.Player.PlayerMotor motor;
        private bool thoughtSaid;

        /// <summary>
        /// A caixa que vai ao colo, ou nula.
        ///
        /// Estatico pelo mesmo motivo do <see cref="HidingSpot.Occupied"/>: a
        /// pergunta "ja ha uma nas maos?" tem sempre uma resposta so, e varrer a
        /// cena para a responder era pagar uma busca por nada.
        /// </summary>
        public static CarryableBox Carried { get; private set; }

        public bool IsCarried => carrier != null;

        /// <summary>Ja foi pousada dentro da zona certa.</summary>
        public bool Delivered { get; private set; }

        public string Prompt
        {
            get
            {
                if (Delivered) return string.Empty;
                if (IsCarried) return putDownPrompt;

                // **Uma de cada vez.** As tres caixas do prologo podiam ser
                // apanhadas ao mesmo tempo: o `SphereCast` do `PlayerInteractor`
                // atravessa a caixa que ja vai ao colo — a origem do raio esta
                // dentro dela — e ia buscar a seguinte que estivesse no caminho.
                //
                // Prompt vazio chega, porque para o `PlayerInteractor` um
                // interactavel sem prompt deixou de ser alvo: a mira passa-lhe ao
                // lado e vai parar a caixa que ele tem mesmo nas maos.
                return Carried != null ? string.Empty : pickUpPrompt;
            }
        }

        public bool HoldToInteract => false;

        private void Awake()
        {
            box = GetComponent<BoxCollider>();
            body = GetComponent<Rigidbody>();
            if (director == null) director = FindObjectOfType<ChapterDirector>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();

            // As caixas comecam pousadas no canto da entrada. Pousadas quer dizer
            // sem corpo solido — ver `Settle`.
            if (body == null || body.isKinematic) Settle();
        }

        /// <summary>
        /// Uma caixa pousada no chao **deixa de ter corpo solido**.
        ///
        /// ---
        ///
        /// O colisor delas nasceu com 37 cm de altura por uma razao que ja nao existe:
        /// o `stepOffset` do `CharacterController` e 25 cm, as caixas mediam 23–25, e
        /// um obstaculo exactamente no limiar poe o controlador a subir e a gravidade
        /// a puxar, em vaivem. A correccao de entao foi **subir** o obstaculo para
        /// fora do limiar.
        ///
        /// Resolveu o tremor e deixou o resto: tres caixas de 37 cm espalhadas pelo
        /// unico caminho entre a entrada e o quarto, num percurso que o jogador faz
        /// seis vezes com a vista tapada por outra caixa. Nao ha nada a ganhar em
        /// tropecar nelas — nao sao um obstaculo de desenho, sao cenario que se
        /// levanta.
        ///
        /// Em trigger, continuam a existir para tudo o que importa: o
        /// `PlayerInteractor` procura alvos com `QueryTriggerInteraction.Collide`, a
        /// `BoxDropZone` conta-as, e o cast de mira encontra-as na mesma. O que
        /// desaparece e o joelho.
        ///
        /// **Solidas so enquanto caem.** Largar uma caixa em trigger fazia-a atravessar
        /// o chao. O <see cref="PutDown"/> devolve-lhe o corpo, ela cai, e isto volta
        /// a tira-lo quando ela parar.
        /// </summary>
        private void Settle()
        {
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
                body.detectCollisions = true;
            }

            if (box != null) { box.enabled = true; box.isTrigger = true; }
        }

        /// <summary>
        /// Espera que ela pare de cair e tira-lhe o corpo.
        ///
        /// ---
        ///
        /// **A caixa ficava a flutuar onde era largada**, e a causa era esta funcao
        /// perguntar cedo de mais.
        ///
        /// No frame em que o `PutDown` devolve o corpo, o `Rigidbody` acabou de deixar
        /// de ser cinematico e **a velocidade dele ainda e zero** — a gravidade so
        /// entra no passo de fisica seguinte. A condicao "ja parou?" dava verdade
        /// imediatamente, o `Settle` punha-a cinematica outra vez, e ela ficava
        /// pendurada no ar exactamente na pose em que ia ao colo.
        ///
        /// Duas guardas, e sao precisas as duas:
        ///
        /// - **um tempo minimo a cair.** Antes disso nao se pergunta nada, porque a
        ///   resposta nao quer dizer nada;
        /// - **parada durante um bocado, e nao num instante.** Uma caixa a bater no
        ///   chao inverte a velocidade e passa por zero a meio do ressalto; medir num
        ///   frame so apanhava esse zero e congelava-a a meio do salto.
        ///
        /// O limite de tempo continua la: uma caixa encravada entre dois moveis pode
        /// nunca chegar a parar, e ficar solida para sempre por causa disso seria
        /// trocar um defeito raro por outro.
        /// </summary>
        private IEnumerator SettleWhenStill()
        {
            float fallingUntil = Time.time + 0.35f;
            while (Time.time < fallingUntil) yield return new WaitForFixedUpdate();

            float giveUp = Time.time + 4f;
            float stillFor = 0f;

            while (Time.time < giveUp)
            {
                if (body == null) break;

                bool slow = body.velocity.sqrMagnitude < 0.01f &&
                            body.angularVelocity.sqrMagnitude < 0.01f;

                stillFor = slow ? stillFor + Time.fixedDeltaTime : 0f;
                if (stillFor >= 0.20f) break;

                yield return new WaitForFixedUpdate();
            }

            // Se entretanto voltou ao colo, quem manda no colisor e o `PickUp`.
            if (Carried == this) yield break;
            Settle();
        }

        /// <summary>
        /// A caixa pode desaparecer com o jogador a segura-la — trocar de cena a
        /// meio de uma viagem, por exemplo. Sem isto ficava um estatico apontado a
        /// um objecto destruido (nenhuma caixa se apanhava no capitulo seguinte) e
        /// o jogador ficava a andar a 62% da velocidade para sempre.
        /// </summary>
        private void OnDisable()
        {
            if (Carried != this) return;
            Carried = null;
            SetCarrySpeed(false);   // antes de largar o `carrier`: e por ele que se chega ao motor
            carrier = null;
        }

        private void LateUpdate()
        {
            if (carrier == null) return;

            // Depois da camara ter andado, senao a caixa vai sempre um frame
            // atrasada e balanca por tras do movimento da cabeca.
            transform.position = carrier.TransformPoint(carryOffset);
            transform.rotation = carrier.rotation;
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (Delivered) return;
            if (IsCarried) PutDown();
            else PickUp(interactor);
        }

        private void PickUp(PlayerInteractor interactor)
        {
            // Segunda guarda, a mesma regra. O prompt vazio tira esta caixa da
            // mira, mas quem chamar isto por codigo — uma ferramenta, um director
            // — passava-lhe ao lado, e duas caixas na mesma camara e um estado de
            // onde nao se sai.
            if (Carried != null && Carried != this) return;

            var camera = interactor != null ? interactor.GetComponentInChildren<Camera>(true) : Camera.main;
            if (camera == null) return;

            carrier = camera.transform;
            Carried = this;

            // Sem fisica enquanto vai ao colo: um Rigidbody a ser arrastado por
            // `transform` empurra tudo o que toca e fica a tremer contra o chao.
            // `detectCollisions` **fica ligado**. Desliga-lo apaga os colisores deste
            // corpo para toda a fisica — incluindo as consultas que o
            // `PlayerInteractor` usa para escolher o alvo. Foi por isso que a caixa
            // continuou impossivel de pousar mesmo depois de o colisor voltar a
            // estar activo: ele estava activo e invisivel ao mesmo tempo.
            //
            // Nao e preciso para nada: um corpo cinematico com o colisor em trigger
            // ja nao empurra coisa nenhuma.
            if (body != null) { body.isKinematic = true; body.detectCollisions = true; }

            // **O colisor fica ligado, como trigger.**
            //
            // Estava a ser desligado, e isso tornava a caixa impossivel de pousar: o
            // `PlayerInteractor` encontra alvos por colisores, e uma caixa sem
            // colisor nao existe para ele. O prompt "Put it down" nunca chegava a
            // aparecer, e o jogador ficava preso a ela sem perceber porque.
            //
            // Trigger e nao solido porque o que se queria ao desliga-lo era que ela
            // deixasse de empurrar o mundo — isso continua garantido.
            if (box != null) { box.enabled = true; box.isTrigger = true; }

            SetCarrySpeed(true);

            if (!thoughtSaid && !string.IsNullOrWhiteSpace(firstLiftThought))
            {
                thoughtSaid = true;
                thoughts?.Think($"box_{GetInstanceID()}", firstLiftThought, 2, true, 3.2f);
            }
        }

        private void PutDown()
        {
            carrier = null;
            if (Carried == this) Carried = null;
            // Solida enquanto cai, e so enquanto cai. Ver `Settle`.
            if (box != null) { box.enabled = true; box.isTrigger = false; }
            if (body != null) { body.isKinematic = false; body.detectCollisions = true; }
            if (isActiveAndEnabled) StartCoroutine(SettleWhenStill());

            SetCarrySpeed(false);

            bool inPlace = dropZone == null || dropZone.Contains(transform.position);
            if (!inPlace)
            {
                // Pousar no sitio errado nao dava sinal nenhum: a caixa caia, nada
                // acontecia, e o jogador ficava sem saber se tinha feito a coisa
                // certa no sitio errado ou a coisa errada. E a regra de clareza do
                // projecto — o misterio e *porque*, nunca *o que*.
                thoughts?.Think("box_wrong_place", wrongPlaceThought, 4, false, 3.0f);
                return;
            }

            Delivered = true;
            if (dropZone != null) dropZone.Accept(this);
            if (!string.IsNullOrWhiteSpace(deliveredEvent))
            {
                director = ChapterDirector.Resolve(director);
                director?.Notify(deliveredEvent);
            }
        }

        /// <summary>
        /// Reutiliza a mesma cena em testes com Enter Play Mode sem reload. Os
        /// campos de entrega nao sao serializados e, sem esta reposicao, uma caixa
        /// podia continuar entregue na execucao seguinte.
        /// </summary>
        public void ResetForPrologue()
        {
            if (Carried == this) Carried = null;
            carrier = null;
            Delivered = false;
            thoughtSaid = false;
            Settle();
            SetCarrySpeed(false);
        }

        /// <summary>
        /// Abranda ou repoe o passo, pelo multiplicador do `PlayerMotor`.
        ///
        /// Isto chegou a ser feito por reflexao sobre o campo privado da velocidade.
        /// Funcionava — e era exactamente o tipo de ligacao que se parte em silencio
        /// no dia em que alguem renomear o campo, sem erro de compilacao nenhum a
        /// avisar.
        /// </summary>
        private void SetCarrySpeed(bool carrying)
        {
            if (motor == null && carrier != null)
                motor = carrier.GetComponentInParent<Pungent.Player.PlayerMotor>();
            if (motor == null) return;

            motor.SpeedMultiplier = carrying ? carrySpeedFactor : 1f;
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(BoxDropZone zone, string raisedEvent, string thought)
        {
            dropZone = zone;
            deliveredEvent = raisedEvent;
            firstLiftThought = thought;
        }
#endif
    }
}
