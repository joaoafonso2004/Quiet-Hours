using UnityEngine;
using UnityEngine.Events;

namespace Pungent.NPC
{
    /// <summary>
    /// Marca um anchor de rotina como sítio onde o NPC *faz* alguma coisa, em vez
    /// de apenas fazer uma pausa.
    ///
    /// É o que separa "andar em círculos" de "viver ali": o Rui encosta-se à
    /// bancada, abre o frigorífico, senta-se no sofá. O plano mestre trata isto
    /// como essencial ao slow-burn (pilar P7).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoutineActionAnchor : MonoBehaviour
    {
        [Tooltip("Nome do estado no AnimatorController. Ver RuiAnimatorSetup.Actions.")]
        [SerializeField] private string animatorState = "CounterLean";
        [Tooltip("Quanto tempo fica na ação antes de retomar o circuito.")]
        [SerializeField, Min(0.5f)] private float seconds = 6f;
        [Tooltip("Variação aleatória somada ao tempo, para não ser sempre igual.")]
        [SerializeField, Min(0f)] private float variance = 2.5f;
        [Tooltip("Vira-se para o forward do anchor antes de começar.")]
        [SerializeField] private bool alignToAnchor = true;

        [Header("Deslocamento da ação")]
        [Tooltip("Para onde o corpo desliza durante a ação, em espaço local do anchor. "
               + "Existe porque o sítio onde se faz a coisa nem sempre é sítio onde se "
               + "pode chegar: a NavMesh é cozida a partir dos meshes, portanto o "
               + "assento do sofá é um buraco e nenhum agente lá chega. O anchor fica "
               + "à frente do sofá, alcançável, e o corpo entra nos últimos 40 cm "
               + "com o agente desligado.")]
        [SerializeField] private Vector3 actionOffset = Vector3.zero;

        [Tooltip("Quanto tempo demora a entrar e a sair do deslocamento.")]
        [SerializeField, Min(0.05f)] private float settleSeconds = 0.7f;

        [Header("Transições")]
        [Tooltip("Estado tocado antes da acção. Sem ele o CrossFade salta direto para "
               + "a pose e o NPC aparece já sentado em vez de se sentar.")]
        [SerializeField] private string enterState;
        [Tooltip("Estado tocado ao sair da acção, antes de retomar o circuito.")]
        [SerializeField] private string exitState;

        public string EnterState => enterState;
        public string ExitState => exitState;
        public bool HasEnter => !string.IsNullOrEmpty(enterState);
        public bool HasExit => !string.IsNullOrEmpty(exitState);

        [Header("Events")]
        public UnityEvent<string> onActionStarted;
        public UnityEvent<string> onActionEnded;

        public string AnimatorState => animatorState;
        public bool AlignToAnchor => alignToAnchor;
        public float PickDuration() => seconds + Random.Range(0f, variance);
        public bool HasOffset => actionOffset.sqrMagnitude > 0.0001f;
        public float SettleSeconds => settleSeconds;

        /// <summary>Onde o corpo fica durante a acção, em espaço do mundo.</summary>
        public Vector3 ActionPosition => transform.TransformPoint(actionOffset);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.35f, 0.8f, 1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.3f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward * 0.8f);
        }
    }
}
