using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Pungent.Interaction;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Diz **quais** sao os alvos maus, e porque.
    ///
    /// A medicao que ja existia — o <see cref="AimProbe"/> — poe o jogador a volta
    /// de tres caixas e conta acertos. E a medida certa, mas so responde sobre o
    /// que estiver vivo naquele momento do capitulo, e o apartamento tem oitenta e
    /// tal interactaveis espalhados por quatro dias. Uma coisa que so fala quando o
    /// dia certo esta a correr nao serve para varrer o jogo.
    ///
    /// Isto corre fora do Play e nao pergunta nada a fisica: le a geometria dos
    /// volumes e procura as quatro formas conhecidas de um alvo se tornar
    /// impossivel de apontar. Nenhuma delas e opiniao — todas se veem nos numeros:
    ///
    /// 1. **sem volume**: interactavel sem colisor ligado nenhum. Nao existe para
    ///    a mira, por muito bem que esteja escrito;
    /// 2. **empilhados**: dois verbos no mesmo GameObject. O `PlayerInteractor`
    ///    resolve pelo primeiro que a `GetComponents` devolver, que e a ordem em
    ///    que os componentes foram adicionados — invisivel na cena e invisivel na
    ///    revisao. O lava-loica tem cinco;
    /// 3. **engolidos**: o volume de A quase todo dentro do volume de B. Quem
    ///    aponta a A atravessa B primeiro, e a mira fica com quem tiver o angulo
    ///    mais simpatico. E o passo de arrumar a roupa dentro do volume da cama;
    /// 4. **pequenos de mais**: o alvo ocupa menos de meio grau de raio a 1,1 m,
    ///    a distancia a que se joga isto. Abaixo disso ja nao se aponta, procura-se.
    ///
    /// Nao mexe em nada. So conta.
    /// </summary>
    internal static class InteractionHitboxAudit
    {
        /// <summary>Distancia tipica de uso, medida no apartamento.</summary>
        private const float Reference = 1.1f;

        /// <summary>
        /// Raio angular abaixo do qual apontar deixa de ser apontar.
        ///
        /// Meio grau a 1,1 m sao dez milimetros de alvo. O `probeRadius` do
        /// interactor e 0,11 m, portanto tudo acima disto ja e apanhado pelo cone;
        /// o que cai abaixo depende de o raio fino acertar em cheio.
        /// </summary>
        private const float MinAngularRadius = 0.5f;

        /// <summary>A partir de quanto e que estar dentro de outro passa a ser um problema.</summary>
        private const float EngulfedFraction = 0.8f;

        private sealed class Target
        {
            public MonoBehaviour Behaviour;
            public GameObject Owner;
            public Bounds Volume;
            public bool HasVolume;
            public List<Collider> Colliders = new List<Collider>();
        }

        [MenuItem("Pungent/Debug/Auditar os volumes de interaccao", false, 200)]
        internal static void Audit()
        {
            List<Target> targets = Collect();

            var report = new StringBuilder();
            int noVolume = 0, stacked = 0, engulfed = 0, tiny = 0;

            // ---- 1. sem volume ----
            var lines = new StringBuilder();
            foreach (Target t in targets)
            {
                if (t.HasVolume) continue;
                noVolume++;
                lines.Append("   ").Append(Describe(t)).Append('\n');
            }
            Section(report, "SEM VOLUME (a mira nao lhes chega)", noVolume, lines);

            // ---- 2. empilhados no mesmo GameObject ----
            lines.Length = 0;
            var byOwner = new Dictionary<GameObject, List<Target>>();
            foreach (Target t in targets)
            {
                List<Target> list;
                if (!byOwner.TryGetValue(t.Owner, out list))
                {
                    list = new List<Target>();
                    byOwner[t.Owner] = list;
                }
                list.Add(t);
            }
            foreach (var pair in byOwner)
            {
                if (pair.Value.Count < 2) continue;

                // Uma frase sobre um movel empilhada com um verbo ja nao e um
                // problema: o `PlayerInteractor` poe o ambiente sempre a perder, e
                // um `IPriorityInteractable` ganha sempre. O que continua por
                // decidir e **dois verbos do mesmo escalao** no mesmo GameObject,
                // porque ai quem escolhe volta a ser a ordem dos componentes.
                int verbs = 0;
                foreach (Target t in pair.Value)
                    if (!(t.Behaviour is IAmbientInteractable) &&
                        !(t.Behaviour is IPriorityInteractable)) verbs++;
                if (verbs < 2) continue;

                stacked++;
                lines.Append("   ").Append(pair.Key.name).Append(": ");
                // A ordem e a que o `Resolve` do interactor percorre.
                MonoBehaviour[] order = pair.Key.GetComponents<MonoBehaviour>();
                bool first = true;
                foreach (MonoBehaviour mb in order)
                {
                    if (!(mb is IPlayerInteractable)) continue;
                    if (mb is IAmbientInteractable || mb is IPriorityInteractable) continue;
                    lines.Append(first ? "" : " > ").Append(mb.GetType().Name);
                    first = false;
                }
                lines.Append("   (ganha o primeiro que tiver prompt)\n");
            }
            Section(report, "DOIS VERBOS NO MESMO SITIO (a ordem dos componentes decide)",
                stacked, lines);

            // ---- 3. engolidos ----
            lines.Length = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                Target a = targets[i];
                if (!a.HasVolume) continue;

                for (int j = 0; j < targets.Count; j++)
                {
                    if (i == j) continue;
                    Target b = targets[j];
                    if (!b.HasVolume) continue;
                    if (a.Owner == b.Owner) continue;                 // ja contado acima
                    if (IsRelated(a.Owner, b.Owner)) continue;        // pai e filho sao o mesmo objecto
                    if (Volume(a.Volume) > Volume(b.Volume)) continue; // so interessa o pequeno dentro do grande

                    float inside = Overlap(a.Volume, b.Volume) / Mathf.Max(0.000001f, Volume(a.Volume));
                    if (inside < EngulfedFraction) continue;

                    engulfed++;
                    lines.Append("   ").Append(Describe(a))
                         .Append("\n      ").Append(Mathf.RoundToInt(inside * 100f))
                         .Append("% dentro de ").Append(Describe(b)).Append('\n');
                }
            }
            Section(report, "ENGOLIDOS (o volume de outro esta por cima)", engulfed, lines);

            // ---- 4. pequenos de mais ----
            lines.Length = 0;
            foreach (Target t in targets)
            {
                if (!t.HasVolume) continue;
                Vector3 e = t.Volume.extents;
                float smallest = Mathf.Min(e.x, Mathf.Min(e.y, e.z));
                float degrees = Mathf.Atan2(smallest, Reference) * Mathf.Rad2Deg;
                if (degrees >= MinAngularRadius) continue;

                tiny++;
                lines.Append("   ").Append(Describe(t))
                     .Append("   ").Append(degrees.ToString("0.00"))
                     .Append(" graus de raio a ").Append(Reference).Append(" m\n");
            }
            Section(report, "PEQUENOS DE MAIS (procurar em vez de apontar)", tiny, lines);

            Debug.Log(string.Format(
                "[Volumes] {0} interactaveis: {1} sem volume, {2} GameObjects com verbos " +
                "empilhados, {3} engolidos, {4} pequenos de mais.\n\n{5}",
                targets.Count, noVolume, stacked, engulfed, tiny, report));
        }

        private static List<Target> Collect()
        {
            var targets = new List<Target>();
            foreach (var behaviour in Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (!(behaviour is IPlayerInteractable)) continue;
                if (behaviour.gameObject.scene.IsValid() == false) continue;

                var t = new Target { Behaviour = behaviour, Owner = behaviour.gameObject };

                // Os limites de um colisor desligado, ou num GameObject inactivo, sao
                // zero — a fisica ainda nao os construiu. Ler `bounds` aqui dava uma
                // caixa vazia na origem e metade da cena aparecia como "engolida" por
                // ela. As medidas saem da forma e da escala, que existem sempre.
                foreach (var collider in behaviour.GetComponentsInChildren<Collider>(true))
                {
                    if (!collider.enabled) continue;
                    Bounds b;
                    if (!TryMeasure(collider, out b)) continue;

                    t.Colliders.Add(collider);
                    if (!t.HasVolume) { t.Volume = b; t.HasVolume = true; }
                    else t.Volume.Encapsulate(b);
                }

                targets.Add(t);
            }
            return targets;
        }

        /// <summary>
        /// Os limites de um colisor sem depender de a fisica os ter construido.
        ///
        /// So sabe ler as tres formas que o projecto usa. Uma malha concava nao tem
        /// resposta util a esta pergunta e fica de fora de proposito.
        /// </summary>
        private static bool TryMeasure(Collider collider, out Bounds bounds)
        {
            Transform t = collider.transform;
            Vector3 scale = t.lossyScale;

            var box = collider as BoxCollider;
            if (box != null)
            {
                Vector3 size = Vector3.Scale(box.size, Abs(scale));
                bounds = new Bounds(t.TransformPoint(box.center), size);
                return true;
            }

            var sphere = collider as SphereCollider;
            if (sphere != null)
            {
                float r = sphere.radius * Mathf.Max(Mathf.Abs(scale.x),
                    Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                bounds = new Bounds(t.TransformPoint(sphere.center), Vector3.one * (r * 2f));
                return true;
            }

            var capsule = collider as CapsuleCollider;
            if (capsule != null)
            {
                float r = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                float h = Mathf.Max(capsule.height * Mathf.Abs(scale.y), r * 2f);
                bounds = new Bounds(t.TransformPoint(capsule.center), new Vector3(r * 2f, h, r * 2f));
                return true;
            }

            var mesh = collider as MeshCollider;
            if (mesh != null && mesh.sharedMesh != null)
            {
                Bounds local = mesh.sharedMesh.bounds;
                bounds = new Bounds(t.TransformPoint(local.center), Vector3.Scale(local.size, Abs(scale)));
                return true;
            }

            bounds = default(Bounds);
            return false;
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        private static float Volume(Bounds b)
        {
            return Mathf.Max(0f, b.size.x) * Mathf.Max(0f, b.size.y) * Mathf.Max(0f, b.size.z);
        }

        private static float Overlap(Bounds a, Bounds b)
        {
            float x = Mathf.Min(a.max.x, b.max.x) - Mathf.Max(a.min.x, b.min.x);
            float y = Mathf.Min(a.max.y, b.max.y) - Mathf.Max(a.min.y, b.min.y);
            float z = Mathf.Min(a.max.z, b.max.z) - Mathf.Max(a.min.z, b.min.z);
            if (x <= 0f || y <= 0f || z <= 0f) return 0f;
            return x * y * z;
        }

        private static bool IsRelated(GameObject a, GameObject b)
        {
            return a.transform.IsChildOf(b.transform) || b.transform.IsChildOf(a.transform);
        }

        private static string Describe(Target t)
        {
            return t.Owner.name + " [" + t.Behaviour.GetType().Name + "]" +
                   (t.Owner.activeInHierarchy ? "" : " (inactivo)");
        }

        private static void Section(StringBuilder report, string title, int count, StringBuilder body)
        {
            report.Append("== ").Append(title).Append(": ").Append(count).Append(" ==\n");
            if (body.Length == 0) report.Append("   nada\n");
            else report.Append(body);
            report.Append('\n');
        }
    }
}
