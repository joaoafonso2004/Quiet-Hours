using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Faz o Play comecar sempre pelo menu, esteja aberta a cena que estiver.
    ///
    /// ---
    ///
    /// **O que isto resolve, e que nao era um bug.** O `Main_Menu` e a cena 0 das
    /// Build Settings, portanto **um build ja comeca la**. O Editor e que nao: o
    /// Play corre sempre a cena que esta aberta, e como se trabalha o dia inteiro
    /// dentro do apartamento, carregar em Play cai direito no prologo e no texto de
    /// abertura. Parece que o menu nao existe. Existe, e nunca chega a ser pedido.
    ///
    /// ---
    ///
    /// **Um interruptor e nao um comportamento.** Deixar isto sempre ligado tem um
    /// preco real: cada volta de teste a uma coisa do Dia 3 passa a comecar por um
    /// menu que e preciso despachar. E por isso que e uma opcao no menu do Unity,
    /// com visto, e nao uma regra escrita no projecto — liga-se para ver o arranque
    /// como o jogador o ve, e desliga-se para trabalhar.
    ///
    /// **Sobrevive a recarga do dominio e ao fechar do Unity.** O
    /// `playModeStartScene` e um campo do editor e nao e gravado em lado nenhum: um
    /// recompile limpa-o e o interruptor parecia desligar-se sozinho. A escolha vive
    /// nos `EditorPrefs`, com o caminho do projecto na chave para dois checkouts na
    /// mesma maquina nao se pisarem, e e reaplicada em cada carregamento.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayFromMenu
    {
        private const string MenuPath = "Pungent/Play/Comecar sempre pelo menu";
        private const string MenuScene = "Assets/Scenes/Main_Menu.unity";

        private static string Key => "Pungent.PlayFromMenu." + Application.dataPath.GetHashCode();

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(Key, false);
            set => EditorPrefs.SetBool(Key, value);
        }

        /// <summary>
        /// Reaplica a escolha a cada carregamento de dominio — que e a seguir a cada
        /// recompilacao, e nao so ao abrir o Unity.
        /// </summary>
        static PlayFromMenu() => EditorApplication.delayCall += Apply;

        [MenuItem(MenuPath, false, 1)]
        private static void Toggle()
        {
            Enabled = !Enabled;
            Apply();

            Debug.Log(Enabled
                ? "[Play] O Play passa a comecar pelo `Main_Menu`, esteja aberta a cena que " +
                  "estiver. Desliga em '" + MenuPath + "' quando estiveres a iterar — senao " +
                  "cada volta de teste comeca por despachar um menu."
                : "[Play] O Play volta a correr a cena aberta. O menu continua a ser a cena 0 " +
                  "das Build Settings, portanto o build comeca la de qualquer maneira.");
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            // `UnityEditor.Menu` por extenso: este projecto tem um namespace
            // `Pungent.Menu` (o menu inicial e o de pausa vivem la), e de dentro de
            // `Pungent.EditorTools` o nome curto resolve para **esse** namespace e
            // nao para a classe do editor. O erro que da e sobre um `SetChecked` que
            // nao existe em `Pungent.Menu`, que manda procurar no sitio errado.
            UnityEditor.Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        /// <summary>
        /// Abre o menu, sem mexer no interruptor. Para o caso de so se querer ver o
        /// arranque uma vez.
        /// </summary>
        [MenuItem("Pungent/Play/Abrir a cena do menu", false, 2)]
        private static void OpenMenuScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        }

        private static void Apply()
        {
            if (!Enabled) { EditorSceneManager.playModeStartScene = null; return; }

            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene);
            if (asset == null)
            {
                // Na duvida cala-se **e desliga-se**: um interruptor ligado que nao
                // faz nada e pior do que um desligado, porque quem o ve com visto
                // deixa de procurar a razao de o Play nao comecar onde devia.
                Debug.LogWarning("[Play] Sem " + MenuScene + ". O interruptor fica desligado.");
                Enabled = false;
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            EditorSceneManager.playModeStartScene = asset;
        }
    }
}
