using UnityEditor;
using UnityEngine;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Impede que os construtores de cena corram em Play Mode.
    ///
    /// Existe por causa de um estrago real: correr o `Rebuild` durante o Play Mode
    /// destrói o root do blockout, reconstrói-o na cena temporária e depois
    /// rebenta em `MarkSceneDirty`, que é proibido em Play Mode. Ao sair do Play
    /// Mode a cena é revertida e o resultado é imprevisível — no pior caso fica-se
    /// sem paredes e sem perceber porquê.
    ///
    /// Todos os pontos de entrada em `Pungent/Blockout/...` chamam isto primeiro.
    /// </summary>
    public static class BuildGuard
    {
        /// <summary>Devolve true e avisa se não for seguro construir agora.</summary>
        public static bool Blocked(string tool)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return false;

            Debug.LogWarning(
                $"[{tool}] Ignorado: os construtores de cena não podem correr em Play Mode. " +
                "Sai do Play Mode e volta a executar.");
            return true;
        }
    }
}
