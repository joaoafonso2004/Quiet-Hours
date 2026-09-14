using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// So os postes perto do carro tem a lampada acesa.
    ///
    /// Cento e cinquenta luzes vivas ao mesmo tempo nao servem para nada: o
    /// nevoeiro come tudo depois dos ~250 m. O renderer esta em **Forward+**
    /// precisamente por causa dos postes — no Forward classico ha 8 luzes por
    /// objecto, o alcatrao e UMA malha de dez quilometros, e a fila de candeeiros
    /// disputava esses slots com os farois do carro.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoleLights : MonoBehaviour
    {
        [Tooltip("Perto de quem. O carro.")]
        [SerializeField] private Transform focus;

        [Tooltip("Acende dentro desta distancia; o resto fica apagado. Um pouco "
               + "alem do nevoeiro, para a fila de luzes se perder na noite.")]
        [SerializeField, Min(10f)] private float litWithin = 220f;

        [Tooltip("As lampadas todas, preenchidas por quem monta a estrada.")]
        [SerializeField] private Light[] lamps = new Light[0];

        private float nextSweepAt;

        private void Update()
        {
            if (focus == null || Time.time < nextSweepAt) return;
            nextSweepAt = Time.time + 0.4f;

            float sqr = litWithin * litWithin;
            foreach (var lamp in lamps)
            {
                if (lamp == null) continue;
                bool near = (lamp.transform.position - focus.position).sqrMagnitude < sqr;
                if (lamp.enabled != near) lamp.enabled = near;
            }
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de montagem, que vive noutra assembly.</summary>
        public void EditorSetLamps(Light[] value, Transform focusValue)
        {
            lamps = value;
            focus = focusValue;
        }
#endif
    }
}
