using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Interaction;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Liga o fecho do dia: trancar a porta do quarto e dormir.
    ///
    /// Separado do "Wire Apartment V2" de proposito. Aquele reconstroi meia cena e
    /// so deve correr quando se quer isso mesmo; isto toca em tres objectos e pode
    /// correr a qualquer momento. Re-executavel.
    /// </summary>
    public static class DayEndWiring
    {
        [MenuItem("Pungent/Blockout/Wire Day End (lock + sleep)", false, 31)]
        public static void Wire()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var log = new List<string>();

            var player = GameObject.Find("PlayerRoot");
            if (player == null) { Debug.LogError("[DayEnd] PlayerRoot nao encontrado."); return; }

            var bed = GameObject.Find("Bed_Tomas");
            if (bed == null) { Debug.LogError("[DayEnd] Bed_Tomas nao encontrado."); return; }

            var quest = Object.FindObjectOfType<OpeningQuestDirector>(true);
            var hud = player.GetComponent<PrototypeHUD>();
            var thoughts = player.GetComponent<PlayerThoughtDirector>();
            var door = FindDoor("Door_Bedroom_Tomas");
            var bedroomLight = GameObject.Find("Bedroom_Light");

            // --- PlayerSleep na cama ---
            var sleep = bed.GetComponent<PlayerSleep>();
            if (sleep == null) sleep = Undo.AddComponent<PlayerSleep>(bed.gameObject);

            // A cama e larga, mas muito baixa. Sem uma area de mira alta, olhar
            // naturalmente para a cama (em vez de para o colchao junto ao chao)
            // falhava a maior parte das inclinacoes medidas.
            var bedBody = bed.GetComponent<BoxCollider>();
            var sleepTarget = bed.transform.Find("SleepTarget");
            if (sleepTarget == null)
            {
                var targetObject = new GameObject("SleepTarget");
                Undo.RegisterCreatedObjectUndo(targetObject, "Add bed sleep target");
                sleepTarget = targetObject.transform;
                sleepTarget.SetParent(bed.transform, false);
            }
            var sleepCollider = sleepTarget.GetComponent<BoxCollider>();
            if (sleepCollider == null)
                sleepCollider = Undo.AddComponent<BoxCollider>(sleepTarget.gameObject);
            sleepCollider.isTrigger = true;
            sleepCollider.center = bedBody != null
                ? new Vector3(bedBody.center.x, 0.88f, bedBody.center.z)
                : new Vector3(0f, 0.88f, 0f);
            sleepCollider.size = bedBody != null
                ? new Vector3(bedBody.size.x, 1.76f, bedBody.size.z)
                : new Vector3(2.1f, 1.76f, 1.1f);

            var so = new SerializedObject(sleep);
            so.FindProperty("player").objectReferenceValue = player.transform;
            so.FindProperty("cameraPivot").objectReferenceValue = player.transform.Find("ViewYaw");

            var camera = player.GetComponentInChildren<Camera>(true);
            if (camera != null)
                so.FindProperty("tiltTransform").objectReferenceValue = camera.transform;

            so.FindProperty("thoughts").objectReferenceValue = thoughts;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("quest").objectReferenceValue = quest;
            so.FindProperty("bedroomDoor").objectReferenceValue = door;

            // O telemovel na mao desaparece ao deitar, como na abertura.
            var heldPhone = player.GetComponentInChildren<PhoneLightController>(true);
            if (heldPhone != null)
                so.FindProperty("heldPhone").objectReferenceValue = heldPhone.gameObject;

            // Deitado, o corpo assenta no centro da cama. A altura vem do jogador e
            // nao da cama: quem baixa e o pivo da camara, o corpo fica ao nivel do chao.
            var bedCollider = bed.GetComponentInChildren<Collider>(true);
            if (bedCollider != null)
            {
                Bounds b = bedCollider.bounds;
                var lying = new Vector3(b.center.x, player.transform.position.y, b.center.z);
                so.FindProperty("lyingPosition").vector3Value = lying;
                log.Add($"  deitado em {lying:F2} (centro da cama)");
            }

            // Os mesmos componentes que a abertura a secretaria suspende.
            var suppressed = new List<Object>
            {
                player.GetComponent<Pungent.Player.PlayerMotor>(),
                player.GetComponent<PlayerInteractor>(),
                player.GetComponent<PrototypePhoneUI>()
            };
            suppressed.RemoveAll(c => c == null);
            var suppressedProperty = so.FindProperty("suppressed");
            suppressedProperty.arraySize = suppressed.Count;
            for (int i = 0; i < suppressed.Count; i++)
                suppressedProperty.GetArrayElementAtIndex(i).objectReferenceValue = suppressed[i];

            // Luzes que tem de estar apagadas: as do quarto do Tomas.
            var mustBeOff = new List<Light>();
            if (bedroomLight != null)
            {
                var light = bedroomLight.GetComponent<Light>();
                if (light != null) mustBeOff.Add(light);
            }
            var offProperty = so.FindProperty("lightsThatMustBeOff");
            offProperty.arraySize = mustBeOff.Count;
            for (int i = 0; i < mustBeOff.Count; i++)
                offProperty.GetArrayElementAtIndex(i).objectReferenceValue = mustBeOff[i];

            so.ApplyModifiedProperties();
            log.Add($"  PlayerSleep na cama: porta={(door != null ? door.name : "NENHUMA")}, " +
                    $"luzes a apagar={mustBeOff.Count}, suprimidos={suppressed.Count}; mira alta ligada");

            // --- a cadeia de objectivos ---
            if (quest != null)
            {
                var questSo = new SerializedObject(quest);
                questSo.FindProperty("bedroomDoor").objectReferenceValue = door;

                var smoke = FindSmokeTask();
                questSo.FindProperty("optionalTask").objectReferenceValue = smoke;
                questSo.ApplyModifiedProperties();
                log.Add($"  quest: porta ligada, tarefa opcional={(smoke != null ? smoke.name : "NENHUMA")}");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[DayEnd] Fecho do dia ligado.\n" + string.Join("\n", log));
        }

        private static DoorDragInteractable FindDoor(string name)
        {
            foreach (var door in Object.FindObjectsOfType<DoorDragInteractable>(true))
                if (door.gameObject.name == name) return door;
            return null;
        }

        private static HomeTaskInteractable FindSmokeTask()
        {
            foreach (var task in Object.FindObjectsOfType<HomeTaskInteractable>(true))
            {
                var so = new SerializedObject(task);
                if (so.FindProperty("id").stringValue == "smoke") return task;
            }
            return null;
        }
    }
}
