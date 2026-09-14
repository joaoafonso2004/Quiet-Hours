using UnityEngine;

namespace Pungent.Interaction
{
    /// <summary>
    /// Interruptor de parede funcional. Liga e desliga um conjunto de luzes e
    /// roda fisicamente a tecla, para a acção ser legível sem HUD.
    ///
    /// O estado inicial é lido das próprias luzes no Awake, de modo a que a cena
    /// continue a ser a fonte de verdade sobre o que está aceso às 02:47.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LightSwitchInteractable : MonoBehaviour, IPlayerInteractable
    {
        [Header("Luzes controladas")]
        [SerializeField] private Light[] lights;
        [Tooltip("Objetos acesos/apagados com o interruptor, tipicamente candeeiros emissivos.")]
        [SerializeField] private GameObject[] emissiveObjects;

        [Header("Tecla")]
        [SerializeField] private Transform toggleTransform;
        [SerializeField, Range(2f, 25f)] private float toggleAngle = 11f;
        [SerializeField, Min(1f)] private float toggleSpeed = 16f;

        [Header("Som")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip clickOn;
        [SerializeField] private AudioClip clickOff;
        [SerializeField, Range(0f, 1f)] private float clickVolume = 0.5f;

        [Header("Texto")]
        [SerializeField] private string roomName = "light";

        private bool isOn;
        private Quaternion restRotation;
        private float visualAngle;

        public string Prompt => isOn ? $"Turn off the {roomName}" : $"Turn on the {roomName}";
        public bool HoldToInteract => false;

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (toggleTransform != null) restRotation = toggleTransform.localRotation;
            
            Debug.Log($"[LightSwitch] {gameObject.name}: audioSource={(audioSource != null)}, clickOn={(clickOn != null)}, clickOff={(clickOff != null)}");

            // O estado da cena manda: se a luz ja estava acesa, o interruptor esta ligado.
            isOn = false;
            if (lights != null)
                foreach (var light in lights)
                    if (light != null && light.enabled) { isOn = true; break; }

            visualAngle = isOn ? toggleAngle : -toggleAngle;
            ApplyToggleRotation(visualAngle);
        }

        private void Update()
        {
            if (toggleTransform == null) return;
            float target = isOn ? toggleAngle : -toggleAngle;
            if (Mathf.Approximately(visualAngle, target)) return;

            visualAngle = Mathf.MoveTowards(visualAngle, target, toggleSpeed * 60f * Time.deltaTime);
            ApplyToggleRotation(visualAngle);
        }

        private void ApplyToggleRotation(float angle)
        {
            if (toggleTransform == null) return;
            toggleTransform.localRotation = restRotation * Quaternion.Euler(angle, 0f, 0f);
        }

        public void BeginInteraction(PlayerInteractor interactor) => Toggle();

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

        /// <summary>Permite que a narrativa apague luzes sem passar pelo jogador.</summary>
        public void SetState(bool on)
        {
            if (isOn == on) return;
            Toggle();
        }

        private void Toggle()
        {
            isOn = !isOn;

            if (lights != null)
                foreach (var light in lights)
                    if (light != null) light.enabled = isOn;

            if (emissiveObjects != null)
                foreach (var go in emissiveObjects)
                    if (go != null) go.SetActive(isOn);

            if (audioSource != null)
            {
                AudioClip clip = isOn ? clickOn : clickOff;
                if (clip != null) audioSource.PlayOneShot(clip, clickVolume);
            }
        }
    }
}
