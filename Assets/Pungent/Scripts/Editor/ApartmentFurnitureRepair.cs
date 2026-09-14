using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Devolve a malha aos moveis que a perderam.
    ///
    /// ---
    ///
    /// **O sintoma.** Quinze moveis do apartamento sao transforms vazios. O colisor
    /// esta la, o `FlavourInteractable` esta la, os sinalizadores de estatico estao
    /// la, as tarefas domesticas estao penduradas neles — **so o filho `_Mesh` e que
    /// desapareceu.** A poltrona da sala, as duas cadeiras da cozinha, a mesa da
    /// cozinha, as duas estantes, o candeeiro de pe, tres tapetes, duas prateleiras e
    /// tres vasos.
    ///
    /// Nao e falta de arte: os treze modelos estao todos em
    /// `ThirdParty/Quaternius/UltimateHouseInterior/FBX/`, e o `ApartmentV2Dressing`
    /// sabe o nome e a escala de cada um. Alguma passagem levou os filhos e deixou os
    /// pais.
    ///
    /// **Custa mais do que parece.** A `CHG_Armchair` do `DailyPressureWiring` roda a
    /// poltrona da sala quando o jogador nao esta a ver — e um dos tres sustos do Dia
    /// 2, e ha meses que roda um objecto invisivel. Uma mudanca que nao se ve nao e
    /// uma mudanca fraca: e uma mudanca que nunca aconteceu, e gasta a unica
    /// oportunidade que aquele momento tinha.
    ///
    /// ---
    ///
    /// **Porque nao chega correr o `Dress Apartment V2` outra vez.**
    ///
    /// Aquela ferramenta comeca por destruir o `ART_PASS_V2` inteiro e reconstrui-lo.
    /// Os moveis voltam com malha — e levam atras tudo o que foi montado por cima
    /// deles desde entao: os pontos quentes `TASK_*` do `DailyPressureWiring` sao
    /// filhos dos moveis, os `WitnessedCue` da passagem de inquietacao guardam
    /// referencias directas para estes transforms, e as referencias serializadas
    /// passam a apontar para objectos destruidos. Recuperar-se-ia a poltrona e
    /// perdiam-se as dez tarefas domesticas e metade dos sustos.
    ///
    /// Isto so acrescenta o que falta. Nao destroi nada, nao mexe em colisores, nao
    /// mexe em componentes, e num movel que ja tenha malha nao toca. Re-executavel
    /// por construcao: a segunda passagem nao encontra nada para fazer.
    ///
    /// A tabela e copia da do `ApartmentV2Dressing` — modelo e escala, movel a movel.
    /// Duplicada de propósito e nao partilhada: se alguem la mudar uma escala, o pior
    /// que acontece aqui e um movel reparado com o tamanho antigo, e nao esta
    /// ferramenta a reconstruir a casa com uma tabela de que ninguem se lembra.
    /// </summary>
    internal static class ApartmentFurnitureRepair
    {
        private const string ModelFolder = "Assets/ThirdParty/Quaternius/UltimateHouseInterior/FBX/";

        /// <summary>Movel, modelo, escala. Os mesmos numeros do dressing.</summary>
        private static readonly (string Prop, string Model, float Scale)[] Missing =
        {
            // The other entries that used to be here were deliberately removed
            // by the owner on 2026-08-11. A repair pass must not resurrect them.
            ("Kit_Plant",        "Houseplant_5",     0.70f),
            ("Balcony_Plant_A",  "Houseplant_4",     0.80f),
            ("Balcony_Plant_B",  "Houseplant_7",     0.70f),
        };

        [MenuItem("Pungent/Blockout/Repair Missing Furniture Meshes", false, 12)]
        internal static void Repair()
        {
            if (BuildGuard.Blocked("RepairFurniture")) return;

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Apartment"))
            {
                Debug.LogError("[Reparar] Abrir a `Apartment_Blockout_V2` primeiro.");
                return;
            }

            var filled = new List<string>();
            var already = new List<string>();
            var lost = new List<string>();

            foreach (var (prop, model, scale) in Missing)
            {
                var wrapper = FindAnywhere(prop);
                if (wrapper == null) { lost.Add(prop + " (movel nao existe na cena)"); continue; }

                // Ja tem malha? Nao se toca. E o que torna isto re-executavel sem
                // condicoes escritas em lado nenhum.
                if (wrapper.GetComponentInChildren<Renderer>(true) != null)
                {
                    already.Add(prop);
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + model + ".fbx");
                if (asset == null) { lost.Add(prop + " (sem `" + model + ".fbx`)"); continue; }

                var mesh = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                mesh.name = prop + "_Mesh";
                mesh.transform.SetParent(wrapper.transform, false);

                // Os tres numeros que o `Place` do dressing aplica, e pela mesma razao:
                // o pack vem do Blender com Z para cima, e o wrapper e que carrega a
                // posicao e o `yaw` — a malha nunca leva rotacao propria em Y.
                mesh.transform.localPosition = Vector3.zero;
                mesh.transform.localRotation = Quaternion.Euler(270f, 0f, 0f);
                mesh.transform.localScale = Vector3.one * scale;

                // Os `MeshCollider` importados sao caros e o movel ja tem o seu
                // `BoxCollider` — que sobreviveu, e e por isso que a mira e as tarefas
                // continuaram a funcionar em moveis invisiveis.
                foreach (var collider in mesh.GetComponentsInChildren<Collider>(true))
                    Object.DestroyImmediate(collider);

                GameObjectUtility.SetStaticEditorFlags(mesh,
                    StaticEditorFlags.ContributeGI |
                    StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.ReflectionProbeStatic);

                Undo.RegisterCreatedObjectUndo(mesh, "Repair furniture meshes");
                filled.Add(prop + " <- " + model + " (x" + scale.ToString("F2") + ")");
            }

            EditorSceneManager.MarkSceneDirty(scene);

            var report = new System.Text.StringBuilder(
                "[Reparar] " + filled.Count + " moveis recuperados, " + already.Count +
                " ja tinham malha.\n");
            foreach (var line in filled) report.Append("  + ").Append(line).Append('\n');
            foreach (var line in lost) report.Append("  ! ").Append(line).Append('\n');

            if (filled.Count > 0)
                report.Append("\n  A `CHG_Armchair` do Dia 2 volta a ter um objecto visivel para rodar.\n");

            Debug.Log(report.ToString());
        }

        private static GameObject FindAnywhere(string name)
        {
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
                if (t.name == name) return t.gameObject;
            return null;
        }
    }
}
