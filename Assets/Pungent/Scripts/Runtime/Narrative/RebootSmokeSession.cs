using System.Collections;
using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Fumar um cigarro do princípio ao fim: vinte segundos, com brasa, a encurtar,
    /// e uma barra a dizer quanto falta.
    ///
    /// **Tem fim, e é isso que o separa de um adereço.** Em loop aberto o gesto
    /// deixava de querer dizer alguma coisa ao fim de meia dúzia de repetições — o
    /// Tomás fumava para sempre enquanto o jogador tratava da vida. Com duração, o
    /// cigarro é uma pausa: começa, consome-se à vista, e acaba.
    ///
    /// A barra existe pela mesma razão que a do portátil: sem ela, "quanto falta"
    /// só se sabe olhando para o cigarro, e o cigarro está no canto do olho.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RebootSmokeSession : MonoBehaviour
    {
        [Header("Peças")]
        [Tooltip("A malha do cigarro. Encolhe ao longo do próprio comprimento; a "
               + "ponta que se leva à boca fica onde está.")]
        [SerializeField] private Transform cigarette;

        [SerializeField] private Animator animator;
        [SerializeField] private SmokeExhale exhale;
        [SerializeField] private PrototypeHUD hud;

        [Header("Tempo")]
        [Tooltip("Quanto dura o cigarro inteiro.")]
        [SerializeField, Min(3f)] private float seconds = 20f;

        [SerializeField] private string progressLabel = "Smoking";

        [Header("A arder")]
        [Tooltip("Que fracção do comprimento sobra no fim. É o filtro, e é o que se "
               + "deita fora — nenhum cigarro se fuma até desaparecer.")]
        [SerializeField, Range(0.15f, 0.8f)] private float finalLength = 0.34f;

        [SerializeField] private Color emberCold = new Color(0.30f, 0.07f, 0.02f);
        [SerializeField] private Color emberHot = new Color(1f, 0.38f, 0.09f);

        [Tooltip("A brasa aviva quando ele puxa. É o mesmo instante em que o clip "
               + "manda a baforada, meio segundo antes.")]
        [SerializeField, Min(0.05f)] private float glowSeconds = 1.1f;

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");

        private Transform ember;
        private Renderer emberRenderer;
        private MaterialPropertyBlock block;
        private Vector3 startScale;
        private Vector3 startLocalPos;
        private Vector3 mouthEndLocal;   // a ponta do filtro, em espaço deste rig
        private float glowUntil;
        private bool running;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (exhale == null) exhale = GetComponent<SmokeExhale>();
            if (hud == null) hud = FindObjectOfType<PrototypeHUD>();
            block = new MaterialPropertyBlock();

            if (cigarette == null)
            {
                Debug.LogError("[RebootSmokeSession] Falta a malha do cigarro.", this);
                enabled = false;
                return;
            }

            startScale = cigarette.localScale;
            startLocalPos = cigarette.localPosition;
            mouthEndLocal = MouthEnd();
            BuildEmber();
        }

        private void OnEnable()
        {
            if (!enabled || cigarette == null) return;
            if (!running) StartCoroutine(Burn());
        }

        /// <summary>
        /// A ponta do cigarro que fica do lado da boca, em espaço deste rig.
        ///
        /// Medida e não escrita à mão: a malha vem de um FBX com o pivô fora do
        /// centro e já rodada 180°, e adivinhar qual das pontas é o filtro dava
        /// um cigarro a encolher para o lado errado.
        /// </summary>
        private Vector3 MouthEnd()
        {
            var r = cigarette.GetComponent<Renderer>();
            if (r == null) return cigarette.localPosition;

            Vector3 a = transform.InverseTransformPoint(r.bounds.center + Vector3.zero);
            // as duas pontas ao longo do eixo longo do rig (o Z dele)
            Vector3 c = transform.InverseTransformPoint(r.bounds.center);
            Vector3 e = transform.InverseTransformVector(Vector3.Scale(r.bounds.extents, Vector3.one));
            float half = Mathf.Abs(e.z);
            Vector3 near = c; near.z -= half;
            Vector3 far = c; far.z += half;
            // a boca e a camara: o Z do rig aponta para longe dela, por isso a ponta
            // do filtro e a de Z menor.
            return near;
        }

        private void BuildEmber()
        {
            if (ember != null) return;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Ember";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            ember = go.transform;
            ember.SetParent(transform, false);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader);
            mat.color = emberCold;
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            emberRenderer = go.GetComponent<Renderer>();
            emberRenderer.sharedMaterial = mat;
            emberRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // O cilindro do Unity tem 2 m de altura e o eixo em Y: dai a rotacao e
            // a escala minusculas.
            ember.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ember.localScale = new Vector3(0.0052f, 0.0016f, 0.0052f);
        }

        private IEnumerator Burn()
        {
            running = true;
            float t = 0f;
            if (animator != null) animator.Rebind();

            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / seconds);

                // encolhe ao longo do comprimento e devolve a ponta do filtro ao sitio
                Vector3 s = startScale;
                s.z = startScale.z * Mathf.Lerp(1f, finalLength, k);
                cigarette.localScale = s;
                Vector3 agora = MouthEnd();
                cigarette.localPosition += (mouthEndLocal - agora);

                PlaceEmber();
                PaintEmber();

                if (hud != null) hud.SetTaskProgress(progressLabel, k);
                yield return null;
            }

            if (hud != null) hud.ClearTaskProgress();
            if (exhale != null) exhale.ClearSmoke();
            running = false;
            gameObject.SetActive(false);      // o filtro vai fora; a mão fica vazia
        }

        /// <summary>A brasa vive na ponta que arde, e essa ponta anda para trás.</summary>
        private void PlaceEmber()
        {
            if (ember == null) return;
            var r = cigarette.GetComponent<Renderer>();
            if (r == null) return;
            Vector3 c = transform.InverseTransformPoint(r.bounds.center);
            Vector3 e = transform.InverseTransformVector(r.bounds.extents);
            Vector3 tip = c; tip.z += Mathf.Abs(e.z);
            ember.localPosition = tip;
        }

        private void PaintEmber()
        {
            if (emberRenderer == null) return;
            float hot = Mathf.Clamp01((glowUntil - Time.time) / glowSeconds);
            Color c = Color.Lerp(emberCold, emberHot, hot);
            emberRenderer.GetPropertyBlock(block);
            block.SetColor(BaseColour, c);
            block.SetColor(EmissionColour, c * Mathf.Lerp(1.1f, 4.5f, hot));
            emberRenderer.SetPropertyBlock(block);
        }

        /// <summary>Chamado pelo `AnimationEvent`, no mesmo instante da baforada.</summary>
        public void Glow() => glowUntil = Time.time + glowSeconds;
    }
}
