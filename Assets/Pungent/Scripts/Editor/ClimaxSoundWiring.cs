using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Audio;
using Pungent.NPC;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Da som ao Rui na noite do Dia 5.
    ///
    /// A seccao 5 pede, para este capitulo, que *"sons filtrados por paredes e
    /// portas orientem a navegacao"*. O <see cref="MuffledThroughWalls"/> foi
    /// escrito para isso e estava pronto ha meses — **e nao havia nada a filtrar.**
    /// O Rui atravessava a casa as escuras em silencio absoluto.
    ///
    /// **Correccao importante:** os passos nunca faltaram. O `RuiHunt` ja tinha uma
    /// fonte propria (`Rui_Footsteps`), dez clips gravados e o mesmo passa-baixo. O
    /// que se ouvia era silencio porque a caca estava encravada e ele nao andava um
    /// centimetro — ver a nota do <see cref="RuiPresenceAudio"/>. Corrigida a
    /// paragem, os passos voltaram sozinhos.
    ///
    /// O que esta fonte acrescenta e o que faltava mesmo: **ruidos de casa** —
    /// gavetas, puxadores, uma cadeira — enquanto ele procura, que dizem em que
    /// divisao ele esta; e um som raro e muito baixo enquanto espera na lavandaria.
    ///
    /// **Ao nivel do chao e nao na cabeca dele**, para ficar a mesma altura da
    /// fonte dos passos e o jogador nao ouvir a mesma pessoa em dois sitios.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class ClimaxSoundWiring
    {
        private const string SourceName = "RUI_PRESENCE_AUDIO";

        [MenuItem("Pungent/Blockout/Wire Climax Sound (Rui through walls)", false, 14)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireClimaxSound")) return;

            var scene = EditorSceneManager.GetActiveScene();

            var rui = GameObject.Find("NPC_Rui");
            if (rui == null)
            {
                Debug.LogError("[ClimaxSom] NPC_Rui nao encontrado nesta cena.");
                return;
            }

            var hunt = rui.GetComponentInChildren<RuiHunt>(true);
            if (hunt == null)
                Debug.LogWarning("[ClimaxSom] O Rui nao tem `RuiHunt`: correr " +
                                 "'Wire Climax (Door 3B)' primeiro. A fonte fica montada " +
                                 "e o componente re-encontra-o em jogo.");

            // Re-executavel: fora o antigo antes de montar o novo.
            var old = rui.transform.Find(SourceName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject(SourceName);
            Undo.RegisterCreatedObjectUndo(go, "Wire climax sound");
            go.transform.SetParent(rui.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.06f, 0f);

            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.2f;
            source.maxDistance = 18f;
            source.dopplerLevel = 0f;
            source.volume = 1f;

            // A ordem importa: o `MuffledThroughWalls` le o `volume` da fonte no
            // proprio `Awake` para guardar o valor base. Montado antes de o volume
            // estar posto, guardava o que estivesse por omissao.
            go.AddComponent<MuffledThroughWalls>();

            var presence = go.AddComponent<RuiPresenceAudio>();
            presence.EditorConfigure(hunt);
            EditorUtility.SetDirty(presence);

            WireCaughtSting(rui, hunt);

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[ClimaxSom] Fonte montada em NPC_Rui/" + SourceName +
                      ". Ruidos de casa enquanto procura, e quase nada enquanto " +
                      "espera na lavandaria. Os passos sao do `RuiHunt`, que ja os " +
                      "tinha com os proprios clips e o proprio filtro.");
        }

        /// <summary>
        /// O som de ser apanhado.
        ///
        /// ---
        ///
        /// **Numa fonte propria, plana, e nao na que este ficheiro acabou de montar.**
        ///
        /// Tudo o resto que o Rui faz nesta noite e espacial e passa pelo
        /// <see cref="MuffledThroughWalls"/>: os passos, as gavetas, a voz. E assim que
        /// o jogador o localiza no escuro, e e a unica informacao que tem.
        ///
        /// Este som e o oposto disso. No instante em que a mao cai no ombro dele, a
        /// pergunta "onde e que ele esta" deixa de existir — e um som que ainda viesse
        /// da direita, abafado por uma parede, seria a resposta a uma pergunta que ja
        /// nao se faz. Plano, sem filtro, e sem sitio.
        ///
        /// Dura oito segundos: entra no mesmo instante da captura, atravessa os quatro
        /// e meio de escuro, e ainda esta a acabar quando ele acorda no quarto. E o
        /// unico som do jogo que sobrevive a um corte.
        /// </summary>
        private static void WireCaughtSting(GameObject rui, RuiHunt hunt)
        {
            if (hunt == null) return;

            const string Name = "RUI_CAUGHT_STING";

            var found = rui.transform.Find(Name);
            GameObject host = found != null ? found.gameObject : new GameObject(Name);
            if (found == null)
            {
                host.transform.SetParent(rui.transform, false);
                Undo.RegisterCreatedObjectUndo(host, "Wire caught sting");
            }

            var source = host.GetComponent<AudioSource>();
            if (source == null) source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;   // ver a nota acima
            source.dopplerLevel = 0f;

            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/ThirdParty/Audio/caught_sting.mp3");

            var so = new SerializedObject(hunt);
            so.FindProperty("caughtSource").objectReferenceValue = source;
            if (clip != null) so.FindProperty("caughtClip").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hunt);

            Debug.Log(clip != null
                ? "[ClimaxSom] `caught_sting.mp3` ligado a captura, plano, por cima do corte."
                : "[ClimaxSom] POR PREENCHER: `Assets/ThirdParty/Audio/caught_sting.mp3` " +
                  "nao encontrado. Ser apanhado corre em silencio.");
        }
    }
}
