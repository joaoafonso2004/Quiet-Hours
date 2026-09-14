using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Pungent.Interaction;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Mede se a mira acerta no que esta a ser apontado, e devolve um numero.
    ///
    /// Existe porque "nao consigo apanhar a caixa" e um sintoma que ninguem
    /// consegue reproduzir de propósito: acontece nalgumas direccoes e nao noutras,
    /// e a jogar parece intermitencia. A primeira vez que isto foi medido, o
    /// resultado foi **11 de 24 direccoes a acertar** — deixou de ser intermitencia
    /// e passou a ser um defeito com endereco.
    ///
    /// Media tres caixas. Agora mede a casa inteira, porque a queixa tambem e da
    /// casa inteira: poe o jogador a volta de **cada alvo com prompt vivo**, de oito
    /// direccoes e duas distancias, com a camara apontada ao centro do alvo, e
    /// pergunta ao proprio `PlayerInteractor` o que e que ganhava o clique.
    ///
    /// Apontada ao centro, de proposito. Nao mede tolerancia — mede o caso em que o
    /// jogador nao tem duvida nenhuma sobre o que esta a olhar. O que falha aqui
    /// falha com o jogador a fazer tudo bem.
    ///
    /// Alvos tapados por uma parede a partir de uma dada direccao nao contam: a
    /// medicao verifica a linha de vista antes de pontuar, senao metade dos
    /// "falhados" seriam o jogador dentro do frigorifico.
    ///
    /// **So em Play**, e mexe mesmo no jogador: corre isto num dia acabado de
    /// comecar e nao a meio de uma jogada a serio.
    /// </summary>
    internal static class AimProbe
    {
        private static readonly float[] Distances = { 0.9f, 1.5f };
        private const int AzimuthStep = 45;

        [MenuItem("Pungent/Debug/Medir a mira", false, 201)]
        internal static void Measure()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Mira] So em Play: isto move o jogador e le a fisica.");
                return;
            }

            var interactor = Object.FindObjectOfType<PlayerInteractor>(true);
            var motor = Object.FindObjectOfType<Pungent.Player.PlayerMotor>(true);
            if (interactor == null || motor == null)
            {
                Debug.LogWarning("[Mira] Sem `PlayerInteractor` ou sem `PlayerMotor` nesta cena.");
                return;
            }

            var camera = interactor.GetComponentInChildren<Camera>(true);
            var body = motor.GetComponent<CharacterController>();
            var find = typeof(PlayerInteractor).GetMethod("FindFocusedInteractable",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (camera == null || find == null)
            {
                Debug.LogWarning("[Mira] Sem camara, ou o `FindFocusedInteractable` mudou de nome.");
                return;
            }

            // Onde ele estava. Uma ferramenta de medicao que deixa o jogador noutro
            // sitio estraga a jogada que estava a ser medida.
            Vector3 wasAt = motor.transform.position;
            Quaternion wasLooking = camera.transform.rotation;
            float floor = wasAt.y;

            List<MonoBehaviour> live = LiveTargets();
            var report = new StringBuilder();
            var thieves = new Dictionary<string, int>();
            int hits = 0, total = 0, skipped = 0, yielded = 0, throughWall = 0;
            var walls = new StringBuilder();

            foreach (MonoBehaviour target in live)
            {
                Vector3 aimAt;
                if (!AimPointOf(target, out aimAt)) continue;

                int good = 0, tried = 0;
                var wrong = new StringBuilder();

                for (int azimuth = 0; azimuth < 360; azimuth += AzimuthStep)
                {
                    Vector3 away = Quaternion.Euler(0f, azimuth, 0f) * Vector3.forward;

                    foreach (float distance in Distances)
                    {
                        Vector3 stand = aimAt + away * distance;
                        stand.y = floor;

                        if (body != null) body.enabled = false;
                        motor.transform.position = stand;
                        Physics.SyncTransforms();

                        camera.transform.rotation =
                            Quaternion.LookRotation(aimAt - camera.transform.position, Vector3.up);

                        // De onde nao se ve, nao se aponta — mas essas posicoes nao
                        // se deitam fora: sao **a medicao da parede**. Se o alvo
                        // continua a ganhar o clique com uma parede pelo meio, isso
                        // e um numero e nao uma impressao.
                        if (!InSight(camera.transform.position, aimAt, target))
                        {
                            skipped++;
                            var behind = find.Invoke(interactor, null) as IPlayerInteractable;
                            if (behind == (IPlayerInteractable)target)
                            {
                                throughWall++;
                                walls.Append("   ").Append(target.gameObject.name)
                                     .Append("  de ").Append(azimuth).Append("graus a ")
                                     .Append(distance.ToString("0.0")).Append(" m\n");
                            }
                            continue;
                        }

                        var found = find.Invoke(interactor, null) as IPlayerInteractable;
                        var behaviour = found as MonoBehaviour;

                        // Uma frase sobre um movel perder para um verbo a serio nao e
                        // uma falha da mira — e a regra. Contar isso como erro fazia
                        // a pontuacao piorar de cada vez que uma tarefa era ligada em
                        // cima de um movel que tem uma linha para dizer.
                        if (found != (IPlayerInteractable)target &&
                            target is IAmbientInteractable && !(found is IAmbientInteractable) &&
                            found != null)
                        {
                            yielded++;
                            continue;
                        }

                        tried++;
                        total++;
                        if (found == (IPlayerInteractable)target) { good++; hits++; }
                        else
                        {
                            string got = behaviour != null
                                ? behaviour.gameObject.name + "[" + behaviour.GetType().Name + "]"
                                : "nada";
                            wrong.Append(azimuth).Append("/").Append(distance.ToString("0.0"))
                                 .Append("m->").Append(got).Append("  ");

                            int seen;
                            thieves.TryGetValue(got, out seen);
                            thieves[got] = seen + 1;
                        }
                    }
                }

                if (tried == 0) continue;

                report.Append(good == tried ? "  ok  " : " FALHA ")
                      .Append(good).Append('/').Append(tried).Append("   ")
                      .Append(target.gameObject.name).Append(" [").Append(target.GetType().Name).Append(']')
                      .Append('\n');
                if (wrong.Length > 0) report.Append("        ").Append(wrong).Append('\n');
            }

            if (body != null) body.enabled = false;
            motor.transform.position = wasAt;
            camera.transform.rotation = wasLooking;
            Physics.SyncTransforms();
            if (body != null) body.enabled = true;

            var ranking = new List<KeyValuePair<string, int>>(thieves);
            ranking.Sort((x, y) => y.Value.CompareTo(x.Value));
            var stolen = new StringBuilder();
            for (int i = 0; i < ranking.Count && i < 8; i++)
                stolen.Append("   ").Append(ranking[i].Value).Append("x  ").Append(ranking[i].Key).Append('\n');

            float score = total > 0 ? 100f * hits / total : 0f;
            string full = string.Format(
                "[Mira] {0}/{1} ({2:0}%) das miras acertam no alvo apontado.  " +
                "{3} alvos vivos, {4} vezes que um movel cedeu a um verbo.\n" +
                "ATRAVES DA PAREDE: {5} de {6} posicoes tapadas.\n{7}\n{8}\nQUEM ROUBA A MIRA:\n{9}",
                hits, total, score, live.Count, yielded, throughWall, skipped, walls, report, stolen);

            // O relatorio inteiro vai para ficheiro. A consola do Unity guarda a
            // primeira linha e o resto so se ve com o rato — e este relatorio existe
            // para ser lido de uma vez e comparado com o da corrida anterior.
            string path = System.IO.Path.Combine(Application.dataPath, "../Temp/mira.txt");
            System.IO.File.WriteAllText(System.IO.Path.GetFullPath(path), full);

            Debug.Log(string.Format(
                "[Mira] {0}/{1} ({2:0}%) acertam.  Atraves da parede: {3} de {4}.  " +
                "Relatorio inteiro em Temp/mira.txt",
                hits, total, score, throughWall, skipped));
        }

        /// <summary>Os interactaveis que **neste momento** teem alguma coisa a oferecer.</summary>
        private static List<MonoBehaviour> LiveTargets()
        {
            var live = new List<MonoBehaviour>();
            foreach (var behaviour in Object.FindObjectsOfType<MonoBehaviour>(false))
            {
                var interactable = behaviour as IPlayerInteractable;
                if (interactable == null) continue;
                if (!behaviour.gameObject.activeInHierarchy) continue;
                if (string.IsNullOrEmpty(interactable.Prompt)) continue;
                live.Add(behaviour);
            }
            return live;
        }

        /// <summary>
        /// Para onde o jogador olha quando quer este objecto: **o que esta
        /// desenhado**.
        ///
        /// A primeira versao disto media o centro dos colisores, e foi por isso que
        /// deu 94% na primeira passagem. O centro dos colisores de uma caixa do
        /// prologo esta a 87 cm do chao — dentro da coluna invisivel de ajuda de
        /// mira, e nao no cartao a 12 cm que o jogador ve. A medicao estava a
        /// validar a ajuda de mira contra ela propria.
        ///
        /// Um alvo sem nada desenhado — um volume de tarefa em cima de um movel —
        /// cai no colisor, que ai e mesmo a unica descricao que ha dele.
        /// </summary>
        private static bool AimPointOf(MonoBehaviour target, out Vector3 point)
        {
            Bounds bounds = default(Bounds);
            bool any = false;

            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!any) { bounds = renderer.bounds; any = true; }
                else bounds.Encapsulate(renderer.bounds);
            }

            if (!any)
                foreach (var collider in target.GetComponentsInChildren<Collider>(true))
                {
                    if (!collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                    if (!any) { bounds = collider.bounds; any = true; }
                    else bounds.Encapsulate(collider.bounds);
                }

            point = bounds.center;
            return any;
        }

        /// <summary>
        /// Se ha parede entre o olho e o alvo.
        ///
        /// As excepcoes sao **as mesmas** que o `PlayerInteractor.Blocked` usa, e tem
        /// de ser: uma medicao que conte como parede o que o jogo nao conta nao esta
        /// a medir o jogo. Sem elas, contava 53 "atraves da parede" que eram todas o
        /// mesmo caso legitimo — um ponto de tarefa vive dentro do movel a que
        /// pertence, e o sofa nao e a parede do seu proprio "sentar-se".
        /// </summary>
        private static bool InSight(Vector3 eye, Vector3 aimAt, MonoBehaviour target)
        {
            Vector3 delta = aimAt - eye;
            float length = delta.magnitude;
            if (length < 0.10f) return true;

            var hits = Physics.RaycastAll(eye, delta / length, length - 0.05f, ~0,
                QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                Collider blocker = hit.collider;
                if (blocker == null) continue;
                if (blocker.transform.IsChildOf(target.transform)) continue;
                if (target.transform.IsChildOf(blocker.transform)) continue;
                if (blocker.GetComponentInParent<Pungent.Player.PlayerMotor>() != null) continue;
                if (IsSilentInteractable(blocker)) continue;
                return false;
            }
            return true;
        }

        /// <summary>Um interactavel sem nada a oferecer agora nao tapa — nem no jogo.</summary>
        private static bool IsSilentInteractable(Collider collider)
        {
            bool any = false;
            foreach (var behaviour in collider.GetComponentsInParent<MonoBehaviour>(true))
            {
                var interactable = behaviour as IPlayerInteractable;
                if (interactable == null) continue;
                any = true;
                if (!string.IsNullOrEmpty(interactable.Prompt)) return false;
            }
            return any;
        }
    }
}
