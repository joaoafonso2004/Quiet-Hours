using UnityEngine;

namespace Pungent.Player
{
    /// <summary>Integração implícita de mola amortecida, estável entre diferentes frame rates.</summary>
    public static class SpringMath
    {
        private const float TwoPi = 2f * Mathf.PI;

        public static void Step(ref float current, ref float velocity, float target,
            float frequencyHz, float dampingRatio, float deltaTime)
        {
            if (deltaTime <= 0f) return;

            float angularFrequency = TwoPi * Mathf.Max(0.01f, frequencyHz);
            float damping = Mathf.Max(0f, dampingRatio);
            float f = 1f + 2f * deltaTime * damping * angularFrequency;
            float oo = angularFrequency * angularFrequency;
            float hoo = deltaTime * oo;
            float hhoo = deltaTime * hoo;
            float inverseDeterminant = 1f / (f + hhoo);
            float previous = current;

            current = (f * current + deltaTime * velocity + hhoo * target) * inverseDeterminant;
            velocity = (velocity + hoo * (target - previous)) * inverseDeterminant;
        }

        public static void Step(ref Vector3 current, ref Vector3 velocity, Vector3 target,
            float frequencyHz, float dampingRatio, float deltaTime)
        {
            float x = current.x, y = current.y, z = current.z;
            float vx = velocity.x, vy = velocity.y, vz = velocity.z;
            Step(ref x, ref vx, target.x, frequencyHz, dampingRatio, deltaTime);
            Step(ref y, ref vy, target.y, frequencyHz, dampingRatio, deltaTime);
            Step(ref z, ref vz, target.z, frequencyHz, dampingRatio, deltaTime);
            current = new Vector3(x, y, z);
            velocity = new Vector3(vx, vy, vz);
        }
    }
}
