using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Poe a chamada do Dia 4 de pe — a meia conversa que fecha o triangulo.
    ///
    /// ---
    ///
    /// **O que a garagem ja tinha, e o que lhe faltava.** O `GarageDressing` ja monta
    /// a lona, o monte de pecas, os numeros raspados, a lanterna, a bagageira e a
    /// saida. Ou seja: **a prova de que as pecas sao roubadas ja existia**. O que nao
    /// existia era a ligacao ao Rui — e sem ela o Dia 4 entrega um vendedor de coisas
    /// roubadas, que e um problema pequeno e de outra pessoa.
    ///
    /// A funcao deste dia, escrita no §5, e *converter suspeita domestica em perigo
    /// concreto*. E isso acontece numa frase. Ver <see cref="OverheardCall"/>.
    ///
    /// ---
    ///
    /// **Onde ele vai atender.** Ao fundo do patio, junto a saida das traseiras, de
    /// costas. Longe o suficiente para o jogador ter de decidir aproximar-se, e num
    /// sitio onde estar perto dele nao e um sitio onde se esteja por acaso.
    ///
    /// Re-executavel. Corre com a `Garage_Blockout` aberta.
    /// </summary>
    internal static class GarageCallWiring
    {
        [MenuItem("Pungent/Blockout/Wire Garage Call (Day 4)", false, 20)]
        internal static void Wire()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Garage"))
            {
                Debug.LogError("[Chamada] Abrir a `Garage_Blockout` primeiro. " +
                               "Esta ferramenta mexe na cena da garagem.");
                return;
            }

            var log = new List<string>();

            // O vendedor e montado pelo `GarageWiring` num objecto chamado SELLER.
            var seller = GameObject.Find("SELLER");
            if (seller == null)
            {
                Debug.LogError("[Chamada] Sem `SELLER` na cena. Correr " +
                               "'Pungent/Blockout/Wire Garage (Day 4)' primeiro.");
                return;
            }

            Transform spot = BuildCallSpot(log);

            var call = Object.FindObjectOfType<OverheardCall>(true);
            if (call == null)
            {
                call = Undo.AddComponent<OverheardCall>(seller);
                log.Add("  OverheardCall criada em SELLER");
            }

            var phone = seller.GetComponentInChildren<AudioSource>(true);
            if (phone == null)
            {
                var holder = new GameObject("Seller_Phone");
                holder.transform.SetParent(seller.transform, false);
                holder.transform.localPosition = new Vector3(0f, 1.15f, 0.12f);
                phone = holder.AddComponent<AudioSource>();
                phone.spatialBlend = 1f;
                phone.rolloffMode = AudioRolloffMode.Linear;
                phone.minDistance = 1.2f;
                phone.maxDistance = 14f;
                phone.playOnAwake = false;
                log.Add("  fonte de som do telemovel criada em SELLER/Seller_Phone");
            }

            var voice = BuildVoice(seller.transform, log);

            call.EditorConfigure(seller, spot, phone);

            var so = new SerializedObject(call);
            so.FindProperty("armOnEvent").stringValue = "evidence_found";
            so.FindProperty("heardEvent").stringValue = "heard_call";

            // A voz vai para o campo mesmo com o `Awake` do `OverheardCall` a saber
            // procura-la: quem le a cena no Inspector tem de conseguir ver de onde
            // vem o som sem correr o jogo.
            if (voice != null) so.FindProperty("voice").objectReferenceValue = voice;

            // O toque vem da pasta pelo nome. E o unico som deste dia que **tem** de
            // existir: nao e atmosfera, e a coisa que puxa o jogador para o fundo do
            // patio. Sem ele o vendedor afasta-se sozinho e nao ha razao nenhuma para
            // o seguir — o capitulo perde a frase que o justifica.
            var ring = so.FindProperty("ringClip");
            if (ring.objectReferenceValue == null)
                ring.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/ThirdParty/Audio/phone_ring.mp3");

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(call);

            log.Add("  chamada armada em 'evidence_found', levanta 'heard_call'");
            log.Add(ring.objectReferenceValue != null
                ? "  toque: `phone_ring.mp3` ligado"
                : "  POR PREENCHER: falta `Assets/ThirdParty/Audio/phone_ring.mp3` — " +
                  "sem ele nada puxa o jogador para la");

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Chamada] Ligada.\n" + string.Join("\n", log));
        }

        /// <summary>
        /// A voz dele.
        ///
        /// ---
        ///
        /// **O `OverheardCall` procurava-a e nunca a encontrava.** O `Awake` faz
        /// `GetComponentInChildren&lt;NpcMumbleVoice&gt;` no vendedor e nenhuma
        /// ferramenta lha montava — nem esta, nem a `GarageWiring` que o poe de pe. O
        /// campo ficava nulo, o `ShowReaction` aceitava nulo sem se queixar, e as
        /// quatro falas da chamada apareciam legendadas **em silencio absoluto**.
        ///
        /// Nao faltava ficheiro nenhum: o `NpcMumbleVoice` gera as silabas por codigo
        /// quando nao lhe dao clips. Faltava o componente.
        ///
        /// ---
        ///
        /// **Mais grave do que o Rui, e de propósito.**
        ///
        /// A `GarageWiring` ja escolheu uma cara diferente da dele pela mesma razao:
        /// sao do mesmo pack, e a semelhanca entre os dois seria lida como pista
        /// quando a ligacao entre eles e para ser descoberta pelo que se diz. Uma voz
        /// parecida desfazia isso pelo ouvido em vez de pelos olhos — e o ouvido
        /// chega la primeiro, porque a chamada ouve-se de seis metros e a cara so se
        /// ve de perto.
        ///
        /// **Alcance grande, volume baixo.** O `earshot` do `OverheardCall` sao 6,5 m
        /// e o `noticeRange` sao 2,4: a cena inteira vive na faixa entre os dois, com
        /// o jogador longe o suficiente para nao ser apanhado e perto o suficiente
        /// para ouvir. Uma voz que se apague aos cinco metros fecha essa faixa e nao
        /// ha onde por a cena.
        /// </summary>
        private static Pungent.Dialogue.NpcMumbleVoice BuildVoice(Transform seller,
            List<string> log)
        {
            const string Name = "Seller_Voice";

            var found = seller.Find(Name);
            GameObject go;
            if (found != null) go = found.gameObject;
            else
            {
                go = new GameObject(Name);
                go.transform.SetParent(seller, false);
                Undo.RegisterCreatedObjectUndo(go, "Wire seller voice");
                log.Add("  voz criada em SELLER/" + Name);
            }

            // A altura da boca. Filha do vendedor e nao solta na raiz: ele anda dez
            // metros ate ao canto durante a cena, e uma fonte parada no sitio onde ele
            // estava deixava a voz para tras.
            go.transform.localPosition = new Vector3(0f, 1.62f, 0f);

            // O `RequireComponent` so acrescenta o `AudioSource` ao adicionar pelo
            // inspector. Mesma nota do `NightRoadTools`.
            if (go.GetComponent<AudioSource>() == null) go.AddComponent<AudioSource>();

            var voice = go.GetComponent<Pungent.Dialogue.NpcMumbleVoice>();
            if (voice == null) voice = go.AddComponent<Pungent.Dialogue.NpcMumbleVoice>();

            voice.EditorConfigure(isSpatial: true,
                pitch: new Vector2(0.82f, 0.92f),
                loudness: 0.30f, minDistance: 1.6f, maxDistance: 18f);
            EditorUtility.SetDirty(voice);

            log.Add("  voz do vendedor: procedural, grave (0,82–0,92), audivel ate 18 m");
            return voice;
        }

        /// <summary>
        /// O canto onde ele atende.
        ///
        /// Reaproveita a ancora da saida das traseiras se existir — e o unico sitio do
        /// patio que ja esta pensado como "fundo", longe do carro e das pecas. Se nao
        /// existir, cria uma marca propria e diz onde a poisou, para alguem a poder
        /// arrastar com a mao.
        /// </summary>
        private static Transform BuildCallSpot(List<string> log)
        {
            const string Name = "GARAGE_CallSpot";

            var existing = GameObject.Find(Name);
            if (existing != null) return existing.transform;

            var go = new GameObject(Name);
            Undo.RegisterCreatedObjectUndo(go, "Wire Garage Call");

            var anchors = GameObject.Find("ANCHORS");
            if (anchors != null) go.transform.SetParent(anchors.transform, true);

            var back = GameObject.Find("GARAGE_BackExit");
            if (back != null)
            {
                // Um pouco antes da saida, e virado para ela: de costas para o patio,
                // que e a leitura toda — aquilo nao e para os ouvidos do jogador.
                go.transform.position = back.transform.position + back.transform.forward * -0.9f;
                go.transform.rotation = back.transform.rotation;
                log.Add("  canto da chamada junto a GARAGE_BackExit");
            }
            else
            {
                go.transform.position = new Vector3(-6.5f, 0f, 7.5f);
                go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                log.Add("  AVISO: sem GARAGE_BackExit; canto da chamada posto em " +
                        go.transform.position.ToString("F1") + " — arrastar a mao se ficar mal");
            }

            return go.transform;
        }
    }
}
