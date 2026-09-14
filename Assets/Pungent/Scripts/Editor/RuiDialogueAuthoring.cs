using UnityEditor;
using UnityEngine;
using Pungent.Dialogue;
using Beat = Pungent.Dialogue.DialogueSequenceDefinition.Beat;

namespace Pungent.EditorTools
{
    /// <summary>
    /// Escreve as conversas do Rui que faltavam, como assets.
    ///
    /// ---
    ///
    /// **A que faltava mesmo.** O inventario enganava: o vendedor, o estranho da
    /// estrada, a chamada ouvida na garagem, os avisos do quarto e o comentario ao
    /// carro no Dia 1 ja estavam todos escritos — mas inline nos componentes, e
    /// nao como assets `DLG_`. O que nao existia era conversa **com escolhas**
    /// depois das 02:47.
    ///
    /// **Escreveram-se duas e ficou uma.** A do Dia 3 — o Rui a perguntar a hora,
    /// o caminho e se ias sozinho, a porta da rua — foi apagada: o
    /// `DoorConversation` ja faz essa cena no mesmo dia, com as mesmas perguntas
    /// de logistica e o mesmo par confrontar/calar-se. Duas versoes da mesma
    /// conversa na mesma manha nao sao o dobro da tensao; sao a primeira a
    /// desmentir a segunda.
    ///
    /// Morreu com ela uma fala que nao tinha copia — *"There is no signal past the
    /// gate at the industrial park"*, o unico sitio em que ele mostrava saber o
    /// **destino** e nao so a distancia. Se algum dia essa informacao fizer falta
    /// antes do Dia 4, o sitio dela e o `DoorConversation` e nao um segundo
    /// dialogo.
    ///
    /// ---
    ///
    /// **As regras de voz, tiradas do que ja la estava** e nao inventadas:
    ///
    /// - Ingles seco, **sem contraccoes**. "You have got", "It is", "I will". E o
    ///   que faz o Rui soar ligeiramente formal de mais para a situacao.
    /// - Frases curtas. Ninguem faz discursos numa cozinha.
    /// - Tres respostas: uma calma, uma cortante, e calar-se. O silencio **e**
    ///   sempre uma opcao, e o Rui tem sempre resposta para ele — que e pior do
    ///   que nao ter.
    /// - O Rui nunca ameaca e nunca se defende. As respostas as opcoes cortantes
    ///   desviam com calma, porque um homem que se irrita e um homem que se
    ///   explica.
    /// - **A ultima fala traz o facto que ele nao devia ter.** E a estrutura das
    ///   duas conversas que ja existiam — o "You have got that early class" das
    ///   02:47 — e e o motor do jogo todo: nada acontece, e mesmo assim fica mal.
    ///
    /// Re-executavel: reescreve os assets no sitio, mantendo os GUIDs e portanto
    /// as ligacoes de quem ja lhes aponta.
    /// </summary>
    internal static class RuiDialogueAuthoring
    {
        private const string Folder = "Assets/Pungent/Dialogue/Sequences/";

        [MenuItem("Pungent/Narrativa/Write Rui Dialogues", false, 60)]
        internal static void Write()
        {
            WriteClimaxDoor();
            RetireDayThree();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Dialogos] DLG_Rui_Climax_Door escrito em " + Folder);
        }

        /// <summary>
        /// Apaga o asset da conversa do Dia 3, se ainda la estiver.
        ///
        /// Deixar de o escrever nao chega: o asset fica no disco e continua a ser
        /// carregavel por caminho, e a proxima pessoa que procurar por
        /// `DLG_Rui_Day3_Leaving` encontra-o e volta a liga-lo sem saber porque e
        /// que ele tinha saido. O `.meta` vai com ele, que e a unica maneira
        /// correcta de apagar um asset neste projecto.
        /// </summary>
        private static void RetireDayThree()
        {
            const string path = Folder + "DLG_Rui_Day3_Leaving.asset";
            if (AssetDatabase.LoadAssetAtPath<DialogueSequenceDefinition>(path) == null) return;

            if (AssetDatabase.DeleteAsset(path))
                Debug.Log("[Dialogos] DLG_Rui_Day3_Leaving APAGADO: duplicava a cena que o " +
                          "DoorConversation ja faz no Dia 3.");
            else
                Debug.LogWarning("[Dialogos] Nao consegui apagar " + path + ". Apaga a mao, " +
                                 "com o `.meta`.");
        }

        /// <summary>
        /// Dia 5, atraves da porta do quarto.
        ///
        /// A §5 pede *"fala calma atraves da porta"*, e calma e a palavra que faz
        /// isto funcionar. Ele nao grita, nao bate, e nao acusa o Tomas de nada —
        /// pelo contrario, tira-lhe a culpa de cima. **"Nobody said you took
        /// anything."** e a fala mais violenta do jogo inteiro precisamente porque
        /// e tranquilizadora.
        ///
        /// O ultimo beat nao tem escolhas de proposito. A pergunta que fica nao e
        /// o que dizer: e se abre a porta, e essa responde-se com as maos.
        /// </summary>
        private static void WriteClimaxDoor()
        {
            var beats = new[]
            {
                new Beat
                {
                    Line = "You are back. I heard the car.",
                    Choices = new[]
                    {
                        Choice("I am tired. I am going to sleep.",
                               "You have not taken your shoes off. You always take them off at the door.", DialogueTone.Calm),
                        Choice("Get away from my door.",
                               "I am not at your door. I am in the hall.", DialogueTone.Edgy),
                        Choice("Say nothing",
                               "…I can see the light under it, Tomás.", DialogueTone.Silent)
                    }
                },
                // Dizia *"Vitor called me. He said you left in a hurry."* — o Tomas ja
                // nao sai de casa desde o corte de 11-08, e o Vitor nunca chegou a
                // ve-lo. Passa a ser sobre a caixa que ficou no patamar: a mesma
                // acusacao sem acusacao, com uma coisa que o Rui pode mesmo ter visto.
                //
                // A ultima resposta e a que fecha: ele nao diz que abriu a caixa. Diz
                // que sabe o que estava la dentro.
                new Beat
                {
                    Line = "That box was on the landing half the evening.",
                    Choices = new[]
                    {
                        Choice("I did not take anything.", "Nobody said you took anything.", DialogueTone.Honest),
                        Choice("It is just car parts.", "It is. I had a look at the label.", DialogueTone.Evasive),
                        Choice("Say nothing", "…You did not seem surprised by what was in it.", DialogueTone.Silent)
                    }
                },
                new Beat
                {
                    Line = "Open the door. We will sort it out and you can go to sleep."
                }
            };

            // Sem tempo limite: a escolha espera. Uma conversa atraves de uma porta
            // trancada em que o silencio decide sozinho ao fim de nove segundos
            // tirava ao jogador a unica coisa que ele ainda controla.
            Populate("DLG_Rui_Climax_Door", "Rui", beats, 0f, 3.0f);
        }

        // ------------------------------------------------------------------

        private static DialogueChoice Choice(string text, string reply, DialogueTone tone)
            => new DialogueChoice { Text = text, Reply = reply, Tone = tone };

        private static void Populate(string name, string speaker, Beat[] beats,
            float choiceSeconds, float hold)
        {
            string path = Folder + name + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<DialogueSequenceDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<DialogueSequenceDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.EditorPopulate(speaker, beats, choiceSeconds, hold);
            EditorUtility.SetDirty(asset);
        }
    }
}
