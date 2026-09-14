using Pungent.Interaction;
using Pungent.NPC;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// A noite em que ele ja la esta.
    ///
    /// O climax corre no mesmo `Apartment_Blockout_V2` do prologo, do Dia 2 e do
    /// Dia 3, e essa cena traz consigo tudo o que esses capitulos montaram. Voltar
    /// a carrega-la sem mais nada fazia o `PrologueStage.Start` correr outra vez:
    /// o jogador era teleportado para a porta, as caixas voltavam a aparecer, o
    /// texto de abertura entrava por cima, e o Rui reaparecia na entrada a dar a
    /// chave — no capitulo em que ele e a ameaca. Nao dava erro nenhum: dava a
    /// noite errada.
    ///
    /// Este componente e o interruptor entre as duas casas. Pergunta ao director se
    /// o `climax` ja aconteceu; se nao, nao faz absolutamente nada e a cena e a de
    /// sempre, que e o que se quer para abrir o apartamento no editor e trabalhar
    /// nos capitulos anteriores.
    ///
    /// **O desligar tem de ser no `Awake`.** O Unity corre todos os `Awake` antes
    /// de qualquer `Start`, e um componente desactivado durante essa fase nunca ve
    /// o seu `Start`. Por isso nao interessa qual dos dois acorda primeiro: no fim
    /// da fase de `Awake` o prologo esta desligado, e a encenacao desta noite faz-se
    /// em paz no `Start`.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ClimaxStage : MonoBehaviour
    {
        [Tooltip("Acontecimento que diz que esta e a noite do Dia 5. Sem ele este "
               + "componente e inerte e a cena corre como sempre correu.")]
        [SerializeField] private string climaxEvent = "climax";

        [Header("O que nao acontece esta noite")]
        [Tooltip("Directores dos capitulos anteriores. Desligados no Awake — ver a "
               + "nota da classe: e a unica altura em que desligar impede o Start.")]
        [SerializeField] private MonoBehaviour[] suspended = new MonoBehaviour[0];

        [Tooltip("Objectos inteiros que pertencem a outras noites: as caixas do "
               + "prologo, os sinais do Dia 3, as tarefas domesticas.\n\n"
               + "Nao chega desligar os directores. Um `ChapterEventRaiser` que ja "
               + "disparou continua a ser um `IPlayerInteractable` na cena, e o "
               + "`PlayerInteractor` escolhe o alvo pelo colisor que apanha e nao "
               + "pelo que tem prompt: o das chaves do Dia 3 esta em cima do movel "
               + "da entrada e roubava o clique ao desta noite, calado.")]
        [SerializeField] private GameObject[] deactivated = new GameObject[0];

        [Header("O que so existe esta noite")]
        [Tooltip("O grupo com tudo o que e do Dia 5: o movel das chaves, a "
               + "fechadura, a secretaria do Rui, a saida e os esconderijos.\n\n"
               + "Comeca desligado e so acende aqui. Deixado activo, o prologo "
               + "ficava com um prompt 'Hide' debaixo da cama e a entrada oferecia "
               + "'Put your keys down' na noite em que ele se mudou — conteudo de um "
               + "capitulo a vazar para os outros tres que correm no mesmo sitio.")]
        [SerializeField] private GameObject content;

        [Tooltip("Componentes que so acordam esta noite. O `RuiHunt` vive no Rui, "
               + "que nao esta dentro do grupo de cima: nas outras noites tem de "
               + "ficar quieto e deixar a rotina domestica mandar no corpo dele.")]
        [SerializeField] private MonoBehaviour[] awakened = new MonoBehaviour[0];

        [Header("A casa as 01:56")]
        [Tooltip("As caixas do prologo. Fora.")]
        [SerializeField] private GameObject boxes;

        [Tooltip("Todas apagadas. A casa esta as escuras e e assim que ela parece "
               + "vazia — que e exactamente o que ela nao esta.")]
        [SerializeField] private Light[] lightsOff = new Light[0];

        [Tooltip("O ceu, a neblina e a lua. Sao 01:56 — sem isto a cena fica com o "
               + "perfil que estiver gravado, e quem o aplicava era o `DayTwoDirector`, "
               + "que esta noite nao corre. Um climax a luz do dia nao e uma nota "
               + "errada: e outro jogo.")]
        [SerializeField] private Pungent.Atmosphere.DayCycleController dayCycle;

        [Tooltip("Onde ele entra: a porta 3B, do lado de dentro.")]
        [SerializeField] private Vector3 startPosition = new Vector3(5.10f, 0.05f, -2.20f);
        [Tooltip("Virado para dentro de casa: o corredor fica a oeste.")]
        [SerializeField] private float startYaw = 270f;

        [Header("As portas")]
        [Tooltip("Fechadas e por trancar. A do quarto do Rui vive aqui: o capitulo "
               + "pede que ela se abra, e trancada seria um beco.")]
        [SerializeField] private DoorDragInteractable[] closedDoors = new DoorDragInteractable[0];

        [Tooltip("Abertas. A do quarto do Tomas, porque hoje ja nao ha nada nela que "
               + "a mantenha fechada; a da casa de banho e a da lavandaria, porque e "
               + "por ali que ele sai quando sair.\n\n"
               + "Nao e so encenacao: uma porta fechada tapa o vao na NavMesh, e com "
               + "estas duas fechadas o Rui ficava emparedado na lavandaria e a caca "
               + "nunca acontecia.")]
        [SerializeField] private DoorDragInteractable[] openDoors = new DoorDragInteractable[0];

        [Tooltip("A da rua. Trancada, e a chave nao esta com ele.")]
        [SerializeField] private DoorDragInteractable frontDoor;

        [Header("O Rui")]
        [SerializeField] private GameObject rui;
        [Tooltip("Onde ele espera enquanto o jogador anda pela casa: a lavandaria, "
               + "atras de duas portas fechadas. Nao esta escondido por truque de "
               + "camara — esta mesmo la, e da para o ouvir se houver silencio.")]
        [SerializeField] private Vector3 ruiWaitingSpot = new Vector3(1.55f, 0f, -4.30f);
        [SerializeField] private float ruiWaitingYaw = 0f;

        [SerializeField] private ChapterDirector director;

        private bool staged;

        /// <summary>Verdadeiro quando esta cena esta a correr como Dia 5.</summary>
        public bool IsClimaxNight { get; private set; }

        private void Awake()
        {
            IsClimaxNight = HasFired(climaxEvent);
            if (!IsClimaxNight) return;

            foreach (var component in suspended)
                if (component != null) component.enabled = false;

            foreach (var go in deactivated)
                if (go != null) go.SetActive(false);

            DeactivateEarlierChapterContent();

            // A rotina domestica e o territorio pertencem aos dias em que ele ainda
            // fingia. Esta noite quem manda no corpo dele e o `RuiHunt`.
            if (rui != null)
            {
                var routine = rui.GetComponent<PrototypeNpcRoutine>();
                if (routine != null) routine.enabled = false;
            }
            foreach (var territory in FindObjectsOfType<NpcRoomTerritory>())
                territory.enabled = false;

            // Dormir nao e uma saida hoje. Deixar a cama a oferecer "Sleep" dava ao
            // jogador um botao para fechar um capitulo que ele nao resolveu.
            foreach (var sleep in FindObjectsOfType<PlayerSleep>(true))
                sleep.enabled = false;
        }

        /// <summary>
        /// O acontecimento ja passou?
        ///
        /// Pergunta a **todos** os directores e nao ao primeiro que encontrar. A
        /// cena nova traz o seu proprio `GAME_SYSTEMS`, que se desactiva sozinho por
        /// haver ja um vindo da cena anterior — mas isso acontece no `Awake` dele, e
        /// a ordem entre `Awake`s de objectos diferentes nao esta definida. Apanhar
        /// o exemplar errado dava um registo de acontecimentos vazio e esta noite
        /// nunca comecava.
        /// </summary>
        private static bool HasFired(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            foreach (var candidate in FindObjectsOfType<ChapterDirector>())
                if (candidate.HasSeen(id)) return true;
            return false;
        }

        private void Start()
        {
            if (!IsClimaxNight || staged) return;
            staged = true;

            // Reafirmado. Desligar no `Awake` garante que nenhum `Start` alheio
            // corre, mas nao garante que nenhum `Awake` alheio volte a ligar: a
            // ordem entre `Awake`s de objectos diferentes nao esta definida, e o
            // `PrologueStage.Awake` liga o `PlayerDeskOpening` com as proprias maos.
            // Se ele acordar depois deste, a abertura a secretaria ficava viva no
            // meio do climax. Aqui ja todos acordaram.
            foreach (var component in suspended)
                if (component != null) component.enabled = false;

            if (director == null) director = FindObjectOfType<ChapterDirector>();
            if (dayCycle == null) dayCycle = FindObjectOfType<Pungent.Atmosphere.DayCycleController>();
            dayCycle?.ApplyNight();

            if (content != null) content.SetActive(true);
            foreach (var component in awakened)
                if (component != null) component.enabled = true;

            DeactivateEarlierChapterContent();
            FindObjectOfType<RouterInteractable>(true)?.SetState(false, true);
            foreach (var light in lightsOff)
                if (light != null) light.enabled = false;

            PlacePlayer();
            PlaceRui();
            SetDoors();
        }

        /// <summary>
        /// Limpa os grupos que a mesma cena usa nos capítulos anteriores.
        ///
        /// Algumas referências do array ficaram vazias no asset da cena. Procurar
        /// estes cinco grupos estáveis pelo nome dá uma segunda rede: no Dia 5 não
        /// podem reaparecer caixas, chaves, tarefas ou volumes do Dia 1/3 a roubar
        /// interações ao clímax.
        /// </summary>
        private void DeactivateEarlierChapterContent()
        {
            if (boxes == null) boxes = GameObject.Find("PROLOGUE_BOXES");
            if (boxes != null) boxes.SetActive(false);

            Deactivate("PROLOGUE_KEYS");
            Deactivate("DAY1_GOODDEAL");
            Deactivate("DAY3_SIGNS");
            Deactivate("HOME_TASKS");

            // A casa cala-se.
            //
            // Os pensamentos de passagem sao o que faz o apartamento parecer
            // habitado nos outros dias, e sao exactamente por isso a pior coisa que
            // pode acontecer esta noite: o jogador esta a atravessar a cozinha as
            // escuras com o Rui algures na casa, e o Tomas comenta que ha sempre
            // uma caneca no lava-loica. Uma banalidade dita no momento errado nao e
            // uma banalidade — e o jogo a dizer ao jogador que nao ha perigo nenhum.
            //
            // Cada um deles ja tem `silencedByEvent: climax`. Isto e a segunda rede,
            // pela mesma razao que os outros quatro grupos tem duas: se a ferramenta
            // de ambiente nao tiver corrido desde a ultima vez que a tabela mudou, a
            // porta esta no sitio errado e nao ha erro nenhum a dizer.
            Deactivate("AMBIENT_THOUGHTS");

            // As tarefas domesticas e as mudancas do Dia 2/3. Cada tarefa ja tem a
            // sua janela e cada mudanca ja aconteceu, mas a regra desta casa e a
            // mesma para os seis grupos: nada de outro capitulo fica vivo esta
            // noite. Um prompt "Put a wash on" na lavandaria, que e por onde ele
            // sai, era a pior das heranças possiveis.
            Deactivate("DAY_PRESSURE");
        }

        private static void Deactivate(string objectName)
        {
            var go = GameObject.Find(objectName);
            if (go != null) go.SetActive(false);
        }

        private void PlacePlayer()
        {
            var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
            if (motor == null) return;

            // O `CharacterController` sobrepoe-se a escritas directas no transform:
            // mover sem o desligar deixava o jogador onde estava, calado.
            var body = motor.GetComponent<CharacterController>();
            if (body != null) body.enabled = false;
            motor.transform.position = startPosition;
            motor.transform.rotation = Quaternion.Euler(0f, startYaw, 0f);
            if (body != null) body.enabled = true;
        }

        private void PlaceRui()
        {
            if (rui == null) return;

            var agent = rui.GetComponent<UnityEngine.AI.NavMeshAgent>();
            bool wasEnabled = agent != null && agent.enabled;
            if (agent != null) agent.enabled = false;

            rui.transform.position = ruiWaitingSpot;
            rui.transform.rotation = Quaternion.Euler(0f, ruiWaitingYaw, 0f);
            rui.SetActive(true);

            if (agent != null) agent.enabled = wasEnabled;
        }

        /// <summary>
        /// O estado das portas e metade do primeiro minuto deste capitulo.
        ///
        /// A do quarto dele esta fechada, e nunca esteve. A do quarto do Tomas esta
        /// aberta, porque ja nao ha nada nela que a mantenha fechada. Nenhuma das
        /// duas e comentada por ninguem — sao so o que se ve ao entrar.
        /// </summary>
        private void SetDoors()
        {
            foreach (var door in closedDoors)
            {
                if (door == null) continue;
                door.SetLocked(false);
                door.SetLockPromptArmed(false);
                door.ForceClosed();
            }

            foreach (var door in openDoors)
            {
                if (door == null) continue;
                // Destrancar antes de abrir: o `ForceOpen` recusa-se a mexer numa
                // porta trancada, e a do quarto do Tomas ficou trancada da noite
                // anterior — o Dia 3 acaba com ele a tranca-la para dormir.
                door.SetLocked(false);
                door.SetLockPromptArmed(false);
                door.ForceOpen();
            }

            if (frontDoor != null)
            {
                frontDoor.ForceClosed();
                frontDoor.SetLocked(true);
            }
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(MonoBehaviour[] toSuspend, GameObject[] toDeactivate,
            GameObject climaxContent, MonoBehaviour[] toWake,
            GameObject prologueBoxes, Light[] toTurnOff, GameObject ruiObject,
            DoorDragInteractable[] closed, DoorDragInteractable[] open,
            DoorDragInteractable front)
        {
            suspended = toSuspend;
            deactivated = toDeactivate;
            content = climaxContent;
            awakened = toWake;
            boxes = prologueBoxes;
            lightsOff = toTurnOff;
            rui = ruiObject;
            closedDoors = closed;
            openDoors = open;
            frontDoor = front;
        }
#endif
    }
}
