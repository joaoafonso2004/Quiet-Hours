using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O que se ve nas maos durante uma tarefa domestica: um copo que esvazia, uma
    /// caneca a ser esfregada, um prato que se come, roupa que se arruma.
    ///
    /// ---
    ///
    /// **Porque e um componente so e nao quatro.**
    ///
    /// O <see cref="HeldCigarette"/> resolveu o cigarro e ensinou a licao inteira:
    /// *"sem nada na mao, aquilo era so uma bola cor de laranja no ar. O que vende o
    /// gesto e ver o cigarro encurtar."* As dez tarefas novas tinham exactamente o
    /// mesmo problema — o jogador ficava preso no sitio catorze segundos a olhar
    /// para uma bancada, sem nada a acontecer no ecra e sem nada nas maos.
    ///
    /// Escrever dez classes para isso era dez vezes o mesmo ficheiro. O que muda de
    /// tarefa para tarefa nao e o codigo: e a forma do objecto e o que o progresso
    /// lhe faz. Isso cabe num enum.
    ///
    /// **Construido por primitivas**, como o cigarro, para nao depender de nenhum
    /// modelo. Havendo FBX, entra em `authoredModel` e as primitivas deixam de ser
    /// criadas.
    ///
    /// **Primeira pessoa.** Este jogo nunca mostra o corpo do Tomas, e por isso
    /// nenhuma destas tarefas precisa de animacao de esqueleto: o que se ve e o
    /// objecto a mexer-se em espaco de camara. Vive num filho da `Main Camera`, como
    /// o cigarro.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeldTaskProp : MonoBehaviour, ITaskProgressVisual
    {
        /// <summary>
        /// O que esta na mao. Decide a forma **e** o que o progresso lhe faz — as
        /// duas coisas andam sempre juntas, e separa-las dava combinacoes que nao
        /// querem dizer nada (um copo a ser dobrado).
        /// </summary>
        public enum Kind
        {
            /// <summary>Copo de agua: o nivel desce e ele inclina-se para beber.</summary>
            Glass,
            /// <summary>Caneca no lava-loica: movimento circular de esfregar.</summary>
            Mug,
            /// <summary>Prato com comida: a comida encolhe ate ao prato vazio.</summary>
            Plate,
            /// <summary>Roupa dobrada: a pilha baixa a medida que e arrumada.</summary>
            Cloth
        }

        [SerializeField] private Kind kind = Kind.Glass;

        [Tooltip("Modelo proprio, se houver. Vazio = gera a forma por codigo.")]
        [SerializeField] private GameObject authoredModel;

        [Tooltip("Nome do filho do modelo que representa o conteudo: a agua dentro "
               + "do copo, a comida no prato, a pilha de roupa.\n\n"
               + "E ele que encolhe ao longo da tarefa. O pivot tem de estar **na "
               + "base** do conteudo, senao encolhe para o centro e fica a flutuar. "
               + "Um modelo sem este filho aparece na mao e nao se gasta.")]
        [SerializeField] private string contentsChild = "Contents";

        [Header("Pose em espaco de camara")]
        [Tooltip("Ligeiramente abaixo do centro e para o lado da mao dominante. O "
               + "near clip da camara e 0.05, por isso nada disto pode estar mais "
               + "perto do que isso.")]
        [SerializeField] private Vector3 holdPosition = new Vector3(0.135f, -0.145f, 0.32f);
        [SerializeField] private Vector3 holdEuler = new Vector3(0f, -12f, 6f);

        [Header("Medidas (metros)")]
        [SerializeField] private float scale = 1f;

        private Transform rig;
        private Transform contents;   // liquido, comida, ou a pilha de roupa
        private Vector3 contentsBase;
        private Vector3 contentsBasePosition;
        private bool active;
        private float progress;

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            Build();
            SetVisible(false);
        }

        // ------------------------------------------------------------------
        // ITaskProgressVisual
        // ------------------------------------------------------------------

        public void BeginTask()
        {
            Build();
            progress = 0f;
            Apply(0f);
            SetVisible(true);
            active = true;
        }

        public void SetTaskProgress(float normalised)
        {
            if (!active) return;
            progress = Mathf.Clamp01(normalised);
            Apply(progress);
        }

        public void EndTask()
        {
            active = false;
            SetVisible(false);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// O gesto. Tudo em espaco de camara, e tudo pequeno.
        ///
        /// A tentacao aqui e animar de mais. Um copo que sobe ate ao ecra inteiro
        /// tapa o apartamento, e o apartamento e a unica coisa que interessa durante
        /// estes segundos — e para o jogador **olhar em volta** que a tarefa o
        /// prende no sitio. O objecto tem de dar sinal de vida ao canto do olho e
        /// mais nada.
        /// </summary>
        private void Apply(float t)
        {
            if (rig == null) return;

            switch (kind)
            {
                case Kind.Glass:
                    // Tres goles: sobe, inclina, desce. O nivel so desce depois de
                    // cada gole, e nao continuamente — beber e discreto.
                    float sips = Mathf.Floor(t * 3f);
                    float withinSip = Mathf.Repeat(t * 3f, 1f);
                    float lift = Mathf.Sin(withinSip * Mathf.PI) * 0.06f;
                    float tilt = Mathf.Sin(withinSip * Mathf.PI) * 34f;

                    rig.localPosition = holdPosition + new Vector3(-lift * 0.4f, lift, -lift * 0.5f);
                    rig.localRotation = Quaternion.Euler(holdEuler + new Vector3(-tilt, 0f, 0f));
                    SetContentsFill(1f - Mathf.Clamp01(sips / 3f));
                    break;

                case Kind.Mug:
                    // Esfregar: circulo pequeno e rapido, com a caneca a rodar um
                    // pouco na mao. Sem deslocacao vertical — as maos estao dentro
                    // do lava-loica.
                    float angle = t * Mathf.PI * 2f * 6f;
                    rig.localPosition = holdPosition + new Vector3(
                        Mathf.Cos(angle) * 0.022f, Mathf.Sin(angle) * 0.016f, 0f);
                    rig.localRotation = Quaternion.Euler(
                        holdEuler + new Vector3(0f, Mathf.Sin(angle * 0.5f) * 14f, 0f));
                    break;

                case Kind.Plate:
                    // Comer: a comida encolhe, e o prato balanca devagar como quem
                    // o segura com uma mao so.
                    rig.localPosition = holdPosition + new Vector3(
                        0f, Mathf.Sin(t * Mathf.PI * 2f * 1.6f) * 0.008f, 0f);
                    rig.localRotation = Quaternion.Euler(
                        holdEuler + new Vector3(Mathf.Sin(t * Mathf.PI * 1.3f) * 3f, 0f, 0f));
                    SetContentsFill(1f - t);
                    break;

                case Kind.Cloth:
                    // Arrumar: a pilha baixa, e a mao vai e vem da gaveta. O
                    // movimento e mais largo do que os outros porque este e o unico
                    // em que ele se dobra para a frente.
                    float reach = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3f));
                    rig.localPosition = holdPosition + new Vector3(0f, -reach * 0.05f, reach * 0.07f);
                    rig.localRotation = Quaternion.Euler(holdEuler + new Vector3(reach * 18f, 0f, 0f));
                    SetContentsFill(1f - t);
                    break;
            }
        }

        /// <summary>
        /// O conteudo encolhe a partir de baixo, e nao a partir do centro.
        ///
        /// Escalar pelo centro fazia a agua encolher para o meio do copo e ficar a
        /// flutuar la dentro. O deslocamento compensa a escala para a base ficar
        /// onde estava.
        /// </summary>
        private void SetContentsFill(float fill)
        {
            if (contents == null) return;

            fill = Mathf.Clamp01(fill);
            var s = contents.localScale;
            s.y = contentsBase.y * Mathf.Max(fill, 0.001f);
            contents.localScale = s;

            var p = contents.localPosition;
            float pivotCompensation = kind == Kind.Glass ? 1f : 0.5f;
            p.y = contentsBasePosition.y
                - (contentsBase.y - s.y) * pivotCompensation;
            contents.localPosition = p;
        }

        // ------------------------------------------------------------------
        // Construcao
        // ------------------------------------------------------------------

        private void Build()
        {
            if (rig != null) return;

            rig = new GameObject($"{kind}Rig").transform;
            rig.SetParent(transform, false);
            rig.localPosition = holdPosition;
            rig.localRotation = Quaternion.Euler(holdEuler);
            rig.localScale = Vector3.one * scale;

            if (authoredModel != null)
            {
                var model = Instantiate(authoredModel, rig);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                // O modelo substitui a forma, **nao o gesto**.
                //
                // A primeira versao fazia `return` aqui e o `contents` ficava nulo:
                // um copo autorado deixava de ter agua a descer, e um prato deixava
                // de esvaziar. Ou seja, trocar primitivas por um modelo bonito
                // desligava a unica coisa que estas tarefas tem para mostrar.
                //
                // Basta o modelo trazer um filho chamado como `contentsChild`. Nao
                // trazendo, o objecto e sólido e o progresso so lhe mexe a pose —
                // que e o que se quer para a caneca, que nao tem conteudo nenhum.
                // A regra das sombras vale para o modelo autorado tambem, e nao so
                // para as primitivas do `Prepare`. Um objecto que so existe em
                // espaco de camara projecta no chao do apartamento uma sombra vinda
                // de lado nenhum — e um FBX chega com as sombras ligadas por
                // omissao, portanto o defeito entrava com o primeiro asset bonito.
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                // Nem colisores: isto vive a menos de meio metro da camara e
                // empurrava o `CharacterController` do proprio jogador.
                foreach (var collider in model.GetComponentsInChildren<Collider>(true))
                    Destroy(collider);

                contents = FindChild(model.transform, contentsChild);
                // A pilha de roupa chegou como um unico mesh, sem um filho
                // `Contents`. Nesse caso o proprio mesh e aquilo que se gasta. O
                // maior renderer e usado em vez da raiz do prefab para a escala e
                // a compensacao acontecerem no tamanho real do modelo, nao numa
                // raiz a escala 1 que o atiraria um metro para baixo.
                if (contents == null && kind == Kind.Cloth)
                    contents = FindLargestRenderer(model.transform);
                if (contents != null)
                {
                    contentsBase = contents.localScale;
                    contentsBasePosition = contents.localPosition;
                }
                else if (WantsContents(kind))
                    Debug.LogWarning($"[HeldTaskProp] '{authoredModel.name}' nao tem um " +
                                     $"filho chamado '{contentsChild}': o objecto aparece " +
                                     "na mao mas nao se gasta.");
                return;
            }

            switch (kind)
            {
                case Kind.Glass: BuildGlass(); break;
                case Kind.Mug: BuildMug(); break;
                case Kind.Plate: BuildPlate(); break;
                case Kind.Cloth: BuildCloth(); break;
            }
        }

        private void BuildGlass()
        {
            var glass = MakeCylinder("Glass", rig, 0.105f, 0.032f,
                Material(new Color(0.86f, 0.90f, 0.92f, 1f), 0.85f));
            glass.localPosition = Vector3.zero;

            contents = MakeCylinder("Water", rig, 0.084f, 0.028f,
                Material(new Color(0.62f, 0.74f, 0.80f), 0.92f));
            contents.localPosition = new Vector3(0f, -0.008f, 0f);
            contentsBase = contents.localScale;
            contentsBasePosition = contents.localPosition;
        }

        private void BuildMug()
        {
            var mug = MakeCylinder("Mug", rig, 0.088f, 0.040f,
                Material(new Color(0.90f, 0.88f, 0.84f), 0.30f));
            mug.localPosition = Vector3.zero;

            // A asa. Um cubo achatado ao lado chega: a esta distancia e com esta
            // paleta, ninguem lhe conta os lados.
            var handle = MakeCube("Handle", rig, new Vector3(0.012f, 0.042f, 0.030f),
                Material(new Color(0.90f, 0.88f, 0.84f), 0.30f));
            handle.localPosition = new Vector3(0.046f, 0f, 0f);

            contents = null;   // uma caneca a ser lavada nao tem conteudo
        }

        private void BuildPlate()
        {
            var plate = MakeCylinder("Plate", rig, 0.012f, 0.105f,
                Material(new Color(0.92f, 0.91f, 0.88f), 0.35f));
            plate.localPosition = Vector3.zero;

            contents = MakeCube("Food", rig, new Vector3(0.085f, 0.030f, 0.070f),
                Material(new Color(0.55f, 0.42f, 0.28f), 0.10f));
            contents.localPosition = new Vector3(0f, 0.020f, 0f);
            contentsBase = contents.localScale;
            contentsBasePosition = contents.localPosition;
        }

        private void BuildCloth()
        {
            contents = MakeCube("Stack", rig, new Vector3(0.170f, 0.090f, 0.130f),
                Material(new Color(0.58f, 0.60f, 0.63f), 0.08f));
            contents.localPosition = Vector3.zero;
            contentsBase = contents.localScale;
            contentsBasePosition = contents.localPosition;
        }

        // ------------------------------------------------------------------

        /// <summary>Procura por nome em toda a arvore do modelo, incluindo inactivos.</summary>
        private static Transform FindChild(Transform root, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static Transform FindLargestRenderer(Transform root)
        {
            Renderer largest = null;
            float largestVolume = -1f;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Vector3 size = renderer.bounds.size;
                float volume = size.x * size.y * size.z;
                if (volume <= largestVolume) continue;
                largest = renderer;
                largestVolume = volume;
            }
            return largest != null ? largest.transform : null;
        }

        /// <summary>Que formas e que se gastam. A caneca nao: e a loica, nao o que la ia dentro.</summary>
        private static bool WantsContents(Kind kind) => kind != Kind.Mug;

        private static Material Material(Color colour, float smoothness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = "M_HeldProp_Runtime" };
            material.SetColor(BaseColour, colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            return material;
        }

        private static Transform MakeCylinder(string name, Transform parent, float length,
            float radius, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Prepare(go, name, parent, material);
            // A primitiva tem 2 unidades de altura, dai o meio comprimento na escala.
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            return go.transform;
        }

        private static Transform MakeCube(string name, Transform parent, Vector3 size,
            Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Prepare(go, name, parent, material);
            go.transform.localScale = size;
            return go.transform;
        }

        private static void Prepare(GameObject go, string name, Transform parent, Material material)
        {
            go.name = name;
            // Sem colisor: isto vive a trinta centimetros da camara e empurrava o
            // `CharacterController` do proprio jogador.
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            // Sem sombra: uma sombra projectada por um objecto que so existe em
            // espaco de camara aparece no chao do apartamento vinda de lado nenhum.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void SetVisible(bool visible)
        {
            if (rig == null) return;
            foreach (var renderer in rig.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = visible;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(Kind what, Vector3 position, Vector3 euler)
        {
            kind = what;
            holdPosition = position;
            holdEuler = euler;
        }
#endif
    }
}
