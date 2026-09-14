using System.Collections;
using Pungent.Dialogue;
using Pungent.Interaction;
using Pungent.NPC;
using Pungent.Player;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// O primeiro objectivo do Dia 1: tirar o queijo do frigorífico e comê-lo.
    ///
    /// **É a rotina, e a rotina é o material dos dias seguintes.** Este bloco não
    /// tem susto nenhum de propósito. Serve para o jogador aprender que abre o
    /// frigorífico, tira uma coisa, come, e que o Rui anda pela casa enquanto isso
    /// acontece — para que no Dia 1 à tarde a mesma porta de frigorífico o possa
    /// apanhar, e no Dia 2 a mesma cozinha esteja subtilmente errada.
    ///
    /// Vive no próprio queijo: o objecto que se olha é o objecto que responde. Uma
    /// caixa de gatilho invisível ao lado teria o mesmo efeito e mentia sobre onde
    /// está a coisa.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class RebootEatFromFridge : MonoBehaviour, IPlayerInteractable
    {
        [Header("Cena")]
        [Tooltip("Onde o queijo aparece na mão. Filho da câmara; a pose é composta "
               + "à mão como a do telemóvel.")]
        [SerializeField] private Transform handAnchor;

        [Tooltip("O frigorífico onde o queijo está. Vazio = procurado nos pais no "
               + "Awake, que é onde ele está desde que o queijo passou a viver "
               + "dentro do `Fridge_Interior`. Ver `Available`.")]
        [SerializeField] private FridgeDoor fridge;

        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PrototypeHUD hud;
        [SerializeField] private WorldDialogueController dialogue;

        [Header("Texto")]
        [SerializeField] private string prompt = "Take the cheese";

        [Tooltip("Objectivo enquanto o queijo não estiver comido. Vazio = o "
               + "objectivo é posto por quem arma este passo.")]
        [SerializeField] private string objectiveWhileHungry = "OBJECTIVE: Eat something from the fridge";

        [SerializeField] private string objectiveWhenDone = string.Empty;
        [SerializeField] private ThoughtLineSet afterEatingThought;

        [Tooltip("O passo seguinte do Dia 1, armado quando o susto acaba. Vazio = o "
               + "dia termina aqui e o jogador fica sem objectivo — que foi como o "
               + "dono encontrou a saída do dia falso.")]
        [SerializeField] private RebootLaptopExport nextBeat;

        [Tooltip("Porta destrancada quando o Dia 1 arranca. É a do Rui: no prólogo "
               + "fica trancada, e só abre depois do dia falso. Ver `SetArmed`.")]
        [SerializeField] private DoorDragInteractable unlockOnDay1;

        [Header("Dentadas")]
        [Tooltip("Quantas dentadas até desaparecer. Cada uma sobe, encolhe e desce.")]
        [SerializeField, Min(1)] private int bites = 4;

        [SerializeField, Min(0.1f)] private float biteSeconds = 0.55f;

        [Tooltip("Pausa entre dentadas. É o mastigar, e é o que impede isto de "
               + "parecer um objecto a desinflar.")]
        [SerializeField, Min(0f)] private float chewSeconds = 0.45f;

        [Tooltip("Quanto sobe em direcção à boca, em metros.")]
        [SerializeField, Min(0f)] private float biteRise = 0.09f;

        [Tooltip("Tamanho que sobra depois da última dentada, antes de desaparecer.")]
        [SerializeField, Range(0f, 0.6f)] private float finalScale = 0.15f;

        [SerializeField, Min(0.05f)] private float raiseSeconds = 0.35f;

        [Header("Susto do frigorífico")]
        [Tooltip("O Rui aparece quando o queijo acaba. Não é teletransporte gratuito: "
               + "a porta do frigorífico tapou-lhe o campo de visão, que é "
               + "exactamente como a história encena este susto.")]
        [SerializeField] private PrototypeNpcRoutine rui;

        [Tooltip("A que distância, à direita do jogador, ele aparece.")]
        [SerializeField, Min(0.6f)] private float scareDistance = 1.35f;

        [Tooltip("Onde o Rui espera enquanto o objectivo do queijo está por cumprir: "
               + "parado, sem andar, só com a cabeça a seguir-te. Vazio = ele mantém "
               + "o roaming normal e o susto continua a funcionar. "
               + "A ROTAÇÃO CONTA: a cabeça dele só chega a 76° do peito, por isso um "
               + "Rui posto de costas para a divisão onde tu estás fica a olhar para "
               + "a parede. Ver `PrototypeNpcRoutine.SetHoldFacing`.")]
        [SerializeField] private Transform ruiWaitAnchor;

        [SerializeField] private AudioClip stingClip;
        [SerializeField, Range(0f, 1f)] private float stingVolume = 1f;
        [SerializeField, Min(0.1f)] private float cameraLockSeconds = 1.1f;

        [Tooltip("Falas, por ordem. Escritas em maiúsculas, saem a tremer.")]
        [SerializeField] private string tomasLine = "YOU SCARED ME";
        [SerializeField] private string[] ruiLines =
        {
            "That shelf's mine.",
            "Other one. You're fine."
        };
        [SerializeField] private string tomasSpeaker = "TOMÁS";
        [SerializeField] private string ruiSpeaker = "RUI";
        [SerializeField, Min(0.5f)] private float lineSeconds = 2.4f;

        [Header("Som")]
        [SerializeField] private AudioSource sound;

        [Tooltip("Mastigar contínuo, tocado uma vez do princípio ao fim e "
               + "desvanecido quando o queijo acaba.\n\nUma vez e não por dentada: "
               + "o clip tem nove segundos e as dentadas estão a um por segundo — "
               + "disparado quatro vezes ficavam quatro cópias sobrepostas, que "
               + "soa a uma sala cheia de gente a comer.")]
        [SerializeField] private AudioClip eatingClip;

        [Tooltip("Dentadas isoladas, se algum dia houver. Somam-se ao contínuo.")]
        [SerializeField] private AudioClip[] biteClips = new AudioClip[0];

        [SerializeField, Range(0f, 1f)] private float biteVolume = 0.7f;
        [SerializeField, Min(0.05f)] private float eatingFadeSeconds = 0.4f;

        // **Desarmado ate alguem o armar, e isso e o contrario do que estava.**
        //
        // Comecava a `true`, e por isso o queijo respondia desde o primeiro frame
        // do jogo: dava para o comer no prologo, a caminho da chave. Nao dava erro
        // nenhum — so gastava o objectivo do Dia 1 antes de o Dia 1 existir, e
        // quando o dia falso o armasse o queijo ja nao estava la para comer.
        //
        // Quem arma isto e o `RebootFalseDay`, ao levantar-se da segunda vez.
        private bool armed;
        private bool eaten;
        private bool parked;
        private Transform held;

        /// <summary>
        /// O queijo oferece-se: armado, por comer, e **com a porta aberta**.
        ///
        /// **A porta e uma condicao escrita e nao uma parede, e tem de ser.** O
        /// `Kit_Fridge` tem um colisor so — uma caixa solida que ocupa tambem o
        /// interior do frigorifico — e a porta que se ve abrir e apenas uma malha a
        /// rodar, sem colisor nenhum. Enquanto o queijo viveu fora da arvore do
        /// frigorifico, essa caixa era, para o `PlayerInteractor`, uma parede entre o
        /// olho e o queijo: o `Blocked` so isenta o movel do seu proprio conteudo por
        /// hierarquia, e o queijo estava pendurado no `DRESS_CounterFood`. Media-se
        /// `NADA` em cinco direccoes de mira seguidas — nem o queijo nem o
        /// frigorifico ganhavam, porque tambem o queijo tapava o frigorifico.
        ///
        /// Por-lhe o queijo dentro, no `Fridge_Interior`, resolve a mira e diz a
        /// verdade sobre onde ele esta. Mas tira tambem a unica coisa que impedia que
        /// se tirasse o queijo com o frigorifico fechado — e nao havia porta solida
        /// nenhuma a fazer esse trabalho, so aquela caixa. Daqui em diante quem o faz
        /// e esta linha.
        ///
        /// Nao e so arrumacao: o susto do Rui e encenado com a porta aberta a tapar o
        /// campo de visao. Um queijo que se tira pela porta fechada nao chega la.
        /// </summary>
        private bool Available => armed && !eaten && (fridge == null || fridge.IsOpen);

        public string Prompt => Available ? prompt : string.Empty;
        public bool HoldToInteract => false;

        private void Awake()
        {
            if (input == null) input = FindObjectOfType<PlayerInputReader>();
            if (hud == null) hud = FindObjectOfType<PrototypeHUD>();
            if (dialogue == null) dialogue = FindObjectOfType<WorldDialogueController>();
            if (fridge == null) fridge = GetComponentInParent<FridgeDoor>();

            // Sem frigorifico nao ha porta a respeitar, e o queijo passa a poder ser
            // tirado a qualquer hora. Nao desliga o passo — isso matava o objectivo do
            // Dia 1 — mas tambem nao fica calado, que foi como isto se perdeu a
            // primeira vez. Se isto aparecer, o queijo saiu de dentro do frigorifico.
            if (fridge == null)
                Debug.LogError("[RebootEatFromFridge] " + name + " nao esta dentro de nenhum "
                    + "FridgeDoor. O queijo vai poder ser tirado com a porta fechada, e o "
                    + "susto do Rui e encenado com ela aberta.", this);

            if (handAnchor == null || input == null || hud == null)
            {
                Debug.LogError("[RebootEatFromFridge] Faltam referências de cena.", this);
                enabled = false;
            }
        }

        /// <summary>Liga ou desliga o passo. Serve o dia que o quiser usar.</summary>
        public void SetArmed(bool value)
        {
            armed = value;
            if (!value || eaten) return;

            if (!string.IsNullOrEmpty(objectiveWhileHungry))
                hud.SetObjective(objectiveWhileHungry);

            // **Este metodo e o sinal de que o Dia 1 comecou**, e por isso e daqui
            // que a porta do Rui se abre. No prologo ela esta trancada na cena: nessa
            // altura o Tomas acabou de chegar, e um quarto alheio aberto a primeira
            // noite convida a entrar la antes de o jogo ter alguma coisa la dentro
            // para se encontrar.
            //
            // Fica aqui e nao no `RebootPrologueEnd` porque o dia falso ainda esta
            // entre os dois; e fica aqui e nao no `RebootFalseDay` porque esse vai
            // mudar de sitio — o plano e ele passar para depois do Dia 1. O arranque
            // do Dia 1 e este.
            if (unlockOnDay1 != null) unlockOnDay1.SetLocked(false);

            ParkRui();
        }

        /// <summary>
        /// Poe o Rui a espera, parado, com a cabeca a seguir o jogador.
        ///
        /// **Ele tem de vir de algum lado, e o jogador tem de o ter visto la.** O
        /// susto move-o para o pe de ti, e enquanto ele andava pela casa isso lia-se
        /// como teletransporte: o dono viu-o na televisao e um instante depois
        /// tinha-o atras das costas. O apartamento e pequeno de mais para esconder
        /// uma coisa dessas — e nao e a primeira vez, o dia falso teve de o desligar
        /// por completo pela mesma razao, ou o jogador via dois Ruis.
        ///
        /// Parado ao pe da porta do quarto do Tomas, ele deixa de aparecer do nada:
        /// vem do sitio de onde ja estava a olhar. Nao anda, porque um Rui a
        /// aproximar-se estraga o susto antes de ele acontecer; e nao roda o corpo,
        /// so a cabeca, porque um homem que gira sozinho para te acompanhar pela
        /// casa e o Rui do Dia 3 e nao o do Dia 1.
        ///
        /// Sem ancora nao faz nada: o roaming fica como estava e o susto continua a
        /// funcionar como antes. E uma encenacao a mais, nao um requisito.
        ///
        /// **Espera para o pousar.** O `HoldAt` nao o convida para ali, atira-o para
        /// ali — e este passo e armado logo a seguir ao dia falso, com o jogador de
        /// pe no quarto e a porta aberta para o corredor onde a ancora esta. Pousa-lo
        /// a vista era trocar um teletransporte por outro. Por isso so acontece num
        /// instante em que a ancora esta fora do ecra ou tapada, e ate la ele anda
        /// como sempre andou.
        /// </summary>
        private void ParkRui()
        {
            if (rui == null || ruiWaitAnchor == null || parked) return;
            StartCoroutine(ParkRuiWhenUnseen());
        }

        private IEnumerator ParkRuiWhenUnseen()
        {
            // Se o jogador nunca desviar o olhar daquele ponto, ele nunca e pousado
            // e o beat corre exactamente como corria antes desta encenacao existir.
            // Preferivel a um Rui a nascer no meio do corredor a olhar para ele.
            while (!eaten && AnchorIsVisible())
                yield return new WaitForSeconds(0.2f);

            if (eaten || parked) yield break;

            parked = true;
            rui.SetHoldFacing(false);
            rui.HoldAt(ruiWaitAnchor);
        }

        /// <summary>
        /// O sitio da ancora esta a ser visto agora?
        ///
        /// Duas perguntas, e as duas sao precisas: dentro do enquadramento, **e** com
        /// linha de vista. Uma parede pelo meio conta como nao visto, que e o unico
        /// motivo por que isto funciona num apartamento com corredores.
        ///
        /// Mede a altura do peito e nao os pes: e a parte dele que primeiro aparece
        /// numa porta, e pousa-lo com a cabeca a vista e os pes tapados era o mesmo
        /// erro com um passo a mais.
        /// </summary>
        private bool AnchorIsVisible()
        {
            var camera = Camera.main;
            if (camera == null) return false;

            Vector3 chest = ruiWaitAnchor.position + Vector3.up * 1.2f;
            Vector3 view = camera.WorldToViewportPoint(chest);
            if (view.z <= 0f || view.x < -0.05f || view.x > 1.05f || view.y < -0.05f || view.y > 1.05f)
                return false;

            Vector3 delta = chest - camera.transform.position;
            return !Physics.Raycast(camera.transform.position, delta.normalized,
                delta.magnitude - 0.05f, ~0, QueryTriggerInteraction.Ignore);
        }

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (!Available) return;
            eaten = true;
            StartCoroutine(EatRoutine());
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }

        private IEnumerator EatRoutine()
        {
            // O queijo do frigorífico desaparece e nasce um igual na mão. Mover o
            // original arrancava-o à composição do dono; assim a arrumação dele
            // fica de pé e o que se come é uma cópia.
            var world = GetComponent<Renderer>();
            if (world != null) world.enabled = false;
            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            held = BuildHeldCopy();
            hud.SetPrompt(string.Empty);

            yield return Raise();

            // Só arranca depois de o queijo estar em cima: o som é de mastigar, e
            // ainda não há nada na boca enquanto ele sobe.
            if (sound != null && eatingClip != null)
            {
                sound.clip = eatingClip;
                sound.volume = biteVolume;
                sound.loop = true;
                sound.Play();
            }

            for (int i = 0; i < bites; i++)
            {
                float from = Mathf.Lerp(1f, finalScale, i / (float)bites);
                float to = Mathf.Lerp(1f, finalScale, (i + 1) / (float)bites);
                yield return Bite(from, to);
                if (i < bites - 1) yield return new WaitForSeconds(chewSeconds);
            }

            if (held != null) Destroy(held.gameObject);
            held = null;

            yield return FadeOutEating();

            hud.SetObjective(objectiveWhenDone);

            yield return FridgeScare();

            Speak(afterEatingThought);

            // O objectivo seguinte entra **depois** do susto e nao antes: com ele no
            // ecra durante as falas, o jogador le "check the export" por cima do Rui
            // a dizer-lhe para nao mexer na comida dele, e a cena passa a ser uma
            // lista de tarefas.
            if (nextBeat != null) nextBeat.SetArmed(true);

            NarrativeBlackboard.Instance?.OnEvent("day1_eaten");
            Debug.Log("[RebootEatFromFridge] Queijo comido; objectivo cumprido.", this);
        }

        /// <summary>
        /// Ele estava ali o tempo todo, à direita, e a porta do frigorífico tapava-o.
        ///
        /// **O susto é a virada de cabeça, não o aparecimento.** O jogador não o vê
        /// chegar: vê-se a si próprio a ser virado para ele, já demasiado perto. É a
        /// mesma encenação do §Jumpscare 2 da história, e cai no primeiro momento em
        /// que o jogador baixou a guarda — acabou de comer.
        ///
        /// Não há corrida nem perseguição. Ele diz duas frases banais sobre uma
        /// prateleira e o assunto morre, que é o que torna aquilo desconfortável:
        /// não aconteceu nada de que se possa queixar.
        /// </summary>
        private IEnumerator FridgeScare()
        {
            if (rui == null) yield break;

            var camera = Camera.main;
            if (camera == null) yield break;

            Vector3 right = camera.transform.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f) yield break;
            right.Normalize();

            Vector3 spot = camera.transform.position + right * scareDistance;
            spot.y = rui.transform.position.y;

            // **Tem de caber.** À direita do jogador está a bancada tantas vezes
            // como está chão livre, e um Rui dentro do lava-loiça é cómico e não
            // assustador. A NavMesh já sabe onde uma pessoa pode estar de pé; o
            // ponto pedido é uma intenção e isto é o sítio mais próximo que a
            // cumpre. Se não houver nenhum, não há susto — melhor do que um errado.
            if (UnityEngine.AI.NavMesh.SamplePosition(spot, out var floor, 1.6f,
                UnityEngine.AI.NavMesh.AllAreas))
            {
                spot = floor.position;
            }
            else
            {
                Debug.LogWarning("[RebootEatFromFridge] Sem chão à direita do jogador; susto saltado.", this);
                yield break;
            }

            rui.HoldAt(rui.transform);           // congela-o antes de o mover
            rui.enabled = false;
            var agent = rui.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled) agent.enabled = false;

            var body = rui.GetComponent<Collider>();
            if (body != null) body.enabled = false;

            Vector3 toPlayer = camera.transform.position - spot;
            toPlayer.y = 0f;
            rui.transform.SetPositionAndRotation(spot,
                Quaternion.LookRotation(toPlayer.normalized, Vector3.up));

            if (sound != null && stingClip != null) sound.PlayOneShot(stingClip, stingVolume);

            // A câmara é virada para ele e segurada. Reafirmada todos os frames: um
            // ForceLookAt só escreve uma vez e a câmara volta ao corpo a seguir.
            var look = FindObjectOfType<CameraPhysics>();
            var target = rui.transform.position + Vector3.up * 1.55f;
            float elapsed = 0f;
            if (input != null) input.SetLookSuppressed(true);
            while (elapsed < cameraLockSeconds)
            {
                elapsed += Time.deltaTime;
                if (look != null) look.ForceLookAt(target);
                yield return null;
            }
            if (input != null) input.SetLookSuppressed(false);

            yield return Say(tomasSpeaker, tomasLine);
            if (ruiLines != null)
                foreach (var line in ruiLines) yield return Say(ruiSpeaker, line);

            // **Ele sai a pe, de onde esta.** Nao volta ao sitio de onde foi tirado.
            //
            // Estava aqui um `SetPositionAndRotation` de volta a pose que ele tinha
            // antes do susto, e era o teleporte que se via: dito e feito o que tinha
            // a dizer, o corpo desaparecia da cozinha e reaparecia no sofa.
            //
            // Nao havia nada para restaurar. O `HoldAt`, la em cima, **ja terminou**
            // o que ele estava a fazer — dispara o `onActionEnded` da ancora e
            // larga-a — por isso pousar-lhe o corpo no assento de onde veio nao o
            // volta a sentar, so o poe la de pe com a rotina a pensar que ele
            // continua na cozinha. Voltava a andar dali a um instante, e ninguem
            // percebia como e que ele tinha atravessado a casa.
            //
            // O `ResumeRoutine` projecta-o para a NavMesh onde ele esta agora e
            // devolve-lhe o agente, portanto ele sai da cozinha a andar, pela casa,
            // como qualquer pessoa que acabou de dizer uma coisa e se foi embora.
            // E incondicional de proposito: o `HoldAt` desliga o agente sempre, e
            // sem isto havia um caminho — ele estar a meio de uma accao quando o
            // susto dispara — que o deixava de pe e sem agente para sempre.
            if (body != null) body.enabled = true;
            rui.enabled = true;
            rui.ResumeRoutine();
        }

        private IEnumerator Say(string speaker, string line)
        {
            if (dialogue == null || string.IsNullOrWhiteSpace(line)) yield break;
            dialogue.ShowReaction(speaker, line, null, lineSeconds, null, autoClose: true);
            while (dialogue.IsBusy) yield return null;
        }

        /// <summary>Sobe até à posição de comer, para a primeira dentada não sair do nada.</summary>
        private IEnumerator Raise()
        {
            Vector3 from = held.localPosition - Vector3.up * biteRise * 1.5f;
            Vector3 to = held.localPosition;
            held.localPosition = from;

            float elapsed = 0f;
            while (elapsed < raiseSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / raiseSeconds));
                held.localPosition = Vector3.Lerp(from, to, t);
                yield return null;
            }
            held.localPosition = to;
        }

        /// <summary>
        /// Uma dentada: sobe, encolhe no topo, desce.
        ///
        /// O encolher acontece **no ponto mais alto**, que é onde a boca está e
        /// onde a mão tapa o objecto. Encolher a meio da descida lia-se como o
        /// queijo a derreter; encolher lá em cima lê-se como uma dentada.
        /// </summary>
        private IEnumerator Bite(float fromScale, float toScale)
        {
            Vector3 rest = held.localPosition;
            Vector3 mouth = rest + Vector3.up * biteRise;
            Vector3 baseScale = held.localScale / fromScale;

            float half = biteSeconds * 0.5f;
            float elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / half));
                held.localPosition = Vector3.Lerp(rest, mouth, t);
                yield return null;
            }

            held.localScale = baseScale * toScale;
            PlayBite();

            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / half));
                held.localPosition = Vector3.Lerp(mouth, rest, t);
                yield return null;
            }
            held.localPosition = rest;
        }

        private Transform BuildHeldCopy()
        {
            var copy = new GameObject("HELD_Cheese");
            var rect = copy.transform;
            rect.SetParent(handAnchor, false);
            rect.localPosition = Vector3.zero;
            rect.localRotation = Quaternion.identity;

            var filter = GetComponent<MeshFilter>();
            var source = GetComponent<Renderer>();
            if (filter != null && source != null)
            {
                copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                copy.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
            }

            // A mesma escala de mundo que tinha na prateleira: o objecto não muda
            // de tamanho por mudar de mãos.
            rect.localScale = Vector3.one;
            Vector3 lossy = rect.lossyScale;
            rect.localScale = new Vector3(
                transform.lossyScale.x / Mathf.Max(0.0001f, lossy.x),
                transform.lossyScale.y / Mathf.Max(0.0001f, lossy.y),
                transform.lossyScale.z / Mathf.Max(0.0001f, lossy.z));

            return rect;
        }

        /// <summary>
        /// Desvanece o mastigar em vez de o cortar.
        ///
        /// O clip tem nove segundos e a refeição tem quatro; parar a seco no fim da
        /// última dentada deixava um corte audível exactamente no momento em que a
        /// cena devia ficar em silêncio.
        /// </summary>
        private IEnumerator FadeOutEating()
        {
            if (sound == null || !sound.isPlaying) yield break;

            float from = sound.volume;
            float elapsed = 0f;
            while (elapsed < eatingFadeSeconds)
            {
                elapsed += Time.deltaTime;
                sound.volume = Mathf.Lerp(from, 0f, Mathf.Clamp01(elapsed / eatingFadeSeconds));
                yield return null;
            }

            sound.Stop();
            sound.loop = false;
            sound.clip = null;
            sound.volume = from;
        }

        private void PlayBite()
        {
            if (sound == null || biteClips == null || biteClips.Length == 0) return;
            var clip = biteClips[Random.Range(0, biteClips.Length)];
            if (clip != null) sound.PlayOneShot(clip, biteVolume);
        }

        private void Speak(ThoughtLineSet lines)
        {
            if (lines == null || lines.Count == 0 || dialogue == null) return;
            int cursor = lines.NewCursor;
            dialogue.ShowThought(lines.Pick(ref cursor), lines.HoldSeconds);
        }

        private void OnDisable()
        {
            if (held != null) { Destroy(held.gameObject); held = null; }
            if (sound != null && sound.isPlaying) { sound.Stop(); sound.loop = false; }
        }
    }
}
