using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Interaction;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Da som ao mini-jogo dos cabos.
    ///
    /// ---
    ///
    /// **O `RouterCablePuzzle` era o unico componente do jogo sem dono.** Estava na
    /// cena, montado a mao, e nenhuma ferramenta lhe tocava — o que fazia dos tres
    /// clips dele as unicas tres entradas da lista de assets que diziam "arrastar no
    /// Inspector". Uma instrucao dessas dura ate a primeira pessoa se esquecer.
    ///
    /// Agora sao carregados por caminho como todos os outros: larga-se o ficheiro em
    /// `ThirdParty/Audio/` com o nome certo e corre-se isto.
    ///
    /// ---
    ///
    /// **O que os tres sons sao**, e porque e que o contexto manda neles:
    ///
    /// Sao 02:47, o Tomas esta de cocoras no corredor as escuras, de costas para a
    /// casa toda, durante uns vinte segundos. Isto nao e um puzzle — sao os vinte
    /// segundos em que ele nao esta a olhar. O som tem de ser **pequeno e proximo**:
    /// coisas que acontecem entre as maos dele, a um palmo da cara. Nada que encha o
    /// corredor, porque o corredor tem de continuar disponivel para o que se ouvir
    /// atras.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class RouterPuzzleWiring
    {
        private const string AudioFolder = "Assets/ThirdParty/Audio/";

        [MenuItem("Pungent/Blockout/Wire Router Puzzle Sound", false, 22)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireRouterPuzzle")) return;

            var puzzle = Object.FindObjectOfType<RouterCablePuzzle>(true);
            if (puzzle == null)
            {
                Debug.LogError("[Router] Sem `RouterCablePuzzle` nesta cena. Ele e montado " +
                               "a mao — se desapareceu, o router volta a ser um clique e a " +
                               "noite perde os vinte segundos de cocoras.");
                return;
            }

            var so = new SerializedObject(puzzle);

            int filled = 0;
            var missing = new System.Collections.Generic.List<string>();

            filled += Fill(so, "plugClip", "router_plug.mp3", missing);
            filled += Fill(so, "wrongClip", "router_wrong.mp3", missing);
            filled += Fill(so, "doneClip", "router_boot.mp3", missing);

            // A fonte tem de existir, senao os clips ficam ligados e mudos. Ao pe do
            // painel e nao no jogador: o que se ouve sao as maos dele no sitio onde
            // estao, e um som plano tirava a unica informacao espacial que este
            // momento tem.
            var source = so.FindProperty("audioSource");
            if (source.objectReferenceValue == null)
            {
                var holder = puzzle.GetComponent<AudioSource>();
                if (holder == null) holder = puzzle.gameObject.AddComponent<AudioSource>();
                holder.playOnAwake = false;
                holder.spatialBlend = 1f;
                holder.rolloffMode = AudioRolloffMode.Linear;
                holder.minDistance = 0.4f;
                holder.maxDistance = 5f;
                holder.dopplerLevel = 0f;
                source.objectReferenceValue = holder;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(puzzle);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            // **Nao avisa pelos que faltam.** Ficou decidido que o `router_wrong` e o
            // `router_boot` nao vao existir, e o `Play` do puzzle ja protege contra
            // clip nulo — sem eles a ficha certa da o seu estalido e o resto acontece
            // calado, que e uma escolha e nao um esquecimento.
            //
            // Um aviso repetido a cada execucao por uma coisa que nao vai mudar treina
            // as pessoas a ignorar avisos, e a seguir passa-lhes ao lado o que importa.
            Debug.Log("[Router] " + filled + " de 3 sons ligados" +
                      (missing.Count > 0
                          ? " (" + string.Join(", ", missing) + " por opcao, ficam calados)."
                          : "."));
        }

        private static int Fill(SerializedObject so, string field, string fileName,
            System.Collections.Generic.List<string> missing)
        {
            var property = so.FindProperty(field);
            if (property == null) return 0;
            if (property.objectReferenceValue != null) return 1;

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + fileName);
            if (clip == null) { missing.Add(fileName); return 0; }

            property.objectReferenceValue = clip;
            return 1;
        }
    }
}
