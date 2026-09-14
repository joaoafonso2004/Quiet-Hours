using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// A linha central da estrada, em metros.
    ///
    /// A estrada do pack e uma malha so, de 10 km, gerada por um spline que ja nao
    /// vem com o projecto — sabe-se onde ela passa mas nao ha caminho nenhum a
    /// dizer isso. Sem esse caminho nao ha forma de responder as unicas perguntas
    /// que este capitulo faz: quantos metros e que ele ja conduziu, e onde e que se
    /// poe um par de farois atras dele.
    ///
    /// Os vertices do alcatrao vem aos pares — bordo esquerdo, bordo direito — pela
    /// ordem em que o gerador os cuspiu. O centro de cada par da a linha central, e
    /// 808 pares dao 9999 m, que bate certo com os 10 km do nome do ficheiro.
    ///
    /// Assado no editor, e nao lido em cada arranque: a malha e legivel, mas ler
    /// onze mil vertices para chegar a oitocentos pontos e trabalho a repetir todos
    /// os dias por nada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoadPath : MonoBehaviour
    {
        [SerializeField] private Vector3[] points = new Vector3[0];
        [SerializeField] private float[] cumulative = new float[0];

        public int Count => points.Length;

        /// <summary>Comprimento total, em metros.</summary>
        public float Length => cumulative.Length > 0 ? cumulative[cumulative.Length - 1] : 0f;

        public Vector3 PointAt(float distance)
        {
            if (points.Length == 0) return transform.position;
            if (points.Length == 1) return transform.TransformPoint(points[0]);

            distance = Mathf.Clamp(distance, 0f, Length);
            int i = IndexBefore(distance);
            int j = Mathf.Min(i + 1, points.Length - 1);

            float segment = cumulative[j] - cumulative[i];
            float t = segment > 0.0001f ? (distance - cumulative[i]) / segment : 0f;
            return transform.TransformPoint(Vector3.Lerp(points[i], points[j], t));
        }

        /// <summary>Para onde a estrada aponta aos <paramref name="distance"/> metros.</summary>
        public Vector3 ForwardAt(float distance)
        {
            if (points.Length < 2) return transform.forward;

            int i = IndexBefore(Mathf.Clamp(distance, 0f, Length));
            int j = Mathf.Min(i + 1, points.Length - 1);
            if (i == j) i = Mathf.Max(0, j - 1);

            Vector3 direction = transform.TransformPoint(points[j]) - transform.TransformPoint(points[i]);
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
        }

        /// <summary>
        /// Quantos metros de estrada ha ate este ponto.
        ///
        /// O <paramref name="hint"/> e o indice da ultima resposta. Um carro anda
        /// para a frente, portanto procurar a volta de onde ele estava evita varrer
        /// as oitocentas seccoes a cada frame — e, mais importante, evita que uma
        /// curva apertada o teleporte para outro sitio da estrada que por acaso
        /// passa perto.
        /// </summary>
        public float DistanceOf(Vector3 worldPosition, ref int hint)
        {
            if (points.Length == 0) return 0f;

            Vector3 local = transform.InverseTransformPoint(worldPosition);

            int from = 0, to = points.Length - 1;
            if (hint >= 0 && hint < points.Length)
            {
                from = Mathf.Max(0, hint - 12);
                to = Mathf.Min(points.Length - 1, hint + 40);
            }

            int best = from;
            float bestSqr = float.MaxValue;
            for (int i = from; i <= to; i++)
            {
                float sqr = (points[i] - local).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = i;
            }

            // Encostou ao limite da janela: a janela ficou para tras (ou o carro foi
            // parar longe). Varrer tudo uma vez para reencontrar o sitio.
            if ((best == from && from > 0) || (best == to && to < points.Length - 1))
            {
                bestSqr = float.MaxValue;
                for (int i = 0; i < points.Length; i++)
                {
                    float sqr = (points[i] - local).sqrMagnitude;
                    if (sqr >= bestSqr) continue;
                    bestSqr = sqr;
                    best = i;
                }
            }

            hint = best;
            return cumulative[best];
        }

        private int IndexBefore(float distance)
        {
            int low = 0, high = cumulative.Length - 1;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (cumulative[mid] <= distance) low = mid; else high = mid - 1;
            }
            return low;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta que assa isto a partir da malha.</summary>
        public void EditorSetPoints(Vector3[] centreline)
        {
            points = centreline;
            cumulative = new float[centreline.Length];

            float total = 0f;
            for (int i = 1; i < centreline.Length; i++)
            {
                total += Vector3.Distance(centreline[i - 1], centreline[i]);
                cumulative[i] = total;
            }
        }
#endif

        private void OnDrawGizmosSelected()
        {
            if (points.Length < 2) return;

            Gizmos.color = new Color(0.95f, 0.75f, 0.25f, 0.9f);
            for (int i = 1; i < points.Length; i++)
                Gizmos.DrawLine(transform.TransformPoint(points[i - 1]), transform.TransformPoint(points[i]));
        }
    }
}
