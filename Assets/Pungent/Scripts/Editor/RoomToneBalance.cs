using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Audio;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Equilibra o leito de ruído do apartamento.
    ///
    /// O problema não era o volume de cada fonte: era haver oito com alcances de
    /// cinco a sete metros num apartamento de treze por dez. Em quase todo o lado
    /// ouviam-se três a cinco ao mesmo tempo, e por cima disso o buzz das lâmpadas
    /// e o frigorífico. Somados, saturavam.
    ///
    /// A correcção é encurtar o alcance de cada divisão — o room tone passa a ser
    /// local, como devia — e baixar os volumes.
    /// </summary>
    internal static class RoomToneBalance
    {
        [MenuItem("Tools/Pungent/Audio/Rebalance Room Tone")]
        internal static void Rebalance()
        {
            int count = 0;
            foreach (var tone in Object.FindObjectsOfType<ProceduralRoomTone>(true))
            {
                var so = new SerializedObject(tone);
                // **0,03 era correcção a dobrar, e o resultado foi silêncio.**
                //
                // Havia oito tons a somar-se e a saturar. A correcção certa foi a
                // de baixo — encurtar o alcance, para cada divisão só se ouvir
                // dentro dela. Isso resolveu a soma sozinho: em quase toda a casa
                // passou a ouvir-se um tom, não cinco. Baixar o volume por cima
                // disso foi aplicar duas vezes o mesmo remédio, e o segundo levou o
                // leito de ruído para 3% — medido a jogar, inaudível.
                //
                // Num jogo em que a tensão vem de a casa fazer barulho enquanto lá
                // está alguém, um leito que não se ouve não é discreto: é ausente.
                //
                // **0,16 era de mais, e não por causa da soma.** Medido no hall com
                // o jogador parado: o encurtamento do alcance funciona — de oito
                // tons só se ouvia um, a 0,134. O que ninguém contou foi a cama de
                // ambiente 2D que entrou por cima depois disto, e essa ouve-se
                // inteira em toda a casa. Eram 0,30 de ruído contínuo, sempre, em
                // qualquer sítio da planta. Isto tira-lhe um terço; o resto sai da
                // cama, que é a camada que nunca muda.
                so.FindProperty("volume").floatValue = 0.10f;
                // Alcance curto: cada divisão só se ouve dentro dela e um pouco à
                // porta, em vez de atravessar a casa toda. **É esta a linha que
                // impede o empastamento** — não o volume.
                so.FindProperty("minDistance").floatValue = 1f;
                so.FindProperty("maxDistance").floatValue = 4f;
                so.ApplyModifiedPropertiesWithoutUndo();

                var source = tone.GetComponent<AudioSource>();
                if (source != null)
                {
                    source.volume = 0.10f;
                    source.minDistance = 1f;
                    source.maxDistance = 4f;
                }
                count++;
            }

            // O buzz das lâmpadas soma-se ao mesmo leito e é da mesma família de
            // frequências; baixa junto para o conjunto não voltar a empastar.
            //
            // **Baixa na mesma proporção que o tom, e não para um número novo.** O
            // buzz estava em 0,10 contra 0,16 do tom — abaixo dele, que é onde tem
            // de estar. Descer só o tom punha os dois iguais, e medido na sala isso
            // dava 0,099 de buzz contra 0,100 de tom: um zumbido de lâmpada tão
            // presente como a divisão inteira. Sendo estreito e agudo, ao ouvido
            // soa mais alto do que isso ainda.
            //
            // O clip do buzz é gerado no Awake do UnstableLight, por isso em modo
            // de edição o AudioSource ainda tem clip nulo — filtrar pelo nome do
            // clip não apanha nada. O botão a mexer é o buzzVolume do componente.
            int buzz = 0;
            foreach (var unstable in Object.FindObjectsOfType<Pungent.Atmosphere.UnstableLight>(true))
            {
                var so = new SerializedObject(unstable);
                so.FindProperty("buzzVolume").floatValue = 0.06f;
                so.FindProperty("buzzMaxDistance").floatValue = 4.5f;
                so.ApplyModifiedPropertiesWithoutUndo();
                buzz++;
            }

            var fridge = GameObject.Find("AUDIO/AMB_FridgeDrone");
            var fridgeSource = fridge != null ? fridge.GetComponent<AudioSource>() : null;
            // O frigorífico é o único som contínuo com carácter próprio da casa —
            // e é o que o jogador ouve quando está tudo calado. Fica acima do leito.
            if (fridgeSource != null) fridgeSource.volume = 0.22f;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Audio] {count} room tones e {buzz} buzz de lampada reequilibrados.");
        }

        /// <summary>
        /// Cama de ambiente do apartamento, por cima dos room tones por divisão.
        ///
        /// Fica em 2D de propósito: os room tones já dão o carácter de cada divisão,
        /// e o que falta é o fundo comum a toda a casa — o prédio, a rua ao longe,
        /// o frigorífico do vizinho. Uma fonte posicional para isso obrigaria a
        /// espalhar cópias pela planta toda e a ouvi-las a entrar e a sair.
        /// </summary>
        [MenuItem("Tools/Pungent/Audio/Wire Apartment Ambience")]
        internal static void WireApartmentAmbience()
        {
            const string path = "Assets/ThirdParty/Audio/Apartment_ambience_sound.mp3";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) { Debug.LogError($"[Audio] Em falta: {path}"); return; }

            var audioRoot = GameObject.Find("AUDIO");
            if (audioRoot == null) audioRoot = new GameObject("AUDIO");

            var existing = audioRoot.transform.Find("AMB_ApartmentBed");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var holder = new GameObject("AMB_ApartmentBed");
            holder.transform.SetParent(audioRoot.transform, false);

            var loop = holder.AddComponent<Pungent.Audio.SeamlessLoopAudio>();
            var so = new SerializedObject(loop);
            so.FindProperty("clip").objectReferenceValue = clip;
            // Baixo: é para se sentir por baixo dos room tones, não para os tapar.
            //
            // **É a camada mais cansativa da casa e por isso é a que leva o corte
            // maior.** Sendo 2D, não muda com o sítio nem com o que o jogador faz:
            // ouve-se exactamente igual no hall, na varanda e debaixo da cama. A
            // 0,14 era metade de todo o ruído contínuo do apartamento — medido, 0,14
            // dela contra 0,134 do room tone da divisão onde o jogador estava.
            so.FindProperty("volume").floatValue = 0.06f;
            so.FindProperty("crossfadeSeconds").floatValue = 3f;
            so.FindProperty("spatialBlend").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Audio] Cama de ambiente ligada ({clip.length:F0}s, crossfade 3s).");
        }

        /// <summary>
        /// Para quando entrar o house ambience a sério: desliga o leito procedural
        /// sem o apagar da cena, para dar para comparar os dois.
        /// </summary>
        [MenuItem("Tools/Pungent/Audio/Toggle Procedural Room Tone")]
        internal static void Toggle()
        {
            var tones = Object.FindObjectsOfType<ProceduralRoomTone>(true);
            if (tones.Length == 0) { Debug.LogWarning("[Audio] Sem room tones na cena."); return; }

            bool turnOff = tones[0].enabled;
            foreach (var tone in tones)
            {
                tone.enabled = !turnOff;
                var source = tone.GetComponent<AudioSource>();
                if (source != null && turnOff) source.Stop();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[Audio] Room tone procedural {(turnOff ? "desligado" : "ligado")} em {tones.Length} fontes.");
        }
    }
}
