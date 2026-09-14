using System.Collections.Generic;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// Semeia a berma a volta do carro: relva seca colada ao alcatrao, relva e
    /// flores na faixa verde, canicos na vala.
    ///
    /// **Por blocos, e nao a cena inteira:** sao dez quilometros de estrada. Semear
    /// tudo no editor punha milhares de objectos gravados na cena; aqui vivem so
    /// os ~10 blocos a alcance da vista, deterministas (mesma semente, mesma
    /// berma), e os que ficam para tras morrem. Com o nevoeiro a comer tudo aos
    /// 200 m, ninguem ve a relva a nascer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadsideScatter : MonoBehaviour
    {
        [SerializeField] private RoadPath path;
        [Tooltip("A volta de quem se semeia. O carro.")]
        [SerializeField] private Transform focus;

        [Tooltip("Coladas ao alcatrao: as secas.")]
        [SerializeField] private GameObject[] nearRoad = new GameObject[0];
        [Tooltip("A faixa verde: relva comum e flores.")]
        [SerializeField] private GameObject[] verge = new GameObject[0];
        [Tooltip("A vala, mais longe: canicos.")]
        [SerializeField] private GameObject[] ditch = new GameObject[0];

        [Tooltip("Onde as manchas assentam. A layer do alcatrao e bermas.")]
        [SerializeField] private LayerMask groundMask = ~0;

        [SerializeField, Min(10f)] private float chunkLength = 50f;
        [SerializeField, Min(50f)] private float viewDistance = 240f;

        [Tooltip("Manchas por bloco de 50 m, contando os dois lados. 40 e uma "
               + "berma cheia; baixar isto e a primeira coisa a fazer se a cena "
               + "pesar.")]
        [SerializeField, Min(1)] private int patchesPerChunk = 40;
        [SerializeField] private int seed = 971;

        private readonly Dictionary<int, GameObject> chunks = new Dictionary<int, GameObject>();
        private readonly List<int> toRemove = new List<int>();
        private int hint = -1;
        private float nextSweepAt;

        private void Update()
        {
            if (path == null || focus == null || Time.time < nextSweepAt) return;
            nextSweepAt = Time.time + 0.5f;

            float centre = path.DistanceOf(focus.position, ref hint);
            int first = Mathf.Max(0, Mathf.FloorToInt((centre - viewDistance) / chunkLength));
            int last = Mathf.FloorToInt((centre + viewDistance) / chunkLength);

            toRemove.Clear();
            foreach (var pair in chunks)
                if (pair.Key < first || pair.Key > last) toRemove.Add(pair.Key);
            foreach (int key in toRemove)
            {
                Destroy(chunks[key]);
                chunks.Remove(key);
            }

            for (int i = first; i <= last; i++)
                if (!chunks.ContainsKey(i) && i * chunkLength < path.Length)
                    chunks.Add(i, BuildChunk(i));
        }

        private GameObject BuildChunk(int index)
        {
            var root = new GameObject($"GRASS_{index:D3}");
            root.transform.SetParent(transform, false);

            // Determinista por bloco: voltar atras na estrada mostra a mesma berma,
            // e nao uma paisagem re-baralhada.
            var rng = new System.Random(seed * 92821 + index);

            for (int n = 0; n < patchesPerChunk; n++)
            {
                float distance = (index + (float)rng.NextDouble()) * chunkLength;
                if (distance >= path.Length) continue;

                float roll = (float)rng.NextDouble();
                GameObject[] pool;
                float lateral, scale;
                if (roll < 0.35f)
                {
                    pool = nearRoad;
                    lateral = Mathf.Lerp(5.4f, 7.2f, (float)rng.NextDouble());
                    scale = Mathf.Lerp(0.45f, 0.7f, (float)rng.NextDouble());
                }
                else if (roll < 0.8f)
                {
                    pool = verge;
                    lateral = Mathf.Lerp(6.6f, 11.5f, (float)rng.NextDouble());
                    scale = Mathf.Lerp(0.5f, 0.85f, (float)rng.NextDouble());
                }
                else
                {
                    pool = ditch;
                    lateral = Mathf.Lerp(10f, 13f, (float)rng.NextDouble());
                    scale = Mathf.Lerp(0.8f, 1.2f, (float)rng.NextDouble());
                }
                if (pool.Length == 0) continue;

                var prefab = pool[rng.Next(pool.Length)];
                int side = rng.Next(2) == 0 ? -1 : 1;

                Vector3 forward = path.ForwardAt(distance);
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                Vector3 probe = path.PointAt(distance) + right * (side * lateral) + Vector3.up * 3f;

                // A berma sobe e desce com o banking; sem o raio, metade das manchas
                // flutuava e a outra metade enterrava-se. Onde nao ha chao (fim do
                // talude), simplesmente nao ha relva — que e o correcto.
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 8f,
                        groundMask, QueryTriggerInteraction.Ignore)) continue;

                var patch = Instantiate(prefab, root.transform);
                patch.transform.SetPositionAndRotation(
                    hit.point + Vector3.down * 0.03f,
                    Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                patch.transform.localScale = Vector3.one * scale;
            }
            return root;
        }
    }
}
