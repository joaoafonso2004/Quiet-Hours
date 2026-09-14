using UnityEditor;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Records the street cars placed manually by the owner on 2026-08-12.
    /// Validation never moves, replaces, or deletes manual scene work. Restore
    /// creates only an entry whose exact protected name is missing.
    /// </summary>
    internal static class RebootManualStreetCarsManifest
    {
        private const string RootPath = "MANUAL_STORY/STREET_CARS";
        private const string CarAssetPath =
            "Assets/Hatchback and Sedan/meshes/HATCHBACK/HATCHBACK_1988.FBX";
        private const string CarMaterialPath =
            "Assets/Hatchback and Sedan/meshes/HATCHBACK/materials/1988.mat";

        private readonly struct Entry
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly Vector3 Euler;
            public readonly Vector3 Scale;

            public Entry(string name, Vector3 position, Vector3 euler, Vector3 scale)
            {
                Name = name;
                Position = position;
                Euler = euler;
                Scale = scale;
            }
        }

        private static readonly Entry[] Cars =
        {
            new Entry("STREET_CAR_01", new Vector3(-25.660f, -23.900f, -22.420f), new Vector3(0f, 92.84595f, 0f), UniformScale()),
            new Entry("STREET_CAR_02", new Vector3(41.270f, -23.900f, -18.900f), new Vector3(0f, 271.032959f, 0f), UniformScale()),
            new Entry("STREET_CAR_03", new Vector3(-6.74963236f, -23.900f, -18.77233f), new Vector3(0f, 271.032959f, 0f), UniformScale()),
            new Entry("STREET_CAR_04", new Vector3(-45.820f, -23.900f, -18.070f), new Vector3(0f, 271.032959f, 0f), UniformScale()),
            new Entry("STREET_CAR_05", new Vector3(-25.020f, -23.900f, 12.800f), new Vector3(0f, 92.84595f, 0f), UniformScale()),
            new Entry("STREET_CAR_06", new Vector3(41.910f, -23.900f, 16.320f), new Vector3(0f, 271.032959f, 0f), UniformScale()),
            new Entry("STREET_CAR_07", new Vector3(-6.109633f, -23.900f, 16.44767f), new Vector3(0f, 271.032959f, 0f), UniformScale()),
            new Entry("STREET_CAR_08", new Vector3(-78.660f, -23.900f, 16.840f), new Vector3(0f, 271.032959f, 0f), UniformScale()),
            new Entry("STREET_CAR_09", new Vector3(-45.180f, -23.900f, 17.1500015f), new Vector3(0f, 271.032959f, 0f), UniformScale())
        };

        [MenuItem("Pungent/Reboot/Validate Manual Street Cars")]
        private static void Validate()
        {
            Transform root = FindRoot();
            if (root == null)
            {
                Debug.LogError("[RebootCars] Missing protected root " + RootPath + ".");
                return;
            }

            int errors = 0;
            if (AssetDatabase.LoadAssetAtPath<Material>(CarMaterialPath) == null)
            {
                Debug.LogError("[RebootCars] Missing manually edited car material: " +
                    CarMaterialPath + ".");
                errors++;
            }

            for (int i = 0; i < Cars.Length; i++)
            {
                Entry entry = Cars[i];
                Transform car = root.Find(entry.Name);
                if (car == null)
                {
                    Debug.LogError("[RebootCars] Missing " + entry.Name + ".");
                    errors++;
                    continue;
                }

                if (!Approximately(car.localPosition, entry.Position, 0.005f) ||
                    !ApproximatelyEuler(car.localEulerAngles, entry.Euler, 0.05f) ||
                    !Approximately(car.localScale, entry.Scale, 0.0005f))
                {
                    Debug.LogError("[RebootCars] " + entry.Name +
                        " moved since the manual manifest was recorded. Current local transform: " +
                        car.localPosition.ToString("F3") + " / " +
                        car.localEulerAngles.ToString("F2") + " / " +
                        car.localScale.ToString("F3") + ".", car);
                    errors++;
                }
            }

            if (errors == 0)
                Debug.Log("[RebootCars] All 9 manually placed street cars match the protected manifest.");
        }

        [MenuItem("Pungent/Reboot/Restore Missing Manual Street Cars")]
        private static void RestoreMissing()
        {
            Transform root = FindRoot();
            if (root == null)
            {
                GameObject manual = GameObject.Find("MANUAL_STORY");
                if (manual == null)
                {
                    Debug.LogError("[RebootCars] Missing MANUAL_STORY. Restore refuses " +
                        "to guess the protected hierarchy.");
                    return;
                }

                GameObject rootObject = new GameObject("STREET_CARS");
                Undo.RegisterCreatedObjectUndo(rootObject, "Restore manual street cars root");
                rootObject.transform.SetParent(manual.transform, false);
                root = rootObject.transform;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarAssetPath);
            if (prefab == null)
            {
                Debug.LogError("[RebootCars] Missing model asset: " + CarAssetPath + ".");
                return;
            }

            int restored = 0;
            for (int i = 0; i < Cars.Length; i++)
            {
                Entry entry = Cars[i];
                if (root.Find(entry.Name) != null) continue;

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab, root) as GameObject;
                if (instance == null) continue;

                Undo.RegisterCreatedObjectUndo(instance, "Restore missing manual street car");
                instance.name = entry.Name;
                instance.transform.localPosition = entry.Position;
                instance.transform.localEulerAngles = entry.Euler;
                instance.transform.localScale = entry.Scale;
                restored++;
            }

            if (restored > 0)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log("[RebootCars] Restored " + restored +
                " missing car(s). Existing manual cars were not changed.");
        }

        private static Transform FindRoot()
        {
            GameObject root = GameObject.Find(RootPath);
            return root != null ? root.transform : null;
        }

        private static Vector3 UniformScale() =>
            new Vector3(0.6166876f, 0.6166876f, 0.6166876f);

        private static bool Approximately(Vector3 a, Vector3 b, float tolerance) =>
            (a - b).sqrMagnitude <= tolerance * tolerance;

        private static bool ApproximatelyEuler(Vector3 a, Vector3 b, float tolerance) =>
            Mathf.Abs(Mathf.DeltaAngle(a.x, b.x)) <= tolerance &&
            Mathf.Abs(Mathf.DeltaAngle(a.y, b.y)) <= tolerance &&
            Mathf.Abs(Mathf.DeltaAngle(a.z, b.z)) <= tolerance;
    }
}
