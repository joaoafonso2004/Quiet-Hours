using UnityEditor;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.NPC;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Poe o <see cref="StareEcho"/> no Rui.
    ///
    /// Vive no proprio `NPC_Rui` e nao num objecto solto: a fala e dele, a distancia
    /// mede-se a partir dele, e o `GlanceAtPlayer` que a acompanha e do
    /// `PrototypeNpcRoutine` que esta no mesmo objecto. Um componente que precisa de
    /// tres coisas do mesmo GameObject nao tem razao para viver noutro.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class StareEchoWiring
    {
        /// <summary>
        /// A janela e a conta, num sitio so.
        ///
        /// **Estavam escritas duas vezes** — uma na chamada e outra, a mao, dentro
        /// da linha de log — e as duas divergiram: o codigo passava
        /// `day_one_done -> climax` e a consola dizia `day_two -> day3_done`, que
        /// era a janela **errada** que esta ferramenta tinha acabado de corrigir. E
        /// a consola e precisamente onde se vai confirmar isto.
        ///
        /// Uma mensagem que repete um valor em vez de o relatar e uma copia sem
        /// dono, e uma copia sem dono envelhece calada.
        /// </summary>
        private const string OpensOn = "day_one_done";
        private const string ClosesOn = "climax";
        private const int Stares = 5;

        [MenuItem("Pungent/Narrativa/Wire Stare Echo", false, 63)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("StareEcho")) return;

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.name.StartsWith("Apartment"))
            {
                Debug.LogError("[Olhares] Abre a cena do apartamento primeiro.");
                return;
            }

            var rui = ApartmentV2WiringUtil.Find(scene, "NPC_Rui");
            if (rui == null) { Debug.LogError("[Olhares] Sem NPC_Rui."); return; }

            var echo = rui.GetComponent<StareEcho>();
            if (echo == null) echo = rui.AddComponent<StareEcho>();

            var dialogue = Object.FindObjectOfType<WorldDialogueController>(true);
            var voice = rui.GetComponentInChildren<NpcMumbleVoice>(true);

            if (dialogue == null)
                Debug.LogWarning("[Olhares] Sem WorldDialogueController — a fala fica por ligar.");
            if (voice == null)
                Debug.LogWarning("[Olhares] Sem NpcMumbleVoice no Rui — a frase sai legendada em silencio.");

            // **Da noite das 02:47 ate ao climax, e nao "o Dia 3".**
            //
            // Apanhado a testar: a janela `day_two -> day3_done` parecia certa no
            // papel e e quase toda uma janela em que **ele nao esta em casa**. O
            // `DayThreeStage` arranca no `day_two` e poe o Rui em (9.5, -4, 9.5),
            // fora da NavMesh, e so o traz de volta no `day3_ready` — a manha
            // inteira do Dia 3 assenta nessa ausencia. Uma fala a espera de o
            // encontrar tinha ali uns segundos no fim do dia para acontecer.
            //
            // Aberta no `day_one_done` ela pode cair na cozinha as 02:47, que e
            // onde o jogador o encontra parado e onde olhar para ele e a propria
            // mecanica da cena. Fechada no `climax` porque a partir dai ele deixa de
            // conversar e passa a procurar.
            echo.EditorConfigure(Stares, OpensOn, ClosesOn, dialogue, voice);

            EditorUtility.SetDirty(echo);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[Olhares] StareEcho no NPC_Rui: {Stares} olhares sustentados, janela " +
                      $"{OpensOn} -> {ClosesOn}, uma fala e mais nada. O encurtamento do " +
                      "stare-down e continuo e vive no PrototypeNpcRoutine.");
        }
    }
}
