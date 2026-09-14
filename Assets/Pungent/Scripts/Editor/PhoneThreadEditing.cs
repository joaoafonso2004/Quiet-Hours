using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Pungent.Interaction;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Mexer num fio de mensagens que ja existe.
    ///
    /// Existe porque tres ferramentas diferentes precisam de escrever no **mesmo**
    /// fio — o do Rui — sem se atropelarem: o prologo mete-lhe a mensagem com que
    /// ele da o numero, o Dia 2 tem la a conversa do router, e o Dia 5 acrescenta
    /// as mensagens da noite. Antes, cada capitulo novo criava um asset novo, e foi
    /// assim que o telemovel passou a ter dois contactos chamados "Rui".
    ///
    /// Tudo aqui e idempotente **pelo texto**: acrescentar duas vezes a mesma linha
    /// nao a duplica. As ferramentas deste projecto sao re-executaveis e alguem vai
    /// correr as tres pela mesma ordem que eu nao previ.
    /// </summary>
    internal static class PhoneThreadEditing
    {
        private const string Folder = "Assets/Pungent/Dialogue/Phone/";

        internal static PhoneConversationDefinition Load(string fileName)
        {
            return AssetDatabase.LoadAssetAtPath<PhoneConversationDefinition>(Folder + fileName + ".asset");
        }

        /// <summary>Os passos que o fio tem hoje, para serem mexidos e reescritos.</summary>
        internal static List<PhoneConversationDefinition.Step> ReadSteps(
            PhoneConversationDefinition thread)
        {
            var steps = new List<PhoneConversationDefinition.Step>();
            if (thread == null) return steps;
            for (int i = 0; i < thread.StepCount; i++) steps.Add(thread.StepAt(i));
            return steps;
        }

        internal static bool HasLine(PhoneConversationDefinition thread, string line)
        {
            if (thread == null || string.IsNullOrEmpty(line)) return false;
            for (int i = 0; i < thread.StepCount; i++)
            {
                var step = thread.StepAt(i);
                if (step != null && step.Line == line) return true;
            }
            return false;
        }

        internal static void Write(PhoneConversationDefinition thread, string contact,
            List<PhoneConversationDefinition.Step> steps)
        {
            if (thread == null) return;
            thread.EditorPopulate(contact, steps.ToArray());
            EditorUtility.SetDirty(thread);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Quem poe este contacto no telemovel. Vazio = ja la estava.
        /// </summary>
        internal static void SetUnlock(PhoneConversationDefinition thread, string eventId)
        {
            if (thread == null) return;
            if (thread.UnlockEvent == eventId) return;
            thread.EditorSetUnlock(eventId);
            EditorUtility.SetDirty(thread);
            AssetDatabase.SaveAssets();
        }

        internal static PhoneConversationDefinition.Step Message(string line, string waitFor,
            float delay, float typing, bool endsThread = false)
        {
            return new PhoneConversationDefinition.Step
            {
                Line = line,
                WaitForEvent = waitFor,
                DelaySeconds = delay,
                TypingSeconds = typing,
                EndsThread = endsThread
            };
        }
    }
}
