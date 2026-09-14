using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Compara a cena com o que os construtores dizem que ela devia ser, e escreve
    /// o resultado como C# pronto a colar.
    ///
    /// A razão de existir: quase tudo nesta cena é gerado por menus em
    /// `Pungent/Blockout/...` e `Tools/Pungent/...`, e voltar a correr qualquer um
    /// deles apaga o root e reconstrói-o da tabela que está no código. Quem arrastar
    /// um objecto à mão no editor perde a alteração no dia em que alguém carregar
    /// no botão outra vez — e não fica a saber porquê.
    ///
    /// A cena está serializada em binário, portanto não há diff de git que ajude.
    /// Esta ferramenta é o substituto: diz o que mudou e dá o texto para fixar a
    /// mudança no sítio certo.
    /// </summary>
    internal static class SceneStateCapture
    {
        [MenuItem("Tools/Pungent/Capture Scene Drift")]
        internal static void Capture()
        {
            var report = new StringBuilder();
            report.AppendLine("=== DERIVA ENTRE A CENA E O CÓDIGO ===");
            report.AppendLine($"cena: {EditorSceneManager.GetActiveScene().name}");
            report.AppendLine();

            CaptureMess(report);
            CaptureLights(report);
            CaptureSwitches(report);

            report.AppendLine();
            report.AppendLine("Nada listado acima = a cena e o código dizem o mesmo.");
            Debug.Log(report.ToString());
        }

        private static void CaptureMess(StringBuilder report)
        {
            var root = GameObject.Find("MESS_V2");
            report.AppendLine("--- MESS_V2 (MessScatter.Pieces) ---");
            if (root == null) { report.AppendLine("  root ausente."); return; }

            report.AppendLine($"  {root.transform.childCount} objectos na cena.");
            report.AppendLine("  Tabela equivalente ao estado actual:");
            foreach (Transform child in root.transform)
            {
                Bounds bounds = Encapsulate(child);
                Vector3 euler = child.eulerAngles;
                bool upright = Mathf.Abs(Mathf.DeltaAngle(euler.x, -90f)) < 20f;
                float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));

                report.AppendLine(
                    $"    new Piece {{ Prefab = \"<pasta>/{child.name}\", " +
                    $"Position = new Vector3({bounds.center.x:F2}f, {bounds.min.y:F2}f, {bounds.center.z:F2}f), " +
                    $"Yaw = {euler.y:F0}f, {(upright ? "Upright = true, " : "")}RealSize = {longest:F3}f }},");
            }
        }

        private static void CaptureLights(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("--- Luzes (ApartmentV2LightingWiring) ---");
            var lighting = GameObject.Find("LIGHTING");
            if (lighting == null) { report.AppendLine("  root ausente."); return; }

            foreach (Transform child in lighting.transform)
            {
                var light = child.GetComponent<Light>();
                if (light == null) continue;
                Vector3 p = child.position;
                report.AppendLine($"    {child.name,-22} new Vector3({p.x:F2}f, {p.y:F2}f, {p.z:F2}f)  " +
                                  $"on={light.enabled} int={light.intensity:F2} range={light.range:F1}");
            }
        }

        private static void CaptureSwitches(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("--- Interruptores (ApartmentV2LightingWiring.Switches) ---");
            var root = GameObject.Find("LIGHT_SWITCHES");
            if (root == null) { report.AppendLine("  root ausente."); return; }

            foreach (Transform child in root.transform)
            {
                var component = child.GetComponent<Pungent.Interaction.LightSwitchInteractable>();
                if (component == null) continue;

                var so = new SerializedObject(component);
                var array = so.FindProperty("lights");
                var names = new List<string>();
                for (int i = 0; i < array.arraySize; i++)
                {
                    var value = array.GetArrayElementAtIndex(i).objectReferenceValue;
                    names.Add(value != null ? $"\"{value.name}\"" : "null");
                }

                Vector3 p = child.position;
                report.AppendLine(
                    $"    new SwitchDef {{ Name = \"{child.name}\", " +
                    $"RoomLabel = \"{so.FindProperty("roomName").stringValue}\", " +
                    $"Position = new Vector3({p.x:F2}f, {p.y:F2}f, {p.z:F2}f), " +
                    $"Yaw = {child.eulerAngles.y:F0}f, Lights = new[] {{ {string.Join(", ", names)} }} }},");

                // A placa e a tecla devem estar centradas no holder. Foi assim que a
                // placa do interruptor do Rui ficou a flutuar no vao da porta: o
                // collider estava certo e a malha tinha um offset local de 0.31.
                foreach (Transform part in child)
                    if (Mathf.Abs(part.localPosition.x) > 0.01f || Mathf.Abs(part.localPosition.y) > 0.01f)
                        report.AppendLine($"      AVISO: '{part.name}' com offset local " +
                                          $"({part.localPosition.x:F2}, {part.localPosition.y:F2}) — malha fora do collider.");
            }
        }

        private static Bounds Encapsulate(Transform target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(target.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
