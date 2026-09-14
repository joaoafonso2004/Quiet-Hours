using Pungent.Narrative;
using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>
    /// Conteudo real de uma caixa do prologo. A caixa so desaparece depois de o
    /// objecto ser retirado e colocado no destino.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoxContentsTask : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private CarryableBox sourceBox;
        [SerializeField] private GameObject carryPrefab;
        [SerializeField] private GameObject placedVisual;
        [SerializeField] private GameObject secondaryPlacedVisual;
        [SerializeField] private string takePrompt = "Take the contents out";
        [SerializeField] private string placePrompt = "Put it away";
        [SerializeField] private string doneEvent;
        [SerializeField, TextArea] private string takeThought;
        [SerializeField, TextArea] private string placeThought;
        [SerializeField] private ChapterDirector director;
        [SerializeField] private PlayerThoughtDirector thoughts;

        /// <summary>Onde o objecto fica na mao, em metros a frente da camara.</summary>
        private static readonly Vector3 HeldOffset = new Vector3(0.14f, -0.14f, 0.45f);
        private static readonly Quaternion HeldPose = Quaternion.Euler(8f, -12f, 2f);

        /// <summary>Maior eixo do objecto na mao, em metros.</summary>
        private const float HeldSize = 0.24f;

        private bool holding;
        private bool done;
        private GameObject heldVisual;

        /// <summary>
        /// O conteudo que vai na mao, ou nulo.
        ///
        /// Estatico pela mesma razao do <see cref="CarryableBox.Carried"/>, e a
        /// falta dele era o bug: **nada impedia tirar as tres caixas ao mesmo
        /// tempo.** As tres tarefas ficam disponiveis no mesmo instante — todas
        /// esperam por `sourceBox.Delivered` e as tres caixas sao entregues ao
        /// mesmo canto — e as caixas acabam empilhadas com colunas de mira de 1,75
        /// m que se sobrepoem, portanto qual delas ganha o clique e sorte.
        ///
        /// A seguir cada `ShowHeld` instanciava o seu visual **no mesmo ponto da
        /// camara**, `HeldOffset`. Dois objectos no mesmo sitio, um a atravessar o
        /// outro, e nenhum destino aceso excepto o do primeiro — porque um destino
        /// so fala pela sua propria tarefa. E isso o "agarra em tudo ao mesmo
        /// tempo e depois nao da para pousar em lado nenhum".
        ///
        /// Uma coisa na mao de cada vez, e a ordem de desempacotar volta a ser a do
        /// capitulo: portatil, ferramentas, cozinha.
        /// </summary>
        public static BoxContentsTask Held { get; private set; }

        public bool CanPlace => holding && !done;
        public string PlacePrompt => CanPlace ? placePrompt : string.Empty;
        public bool IsDone => done;

        public string Prompt
        {
            get
            {
                if (done || holding) return string.Empty;
                if (sourceBox == null || !sourceBox.Delivered) return string.Empty;

                // Prompt vazio chega para tirar isto da mira: para o
                // `PlayerInteractor` um interactavel calado deixou de ser alvo.
                return Held != null ? string.Empty : takePrompt;
            }
        }

        public bool HoldToInteract => false;

        private void Awake()
        {
            if (sourceBox == null) sourceBox = GetComponent<CarryableBox>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            if (placedVisual != null) placedVisual.SetActive(done);
            if (secondaryPlacedVisual != null) secondaryPlacedVisual.SetActive(done);
        }

        /// <summary>
        /// A caixa pode ser desactivada com o conteudo ainda na mao — o
        /// <see cref="Place"/> de outra tarefa, uma troca de cena. Sem isto ficava
        /// um estatico apontado a um objecto destruido (nenhum conteudo se tirava
        /// outra vez) e o visual pendurado na camara para sempre.
        /// </summary>
        private void OnDisable()
        {
            if (Held != this) return;
            Held = null;
            holding = false;
            HideHeld();
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (string.IsNullOrEmpty(Prompt)) return;

            // Segunda guarda, a mesma regra. O prompt vazio tira isto da mira, mas
            // quem chamar por codigo — uma ferramenta, um director — passava-lhe ao
            // lado, e duas coisas na mesma mao e o estado de onde nao se sai.
            if (Held != null && Held != this) return;

            holding = true;
            Held = this;
            ShowHeld(interactor);
            if (!string.IsNullOrWhiteSpace(takeThought))
                thoughts?.Think("box_take_" + name, takeThought, 3, true, 3f);
        }

        public void Place()
        {
            if (!CanPlace) return;
            holding = false;
            if (Held == this) Held = null;
            done = true;
            HideHeld();
            if (placedVisual != null) placedVisual.SetActive(true);
            if (secondaryPlacedVisual != null) secondaryPlacedVisual.SetActive(true);
            if (!string.IsNullOrWhiteSpace(placeThought))
                thoughts?.Think("box_place_" + name, placeThought, 3, true, 3f);

            if (!string.IsNullOrWhiteSpace(doneEvent))
            {
                director = ChapterDirector.Resolve(director);
                director?.Notify(doneEvent);
            }

            // Consequencia, nao temporizador: so agora a caixa vazia sai da cena.
            gameObject.SetActive(false);
        }

        public void ResetTask()
        {
            if (Held == this) Held = null;
            holding = false;
            done = false;
            HideHeld();
            gameObject.SetActive(true);
            if (placedVisual != null) placedVisual.SetActive(false);
            if (secondaryPlacedVisual != null) secondaryPlacedVisual.SetActive(false);
        }

        /// <summary>
        /// Poe o objecto na mao, do tamanho certo e centrado no ponto da mao.
        ///
        /// As duas contas estavam erradas, e pelo mesmo motivo: usavam
        /// `Renderer.bounds`, que e a **caixa alinhada com os eixos do mundo**.
        ///
        /// - **O tamanho.** Medido depois de aplicar a pose e a rotacao da cabeca,
        ///   o que se media era a caixa envolvente do objecto *rodado*, sempre
        ///   maior do que ele — e tanto maior quanto mais torto estivesse. O
        ///   portatil, que e o mais fundo dos tres, encolhia mais do que os outros,
        ///   e encolhia de forma diferente conforme a direccao em que o jogador
        ///   estava virado quando o tirou da caixa.
        /// - **O sitio.** O `localPosition` punha o **pivo** no ponto da mao, e o
        ///   pivo de um prop nao tem de estar dentro dele. O do prato esta 3,1 cm
        ///   acima do prato. E a mesma armadilha que a chave ja tinha apanhado no
        ///   `PrologueWiring`, onde ja esta compensada.
        ///
        /// Aqui mede-se no espaco do proprio objecto, antes de qualquer rotacao, e
        /// e o **centro** dele que vai parar a mao.
        /// </summary>
        private void ShowHeld(PlayerInteractor interactor)
        {
            if (carryPrefab == null || heldVisual != null) return;
            Camera camera = interactor != null ? interactor.GetComponentInChildren<Camera>(true) : Camera.main;
            if (camera == null) return;

            heldVisual = Instantiate(carryPrefab, camera.transform);
            heldVisual.name = "HELD_" + carryPrefab.name;

            Transform held = heldVisual.transform;
            held.localPosition = Vector3.zero;
            held.localRotation = Quaternion.identity;

            foreach (var renderer in heldVisual.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Nao ha fisica nenhuma numa coisa que anda agarrada a camara, e um
            // colisor destes so servia para tapar o que se esta a tentar apontar.
            foreach (var collider in heldVisual.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            Bounds body;
            if (!MeasureLocal(held, out body) || body.size.magnitude < 0.001f)
            {
                held.localPosition = HeldOffset;
                held.localRotation = HeldPose;
                return;
            }

            float longest = Mathf.Max(body.size.x, Mathf.Max(body.size.y, body.size.z));
            held.localScale *= HeldSize / longest;
            held.localRotation = HeldPose;
            held.localPosition = HeldOffset -
                HeldPose * Vector3.Scale(body.center, held.localScale);
        }

        private static readonly Vector3[] BoxCorners =
        {
            new Vector3(-1f, -1f, -1f), new Vector3( 1f, -1f, -1f),
            new Vector3(-1f,  1f, -1f), new Vector3( 1f,  1f, -1f),
            new Vector3(-1f, -1f,  1f), new Vector3( 1f, -1f,  1f),
            new Vector3(-1f,  1f,  1f), new Vector3( 1f,  1f,  1f),
        };

        /// <summary>
        /// A caixa envolvente do objecto **no espaco dele proprio**, e nao no do
        /// mundo: o tamanho de um prop nao pode depender da direccao para onde o
        /// jogador estava virado. Os oito cantos de cada renderer sao levados ao
        /// espaco da raiz, que e o unico sitio onde a pergunta tem uma so resposta.
        /// </summary>
        private static bool MeasureLocal(Transform root, out Bounds bounds)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;

            Matrix4x4 toRoot = root.worldToLocalMatrix;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds local = renderers[i].localBounds;
                Matrix4x4 toLocal = toRoot * renderers[i].transform.localToWorldMatrix;

                for (int c = 0; c < BoxCorners.Length; c++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents, BoxCorners[c]);
                    Vector3 point = toLocal.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
                    else bounds.Encapsulate(point);
                }
            }

            return any;
        }

        private void HideHeld()
        {
            if (heldVisual == null) return;
            if (Application.isPlaying) Destroy(heldVisual);
            else DestroyImmediate(heldVisual);
            heldVisual = null;
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

#if UNITY_EDITOR
        public void EditorConfigure(CarryableBox box, GameObject carried, GameObject placed,
            GameObject placedSecondary,
            string take, string place, string raisedEvent, string takenLine, string placedLine)
        {
            sourceBox = box;
            carryPrefab = carried;
            placedVisual = placed;
            secondaryPlacedVisual = placedSecondary;
            takePrompt = take;
            placePrompt = place;
            doneEvent = raisedEvent;
            takeThought = takenLine;
            placeThought = placedLine;
        }
#endif
    }
}
