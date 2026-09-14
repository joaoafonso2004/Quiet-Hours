using System.Collections;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.Narrative;
using UnityEngine;

namespace Pungent.NPC
{
    /// <summary>
    /// O Rui do outro lado da porta fechada, e a decisao de lhe responder ou nao.
    ///
    /// ---
    ///
    /// **E isto que da o nome ao Dia 3.** "Limites" nao e a gaveta mexida — e o
    /// momento em que o Tomas tem de decidir se diz alguma coisa. A gaveta e
    /// informacao; isto e a primeira vez que o jogador tem de **escolher entre
    /// confortavel e verdadeiro**, e sabendo que vai continuar a viver com a resposta.
    ///
    /// ---
    ///
    /// **A porta esta fechada e fica fechada.** Ninguem abre nada nesta cena, e essa
    /// e a decisao de encenacao inteira. Uma conversa cara a cara transforma isto num
    /// confronto, e um confronto tem um vencedor. Atraves da madeira ninguem ve a
    /// cara do outro: o jogador nao sabe se ele esta a sorrir, e o Rui pode dizer o
    /// que quiser sem que se lhe possa provar nada. Ver <see cref="MuffledThroughWalls"/>
    /// — a voz dele chega filtrada, e e o filtro que faz a cena.
    ///
    /// **Ele nao acusa nem se defende: pergunta.** As falas dele sao logistica pura —
    /// a que horas sais, vais sozinho, e longe. Sao perguntas que qualquer colega de
    /// casa faz e que, ditas atraves de uma porta a alguem que acabou de encontrar a
    /// gaveta aberta, deixam de ser inocentes sem nunca deixarem de o poder ser. **A
    /// regra de escrita do projecto: dramatico le-se como guiao, banal le-se como
    /// vigilancia.**
    ///
    /// **Calar-se e uma resposta, e nao a resposta segura.** Confrontar sobe a
    /// suspeita dele e a insubmissao do jogador; calar-se sobe o `RuiEmboldened` — o
    /// que ele aprendeu sobre o que pode fazer sem que ninguem diga nada.
    ///
    /// Sao duas variaveis e nao uma, e a diferenca importa. O <see cref="NarrativeBlackboard"/>
    /// tem escrito, com a cicatriz a mostrar, que ja houve uma versao em que os dois
    /// ramos somavam na **mesma** variavel e a ameaca subia jogasse o jogador como
    /// jogasse — um estado oculto que sobe sempre deixou de medir seja o que for. Aqui
    /// nao sobe a mesma coisa: sobem dois Ruis diferentes. Um que sabe que o outro
    /// reparou, e um que sabe que o outro nao vai dizer nada.
    ///
    /// Nao ha escolha limpa, que e a §6.1: as escolhas mudam o tom, nao a posicao.
    ///
    /// **Nao bloqueia nada.** O passo do capitulo nao espera por isto. Um jogador que
    /// esteja na cozinha quando ele bate ouve baterem, e mais nada acontece; a cena
    /// espera por ele estar no quarto, e desiste se ele nunca la for. Uma decisao que
    /// tranca um capitulo deixa de ser uma decisao.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorConversation : MonoBehaviour
    {
        [Header("Quando")]
        [Tooltip("Acontecimento que arma a cena. Ela ainda espera pelo jogador estar "
               + "do lado de dentro com a porta fechada.")]
        [SerializeField] private string armOnEvent = "day3_searched";

        [Tooltip("Levantado quando a cena acaba, seja qual for a resposta.")]
        [SerializeField] private string finishedEvent = "day3_door_talk";

        [Tooltip("Passado isto sem o jogador voltar ao quarto, ele desiste e vai-se "
               + "embora. A cena perde-se, e perder-se e melhor do que esperar para "
               + "sempre por alguem que foi fazer outra coisa.")]
        [SerializeField, Min(10f)] private float patienceSeconds = 240f;

        [Header("Onde")]
        [SerializeField] private GameObject rui;
        [Tooltip("A porta do quarto do Tomas. Tem de estar **fechada** para isto correr.")]
        [SerializeField] private DoorDragInteractable door;
        [Tooltip("Do lado de fora da porta, no corredor.")]
        [SerializeField] private Vector3 outsideSpot = new Vector3(-3.55f, 0f, -1.05f);
        [SerializeField] private float outsideYaw = 180f;

        [Tooltip("O jogador tem de estar a menos disto da porta, por dentro.")]
        [SerializeField, Min(1f)] private float insideRange = 4.5f;

        [Header("O que ele diz")]
        [SerializeField, TextArea] private string knockLine = "Tomás? You in there?";

        [Tooltip("A pergunta. Logistica pura — e por isso que funciona.")]
        [SerializeField, TextArea]
        private string question = "What time are you heading out tomorrow? "
                                + "Only I heard you on the phone. Is it far?";

        [SerializeField] private string confrontChoice = "Were you in my room?";
        [SerializeField] private string silentChoice = "Say nothing";

        [Tooltip("Resposta dele a acusacao. Nao nega com forca — corrige com calma, "
               + "que e pior: uma explicacao que funciona nao deixa nada de que "
               + "queixar-se.")]
        [SerializeField, TextArea]
        private string confrontReply = "Your window was open and it was going to rain. "
                                     + "I shut it. That is all.";

        [Tooltip("Resposta ao silencio. Ele nao insiste, e e isso que incomoda.")]
        [SerializeField, TextArea]
        private string silentReply = "…Alright. Night, then.";

        [Tooltip("O que o Tomas pensa depois, seja qual for a escolha.")]
        [SerializeField, TextArea]
        private string afterThought = "I never told him it was far.";

        [Header("Som")]
        [SerializeField] private AudioSource knockSource;
        [SerializeField] private AudioClip knockClip;
        [SerializeField, Range(0f, 1f)] private float knockVolume = 0.8f;

        [Tooltip("Instantes, dentro do clip, em que a folha leva a pancada.\n\n"
               + "Medidos no `door_knock.mp3` e nao escolhidos: o clip tem 1,35 s "
               + "e quatro pancadas em 0,18 / 0,46 / 0,74 / 1,00. Se trocares o "
               + "som, estes numeros deixam de bater certo e a porta estremece "
               + "fora de tempo, que se ve logo.")]
        [SerializeField] private float[] knockTimes = { 0.18f, 0.46f, 0.74f, 1.00f };

        [Header("Ligacoes")]
        [SerializeField] private WorldDialogueController dialogue;
        [SerializeField] private NpcMumbleVoice voice;
        [SerializeField] private PlayerThoughtDirector thoughts;
        [SerializeField] private NarrativeBlackboard blackboard;
        [SerializeField] private ChapterDirector director;

        private enum Phase { Idle, Waiting, Running, Done }

        private Phase phase = Phase.Idle;
        private float armedAt;
        private Transform player;
        private Coroutine running;

        private void Awake()
        {
            if (rui == null) rui = GameObject.Find("NPC_Rui");
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
            if (voice == null && rui != null) voice = rui.GetComponentInChildren<NpcMumbleVoice>(true);
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
            if (blackboard == null) blackboard = FindObjectOfType<NarrativeBlackboard>();
        }

        private void Update()
        {
            switch (phase)
            {
                case Phase.Idle:
                    director = ChapterDirector.Resolve(director);
                    if (director != null && !string.IsNullOrWhiteSpace(armOnEvent)
                        && director.HasSeen(armOnEvent))
                    {
                        phase = Phase.Waiting;
                        armedAt = Time.time;
                    }
                    break;

                case Phase.Waiting:
                    if (Time.time - armedAt > patienceSeconds) { phase = Phase.Done; break; }
                    if (ConditionsMet()) running = StartCoroutine(Run());
                    break;
            }
        }

        /// <summary>
        /// O jogador esta fechado no quarto dele, e nao apenas algures na casa.
        ///
        /// A porta **fechada** e a condicao toda: com ela aberta isto era um homem a
        /// aparecer ao vao a fazer perguntas, que e outra cena e ja existe no
        /// prologo. Aqui o que importa e nao se verem.
        /// </summary>
        private bool ConditionsMet()
        {
            if (door == null || dialogue == null || dialogue.IsBusy) return false;
            if (door.IsOpen) return false;

            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor == null) return false;
                player = motor.transform;
            }

            return Vector3.Distance(player.position, door.transform.position) <= insideRange;
        }

        private IEnumerator Run()
        {
            phase = Phase.Running;

            // Posto no corredor sem ninguem o ver chegar: a porta esta fechada, o
            // jogador esta do outro lado. E o mesmo principio do prologo — encontrar
            // uma coisa feita assusta mais do que ve-la acontecer.
            PrototypeNpcRoutine routine = rui != null ? rui.GetComponent<PrototypeNpcRoutine>() : null;
            UnityEngine.AI.NavMeshAgent agent = rui != null ? rui.GetComponent<UnityEngine.AI.NavMeshAgent>() : null;
            bool routineWas = routine != null && routine.enabled;
            bool agentWas = agent != null && agent.enabled;

            if (routine != null) routine.enabled = false;
            if (agent != null) agent.enabled = false;
            if (rui != null)
            {
                rui.transform.position = outsideSpot;
                rui.transform.rotation = Quaternion.Euler(0f, outsideYaw, 0f);
            }

            if (knockSource != null && knockClip != null)
                knockSource.PlayOneShot(knockClip, knockVolume);

            // A folha, ao mesmo tempo. O jogador esta a olhar para a porta e nao
            // para quem bate — e a porta que tem de reagir.
            StartCoroutine(RattleAlong());

            // O punho, e depois o ombro na ombreira.
            //
            // Isto e um dos dois sitios do jogo em que o Rui esta do lado de fora de
            // uma porta fechada e o jogador do lado de dentro — a cena inteira depende
            // de haver uma folha entre os dois. Ate aqui ele fazia-a na pose de quem
            // espera o autocarro: o som batia na madeira e o corpo nao se mexia.
            //
            // O `Knock` so existe quando o clip existir na pasta; o `CrossFade` para um
            // estado que o controlador nao tem **nao da erro nenhum e nao faz nada**,
            // que e exactamente a armadilha que o `WallLean` do peeking custou. Por
            // isso o encostar vem a seguir de qualquer maneira: se o primeiro nao
            // existir, ainda ha o segundo.
            Pose("Knock", 0.12f);
            yield return new WaitForSeconds(0.9f);
            Pose("ListenAtDoor", 0.4f);

            if (!string.IsNullOrWhiteSpace(knockLine))
            {
                dialogue.ShowReaction("Rui", knockLine, voice, 2.4f, null,
                    autoClose: true, blocksInteraction: false);
                yield return new WaitForSeconds(2.8f);
            }

            int picked = -1;
            dialogue.BeginChoice("Rui", question, voice, confrontChoice, silentChoice,
                8f, delegate(int index) { picked = index; });

            float deadline = Time.time + 14f;
            while (picked < 0 && Time.time < deadline) yield return null;

            // Nao responder **e** o silencio. Sem isto, quem largasse o teclado
            // ficava com a cena aberta e o Rui parado no corredor para sempre.
            bool confronted = picked == 0;

            // Os dois ramos custam, e custam **coisas diferentes**. Confrontar sobe a
            // suspeita dele e a insubmissao do jogador; calar-se ensina-lhe que pode
            // continuar. Ver `NarrativeBlackboard.RuiEmboldened`.
            if (blackboard != null)
            {
                if (confronted) blackboard.OnChoice(DialogueTone.Edgy);
                else blackboard.OnLetItGo();
            }

            yield return new WaitForSeconds(0.6f);

            string reply = confronted ? confrontReply : silentReply;
            if (!string.IsNullOrWhiteSpace(reply))
            {
                dialogue.ShowReaction("Rui", reply, voice, 3.4f, null,
                    autoClose: true, blocksInteraction: false);
                yield return new WaitForSeconds(3.8f);
            }

            // O pensamento chega **depois** de ele se calar, e nao por cima. E o
            // remate: o Tomas percebe que a pergunta dele continha uma coisa que
            // ninguem lhe disse.
            if (!string.IsNullOrWhiteSpace(afterThought))
                thoughts?.Think("day3_door", afterThought, 5, true, 3.6f);

            // Vai-se embora a andar. Nunca desaparece.
            //
            // A pose e largada **antes** de a rotina voltar: ela manda no `Speed` do
            // animator e um estado de accao ainda activo ficaria por cima da
            // locomocao, com ele a atravessar o corredor encostado a uma ombreira que
            // ja nao esta la.
            Pose("Locomotion", 0.35f);
            if (agent != null && agentWas) agent.enabled = true;
            if (routine != null) routine.enabled = routineWas;

            director = ChapterDirector.Resolve(director);
            if (!string.IsNullOrWhiteSpace(finishedEvent)) director?.Notify(finishedEvent);

            phase = Phase.Done;
            running = null;
        }

        /// <summary>
        /// Troca a pose do Rui, se ele tiver animator e o estado existir.
        ///
        /// Tolerante de propósito, mas **nao silenciosa**: um `CrossFade` para um
        /// estado inexistente nao da erro e nao faz nada, e foi assim que o
        /// `WallLean` do peeking correu meses inteiros sem ninguem reparar que a pose
        /// nunca acontecia. Aqui, se o estado nao existir, fica escrito na consola.
        /// </summary>
        /// <summary>
        /// Sacode a folha em cima de cada pancada do clip.
        ///
        /// Corre a parte, e nao dentro da cena: se alguem abrir a porta a meio, a
        /// cena segue e isto simplesmente deixa de ter efeito — o `Rattle` recusa
        /// portas abertas. Nao ha estado nenhum para desfazer.
        /// </summary>
        private IEnumerator RattleAlong()
        {
            if (door == null || knockTimes == null) yield break;

            float already = 0f;
            foreach (float at in knockTimes)
            {
                float wait = at - already;
                if (wait > 0f) yield return new WaitForSeconds(wait);
                already = Mathf.Max(already, at);
                door.Rattle();
            }
        }

        private void Pose(string state, float blend)
        {
            var animator = rui != null ? rui.GetComponentInChildren<Animator>(true) : null;
            if (animator == null || animator.runtimeAnimatorController == null) return;

            if (!animator.HasState(0, Animator.StringToHash(state)))
            {
                Debug.LogWarning("[PortaConversa] O controlador do Rui nao tem o estado `" +
                                 state + "`. Falta o clip na pasta de animacoes; correr " +
                                 "'Setup Rui Animator' depois de o largar la.");
                return;
            }

            animator.CrossFadeInFixedTime(state, blend);
        }

        private void OnDisable()
        {
            if (running != null) StopCoroutine(running);
            running = null;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(GameObject ruiObject, DoorDragInteractable bedroomDoor,
            Vector3 spot, float yaw, AudioSource knocks, AudioClip knock)
        {
            rui = ruiObject;
            door = bedroomDoor;
            outsideSpot = spot;
            outsideYaw = yaw;
            knockSource = knocks;
            knockClip = knock;
        }
#endif
    }
}
