using System.Collections;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Percorre o jogo do principio ao fim e diz onde e que ele para.
    ///
    /// ---
    ///
    /// **Isto responde a uma pergunta so, e nao a duas.**
    ///
    /// A pergunta e "a linha fecha?" — se cada capitulo entrega o evento que o
    /// seguinte espera, se as cenas carregam quando e preciso, e se ha um fim. Nao
    /// responde a "consegue-se jogar": os passos sao satisfeitos por codigo, e um
    /// passo que so se cumpre com uma interaccao que ninguem consegue apontar passa
    /// aqui na mesma. As duas perguntas sao precisas e esta e a que se pode
    /// automatizar.
    ///
    /// Cada passo e fechado pelo que ele proprio diz que precisa: os de evento por
    /// `Notify`, os de area por teletransporte do jogador, os de tempo por espera.
    /// Se um passo nao avancar em `StepTimeout`, para tudo e diz qual foi — e esse
    /// o resultado que interessa.
    ///
    /// **Estraga a jogada em curso**, e de propósito: teletransporta o jogador e
    /// levanta acontecimentos fora de ordem. Correr num arranque limpo.
    /// </summary>
    internal static class CriticalPathRun
    {
        private const float StepTimeout = 12f;
        private const float SceneLoadGrace = 3f;
        private const int MaxSteps = 80;

        [MenuItem("Pungent/Debug/Correr a linha toda", false, 220)]
        internal static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[Linha] So em Play.");
                return;
            }

            var runner = Object.FindObjectOfType<ChapterDirector>();
            if (runner == null) { Debug.LogWarning("[Linha] Sem ChapterDirector."); return; }
            runner.StartCoroutine(Walk());
        }

        private static IEnumerator Walk()
        {
            var log = new StringBuilder();
            int visited = 0;
            string lastChapter = null;
            int lastStep = -2;
            float stepStarted = Time.time;
            float wallClock = Time.time;

            log.AppendLine("passos percorridos:");

            while (visited < MaxSteps && Time.time - wallClock < 240f)
            {
                ChapterDirector director = ChapterDirector.Resolve();
                if (director == null)
                {
                    log.AppendLine("  o ChapterDirector desapareceu (troca de cena?)");
                    yield return new WaitForSeconds(SceneLoadGrace);
                    continue;
                }

                string chapter = director.CurrentChapter;
                int step = director.CurrentStep;

                if (chapter == null)
                {
                    log.AppendLine("  FIM: nenhum capitulo activo.");
                    break;
                }

                bool moved = chapter != lastChapter || step != lastStep;
                if (moved)
                {
                    log.AppendLine($"  {chapter}  passo {step}");
                    lastChapter = chapter;
                    lastStep = step;
                    stepStarted = Time.time;
                    visited++;
                }
                else if (Time.time - stepStarted > StepTimeout)
                {
                    log.AppendLine($"  PRESO em '{chapter}' passo {step} — o passo nao " +
                                   "fechou com o que ele proprio pede.");
                    break;
                }

                // Passo -1 quer dizer "o capitulo esta a abrir": o cartao preto com a
                // data esta no ecra e ainda nao ha passo nenhum. Nao ha nada a fazer
                // senao esperar por ele — e esperar **com pausa**, senao isto gira em
                // vazio e gasta o limite de passos antes de o cartao sair.
                ChapterDefinition.Step definition = CurrentStepOf(director);
                if (definition == null)
                {
                    yield return new WaitForSeconds(0.5f);
                    continue;
                }

                switch (definition.Ends)
                {
                    case ChapterDefinition.Completion.Event:
                        if (!string.IsNullOrWhiteSpace(definition.EventId))
                        {
                            log.AppendLine($"        levanta '{definition.EventId}'");
                            director.Notify(definition.EventId);
                        }
                        break;

                    case ChapterDefinition.Completion.ReachArea:
                        MovePlayer(definition.Place, log);
                        break;

                    case ChapterDefinition.Completion.Timer:
                        yield return new WaitForSeconds(definition.Seconds + 0.3f);
                        break;
                }

                // Espaco para o director reagir, e para uma cena carregar se o
                // evento que acabou de ser levantado a pedir.
                yield return new WaitForSeconds(0.4f);
            }

            if (visited >= MaxSteps) log.AppendLine("  parou no limite de passos.");

            // A consola do Unity guarda a primeira linha e esconde o resto; este
            // relatorio existe para ser lido de uma vez.
            string path = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "../Temp/linha.txt"));
            System.IO.File.WriteAllText(path, log.ToString());
            Debug.Log("[Linha] percorrida. Relatorio inteiro em Temp/linha.txt");
        }

        private static ChapterDefinition.Step CurrentStepOf(ChapterDirector director)
        {
            MethodInfo method = typeof(ChapterDirector).GetMethod("CurrentStepDefinition",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return method?.Invoke(director, null) as ChapterDefinition.Step;
        }

        private static void MovePlayer(Vector3 place, StringBuilder log)
        {
            var motor = Object.FindObjectOfType<Pungent.Player.PlayerMotor>();
            if (motor == null) { log.AppendLine("        sem jogador para mover"); return; }

            var body = motor.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            motor.transform.position = new Vector3(place.x, motor.transform.position.y, place.z);
            Physics.SyncTransforms();
            if (body != null) body.enabled = true;
            log.AppendLine($"        jogador posto em {place.ToString("F1")}");
        }
    }
}
