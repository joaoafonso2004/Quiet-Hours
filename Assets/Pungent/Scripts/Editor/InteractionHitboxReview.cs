using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Interaction;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Poe os volumes de interaccao da cena aberta a obedecer a uma regra so.
    ///
    /// **Um ponto de interaccao sem nada desenhado nao pode ser solido.**
    ///
    /// Estes objectos — pontos de tarefa, sitios que levantam acontecimentos,
    /// destinos de arrumacao — sao marcas invisiveis no ar. Solidos, sao tres
    /// coisas ao mesmo tempo, e as tres mas: uma parede invisivel onde o jogador
    /// bate sem perceber, um empurrao na fisica do que lhes toque, e uma cortina
    /// entre a mira e o que esta atras. Medido: o cubo de 90 cm do `EVT_Day3Ready`
    /// esta em cima do suporte das chaves da entrada, e era ele que tapava as
    /// **proprias chaves** a quem vinha do corredor.
    ///
    /// O que tem malha fica como esta: um movel solido e solido, e tapar o que esta
    /// atras dele e o que se espera de um movel.
    ///
    /// As ferramentas de wiring ja escrevem `isTrigger = true`. Isto e para a cena
    /// que ja foi construida com a versao anterior delas — nao vale a pena voltar a
    /// correr o climax inteiro para trocar um booleano.
    ///
    /// Re-executavel, e diz sempre o que mudou.
    /// </summary>
    internal static class InteractionHitboxReview
    {
        [MenuItem("Pungent/Blockout/Review Interaction Hitboxes", false, 59)]
        internal static void Run()
        {
            var report = new StringBuilder();
            int reviewed = 0, freed = 0;

            foreach (var behaviour in Object.FindObjectsOfType<MonoBehaviour>(true))
            {
                if (!(behaviour is IPlayerInteractable)) continue;
                if (!behaviour.gameObject.scene.IsValid()) continue;
                reviewed++;

                if (!IsHotspot(behaviour)) continue;

                // Alguma coisa desenhada neste objecto, ou nos filhos dele? Se sim,
                // e um objecto do mundo e nao uma marca no ar.
                if (behaviour.GetComponentsInChildren<Renderer>(true).Length > 0) continue;

                foreach (var collider in behaviour.GetComponents<Collider>())
                {
                    if (collider.isTrigger) continue;

                    Undo.RecordObject(collider, "Review Interaction Hitboxes");
                    collider.isTrigger = true;
                    EditorUtility.SetDirty(collider);
                    freed++;

                    report.Append("   ").Append(behaviour.gameObject.name)
                          .Append(" [").Append(behaviour.GetType().Name)
                          .Append("] passou a trigger\n");
                }
            }

            // Os interruptores sao o alvo mais pequeno da casa que ainda e para
            // apontar. Medido a 1,1 m dao 5,8 graus de raio — acima do meio grau em
            // que apontar deixa de ser apontar, e por isso ficam como estao. A
            // medida vem daqui para nao voltar a ser afinada a olho.
            int switches = 0;
            foreach (var lightSwitch in Object.FindObjectsOfType<LightSwitchInteractable>(true))
            {
                var box = lightSwitch.GetComponent<BoxCollider>();
                if (box == null) continue;
                SetWorldSize(box, new Vector3(0.22f, 0.28f, 0.12f));
                switches++;
            }

            var panel = GameObject.Find("ElectricalPanel");
            if (panel != null)
            {
                var box = panel.GetComponent<BoxCollider>();
                if (box != null) SetWorldSize(box, new Vector3(0.42f, 0.52f, 0.18f));
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[HitboxReview] {reviewed} interactaveis revistos; {freed} volumes " +
                      $"invisiveis deixaram de ser paredes; {switches} interruptores medidos.\n" +
                      (report.Length > 0 ? report.ToString() : "   nada para libertar\n"));
        }

        /// <summary>
        /// Se este interactavel e uma **marca no ar** e nao um objecto do mundo.
        ///
        /// A pergunta tinha comecado por ser "tem malha?", e isso estava errado por
        /// uma razao que so se ve a correr: metade dos moveis do apartamento ainda
        /// esta por vestir. O cadeirao da sala e um GameObject com um colisor e mais
        /// nada — igualzinho, na estrutura, a um ponto de tarefa. A primeira versao
        /// disto tirou-lhe a solidez e abriu um buraco na sala por onde se passava.
        ///
        /// Quem separa os dois nao e a forma, e o componente: um ponto de tarefa, um
        /// sitio que levanta um acontecimento ou um destino de arrumacao nunca teve
        /// corpo nenhum. Um `FlavourInteractable` esta sempre pousado em cima de uma
        /// coisa que tem — vestida ou por vestir.
        /// </summary>
        private static bool IsHotspot(MonoBehaviour behaviour)
        {
            return behaviour is Pungent.Narrative.ChapterEventRaiser
                || behaviour is Pungent.Narrative.StagedHomeTaskStep
                || behaviour is Pungent.Narrative.HomeTaskInteractable
                || behaviour is Pungent.Narrative.ClimaxExit
                || behaviour is Pungent.Narrative.ClimaxKeyShelf
                || behaviour is BoxContentsDestination;
        }

        private static void SetWorldSize(BoxCollider box, Vector3 desired)
        {
            Vector3 scale = box.transform.lossyScale;
            box.size = new Vector3(
                desired.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                desired.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
                desired.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        }
    }
}
