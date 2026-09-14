using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pungent.EditorTools
{
    /// <summary>Room tones por divisao e a regra global de alcance do audio 3D.</summary>
    internal static class ApartmentV2AudioWiring
    {
        /// <summary>
        /// Room tone por divisao (Tarefa 3 do plano). Gerado em codigo, sem clips:
        /// um apartamento em silencio absoluto as 02:47 le-se como cenario vazio.
        /// </summary>
        internal static void BuildRoomTones(Scene scene)
        {
            var existing = ApartmentV2WiringUtil.Find(scene, "ROOM_TONES");
            if (existing != null) Object.DestroyImmediate(existing);

            var root = new GameObject("ROOM_TONES");
            Undo.RegisterCreatedObjectUndo(root, "Build room tones");

            var tones = new (string Name, Vector3 Position, Pungent.Audio.ProceduralRoomTone.Character Kind,
                             float Volume, float Max)[]
            {
                ("Tone_Bedroom_Tomas", new Vector3(-5.20f, 1.4f, -2.90f),
                    Pungent.Audio.ProceduralRoomTone.Character.Bedroom, 0.070f, 5.5f),
                ("Tone_Bedroom_Rui", new Vector3(-1.75f, 1.4f, -2.90f),
                    Pungent.Audio.ProceduralRoomTone.Character.Bedroom, 0.055f, 5.0f),
                ("Tone_Corridor_West", new Vector3(-4.60f, 1.6f, -0.05f),
                    Pungent.Audio.ProceduralRoomTone.Character.Corridor, 0.075f, 6.0f),
                ("Tone_Corridor_East", new Vector3(0.60f, 1.6f, -0.05f),
                    Pungent.Audio.ProceduralRoomTone.Character.Corridor, 0.075f, 6.0f),
                ("Tone_Living", new Vector3(2.90f, 1.5f, 2.85f),
                    Pungent.Audio.ProceduralRoomTone.Character.Living, 0.065f, 7.0f),
                ("Tone_Kitchen", new Vector3(-5.00f, 1.5f, 3.20f),
                    Pungent.Audio.ProceduralRoomTone.Character.Kitchen, 0.075f, 5.5f),
                ("Tone_Bathroom", new Vector3(1.15f, 1.5f, -1.90f),
                    Pungent.Audio.ProceduralRoomTone.Character.Bathroom, 0.065f, 4.5f),
                ("Tone_Hall", new Vector3(4.10f, 1.5f, -1.80f),
                    Pungent.Audio.ProceduralRoomTone.Character.Corridor, 0.060f, 5.5f),
            };

            int seed = 1;
            foreach (var tone in tones)
            {
                var go = new GameObject(tone.Name);
                go.transform.SetParent(root.transform, false);
                go.transform.position = tone.Position;

                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = true;

                var component = go.AddComponent<Pungent.Audio.ProceduralRoomTone>();
                var so = new SerializedObject(component);
                so.FindProperty("character").enumValueIndex = (int)tone.Kind;
                so.FindProperty("volume").floatValue = tone.Volume;
                so.FindProperty("minDistance").floatValue = 2.0f;
                so.FindProperty("maxDistance").floatValue = tone.Max;
                so.FindProperty("seed").intValue = seed++;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Regra global de alcance: as fontes de ambiente so se devem ouvir quando o
        /// jogador esta relativamente perto. O frigorifico ouvia-se do apartamento
        /// inteiro. Fontes marcadas como ruidosas ficam de fora.
        /// </summary>
        internal static void TuneAmbientSources()
        {
            // Fontes que sao grandes de proposito e nao devem ser encolhidas.
            // A cidade e o caso obvio: esta seis metros abaixo da varanda, a fazer
            // de rua, e com o alcance de electrodomestico ficava inaudivel — que e
            // exactamente o que acontecia antes de estar nesta lista.
            string[] loudByDesign = { "PhoneRing", "Alarm", "Knock", "Crash", "City" };

            foreach (var source in Object.FindObjectsOfType<AudioSource>(true))
            {
                bool loud = false;
                foreach (var name in loudByDesign)
                    if (source.name.Contains(name)) { loud = true; break; }
                if (loud) continue;

                // Os room tones configuram-se a si proprios em runtime; mexer-lhes
                // aqui so criava duas fontes de verdade.
                if (source.name.StartsWith("Tone_")) continue;

                source.spatialBlend = 1f;                       // totalmente 3D
                source.rolloffMode = AudioRolloffMode.Linear;   // corta mesmo a distancia
                source.dopplerLevel = 0f;

                if (source.name.StartsWith("AMB_"))
                {
                    // Electrodomesticos: presenca so na propria divisao.
                    source.minDistance = 0.6f;
                    source.maxDistance = 4.0f;
                    source.volume = Mathf.Min(source.volume, 0.30f);
                }
                else
                {
                    source.minDistance = Mathf.Min(source.minDistance, 1.0f);
                    source.maxDistance = Mathf.Min(source.maxDistance, 8.0f);
                }

                EditorUtility.SetDirty(source);
            }
        }
    }
}
