using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O cigarro na mão, à frente da câmara, que se consome enquanto se fuma.
    ///
    /// A primeira tentativa foi uma luz laranja a piscar na guarda da varanda. Não
    /// funcionava: sem nada na mão, aquilo era só uma bola cor de laranja no ar. O
    /// que vende o gesto é ver o cigarro encurtar. No fim sobra o filtro — o mesmo
    /// que sobra na vida real — e é isso que ele deita fora.
    ///
    /// Constrói-se por primitivas para não depender de nenhum modelo. Se aparecer
    /// um FBX de cigarro, basta pô-lo em `authoredModel` e o papel/filtro gerados
    /// deixam de ser criados.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeldCigarette : MonoBehaviour, ITaskProgressVisual
    {
        [Tooltip("Modelo próprio, se houver. Vazio = gera papel e filtro por código.")]
        [SerializeField] private GameObject authoredModel;

        [Header("Medidas (metros)")]
        [Tooltip("Comprimento do papel por queimar.")]
        [SerializeField] private float paperLength = 0.062f;
        [Tooltip("Filtro: é o que sobra no fim.")]
        [SerializeField] private float filterLength = 0.026f;
        [SerializeField] private float radius = 0.0055f;

        [Header("Pose na boca")]
        [Tooltip("Posição em espaço da câmara. Ao canto da boca: logo por baixo do "
               + "olho, ligeiramente ao lado, a apontar para a frente. O near clip "
               + "da câmara é 0.05, por isso 0.12 ainda cabe inteiro no ecrã.")]
        [SerializeField] private Vector3 holdPosition = new Vector3(0.052f, -0.062f, 0.115f);
        [Tooltip("Roda +90 em X para o cigarro apontar para a frente; o resto é a "
               + "inclinação natural de quem o tem preso nos lábios.")]
        [SerializeField] private Vector3 holdEuler = new Vector3(96f, 13f, 0f);

        [Header("Brasa")]
        [SerializeField] private Color emberCold = new Color(0.28f, 0.06f, 0.02f);
        [SerializeField] private Color emberHot = new Color(1f, 0.36f, 0.08f);

        private Transform rig;
        private Transform paper;
        private Transform ash;
        private Renderer emberRenderer;
        private MaterialPropertyBlock block;
        private float burn;
        private bool active;

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            Build();
            SetVisible(false);
        }

        private void Build()
        {
            if (rig != null) return;

            rig = new GameObject("CigaretteRig").transform;
            rig.SetParent(transform, false);
            rig.localPosition = holdPosition;
            rig.localRotation = Quaternion.Euler(holdEuler);

            if (authoredModel != null)
            {
                var model = Instantiate(authoredModel, rig);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                return;
            }

            Material paperMaterial = MakeMaterial(new Color(0.93f, 0.92f, 0.89f), 0.18f);
            Material filterMaterial = MakeMaterial(new Color(0.78f, 0.64f, 0.38f), 0.12f);
            Material emberMaterial = MakeMaterial(emberCold, 0.05f, emissive: true);

            // O filtro fica do lado da mão; o papel cresce para longe da câmara.
            var filter = MakeCylinder("Filter", rig, filterLength, radius, filterMaterial);
            filter.localPosition = new Vector3(0f, filterLength * 0.5f, 0f);

            paper = MakeCylinder("Paper", rig, paperLength, radius * 0.98f, paperMaterial);

            // Anel da brasa na ponta que arde.
            ash = MakeCylinder("Ember", rig, 0.004f, radius * 0.99f, emberMaterial);
            emberRenderer = ash.GetComponent<Renderer>();

            ApplyBurn(0f);
        }

        private Material MakeMaterial(Color colour, float smoothness, bool emissive = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = "M_Cigarette_Runtime" };
            material.SetColor(BaseColour, colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionColour, emberCold);
            }
            return material;
        }

        private static Transform MakeCylinder(string name, Transform parent, float length,
            float radius, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            // A primitiva tem 2 unidades de altura, daí o meio comprimento na escala.
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.GetComponent<Renderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        public void BeginTask()
        {
            Build();
            burn = 0f;
            ApplyBurn(0f);
            SetVisible(true);
            active = true;
        }

        public void SetTaskProgress(float normalised)
        {
            if (!active) return;
            burn = Mathf.Clamp01(normalised);
            ApplyBurn(burn);
        }

        public void EndTask()
        {
            // Fica só o filtro por um instante — o cigarro acabou, não desapareceu —
            // e só depois some, como quem o atira pela varanda abaixo.
            ApplyBurn(1f);
            active = false;
            Invoke(nameof(HideNow), 1.1f);
        }

        private void HideNow() => SetVisible(false);

        private void ApplyBurn(float t)
        {
            if (paper == null) return;

            float remaining = Mathf.Max(0.0006f, paperLength * (1f - t));
            paper.localScale = new Vector3(radius * 1.96f, remaining * 0.5f, radius * 1.96f);
            paper.localPosition = new Vector3(0f, filterLength + remaining * 0.5f, 0f);

            if (ash != null)
                ash.localPosition = new Vector3(0f, filterLength + remaining, 0f);

            // A brasa aviva a cada passa em vez de arder constante.
            if (emberRenderer != null)
            {
                float draw = Mathf.Max(0f, Mathf.Sin(Time.time * 1.2f));
                Color hot = Color.Lerp(emberCold, emberHot, draw * draw);
                emberRenderer.GetPropertyBlock(block);
                block.SetColor(BaseColour, hot);
                block.SetColor(EmissionColour, hot * 1.6f);
                emberRenderer.SetPropertyBlock(block);
            }
        }

        private void Update()
        {
            if (active) ApplyBurn(burn);
        }

        private void SetVisible(bool visible)
        {
            if (rig != null) rig.gameObject.SetActive(visible);
        }
    }
}
