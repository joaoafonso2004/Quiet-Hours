using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O fumo que sai da boca, uma baforada de cada vez.
    ///
    /// **Não emite sozinho.** Quem manda é o clip de fumar, por `AnimationEvent`:
    /// a baforada tem de sair depois de o cigarro descer, não enquanto está nos
    /// lábios. Um sistema a emitir de contínuo lê-se como fumo de escape, e é
    /// exactamente o género de coisa que ninguém corrige depois de a ver uma vez.
    ///
    /// O sistema simula em **espaço do mundo** de propósito: assim a nuvem fica
    /// onde foi soprada e o jogador pode virar a cara e vê-la ficar para trás. Em
    /// espaço local acompanhava a câmara e parecia colada à cara.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SmokeExhale : MonoBehaviour
    {
        [Tooltip("O sistema de partículas da baforada. Vive na câmara e não no "
               + "cigarro: o fumo sai da boca, que não se mexe, e o cigarro mexe-se.")]
        [SerializeField] private ParticleSystem smoke;

        [Tooltip("Quantas partículas por baforada. Poucas e grandes leem-se melhor "
               + "do que muitas e pequenas — fumo não tem grão.")]
        [SerializeField, Min(1)] private int particles = 9;

        private void Reset() => smoke = GetComponentInChildren<ParticleSystem>(true);

        /// <summary>Chamado pelo `AnimationEvent` do clip `Smoke_Drag`.</summary>
        public void Puff()
        {
            if (smoke == null) return;
            smoke.Emit(particles);
        }

        /// <summary>Cala o fumo quando o cigarro é largado.</summary>
        public void ClearSmoke()
        {
            if (smoke == null) return;
            smoke.Clear(true);
        }
    }
}
