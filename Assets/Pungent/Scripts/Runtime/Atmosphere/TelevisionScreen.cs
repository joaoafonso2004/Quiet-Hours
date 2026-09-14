using UnityEngine;

namespace Pungent.Atmosphere
{
    /// <summary>
    /// Poe a televisao a dar.
    ///
    /// O objecto TV era so um mesh com um material chapado: nao emitia luz, nao
    /// fazia som e nao mudava. Uma televisao ligada num apartamento as duas da
    /// manha nao e um ecra aceso — e uma luz que muda de cor e de intensidade
    /// sozinha e pinta a parede atras do sofa. E essa luz instavel que faz a sala
    /// parecer habitada quando ninguem la esta.
    ///
    /// O brilho e feito com um quad emissivo por cima do ecra e uma Light, pelo
    /// mesmo motivo que os LEDs do router: mexer no material partilhado do movel
    /// pintava todos os que usam o mesmo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TelevisionScreen : MonoBehaviour, Pungent.Interaction.IPlayerInteractable
    {
        [Header("Peças")]
        [Tooltip("O quad emissivo colado ao ecra. Criado pelo passe de arte.")]
        [SerializeField] private Renderer glow;
        [Tooltip("A luz que pinta a sala. Fica dentro do aparelho, virada para fora.")]
        [SerializeField] private Light spill;
        [SerializeField] private AudioSource audioSource;

        [Header("Estado")]
        [SerializeField] private bool on = true;

        [Header("Imagem")]
        [Tooltip("Cor base do ecra. Fria: a maioria da emissao de madrugada e azulada.")]
        [SerializeField] private Color tint = new Color(0.62f, 0.74f, 1f);
        [SerializeField, Min(0f)] private float baseIntensity = 1.6f;
        [Tooltip("Quanto a imagem salta entre planos.")]
        [SerializeField, Range(0f, 1f)] private float flicker = 0.45f;
        [Tooltip("Cortes de plano por segundo. Baixo = programa parado, alto = anuncios.")]
        [SerializeField, Min(0.1f)] private float cutsPerSecond = 1.4f;

        [Header("Luz na sala")]
        [SerializeField, Min(0f)] private float spillIntensity = 0.55f;
        [SerializeField, Min(0f)] private float spillRange = 6f;

        private static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        private MaterialPropertyBlock block;
        private float nextCut;
        private float currentLevel = 1f;
        private float targetLevel = 1f;
        private Color currentTint;

        public bool IsOn => on;

        /// <summary>
        /// O rectangulo que faz de ecra. Exposto para quem lhe queira por imagem em
        /// cima — este componente trata da luz, nao do que esta a dar.
        /// </summary>
        public Renderer GlowRenderer => glow;

        // --- interacao do jogador ---
        //
        // O prompt dizia "Turn on TV" e o componente por tras era um
        // FlavourInteractable: convidava a liga-la e o clique so devolvia um
        // pensamento. Agora o interruptor e mesmo o interruptor.

        public string Prompt => on ? "Turn off the TV" : "Turn on the TV";
        public bool HoldToInteract => false;

        public void BeginInteraction(Pungent.Interaction.PlayerInteractor interactor) => SetOn(!on);
        public void UpdateInteraction(Pungent.Interaction.PlayerInteractor interactor, Vector2 delta) { }
        public void EndInteraction(Pungent.Interaction.PlayerInteractor interactor) { }

        private void OnEnable()
        {
            currentTint = tint;
            Apply();
        }

        public void SetOn(bool value)
        {
            if (on == value) return;
            on = value;

            if (audioSource != null)
            {
                if (on && audioSource.clip != null) audioSource.Play();
                else audioSource.Stop();
            }

            Apply();
        }

        private void Update()
        {
            if (!on) return;

            // Cortes de plano: o nivel salta de repente e depois estabiliza, em vez
            // de oscilar como uma onda. Uma televisao nao pulsa, muda de imagem.
            if (Time.time >= nextCut)
            {
                nextCut = Time.time + Random.Range(0.35f, 1.6f) / cutsPerSecond;
                targetLevel = 1f + Random.Range(-flicker, flicker * 0.6f);

                // De vez em quando um plano muito mais claro ou mais escuro.
                if (Random.value < 0.12f) targetLevel *= Random.Range(0.35f, 1.9f);

                currentTint = tint * Random.Range(0.88f, 1.12f);
                currentTint.a = 1f;
            }

            currentLevel = Mathf.Lerp(currentLevel, targetLevel, Time.deltaTime * 9f);
            Apply();
        }

        private void Apply()
        {
            float level = on ? Mathf.Max(0f, currentLevel) : 0f;

            if (glow != null)
            {
                // O bloco e criado a cabeca do uso: um domain reload repoe os campos
                // nao serializados a nulo sem voltar a correr o OnEnable.
                if (block == null) block = new MaterialPropertyBlock();
                glow.GetPropertyBlock(block);

                // O ecra usa o shader Unlit do URP, que NAO tem _EmissionColor: a
                // cor sai toda pelo _BaseColor. Escrever so na emissao — que era o
                // que estava aqui — nao pintava nada e o painel ficava um rectangulo
                // baco. O brilho vem de o valor passar de 1: o Bloom do Global
                // Volume trata do resto.
                Color lit = currentTint * baseIntensity * level;
                lit.a = 1f;
                block.SetColor(BaseColour, lit);
                // Mantido para quem trocar o material por um Lit.
                block.SetColor(EmissionColour, lit);

                glow.SetPropertyBlock(block);
                glow.enabled = on;
            }

            if (spill != null)
            {
                spill.enabled = on;
                spill.color = currentTint;
                spill.intensity = spillIntensity * level;
                spill.range = spillRange;
            }
        }
    }
}
