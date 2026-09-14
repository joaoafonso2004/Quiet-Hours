using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.Narrative;
using Pungent.NPC;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Poe de pe o Dia 3 — "Limites".
    ///
    /// ---
    ///
    /// **O que este dia tinha, e o que lhe faltava.** Tinha quatro passos escritos no
    /// `SkeletonWiring`, as tarefas domesticas do `DailyPressureWiring` e os sinais
    /// de invasao do `InvasionSignsWiring`. Ou seja: tinha o **estado** do dia — a
    /// gaveta mexida, a janela aberta — e nao tinha o **dia**. O jogador acordava
    /// numa casa alterada, fazia duas tarefas e saia.
    ///
    /// Faltavam as tres coisas que o §5 pede pelo nome e que nunca chegaram a existir:
    ///
    /// 1. **O Rui fora de casa.** O <see cref="DayThreeStage"/>. E a primeira manha em
    ///    que ele nao esta, e a porta do quarto dele fica aberta.
    /// 2. **A fechadura que falha uma vez.** O <see cref="StickyLock"/>. O Rui promete
    ///    isto na primeira fala do jogo e a fechadura nunca tinha falhado.
    /// 3. **A escolha atraves da porta.** A <see cref="DoorConversation"/>. E o que da
    ///    o nome ao dia.
    ///
    /// ---
    ///
    /// **A ordem em que o dia acontece**, e porque e que e esta:
    ///
    /// - `day_two` monta a casa vazia e aplica os sinais. O jogador acorda sozinho.
    /// - Ir ao quarto do Rui fecha `day3_searched` e **arma a conversa a porta**. A
    ///   cena so acontece se ele tiver mesmo ido la — perguntar-lhe pela gaveta sem
    ///   ter visto a gaveta nao faz sentido nenhum.
    /// - As tarefas da manha (`day3_chores`) gastam o tempo em que a casa esta vazia.
    /// - `day3_ready` traz o Rui de volta. E so entao que ele pode bater a porta.
    /// - A conversa acontece quando o jogador estiver fechado no quarto. Nao bloqueia
    ///   nada: quem nunca la voltar perde a cena e sai de casa na mesma.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class DayThreeWiring
    {
        private const string Root = "DAY3_SETUP";
        private const string AudioFolder = "Assets/ThirdParty/Audio/";

        [MenuItem("Pungent/Blockout/Wire Day 3 (Limits)", false, 15)]
        internal static void Wire()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var log = new List<string>();

            var rui = GameObject.Find("NPC_Rui");
            if (rui == null) { Debug.LogError("[Dia3] NPC_Rui nao encontrado."); return; }

            var host = GameObject.Find("APARTMENT_SYSTEMS") ?? GameObject.Find("PlayerRoot");
            if (host == null) { Debug.LogError("[Dia3] Sem APARTMENT_SYSTEMS nem PlayerRoot."); return; }

            DoorDragInteractable tomasDoor = FindDoor("Door_Bedroom_Tomas");
            DoorDragInteractable ruiDoor = FindDoor("Door_Bedroom_Rui");
            if (tomasDoor == null) log.Add("  AVISO: Door_Bedroom_Tomas nao encontrada");
            if (ruiDoor == null) log.Add("  AVISO: Door_Bedroom_Rui nao encontrada");

            BuildStage(host, rui, ruiDoor, log);
            BuildStickyLock(tomasDoor, log);
            BuildDoorConversation(host, rui, tomasDoor, log);
            BuildMorningMessage(log);

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Dia3] Ligado.\n" + string.Join("\n", log));
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// A mensagem que o Tomas manda ao Rui na manha em que a casa esta vazia — **e
        /// a que fica sem resposta.**
        ///
        /// ---
        ///
        /// **Porque e que isto e um passo com a fala vazia.** O fio do telemovel e
        /// linear e comeca sempre pelo contacto: o outro diz uma coisa, o jogador
        /// escolhe a resposta. Aqui e ao contrario — quem fala primeiro e o Tomas.
        /// Um passo com `Line` vazia e `Choices` preenchidas faz exactamente isso: o
        /// `Deliver` nao escreve nada de ninguem e poe o fio a espera do jogador.
        ///
        /// ---
        ///
        /// **E ele nao responde, e essa e a peca.**
        ///
        /// A `Reply` vazia deixa a mensagem enviada e o fio calado. O jogador acabou
        /// de entrar no quarto dele sem desculpa nenhuma, e agora esta a perguntar-lhe
        /// onde esta — a mensagem fica no ecra a tarde toda, com dois riscos e sem
        /// resposta.
        ///
        /// **A resposta chega depois de ele ja ter estado em casa**, no `day3_done` —
        /// ou seja, depois do beat em que a chave roda na porta, ele vai a casa de
        /// banho e volta a sair sem dizer nada. Ele escreve *"was out all morning"*, e
        /// o jogador sabe que nao esteve, porque se escondeu dele. E a regra desta
        /// casa: nada dramatico, uma coisa banal que so o jogador sabe ser falsa.
        /// </summary>
        private static void BuildMorningMessage(List<string> log)
        {
            var thread = PhoneThreadEditing.Load("PHN_Rui_Router");
            if (thread == null)
            {
                log.Add("  AVISO: sem `PHN_Rui_Router` — a pergunta da manha fica por acrescentar.");
                return;
            }

            const string Ask = "where are you?";
            var steps = PhoneThreadEditing.ReadSteps(thread);

            bool already = steps.Exists(s => s?.Choices != null
                && System.Array.Exists(s.Choices, c => c != null && c.Text == Ask));
            if (already) { log.Add("  a pergunta da manha ja estava no fio do Rui"); return; }

            var question = new PhoneConversationDefinition.Step
            {
                Line = string.Empty,                 // quem fala primeiro e o Tomas
                WaitForEvent = "day3_searched",      // acabou de sair do quarto dele
                DelaySeconds = 0f,
                ReplyDelaySeconds = 0f,              // sem resposta: nao ha o que esperar
                Choices = new[]
                {
                    new DialogueChoice { Text = Ask, Reply = string.Empty, Tone = DialogueTone.Calm },
                    new DialogueChoice { Text = "are you working today?", Reply = string.Empty,
                        Tone = DialogueTone.Evasive }
                }
            };

            // A resposta tardia, ja depois de ele ter entrado e saido de casa.
            var late = PhoneThreadEditing.Message("was out all morning. why", "day3_done", 45f, 5f);

            // **Antes das mensagens da noite.** O fio e linear: enfiado no fim, isto so
            // aparecia depois do climax, que e um dia inteiro tarde de mais.
            int at = steps.FindIndex(s => s != null && s.WaitForEvent == "climax");
            if (at < 0) at = steps.Count;

            steps.Insert(at, question);
            steps.Insert(at + 1, late);

            PhoneThreadEditing.Write(thread, thread.ContactName, steps);
            log.Add("  pergunta ao Rui armada em 'day3_searched'; ele so responde no 'day3_done'");
        }

        private static void BuildStage(GameObject host, GameObject rui,
            DoorDragInteractable ruiDoor, List<string> log)
        {
            var stage = Object.FindObjectOfType<DayThreeStage>(true);
            if (stage == null)
            {
                stage = Undo.AddComponent<DayThreeStage>(host);
                log.Add("  DayThreeStage criado em " + host.name);
            }

            // O que se cala enquanto ele nao esta em casa. A rotina porque ela e o
            // corpo dele a andar; a presenca sonora porque ela e ele a existir atras
            // de uma parede — e hoje nao ha ninguem atras de parede nenhuma.
            var silence = new List<MonoBehaviour>();
            var routine = rui.GetComponent<PrototypeNpcRoutine>();
            foreach (var behaviour in rui.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour == routine || silence.Contains(behaviour))
                    continue;
                silence.Add(behaviour);
            }

            // Several controllers call back into the routine from OnDisable. Keep
            // the routine last so those callbacks cannot make Rui active again.
            if (routine != null) silence.Add(routine);

            var territory = Object.FindObjectOfType<NpcRoomTerritory>(true);

            var dim = new List<Light>();
            foreach (var name in new[] { "Kitchen_Light", "Living_Light" })
            {
                var go = GameObject.Find(name);
                var light = go != null ? go.GetComponent<Light>() : null;
                if (light != null) dim.Add(light);
            }

            stage.EditorConfigure(rui, ruiDoor, territory, silence.ToArray(), dim.ToArray());

            var so = new SerializedObject(stage);
            so.FindProperty("stageOnEvent").stringValue = "day_two";
            so.FindProperty("endOnEvent").stringValue = "day3_ready";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);

            log.Add($"  casa vazia: {silence.Count} componentes calados, {dim.Count} luzes apagadas, "
                  + "porta do Rui aberta");
        }

        /// <summary>
        /// A fechadura do quarto do Tomas passa a poder falhar.
        ///
        /// Armada no `day_two` e nao antes: a primeira noite tem de trancar sem
        /// problema nenhum, senao a terceira nao quer dizer nada. Desarmada no
        /// `climax`, porque nessa noite uma fechadura a emperrar deixa de ser tensao
        /// e passa a ser o jogo a bater no jogador.
        /// </summary>
        private static void BuildStickyLock(DoorDragInteractable door, List<string> log)
        {
            if (door == null) return;

            var sticky = door.GetComponent<StickyLock>();
            if (sticky == null) sticky = Undo.AddComponent<StickyLock>(door.gameObject);

            // O clip vem da pasta pelo nome, e nao de alguem se lembrar de o arrastar
            // para um campo dentro de um componente que vive numa porta. Mesma regra
            // do `LifeSoundsWiring`: quem faz o som larga o ficheiro com o nome certo
            // e corre a ferramenta.
            sticky.EditorConfigure("day_two", "climax",
                "It is not catching. He did say it sticks.",
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "lock_jam.mp3"));

            // A porta tem de saber que ela existe: e a porta que manda no trinco e
            // que pergunta antes de trancar.
            var doorSo = new SerializedObject(door);
            var slot = doorSo.FindProperty("stickyLock");
            if (slot != null)
            {
                slot.objectReferenceValue = sticky;
                doorSo.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(door);
            EditorUtility.SetDirty(sticky);

            log.Add("  fechadura do quarto do Tomas emperra uma vez a partir do Dia 2");
            log.Add(new SerializedObject(sticky).FindProperty("jamClip").objectReferenceValue != null
                ? "  som da falha: `lock_jam.mp3` ligado"
                : "  POR PREENCHER: falta `" + AudioFolder + "lock_jam.mp3` — a falha e muda");
        }

        /// <summary>
        /// O Rui a bater a porta, e a pergunta que faz.
        ///
        /// Armada pelo `day3_searched` — ou seja, so depois de o jogador ter ido ao
        /// quarto dele. Perguntar-lhe pela gaveta sem ter visto a gaveta seria o
        /// Tomas a saber uma coisa que o jogador nao sabe.
        /// </summary>
        private static void BuildDoorConversation(GameObject host, GameObject rui,
            DoorDragInteractable tomasDoor, List<string> log)
        {
            var talk = Object.FindObjectOfType<DoorConversation>(true);
            if (talk == null)
            {
                talk = Undo.AddComponent<DoorConversation>(host);
                log.Add("  DoorConversation criada em " + host.name);
            }

            // Do lado de fora da porta, no corredor, e **a meio do vao**.
            //
            // Medido a partir da malha da folha e nao do transform: o transform de
            // uma porta esta na dobradica, e po-lo la deixava o Rui encostado ao
            // batente em vez de a frente da porta. A folha corre em x de -5,55 a
            // -4,65; o meio dela e onde uma pessoa se poe para bater.
            //
            // O quarto e z < -0,80 e o corredor e z > -0,80 — sondado na NavMesh, nao
            // assumido. Setenta centimetros para o corredor poem-no fora do vao e a
            // distancia a que se fala com alguem atraves de uma porta.
            Vector3 spot = new Vector3(-5.10f, 0f, -0.10f);
            float yaw = 180f;
            if (tomasDoor != null)
            {
                var leaf = tomasDoor.GetComponentInChildren<Renderer>();
                float x = leaf != null ? leaf.bounds.center.x : tomasDoor.transform.position.x;
                spot = new Vector3(x, 0f, tomasDoor.transform.position.z + 0.70f);

                // Virado para a porta, seja de que lado o corredor estiver.
                yaw = 180f;
            }

            AudioSource knocks = rui.GetComponentInChildren<AudioSource>(true);
            talk.EditorConfigure(rui, tomasDoor, spot, yaw, knocks,
                AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "door_knock.mp3"));

            var so = new SerializedObject(talk);
            so.FindProperty("armOnEvent").stringValue = "day3_searched";
            so.FindProperty("finishedEvent").stringValue = "day3_door_talk";
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(talk);

            log.Add($"  conversa a porta armada em 'day3_searched', ele fica em {spot:F2}");
            log.Add(so.FindProperty("knockClip").objectReferenceValue != null
                ? "  batidas: `door_knock.mp3` ligado"
                : "  POR PREENCHER: falta `" + AudioFolder + "door_knock.mp3` — ele fala sem bater");
        }

        private static DoorDragInteractable FindDoor(string name)
        {
            foreach (var d in Object.FindObjectsOfType<DoorDragInteractable>(true))
                if (d.gameObject.name == name) return d;
            return null;
        }
    }
}
