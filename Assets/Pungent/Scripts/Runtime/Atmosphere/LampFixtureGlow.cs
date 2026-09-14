using UnityEngine;

namespace Pungent.Atmosphere
{
    /// <summary>
    /// Faz o abajur do candeeiro acender e apagar com a Light que ele representa.
    ///
    /// Sem isto o vidro do candeeiro ficava emissivo permanentemente: numa casa as
    /// escuras as duas da manha, com os interruptores todos desligados, o tecto
    /// ficava cheio de lampadas a brilhar sem darem luz nenhuma.
    ///
    /// Usa um MaterialPropertyBlock e nao um material novo: os candeeiros partilham
    /// todos o mesmo material do pack, e instanciar um por candeeiro sujava o
    /// projecto com copias e deixava-as penduradas na memoria.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LampFixtureGlow : MonoBehaviour
    {
        private static readonly int EmissionColour = Shader.PropertyToID("_EmissionColor");

        [Tooltip("A luz que este candeeiro representa. E o interruptor que a liga.")]
        [SerializeField] private Light source;
        [Tooltip("Partes do candeeiro que brilham (o vidro, normalmente).")]
        [SerializeField] private Renderer[] glowing;
        [Tooltip("Cor do abajur aceso. Multiplica pela cor da propria luz.")]
        [SerializeField] private Color litColour = new Color(1f, 0.86f, 0.62f);
        [SerializeField, Min(0f)] private float litIntensity = 2.2f;

        private MaterialPropertyBlock block;
        private bool? lastState;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (glowing == null || glowing.Length == 0)
                glowing = GetComponentsInChildren<Renderer>(true);
        }

        private void OnEnable() => lastState = null;

        private void LateUpdate()
        {
            bool on = source != null && source.enabled && source.gameObject.activeInHierarchy;
            if (lastState == on) return;
            lastState = on;
            Apply(on);
        }

        private void Apply(bool on)
        {
            Color tint = source != null ? source.color : Color.white;
            Color emission = on
                ? litColour * tint * litIntensity
                : Color.black;

            // Depois de um domain reload o Awake nao volta a correr mas os campos
            // nao serializados sao repostos a nulo, e o GetPropertyBlock rebentava
            // com ArgumentNullException a cada recompilacao.
            if (block == null) block = new MaterialPropertyBlock();

            foreach (var renderer in glowing)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                block.SetColor(EmissionColour, emission);
                renderer.SetPropertyBlock(block);
            }
        }
    }
}
