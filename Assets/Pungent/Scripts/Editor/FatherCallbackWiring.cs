using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.Narrative;

namespace Pungent.EditorTools
{
    /// <summary>
    /// "Está tudo bem": a mentira pequena volta pela boca do pai.
    ///
    /// ---
    ///
    /// **O que este passo faz que os outros treze nao fazem.** O fio do Pai ja tem
    /// respostas honestas e evasivas, e a conta delas ja decide um final. O que nao
    /// tinha era o jogador a **ver a sua propria resposta a ser usada**. Aqui o pai
    /// nao pergunta nada: informa. Ele tranquilizou a mae com aquilo que o filho lhe
    /// disse, e a mae — que ia telefonar ao colega de casa por causa da humidade —
    /// deixou de ligar.
    ///
    /// Ou seja: a resposta comoda que nao custou nada no momento **desligou a unica
    /// pessoa de fora que ia fazer contacto com aquela casa.** Ninguem diz isso. O
    /// jogador percebe ou nao percebe.
    ///
    /// ---
    ///
    /// **Nao ramifica, e e de proposito.** A linha e a mesma para toda a gente. Quem
    /// respondeu com verdade a noite toda le uma banalidade domestica; quem escolheu
    /// sempre o confortavel ve a frase devolvida. A diferenca esta no jogador e nao
    /// no guiao — que e a maneira mais barata e mais honesta de fazer isto, e evita
    /// um ramo novo em cada mensagem anterior.
    ///
    /// ---
    ///
    /// **A resposta honesta e um pedido estranho.** Pedir a mae que telefone e o
    /// Tomas a querer uma testemunha sem conseguir dizer isso em voz alta. O pai
    /// repara na estranheza e **nao percebe** — *"That is not like you"* — que e o
    /// que este fio faz desde a primeira mensagem: ler tudo pelo lado banal.
    ///
    /// ---
    ///
    /// **Preso ao `day3_ready`**, decidido pelo dono do projecto: cai com o Tomas
    /// ainda em casa, de saida para a garagem, e nao ja na estrada. A casa vazia
    /// da-lhe silencio para o ler.
    ///
    /// A conta subiu para **4 de 5** ao mesmo tempo. Acrescentar uma quinta
    /// oportunidade e deixar o limiar em tres tornava o final mais facil sem ninguem
    /// ter decidido isso — a decisao foi deliberada e nao um efeito secundario.
    ///
    /// Re-executavel. A guarda pergunta pela **escolha** e nao pelo passo, pela mesma
    /// razao que o `BlackboardWiring` acabou de aprender a corrigir: um passo meio
    /// escrito engana uma guarda que so pergunta se ele existe.
    /// </summary>
    internal static class FatherCallbackWiring
    {
        private const string TellEvent = "dad_told_5";
        private const string Gate = "day3_ready";
        private const string SystemsPrefab = "Assets/Pungent/Prefabs/GAME_SYSTEMS.prefab";

        /// <summary>Quantos avisos ao pai e que trazem o carro dele a porta. De cinco.</summary>
        private const int Warnings = 4;

        [MenuItem("Pungent/Narrativa/Write Father Callback", false, 62)]
        internal static void Wire()
        {
            if (BuildGuard.Blocked("FatherCallback")) return;

            var log = new List<string>();
            WriteStep(log);
            AddRule(log);
            RaiseThreshold(log);

            AssetDatabase.SaveAssets();
            Debug.Log("[Pai] " + string.Join("\n[Pai] ", log));
        }

        // ------------------------------------------------------------------

        private static void WriteStep(List<string> log)
        {
            var dad = PhoneThreadEditing.Load("PHN_Dad");
            if (dad == null) { log.Add("AVISO: `PHN_Dad` nao encontrado."); return; }

            var steps = PhoneThreadEditing.ReadSteps(dad);

            int gateAt = -1, evidenceAt = -1;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] == null) continue;
                if (steps[i].WaitForEvent == Gate) gateAt = i;
                if (steps[i].WaitForEvent == "evidence_found") evidenceAt = i;
            }

            if (gateAt >= 0 && Raises(steps[gateAt], TellEvent))
            {
                log.Add("o passo do `" + Gate + "` ja estava completo.");
                return;
            }

            var step = Build();

            if (gateAt >= 0)
            {
                steps[gateAt] = step;
                log.Add("REPARADO: o passo do `" + Gate + "` existia sem a escolha honesta.");
            }
            else
            {
                // **Antes da fotografia e depois de tudo o resto.** O fio e linear: o
                // servico so comeca a contar um passo quando o anterior fechou. Um
                // passo preso ao `day3_ready` colocado depois do `evidence_found`
                // nunca chegaria a ser entregue, porque o da fotografia termina o fio.
                int at = evidenceAt >= 0 ? evidenceAt : steps.Count;
                steps.Insert(at, step);
                log.Add("passo novo do pai em " + at + ", preso ao `" + Gate + "`.");
            }

            PhoneThreadEditing.Write(dad, dad.ContactName, steps);
        }

        private static PhoneConversationDefinition.Step Build()
        {
            return new PhoneConversationDefinition.Step
            {
                // Ele nao pergunta: informa. E a mae ia telefonar ao Rui.
                Line = "Your mother wanted to ring that flatmate of yours about the damp. "
                     + "I told her you said it was all fine, so she has left it.",

                WaitForEvent = Gate,

                // Chega com ele ainda em casa, ja de chaves na mao. Sessenta segundos
                // para nao cair em cima do objectivo de sair.
                DelaySeconds = 60f,
                TypingSeconds = 4f,
                ReplyDelaySeconds = 40f,
                EndsThread = false,

                Choices = new[]
                {
                    new DialogueChoice
                    {
                        // Pedir que ela telefone e querer uma testemunha sem conseguir
                        // dize-lo. O pai repara e nao percebe — como sempre.
                        Text = "Tell her to ring him. I would rather she did.",
                        Reply = "That is not like you. Alright, I will say something.",
                        Tone = DialogueTone.Honest,
                        RaisesEvent = TellEvent
                    },
                    new DialogueChoice
                    {
                        // Quatro palavras, e fecham a porta a unica pessoa de fora que
                        // ia fazer contacto com aquela casa.
                        Text = "Good. It is all fine.",
                        Reply = "That is what I said.",
                        Tone = DialogueTone.Evasive
                    }
                }
            };
        }

        private static bool Raises(PhoneConversationDefinition.Step step, string id)
        {
            if (step?.Choices == null) return false;
            foreach (var choice in step.Choices)
                if (choice != null && choice.RaisesEvent == id) return true;
            return false;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// A regra que faz o `dad_told_5` contar. Acrescenta sem substituir: o
        /// `EditorConfigure` do blackboard troca o array inteiro, e escrever so esta
        /// apagava as outras — o sintoma seriam os quatro finais a deixarem de ser
        /// alcancaveis, sem erro nenhum. Licao ja escrita no `FatherEndingWiring`.
        /// </summary>
        private static void AddRule(List<string> log)
        {
            var blackboard = Object.FindObjectOfType<NarrativeBlackboard>(true);
            if (blackboard == null) { log.Add("AVISO: sem NarrativeBlackboard na cena."); return; }

            var so = new SerializedObject(blackboard);
            var rules = so.FindProperty("rules");

            for (int i = 0; i < rules.arraySize; i++)
                if (rules.GetArrayElementAtIndex(i).FindPropertyRelative("EventId").stringValue == TellEvent)
                {
                    log.Add("a regra do `" + TellEvent + "` ja estava na tabela.");
                    return;
                }

            rules.arraySize++;
            var rule = rules.GetArrayElementAtIndex(rules.arraySize - 1);
            rule.FindPropertyRelative("EventId").stringValue = TellEvent;
            rule.FindPropertyRelative("Target").enumValueIndex =
                (int)NarrativeBlackboard.Variable.FatherWarned;
            rule.FindPropertyRelative("Amount").intValue = 1;
            rule.FindPropertyRelative("Once").boolValue = true;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(blackboard);
            log.Add("regra nova: `" + TellEvent + "` -> FatherWarned.");
        }

        /// <summary>
        /// Quatro de cinco, e nao tres.
        ///
        /// **Escrito na instancia e nao no default do C#.** Mudar o valor por omissao
        /// de um `[SerializeField]` nao mexe em instancias que ja estao na cena — e
        /// uma das armadilhas ja pagas por este projecto, e o sintoma aqui seria o
        /// limiar continuar em tres com o codigo a dizer quatro.
        ///
        /// **E no prefab, nao na cena aberta — que foi a metade que faltava.**
        ///
        /// A primeira versao disto escrevia em `FindObjectOfType&lt;EndingSelector&gt;`,
        /// ou seja no exemplar da cena que por acaso estivesse aberta. Resultado
        /// medido: o apartamento com 4 e o `GAME_SYSTEMS.prefab` com 3, e as outras
        /// duas cenas a trazerem o 3 do prefab. Numa passagem normal nao se nota,
        /// porque o `GAME_SYSTEMS` do apartamento e o primeiro e e esse que
        /// sobrevive as trocas de cena — mas quem abrisse a garagem ou a estrada
        /// sozinhas jogava com um limiar diferente, e nada dizia isso.
        ///
        /// Escrito no prefab, todas as cenas concordam. A seguir limpa-se a
        /// alteracao local em qualquer instancia aberta: um override com o mesmo
        /// valor nao faz mal nenhum hoje e passa a tapar o prefab no dia em que
        /// alguem mudar o numero.
        /// </summary>
        private static void RaiseThreshold(List<string> log)
        {
            var root = PrefabUtility.LoadPrefabContents(SystemsPrefab);
            if (root == null) { log.Add("AVISO: sem " + SystemsPrefab + "."); return; }

            var target = root.GetComponentInChildren<EndingSelector>(true);
            if (target == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                log.Add("AVISO: sem EndingSelector dentro do " + SystemsPrefab + ".");
                return;
            }

            var so = new SerializedObject(target);
            var field = so.FindProperty("warningsForFather");
            if (field == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                log.Add("AVISO: `warningsForFather` nao encontrado.");
                return;
            }

            int was = field.intValue;
            field.intValue = Warnings;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, SystemsPrefab);
            PrefabUtility.UnloadPrefabContents(root);

            log.Add("limiar do final do pai no prefab: " + was + " -> " + Warnings +
                    " (de cinco oportunidades).");
            log.Add(ClearSceneOverrides());
        }

        /// <summary>
        /// Tira o `warningsForFather` local a qualquer `EndingSelector` que esteja
        /// numa cena aberta, para o prefab voltar a mandar.
        ///
        /// Um override com o mesmo valor e invisivel e inofensivo ate ao dia em que
        /// o numero mudar no prefab e uma cena continuar com o antigo — que e
        /// exactamente a divergencia que esta funcao existe para fechar, so que
        /// adiada.
        /// </summary>
        private static string ClearSceneOverrides()
        {
            int cleared = 0, skipped = 0;

            foreach (var selector in Object.FindObjectsOfType<EndingSelector>(true))
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(selector)) { skipped++; continue; }

                var so = new SerializedObject(selector);
                var field = so.FindProperty("warningsForFather");
                if (field == null || !field.prefabOverride) continue;

                PrefabUtility.RevertPropertyOverride(field, InteractionMode.AutomatedAction);
                cleared++;
            }

            if (cleared == 0 && skipped == 0) return "nenhuma cena aberta com EndingSelector.";
            return "alteracoes locais limpas: " + cleared +
                   (skipped > 0 ? " (" + skipped + " fora de prefab, deixados como estao)" : "");
        }
    }
}
