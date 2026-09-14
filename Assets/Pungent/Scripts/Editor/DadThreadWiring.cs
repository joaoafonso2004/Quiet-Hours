using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Pungent.Dialogue;
using Pungent.Interaction;

namespace Pungent.EditorTools
{
    /// <summary>
    /// O fio do Pai, esticado ao jogo inteiro.
    ///
    /// ---
    ///
    /// **O que estava errado.** O primeiro passo esperava por `day_one_done` — as
    /// 02:47 do fim do Dia 1. Ou seja: o Tomas muda-se, passa uma noite e um dia
    /// inteiro numa casa nova, e o pai nao diz nada. A jogar isso nao le como um pai
    /// ocupado, le como um contacto que ainda nao foi implementado.
    ///
    /// ---
    ///
    /// **Tres regras, e as tres saem do que o jogo ja e.**
    ///
    /// *Ele e lento.* Um pai nao responde em dois segundos e nao responde sempre.
    /// Os `ReplyDelaySeconds` sao de dezenas de segundos de proposito: a resposta
    /// chega quando o jogador ja esta a fazer outra coisa, e e ai que um telemovel
    /// parece um telemovel. Ha mensagens dele que **nao esperam por resposta
    /// nenhuma** — sao as melhores.
    ///
    /// *Ele fala de casa.* Sopa, o carro, o domingo, a mae. Nunca do apartamento,
    /// nunca do Rui, nunca de nada que tenha a ver com o que se esta a passar. E a
    /// regra de escrita do projecto ao contrario: enquanto tudo o resto fica
    /// estranho, ele continua exactamente igual — e a normalidade dele, chegando ao
    /// ecra a meio de uma noite errada, e que mede o quanto a outra coisa ja andou.
    ///
    /// *O que o Tomas responde vem do que aconteceu.* E para isso que servem as
    /// escolhas. Depois de o Rui comentar o carro, uma das respostas ao pai e
    /// "there is a bloke here who watches the street"; depois da noite das 02:47,
    /// uma delas e dizer-lhe que nao dormiu. O pai nunca percebe. Le sempre a
    /// resposta pelo lado banal e devolve conselho pratico, que e a coisa mais
    /// solitaria que este fio pode fazer.
    ///
    /// ---
    ///
    /// **Nao fecha nada.** Nenhum passo do capitulo espera por este fio, e nenhuma
    /// mensagem daqui levanta acontecimentos. Se o jogador nunca abrir o telemovel,
    /// nao perde progresso nenhum — perde o pai, que e outra coisa.
    ///
    /// Re-executavel.
    /// </summary>
    internal static class DadThreadWiring
    {
        [MenuItem("Pungent/Dialogue/Wire Dad Thread", false, 44)]
        internal static void Wire()
        {
            var thread = PhoneThreadEditing.Load("PHN_Dad");
            if (thread == null)
            {
                Debug.LogError("[Pai] PHN_Dad nao encontrado. Correr " +
                               "'Pungent/Dialogue/Build Phone Threads' primeiro.");
                return;
            }

            // Sem porta: ele e o pai dele, esta no telemovel desde o primeiro
            // segundo. O que muda e quando ele **fala**, nao quando existe.
            PhoneThreadEditing.SetUnlock(thread, string.Empty);

            var steps = new List<PhoneConversationDefinition.Step>();

            // ---- A noite da mudanca -------------------------------------------
            // Chega enquanto ele ainda tem caixas na mao. Nao pergunta nada de
            // dificil e nao espera resposta: e so um pai a confirmar que o filho
            // chegou.
            steps.Add(Say("Did you get there all right? Your mother wants to know if "
                        + "the room is damp.",
                "prologue_keys_taken", 40f, 3f, 55f,
                Choice("All fine. Bigger than the photos.",
                       "Good. Open a window anyway, first week is the worst.", DialogueTone.Calm),
                Choice("It is a room, Dad.",
                       "It is a room you pay for. Open a window.", DialogueTone.Edgy)));

            steps.Add(Say("Your mother put soup in the KITCHEN box. Get it into the fridge before bed.",
                "boxes_carried", 20f, 2f, 35f,
                Choice("I found it.",
                       "Good. Eat some while you are at it.", DialogueTone.Calm),
                Choice("I am still unpacking.",
                       "Then keep going. It will thaw.", DialogueTone.Edgy)));

            steps.Add(Say("Your mother wants a photo of the room. I told her you were still unpacking.",
                "kitchen_ready", 15f, 3f, 35f,
                Choice("Tell her tomorrow.",
                       "I did. She says before noon.", DialogueTone.Calm),
                Choice("It is not ready.",
                       "That is why she wants the photo.", DialogueTone.Edgy)));

            steps.Add(Say("Lock the door. Then sleep. The boxes will still be there tomorrow.",
                "prologue_door_locked", 8f, 2f, 25f,
                Choice("Already locked.",
                       "Good. Habit worth keeping.", DialogueTone.Calm),
                Choice("I know, Dad.",
                       "Then I have done my job.", DialogueTone.Edgy)));

            // ---- Dia 1 ---------------------------------------------------------
            steps.Add(Say("Morning. Did you eat something that was not bread?",
                "day1_kitchen", 120f, 2f, 60f,
                Choice("I am eating. Stop.",
                       "That is not a yes.", DialogueTone.Calm),
                Choice("Not yet.",
                       "There it is. Eat.", DialogueTone.Honest)));

            // A primeira em que o que aconteceu no jogo entra na resposta. O Rui
            // acabou de lhe devolver a esquina onde o carro esta estacionado.
            steps.Add(Say("Did you ever sort out that noise in the car?",
                "day1_car_remarked", 100f, 3f, 75f,
                Choice("Not yet. Parts are expensive.",
                       "They are. Do not buy the cheap discs, you will pay twice.",
                       DialogueTone.Calm),
                Choice("The bloke here knew what I drive. I never told him.",
                       "Small building. People look out of windows, Tomas.",
                       DialogueTone.Honest),
                Choice("It is fine.",
                       "It is not fine, it has been six months.", DialogueTone.Evasive)));

            steps.Add(Say("Sunday still on? She has already bought too much fish for three people.",
                "day1_deal", 130f, 2f, 90f,
                Choice("I will be there.",
                       "Good. Bring nothing, she has bought everything twice.", DialogueTone.Calm),
                Choice("I will try.",
                       "That means no. I will tell her you are working.", DialogueTone.Evasive)));

            // **As tres do Dia 1 morrem quando o Dia 1 morre.**
            //
            // Os atrasos delas sao longos de proposito — cento e vinte segundos, cem,
            // cento e trinta — porque um pai nao responde logo. Mas um jogador que
            // despache o Dia 1 e se deite recebia-as **as 02:47**, e a primeira diz
            // "Morning". Foi reportado a jogar exactamente assim.
            //
            // O `StaleAfterEvent` salta a mensagem em vez de a entregar fora de horas.
            // Perde-se uma fala; entregar uma manha a meio da noite perdia a cena
            // inteira.
            for (int i = 0; i < steps.Count; i++)
                steps[i].StaleAfterEvent = "day_one_done";

            // ---- A noite das 02:47 ---------------------------------------------
            steps.Add(Say("Still awake? Saw you were online.",
                "day_one_done", 18f, 2f, 50f,
                Choice("Internet went down. Fixing it.",
                       "At three in the morning. Right.", DialogueTone.Calm),
                Choice("Cannot sleep.",
                       "New places do that. Give it a week.", DialogueTone.Honest)));

            steps.Add(Say("Your mother froze some soup for you. It has your name taped on it.",
                "router_restarted", 55f, 3f, 0f));

            steps.Add(Say("Go to bed. You have that early one on Tuesday and you paid for it yourself.",
                "talked_to_rui", 70f, 2f, 0f));

            // ---- Dia 2 ---------------------------------------------------------
            steps.Add(Say("Morning. You were still up when I went to bed, were you not.",
                "day_two", 90f, 3f, 80f,
                Choice("I slept.",
                       "Hm.", DialogueTone.Evasive),
                Choice("Not much. The flat makes noises.",
                       "All flats do. Ours creaks when the heating goes off.",
                       DialogueTone.Honest)));

            steps.Add(Say("Is that flatmate of yours alright? You never say anything about him.",
                "day_two", 260f, 4f, 110f,
                Choice("He is fine. Keeps to himself.",
                       "That is the best kind. Ours downstairs plays music at two.",
                       DialogueTone.Evasive),
                Choice("He is always up when I am.",
                       "Students. He will burn out by December.", DialogueTone.Honest),
                Choice("I do not want to talk about him.",
                       "Alright. Sorry I asked.", DialogueTone.Edgy)));

            // ---- Dia 3 e depois -------------------------------------------------
            steps.Add(Say("Are you eating? Properly, not that thing you call eating.",
                "day3_chores", 120f, 2f, 70f,
                Choice("I am eating.",
                       "Good.", DialogueTone.Calm),
                Choice("I am not really hungry lately.",
                       "That is your mother's side. Eat anyway.", DialogueTone.Honest)));

            steps.Add(Say("Did you get them in the end? Your mother is still asking about Sunday.",
                "evidence_found", 75f, 3f, 0f));

            PhoneThreadEditing.Write(thread, "Dad", steps);

            Debug.Log($"[Pai] Fio do Pai reescrito: {steps.Count} mensagens, da noite da " +
                      "mudanca ao Dia 3. Comeca em `prologue_keys_taken` e nao em " +
                      "`day_one_done` — o pai deixou de aparecer so as 02:47.");
        }

        private static PhoneConversationDefinition.Step Say(string line, string waitFor,
            float delay, float typing, float replyDelay, params DialogueChoice[] choices)
        {
            return new PhoneConversationDefinition.Step
            {
                Line = line,
                WaitForEvent = waitFor,
                DelaySeconds = delay,
                TypingSeconds = typing,
                ReplyDelaySeconds = replyDelay,
                Choices = choices != null && choices.Length > 0 ? choices : null
            };
        }

        private static DialogueChoice Choice(string text, string reply, DialogueTone tone)
        {
            return new DialogueChoice { Text = text, Reply = reply, Tone = tone };
        }
    }
}
