using System;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.Narrative;
using UnityEngine;

namespace Pungent.NPC
{
    /// <summary>
    /// Conversa guionada com o Rui na cozinha: apresenta o enquadramento do jogo
    /// (o quarto barato, o carro, o negócio das peças) e deixa o jogador escolher
    /// duas vezes.
    ///
    /// As escolhas convergem de propósito. O plano é explícito: as decisões mudam
    /// **como** o Rui te trata e a ameaça oculta, não o caminho da história.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuiStoryConversation : MonoBehaviour, IPlayerInteractable
    {
        [Serializable]
        public struct Beat
        {
            [TextArea] public string Line;
            [Tooltip("Vazio nos dois = fala corrida, sem escolha.")]
            [TextArea] public string ChoiceCalm;
            [TextArea] public string ChoiceEdgy;
            [Tooltip("Resposta do Rui se o jogador escolher a opção cortante.")]
            [TextArea] public string ReplyToEdgy;
            [Tooltip("Resposta do Rui se o jogador escolher a opção calma.")]
            [TextArea] public string ReplyToCalm;
        }

        [SerializeField] private WorldDialogueController dialogue;
        [SerializeField] private NpcMumbleVoice voice;
        [SerializeField] private PrototypeNpcRoutine routine;
        [SerializeField] private NpcPhonePresence phonePresence;
        [SerializeField] private OpeningQuestDirector quest;

        [Header("Guiao")]
        [Tooltip("O guiao da conversa. Substitui os campos antigos de texto.")]
        [SerializeField] private DialogueSequenceDefinition conversation;
        [Tooltip("O que ele diz quando ja nao ha nada para falar. Uma vez so — a "
               + "seguir o prompt desaparece ate a historia lhe dar outra conversa.")]
        [SerializeField, TextArea] private string nothingToSayLine = "Not now.";

        [Tooltip("Reaccao do Tomas quando a conversa acaba. O Rui referiu a aula da "
               + "manha e o Tomas nunca lhe disse isso.")]
        [SerializeField, TextArea]
        private string afterConversationThought = "I never told him about Tuesday.";
        [SerializeField] private PlayerThoughtDirector thoughts;

        [SerializeField] private string speaker = "Rui";
        [SerializeField, Min(1f)] private float choiceSeconds = 9f;
        [SerializeField, Min(0.8f)] private float lineHold = 2.6f;

        // Formato antigo, mantido so para a migracao poder le-lo.
        [SerializeField, HideInInspector]
        private Beat[] beats =
        {
            // Ele acabou de mandar mensagem ao Tomas e mandou-o reiniciar o router.
            // Nao pode estranhar que ele esteja acordado nem dar-lhe a noticia da
            // avaria: as duas coisas ja passaram entre os dois.
            new Beat
            {
                Line = "It came back. I saw the light on the box turn green from here.",
                ChoiceCalm = "Took a minute. It is an old thing.",
                ChoiceEdgy = "It was ten steps. You could have gone yourself.",
                ReplyToCalm = "Everything in here is old. Thanks for getting up.",
                ReplyToEdgy = "I could have. You were already awake, though."
            },
            new Beat
            {
                Line = "Your car is the black one, right? The old sedan under the window.",
                ChoiceCalm = "It is. It needs a lot of work.",
                ChoiceEdgy = "How do you know which one is mine?",
                ReplyToCalm = "There is a man near the industrial park who sells parts cheap. I can send you the number.",
                ReplyToEdgy = "You park it in the same spot every night. Hard to miss."
            },
            new Beat
            {
                Line = "Get some sleep. You have got that early class.",
                ChoiceCalm = "",
                ChoiceEdgy = "",
                ReplyToCalm = "",
                ReplyToEdgy = ""
            }
        };

        /// <summary>
        /// Falas soltas para depois da conversa guionada. O Rui continua ali de pe
        /// na cozinha: se o jogador voltar a falar com ele e nao acontecer nada, ele
        /// deixa de ser uma pessoa e passa a ser um botao gasto.
        ///
        /// Formato antigo, mantido so para a migracao poder le-lo.
        /// </summary>
        /// <remarks>
        /// **Estas falas sairam do jogo.** Ficam escritas aqui porque a razao por que
        /// sairam vale mais do que elas: eram seis dicas domesticas que ele repetia
        /// ao acaso sempre que se carregasse nele, e o efeito a jogar era um Rui
        /// prestavel e inesgotavel — exactamente o contrario do homem que o resto do
        /// capitulo esta a construir. Um vizinho que responde sempre nao mete medo
        /// nenhum, e um que responde a decima vez com uma dica sobre a agua quente
        /// deixa de ser uma pessoa e passa a ser uma maquina de falas.
        ///
        /// Agora ha uma linha so, dita uma vez, e a seguir ele cala-se ate a historia
        /// lhe dar outra conversa. Falar com ele volta a ser uma coisa que **acontece
        /// quando ha alguma coisa**, e nao um botao.
        ///
        /// ---
        ///
        /// **Nenhuma destas podia dizer que horas sao.** O mesmo conjunto servia as
        /// duas conversas guionadas do Rui — a noite da mudanca e a das 02:47 — e o
        /// `SetConversation` troca o guiao mas nao troca isto. Estavam escritas so
        /// para a segunda: "Still awake?", "It is nearly tres", "Go on, you have got
        /// that class". Na noite em que o Tomas se muda, o Rui perguntava-lhe se ele
        /// ainda estava acordado as onze da noite e mandava-o dormir por causa de
        /// uma aula que o jogador ainda nao sabe que existe.
        ///
        /// A aula e pior do que fora de horas: **e o detalhe que ele devia repetir
        /// depois**. O plano poe o Pai a menciona-la por SMS, e o efeito e o Rui
        /// cita-la mais tarde. Mas o fio do Pai so abre no `day_one_done`, portanto
        /// no prologo o Rui estava a devolver uma informacao que ninguem lhe deu e
        /// que o jogador nunca leu — a revelacao chegava antes da causa, que e o
        /// mesmo erro que o `day1_car_remarked` ja teve de corrigir uma vez.
        /// </remarks>
        [SerializeField, HideInInspector]
        private string[] idleLines =
        {
            "The fridge does that all night. You stop hearing it after a week.",
            "Do not leave the water running. The bill is shared.",
            "The hot water takes a minute. Let it run.",
            "If the lock sticks, lift the door and then turn. Do not force it.",
            "I am usually up. Do not worry about making noise.",
            "The wall between the two rooms is thinner than it looks."
        };

        private int beatIndex;
        private bool running;
        private bool finished;
        private bool saidNothingToSay;
        private bool bypassQuestGate;
        private bool allowAfterConversationThought = true;
        private bool dialogueStareActive;
        private bool dialoguePhoneActive;

        /// <summary>Disparado quando a conversa guionada termina.</summary>
        public event System.Action Finished;

        /// <summary>
        /// Troca o guiao e recomeca do principio.
        ///
        /// O Rui tem duas conversas guionadas — a da noite em que o Tomas se mudou e
        /// a das 02:47 — e nao vale a pena dois componentes para isso: mudam as
        /// falas, nao o comportamento.
        /// </summary>
        public void SetConversation(DialogueSequenceDefinition definition,
            bool ignoreQuestGate = false, bool showAfterThought = true)
        {
            if (dialogueStareActive) routine?.EndDialogueStare();
            EndDialoguePhone();
            conversation = definition;
            beatIndex = 0;
            running = false;
            finished = false;
            bypassQuestGate = ignoreQuestGate;
            allowAfterConversationThought = showAfterThought;
            dialogueStareActive = false;

            // Guiao novo, boca aberta outra vez. E isto que faz o "Talk to Rui"
            // voltar quando a historia tem alguma coisa para ele dizer, e so ai.
            saidNothingToSay = false;
        }

        // ---- fonte do guiao -------------------------------------------------
        // O asset ganha sempre. Os campos antigos so respondem enquanto a migracao
        // nao tiver corrido nesta cena.

        private bool UsingAsset => conversation != null && conversation.BeatCount > 0;

        private int BeatCount => UsingAsset ? conversation.BeatCount
                                            : (beats != null ? beats.Length : 0);

        private string SpeakerName => UsingAsset && !string.IsNullOrWhiteSpace(conversation.Speaker)
            ? conversation.Speaker
            : speaker;

        private float ChoiceWindow => UsingAsset ? conversation.ChoiceSeconds : choiceSeconds;

        private float Hold => UsingAsset ? conversation.LineHold : lineHold;

        public string Prompt
        {
            get
            {
                if (running) return string.Empty;

                // Ja lhe perguntaram e ele ja disse que nao. Insistir nao da nada, e
                // um prompt que continua aceso a prometer conversa e uma mentira da
                // interface — foi ele que fez o Rui parecer inesgotavel.
                if (finished && saidNothingToSay) return string.Empty;

                if (!bypassQuestGate
                    && quest != null
                    && quest.Current < OpeningQuestDirector.Step.TalkToRui)
                    return string.Empty;
                return "Talk to Rui";
            }
        }
        public bool HoldToInteract => false;

        private void Awake()
        {
            if (routine == null) routine = GetComponent<PrototypeNpcRoutine>();
            if (phonePresence == null) phonePresence = GetComponent<NpcPhonePresence>();
            if (voice == null) voice = GetComponentInChildren<NpcMumbleVoice>(true);
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
            if (quest == null) quest = FindObjectOfType<OpeningQuestDirector>();
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (running || dialogue == null || dialogue.IsBusy) return;
            if (!bypassQuestGate
                && quest != null
                && quest.Current < OpeningQuestDirector.Step.TalkToRui)
                return;

            if (finished) { PlayNothingToSay(); return; }

            running = true;
            beatIndex = 0;
            routine?.SetConversationActive(true);
            PlayBeat();
        }

        /// <summary>
        /// A unica coisa que ele diz quando nao ha nada para dizer, e so uma vez.
        ///
        /// Ver a nota em <see cref="idleLines"/>: o que estava aqui era uma roleta de
        /// seis dicas domesticas que nunca se esgotava.
        /// </summary>
        private void PlayNothingToSay()
        {
            if (string.IsNullOrWhiteSpace(nothingToSayLine)) return;

            // Marcado **antes** de falar, pela mesma razao do `RuiDenial`: a fala
            // demora segundos e o prompt continua vivo durante eles.
            saidNothingToSay = true;

            string line = nothingToSayLine;
            float hold = lineHold;

            running = true;
            routine?.SetConversationActive(true);
            routine?.GlanceAtPlayer(hold + 1.5f);

            // **Uma fala solta nao e uma conversa, e por isso nao prende ninguem.**
            //
            // Isto usava a forma curta do `ShowReaction`, que traz `autoClose: false`
            // e `blocksInteraction: true`. Enquanto o bloqueio so tirava as maos, o
            // pior que fazia era obrigar a um clique para dispensar. Depois de o
            // bloqueio passar a travar tambem os pes e a cabeca, virou armadilha: o
            // jogador ficava preso de pe em frente ao Rui, sem poder virar-se nem
            // afastar-se, com a mira em cima dele e o prompt "Talk to Rui" a
            // aparecer outra vez assim que a fala fechava. Um clique para sair, e o
            // clique dava outra fala. **Ciclo infinito, e o unico bug que impede o
            // prologo de acabar.**
            //
            // Fecha-se sozinha e nao tira nada ao jogador, tal como o `RuiCarRemark`:
            // e uma coisa dita por um homem que esta ali de pe, e nao um dialogo.
            dialogue.ShowReaction(SpeakerName, line, voice, hold, EndIdleLine,
                autoClose: true, blocksInteraction: false);
        }

        private void EndIdleLine()
        {
            running = false;
            routine?.SetConversationActive(false);
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

        private void PlayBeat()
        {
            if (beatIndex >= BeatCount) { Finish(); return; }

            string line, calm, edgy;
            string[] allChoices = null;
            bool usePhone = false;

            if (UsingAsset)
            {
                var beat = conversation.BeatAt(beatIndex);
                line = beat.Line;
                usePhone = beat.UsePhone;
                calm = beat.HasChoices ? beat.Choices[0].Text : null;
                edgy = beat.HasChoices ? beat.Choices[1].Text : null;

                // O asset aguenta N respostas; a terceira e tipicamente o silencio.
                if (beat.HasChoices)
                {
                    allChoices = new string[beat.Choices.Length];
                    for (int i = 0; i < beat.Choices.Length; i++)
                        allChoices[i] = beat.Choices[i].Text;
                }
            }
            else
            {
                Beat beat = beats[beatIndex];
                line = beat.Line;
                calm = beat.ChoiceCalm;
                edgy = beat.ChoiceEdgy;
            }

            bool hasChoice = !string.IsNullOrWhiteSpace(calm) && !string.IsNullOrWhiteSpace(edgy);
            if (usePhone)
            {
                dialoguePhoneActive = true;
                phonePresence?.BeginDialoguePhone();
            }

            if (!hasChoice)
            {
                // A ultima fala fecha-se sozinha. E uma despedida — "get some sleep" —
                // e ficar a espera de um clique para a dispensar deixava a caixa
                // pendurada no ecra com a conversa ja terminada.
                bool isLast = beatIndex == BeatCount - 1;
                dialogue.ShowReaction(SpeakerName, line, voice, Hold, NextBeat, autoClose: isLast);
                return;
            }

            bool started = allChoices != null
                ? dialogue.BeginChoice(SpeakerName, line, voice, allChoices, ChoiceWindow, OnChoice)
                : dialogue.BeginChoice(SpeakerName, line, voice, calm, edgy, ChoiceWindow, OnChoice);

            // Se o diálogo estivesse ocupado, não se perde a conversa: tenta a seguir.
            if (!started)
            {
                EndDialoguePhone();
                running = false;
            }
        }

        private void OnChoice(int index)
        {
            // A pose cobre a fala e o tempo em que espera pelo numero. A resposta
            // pode pedir outro gesto, em especial o Stare provocado pelo silencio.
            EndDialoguePhone();

            DialogueTone tone;
            string reply;

            if (UsingAsset)
            {
                var beat = conversation.BeatAt(beatIndex);
                var choice = beat.Choices[Mathf.Clamp(index, 0, beat.Choices.Length - 1)];
                // O tom vem do asset, nao da posicao: trocar a ordem das opcoes no
                // inspector deixa de inverter em silencio o efeito na ameaca.
                tone = choice.Tone;
                reply = choice.Reply;

                // Uma escolha pode mexer na historia e nao so no tom da casa. Nao ha
                // nenhuma a usar isto no fio do Rui hoje — quem o usa e o telemovel,
                // no fio do Pai — mas o campo vive no `DialogueChoice`, que e o mesmo
                // dos dois lados, e um campo honrado num consumidor e ignorado no
                // outro e uma armadilha a espera de quem escrever a proxima conversa.
                if (choice.RaisesAnything)
                    Pungent.Narrative.ChapterDirector.Resolve()?.Notify(choice.RaisesEvent);
            }
            else
            {
                Beat beat = beats[beatIndex];
                tone = index == 1 ? DialogueTone.Edgy : DialogueTone.Calm;
                reply = tone == DialogueTone.Edgy ? beat.ReplyToEdgy : beat.ReplyToCalm;
            }

            // O tom alimenta a ameaca ja desde o prologo. Silencio nao e uma opcao
            // neutra para quem acabou de te fazer uma pergunta: ele para e encara-te
            // durante a resposta.
            routine?.ApplyDialogueChoice(tone);
            if (tone == DialogueTone.Silent)
            {
                dialogueStareActive = true;
                routine?.BeginDialogueStare();
            }

            // O Rui olha para ti ao responder, independentemente do que escolheste.
            routine?.GlanceAtPlayer(Hold + 1.5f);

            if (string.IsNullOrWhiteSpace(reply)) { NextBeat(); return; }

            dialogue.ShowReaction(SpeakerName, reply, voice, Hold, NextBeat);
        }

        private void NextBeat()
        {
            EndDialoguePhone();

            if (dialogueStareActive)
            {
                routine?.EndDialogueStare();
                dialogueStareActive = false;
            }

            beatIndex++;
            if (beatIndex >= BeatCount) { Finish(); return; }
            PlayBeat();
        }

        private void Finish()
        {
            EndDialoguePhone();

            if (dialogueStareActive)
            {
                routine?.EndDialogueStare();
                dialogueStareActive = false;
            }

            running = false;
            finished = true;
            routine?.SetConversationActive(false);

            // O Rui acabou de referir a aula da manha. O Tomas nunca lhe disse isso
            // — e a primeira coisa do jogo que nao bate certo. Sem esta reaccao a
            // fala passava como simpatia e o gancho perdia-se: o jogador so o
            // apanharia muito depois, se apanhasse.
            //
            // Duvida e nao acusacao, de proposito. Ele nao tem a certeza, e e essa
            // a diferenca entre inquietacao e um aviso do guiao.
            if (allowAfterConversationThought && !string.IsNullOrWhiteSpace(afterConversationThought))
                thoughts?.Think("rui_knew_class", afterConversationThought, 5, true, 4.0f);

            quest?.NotifyConversationFinished();
            Finished?.Invoke();
        }

        private void EndDialoguePhone()
        {
            if (!dialoguePhoneActive) return;
            phonePresence?.EndDialoguePhone();
            dialoguePhoneActive = false;
        }
    }
}
