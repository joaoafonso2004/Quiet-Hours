using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Audio;
using Pungent.NPC;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Poe o som da caca ao lado de quem caca.
    ///
    /// Vive junto do <see cref="RuiHunt"/> e nao no `GAME_SYSTEMS` por uma razao
    /// concreta: o apartamento e **recarregado** para o climax, e uma referencia ao
    /// `RuiHunt` guardada num sistema que atravessa cenas fica a apontar para o
    /// exemplar da cena anterior — que ainda existe nesse frame e depois morre. O
    /// som ficava calado para sempre e nao havia erro nenhum a dizer porque. Vivendo
    /// no mesmo sitio que a caca, nasce e morre com ela.
    ///
    /// Nao precisa de entrar em nenhuma lista de "acordar": enquanto a caca estiver
    /// em `Hidden` o componente cala-se sozinho, que e o estado em que ela passa o
    /// jogo todo ate ao Dia 5.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class ChaseAudioWiring
    {
        [MenuItem("Pungent/Blockout/Wire Chase Audio", false, 42)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("WireChaseAudio")) return;

            var hunt = Object.FindObjectOfType<RuiHunt>(true);
            if (hunt == null)
            {
                Debug.LogError("[Caca] Sem `RuiHunt` nesta cena. Abrir a " +
                               "`Apartment_Blockout_V2` e correr 'Wire Climax (Day 5)' primeiro.");
                return;
            }

            var motor = Object.FindObjectOfType<Pungent.Player.PlayerMotor>(true);
            if (motor == null)
                Debug.LogWarning("[Caca] Sem `PlayerMotor`: o pulso da proximidade " +
                                 "fica sem distancia e so responde ao estado.");

            var audio = hunt.GetComponent<ChaseAudio>();
            if (audio == null) audio = Undo.AddComponent<ChaseAudio>(hunt.gameObject);

            audio.EditorConfigure(hunt, motor != null ? motor.transform : null);
            EditorUtility.SetDirty(audio);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Caca] Som da caca ligado em '" + hunt.gameObject.name + "'. " +
                      "Tres camadas geradas em memoria; havendo gravacoes, preencher " +
                      "os campos de clip no inspector.");
        }
    }
}
