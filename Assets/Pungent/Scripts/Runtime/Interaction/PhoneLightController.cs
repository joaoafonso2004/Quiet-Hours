using Pungent.Player;
using UnityEngine;

namespace Pungent.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PhoneLightController : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Light phoneLight;
        [SerializeField] private Renderer screenRenderer;
        [SerializeField] private Color screenOnColor = new Color(0.35f, 0.65f, 1f, 1f);
        [SerializeField] private Color screenOffColor = new Color(0.015f, 0.02f, 0.025f, 1f);
        [SerializeField] private bool startsOn;

        public bool IsOn { get; private set; }

        private void Awake()
        {
            if (input == null) input = GetComponentInParent<PlayerInputReader>();
            SetState(startsOn);
        }

        private void Update()
        {
            if (input != null && input.FlashlightPressed)
                SetState(!IsOn);
        }

        public void SetState(bool value)
        {
            IsOn = value;
            if (phoneLight != null) phoneLight.enabled = value;
            if (screenRenderer != null)
            {
                Material material = screenRenderer.material;
                material.color = value ? screenOnColor : screenOffColor;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", value ? screenOnColor * 2f : Color.black);
                }
            }
        }
    }
}
