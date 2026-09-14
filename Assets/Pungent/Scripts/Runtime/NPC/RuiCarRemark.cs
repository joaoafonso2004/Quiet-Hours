using Pungent.Dialogue;
using Pungent.Narrative;
using UnityEngine;

namespace Pungent.NPC
{
    /// <summary>
    /// A primeira fissura a serio do jogo: o Rui comenta o carro.
    ///
    /// O §5 pede-a por escrito — *"Rui comenta o carro apesar de Tomas nunca ter
    /// falado dele"* — e ate agora ela estava **implicita**. O jogador olhava da
    /// varanda, enumerava o que o carro tinha de errado, e o pensamento seguinte
    /// dizia "I never told him what I drive". Ninguem tinha dito nada: ele lia uma
    /// reaccao a uma fala que nao ouviu.
    ///
    /// Tres decisoes que fazem esta cena funcionar, e que sao faceis de desfazer sem
    /// dar por isso:
    ///
    /// **Ele ja la esta.** Nao se aproxima, nao ha passos, nao ha porta a abrir. E
    /// posto ao vao enquanto o jogador esta virado para a rua, que e o mesmo
    /// principio do prologo: ver a mudanca acontecer transforma-a num truque;
    /// encontra-la feita e o que assusta.
    ///
    /// **O que ele diz e banal e util.** Nao acusa, nao insinua e nao sabe nada que
    /// nao possa ter sabido de uma janela. A segunda fala ate se explica sozinha —
    /// ouviu os discos. Uma explicacao que funciona e mais incomoda do que uma que
    /// nao funciona, porque deixa o jogador sem nada de concreto de que se queixar.
    ///
    /// **O detalhe repetido e o do jogador.** O Tomas acabou de pensar "terceiro
    /// lugar a contar da esquina"; o Rui devolve-lhe a esquina. E a regra de escrita
    /// do projecto — dramatico le-se como guiao, banal le-se como vigilancia.
    ///
    /// Nao bloqueia as maos do jogador. Isto nao e uma conversa: e uma coisa dita
    /// por cima do ombro enquanto ele olha para a rua.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuiCarRemark : MonoBehaviour
    {
        [Tooltip("Acontecimento que arranca a cena: o jogador acabou de olhar para o "
               + "carro da varanda.")]
        [SerializeField] private string onEvent = "day1_car_seen";

        [Tooltip("Levantado quando ele acaba de falar.\n\n"
               + "Existe por causa da **ordem**. O passo do capitulo que traz o "
               + "\"I never told him what I drive\" fecha-se por um acontecimento, e "
               + "se esse acontecimento fosse o proprio `day1_car_seen` o Tomas "
               + "percebia a fissura antes de o Rui abrir a boca — a reaccao chegava "
               + "primeiro do que a causa. Com isto ha um passo mudo pelo meio, e a "
               + "conta so se faz depois de haver com que a fazer.")]
        [SerializeField] private string finishedEvent = "day1_car_remarked";

        [Tooltip("O que ele diz. Uma fala por linha.")]
        [SerializeField] private ThoughtLineSet lines;

        [SerializeField] private string speakerName = "Rui";
        [SerializeField, Min(1f)] private float holdSeconds = 3.6f;

        [Tooltip("Silencio antes da primeira fala. E o tempo em que o jogador ainda "
               + "pensa que esta sozinho — sem ele, a fala cai em cima do proprio "
               + "pensamento dele e le-se como resposta.")]
        [SerializeField, Min(0f)] private float delaySeconds = 2.2f;

        [Tooltip("Pausa entre falas, alem do tempo de leitura.")]
        [SerializeField, Min(0f)] private float betweenSeconds = 1.3f;

        [Header("Onde ele esta")]
        [SerializeField] private GameObject rui;

        [Tooltip("O vao da varanda, pelo lado de dentro. **Atras do jogador**, que "
               + "esta encostado a guarda virado para a rua: com isto ele nunca e "
               + "posto dentro do enquadramento, e so existe quando o jogador se "
               + "vira.")]
        [SerializeField] private Vector3 doorwaySpot = new Vector3(0.90f, 0f, 4.55f);

        [Tooltip("Virado para a varanda, isto e, para as costas do jogador.")]
        [SerializeField] private float doorwayYaw;

        [Header("Ligacoes")]
        [SerializeField] private ChapterDirector director;
        [SerializeField] private WorldDialogueController dialogue;
        [SerializeField] private NpcMumbleVoice voice;

        [Tooltip("A que distancia do vao o jogador tem de estar para a cena valer a "
               + "pena. Mais longe do que isto e o Rui a falar sozinho na varanda.")]
        [SerializeField, Min(1f)] private float audienceRange = 5f;

        [Tooltip("Rede de seguranca: passado isto ele fala de qualquer maneira.\n\n"
               + "O passo do capitulo fecha-se com o `finishedEvent`, portanto **nao "
               + "pode haver caminho nenhum em que isto nunca aconteca** — seria o "
               + "jogo preso para sempre por causa de uma questao de encenacao.")]
        [SerializeField, Min(5f)] private float patienceSeconds = 90f;

        [SerializeField] private LayerMask sightBlockers = ~0;

        private enum Phase { Waiting, Placing, Silence, Speaking, Done }

        private Phase phase = Phase.Waiting;
        private float nextAt;
        private float placingSince;
        private Transform playerBody;
        private Camera playerCamera;
        private int cursor;
        private int said;
        private bool routineWasEnabled;
        private bool agentWasEnabled;

        private void Awake()
        {
            if (director == null) director = FindObjectOfType<ChapterDirector>();
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
            if (rui == null) rui = GameObject.Find("NPC_Rui");
            if (voice == null && rui != null) voice = rui.GetComponentInChildren<NpcMumbleVoice>(true);
        }

        private void Update()
        {
            switch (phase)
            {
                case Phase.Waiting:
                    if (director != null && director.HasSeen(onEvent))
                    {
                        phase = Phase.Placing;
                        placingSince = Time.time;
                    }
                    break;

                case Phase.Placing:
                    UpdatePlacing();
                    break;

                case Phase.Silence:
                    if (Time.time >= nextAt) phase = Phase.Speaking;
                    break;

                case Phase.Speaking:
                    UpdateSpeaking();
                    break;
            }
        }

        /// <summary>
        /// Espera pelo momento em que po-lo ao vao quer dizer alguma coisa.
        ///
        /// A cena estava a arrancar no instante em que o acontecimento se levantava,
        /// e as duas regras escritas la em cima nao eram verificadas por ninguem:
        ///
        /// **"Ele ja la esta."** Ele era posto no vao mesmo que o vao estivesse a
        /// meio do ecra. Basta o jogador virar-se ao levantar o olhar da rua para o
        /// ver aparecer do nada, e ai deixa de haver duvida nenhuma — viu-se um NPC
        /// a nascer. O `PlayerSight` ja sabia responder a esta pergunta; nunca lhe
        /// tinha sido feita aqui.
        ///
        /// **"Uma coisa dita por cima do ombro."** Por cima do ombro de quem? O
        /// jogador podia ter fechado a porta da varanda e ido para a cozinha entre o
        /// olhar e a fala — o acontecimento fecha-se no instante em que ele larga o
        /// olhar da rua. O Rui aparecia na varanda vazia e falava para ninguem, e o
        /// jogador ouvia uma voz sem corpo a duas divisoes de distancia. Foi assim
        /// que a primeira fissura do jogo se leu como um bug.
        ///
        /// **A paciencia nao e opcional.** O passo do capitulo espera pelo
        /// `finishedEvent`, portanto esperar para sempre por um jogador que nunca
        /// volta a varanda seria o jogo preso. Passado o tempo, ele fala onde estiver
        /// — uma cena imperfeita e sempre melhor do que um capitulo que nao acaba.
        /// </summary>
        private void UpdatePlacing()
        {
            bool impatient = Time.time - placingSince >= patienceSeconds;

            if (!impatient && !AudienceReady()) return;

            Begin();
        }

        /// <summary>
        /// O jogador esta perto do vao **e** nao esta a olhar para ele.
        /// </summary>
        private bool AudienceReady()
        {
            if (playerBody == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor == null) return false;
                playerBody = motor.transform;
                playerCamera = motor.GetComponentInChildren<Camera>(true);
            }
            if (playerCamera == null) return false;

            if (Vector3.Distance(playerBody.position, doorwaySpot) > audienceRange) return false;

            // O sitio onde ele vai nascer tem de estar fora do enquadramento, a
            // cabeca e aos pes: aparecer meio corpo dentro do ecra e o mesmo que
            // aparecer inteiro.
            if (PlayerSight.IsInsideView(playerCamera, doorwaySpot + Vector3.up * 1.6f, -0.05f)) return false;
            if (PlayerSight.IsInsideView(playerCamera, doorwaySpot, -0.05f)) return false;

            return true;
        }

        /// <summary>
        /// Poe-no ao vao e suspende a rotina.
        ///
        /// A rotina tem de sair do caminho: com ela a andar, o `MoveTowards` do
        /// circuito puxava-o de volta para o proximo ponto a meio da fala, e a cena
        /// passava a ser um homem a atravessar a sala a falar sozinho.
        /// </summary>
        private void Begin()
        {
            phase = Phase.Silence;
            nextAt = Time.time + delaySeconds;

            if (rui == null) return;

            var routine = rui.GetComponent<PrototypeNpcRoutine>();
            if (routine != null)
            {
                routineWasEnabled = routine.enabled;
                routine.enabled = false;
            }

            var agent = rui.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agentWasEnabled = agent.enabled;
                agent.enabled = false;
            }

            rui.SetActive(true);
            rui.transform.position = doorwaySpot;
            rui.transform.rotation = Quaternion.Euler(0f, doorwayYaw, 0f);
        }

        private void UpdateSpeaking()
        {
            if (Time.time < nextAt) return;

            if (lines != null && said < lines.Count)
            {
                // Se houver outra coisa no ecra, espera. Atropelar o ultimo
                // pensamento do jogador com a fala dele desfazia a cena.
                if (dialogue == null || dialogue.IsBusy) return;

                dialogue.ShowReaction(speakerName, lines.Pick(ref cursor), voice,
                    holdSeconds, null, autoClose: true, blocksInteraction: false);

                said++;
                nextAt = Time.time + holdSeconds + betweenSeconds;
                return;
            }

            Finish();
        }

        /// <summary>Devolve-lhe o circuito. A partir daqui a manha continua igual.</summary>
        private void Finish()
        {
            phase = Phase.Done;
            director?.Notify(finishedEvent);

            if (rui == null) return;

            var agent = rui.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = agentWasEnabled;

            var routine = rui.GetComponent<PrototypeNpcRoutine>();
            if (routine != null) routine.enabled = routineWasEnabled;
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(GameObject ruiObject, ThoughtLineSet spoken)
        {
            rui = ruiObject;
            lines = spoken;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.9f, 0.5f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(doorwaySpot + Vector3.up * 0.9f, 0.35f);
            Gizmos.DrawRay(doorwaySpot + Vector3.up * 1.6f,
                Quaternion.Euler(0f, doorwayYaw, 0f) * Vector3.forward * 1.5f);
        }
    }
}
