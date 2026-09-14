using Pungent.Atmosphere;
using Pungent.NPC;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// A manha seguinte a mudanca. O Dia 1.
    ///
    /// Este componente existe por duas razoes, e a segunda so se descobriu ao
    /// procurar sitio para a primeira.
    ///
    /// **O capitulo nao existia.** O §5 pede um dia inteiro entre o prologo e a
    /// noite das 02:47: a rotina domestica, o Rui a cozinhar, o carro visto da
    /// varanda, e o negocio das pecas a nascer num anuncio. E onde o Rui comenta o
    /// carro sem o Tomas alguma vez lhe ter falado dele — a primeira fissura a
    /// serio do jogo.
    ///
    /// **E a corrente estava partida.** O ultimo passo do prologo levanta
    /// `prologue_done` e ninguem o ouvia; o `PrologueStage.Finish()` — que poe a
    /// casa no estado normal e arranca a noite das 02:47 — nao era chamado por
    /// codigo nenhum. Resultado: as caixas ficavam na sala para sempre, o Rui nunca
    /// ganhava rotina, e o `PlayerDeskOpening` nunca comecava, porque `Finish()` e
    /// o unico sitio de onde ele arranca. Quem pegava no primeiro `DaySlept` era o
    /// `DayTwoDirector`, que saltava direito a manha do Dia 3.
    ///
    /// E a armadilha de sempre — um passo preso a um evento sem dono nao da erro
    /// nenhum — desta vez entre dois capitulos. Este componente e o dono.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayOneStage : MonoBehaviour
    {
        [Tooltip("O prologo acabou. E aqui que este dia comeca.")]
        [SerializeField] private string startEvent = "prologue_done";

        [Tooltip("Levantado pelo ultimo passo do capitulo. E o que entrega o jogo a "
               + "noite das 02:47.")]
        [SerializeField] private string endEvent = "day_one_done";

        [Tooltip("O grupo com tudo o que e do Dia 1. Comeca desligado: o volume da "
               + "cozinha disparava a meio do prologo, e a secretaria oferecia "
               + "\"Sit down and work\" na noite em que ele ainda desfazia caixas.")]
        [SerializeField] private GameObject content;

        [Header("O prologo")]
        [Tooltip("Quem sabe repor a casa: caixas fora, luzes apagadas, rotina do Rui "
               + "ligada, guiao da noite reposto. Ja existia e nunca era chamado.")]
        [SerializeField] private PrologueStage prologue;

        [Header("A noite das 02:47")]
        [Tooltip("Arrancado no fim deste dia, e nao no fim do prologo. E o Dia 2.")]
        [SerializeField] private PlayerDeskOpening deskOpening;
        [Tooltip("A quest do router. Ligada com a abertura a secretaria, nao antes: "
               + "ligada de manha punha objectivos das 02:47 no ecra durante o Dia 1.")]
        [SerializeField] private OpeningQuestDirector openingQuest;

        [Header("Acordar")]
        [SerializeField] private ScreenEyelid eyelid;
        [SerializeField] private DayCycleController dayCycle;
        [SerializeField] private Transform player;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Transform tiltTransform;

        [Tooltip("Suspensos enquanto ele acorda. O jogador nao anda de olhos fechados.")]
        [SerializeField] private MonoBehaviour[] suppressed = new MonoBehaviour[0];

        [Tooltip("Onde ele fica de pe ao levantar-se da cama.")]
        [SerializeField] private Vector3 standingPosition = new Vector3(-5.20f, 0.05f, -3.20f);
        [SerializeField] private float standingYaw = 0f;

        [Tooltip("Abrir os olhos custa mais do que fecha-los.")]
        [SerializeField, Min(0.3f)] private float eyesOpenSeconds = 3.0f;
        [SerializeField, Min(0.3f)] private float standSeconds = 2.2f;

        [Header("Luzes")]
        [Tooltip("Apagadas de dia. As mesmas que o prologo acendeu.")]
        [SerializeField] private Light[] lightsOffAtDawn = new Light[0];

        [Header("O Rui")]
        [Tooltip("De manha ele esta em casa e a cozinhar — ao contrario do Dia 3, em "
               + "que ele simplesmente nao esta. E preciso que esteja: e hoje que ele "
               + "comenta o carro.")]
        [SerializeField] private GameObject rui;

        [Header("O negocio")]
        [Tooltip("O fio do anuncio. O passo do capitulo fecha quando ele acabar — o "
               + "jogador ja respondeu e ja leu onde tem de ir buscar as pecas.\n\n"
               + "Levantado daqui porque o telemovel nao fala com o director: o "
               + "`PhoneMessageService` consome acontecimentos e nao os levanta. Sem "
               + "dono, o `day1_deal` deixava o capitulo parado no terceiro passo — a "
               + "armadilha de sempre.")]
        [SerializeField] private Pungent.Interaction.PhoneConversationDefinition marketThread;
        [SerializeField] private string dealEvent = "day1_deal";
        [SerializeField] private Pungent.Interaction.PhoneMessageService messages;

        private bool dealSent;

        [SerializeField] private ChapterDirector director;

        private enum Phase { Waiting, Opening, Standing, Playing, Done }

        private Phase phase = Phase.Waiting;
        private float phaseTime;
        private Vector3 lyingPivot;

        public bool HasWoken => phase == Phase.Playing || phase == Phase.Done;

        private void Awake()
        {
            if (director == null) director = FindObjectOfType<ChapterDirector>();
            if (prologue == null) prologue = FindObjectOfType<PrologueStage>(true);
            if (deskOpening == null) deskOpening = FindObjectOfType<PlayerDeskOpening>(true);
            if (eyelid == null) eyelid = FindObjectOfType<ScreenEyelid>();
            if (dayCycle == null) dayCycle = FindObjectOfType<DayCycleController>();

            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor != null) player = motor.transform;
            }
            if (cameraPivot == null && player != null) cameraPivot = player.Find("ViewYaw");
        }

        private void Update()
        {
            // O PlayerSleep deixa o ecra preto e o Dia 1 e quem o volta a abrir.
            // Uma referencia serializada para o antigo director impedia este stage
            // de ver `prologue_done`, apesar de o sono ter terminado normalmente.
            director = ChapterDirector.Resolve(director);

            switch (phase)
            {
                case Phase.Waiting:
                    if (director != null && director.HasSeen(startEvent)) Wake();
                    break;

                case Phase.Opening: UpdateOpening(); break;
                case Phase.Standing: UpdateStanding(); break;

                case Phase.Playing:
                    UpdateDeal();
                    if (director != null && director.HasSeen(endEvent)) HandOver();
                    break;
            }
        }

        /// <summary>
        /// Acorda com o ecra ja preto — o `PlayerSleep` deixou-o assim. Tudo o que
        /// muda o mundo acontece antes de as palpebras subirem: quando os olhos
        /// abrirem, ja e de dia e as caixas ja nao estao.
        /// </summary>
        private void Wake()
        {
            phase = Phase.Opening;
            phaseTime = 0f;

            // A chamada que faltava. Repoe a casa e o Rui, e mais nada — o arranque
            // da noite das 02:47 saiu de dentro dela para poder haver um dia no meio.
            if (prologue != null)
            {
                prologue.Finish();
                if (deskOpening == null) deskOpening = prologue.DeskOpening;
                if (openingQuest == null) openingQuest = prologue.OpeningQuest;
            }

            if (content != null) content.SetActive(true);

            dayCycle?.ApplyMorning();

            foreach (var light in lightsOffAtDawn)
                if (light != null) light.enabled = false;

            if (rui != null) rui.SetActive(true);

            if (eyelid != null) eyelid.Amount = 1f;
            if (cameraPivot != null) lyingPivot = cameraPivot.localPosition;
            SetSuppressed(true);
        }

        private void UpdateOpening()
        {
            SetSuppressed(true);
            phaseTime += Time.deltaTime;

            float t = Mathf.Clamp01(phaseTime / eyesOpenSeconds);
            // Duas piscadelas antes de abrir de vez, como o adormecer tem. Abrir de
            // uma so vez le-se como um corte de camara; o que se quer e sono.
            float blink = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2.5f)) * 0.22f * (1f - t);
            if (eyelid != null) eyelid.Amount = (1f - t * t) + blink;

            if (t < 1f) return;

            if (eyelid != null) eyelid.Amount = 0f;
            phase = Phase.Standing;
            phaseTime = 0f;
        }

        private void UpdateStanding()
        {
            SetSuppressed(true);
            phaseTime += Time.deltaTime;

            float t = Mathf.Clamp01(phaseTime / standSeconds);
            float eased = t * t * (3f - 2f * t);

            if (cameraPivot != null)
            {
                Vector3 upright = lyingPivot;
                upright.y = 1.6f;
                cameraPivot.localPosition = Vector3.Lerp(lyingPivot, upright, eased);
            }

            if (tiltTransform != null)
                tiltTransform.localRotation = Quaternion.Slerp(
                    tiltTransform.localRotation, Quaternion.identity, eased * 0.4f);

            if (t < 1f) return;

            // O corpo so muda de sitio no fim: mexe-lo enquanto a camara sobe dava
            // um deslize que se le como bug.
            if (player != null)
            {
                var body = player.GetComponent<CharacterController>();
                if (body != null) body.enabled = false;
                player.position = standingPosition;
                player.rotation = Quaternion.Euler(0f, standingYaw, 0f);
                if (body != null) body.enabled = true;
            }

            SetSuppressed(false);
            phase = Phase.Playing;
        }

        /// <summary>
        /// O negocio esta feito quando o fio do anuncio se esgota: ele respondeu, e
        /// leu que a entrega e num sitio e nao a porta de casa.
        ///
        /// Nao interessa **o que** ele respondeu — as duas escolhas do §5 levam ao
        /// mesmo sitio de propósito. O que muda e o tom, e o tom ja foi registado
        /// pelo proprio servico quando ele carregou.
        /// </summary>
        private void UpdateDeal()
        {
            if (dealSent || marketThread == null) return;
            if (messages == null) messages = FindObjectOfType<Pungent.Interaction.PhoneMessageService>();
            if (messages == null) return;

            foreach (var thread in messages.Threads)
            {
                if (thread.Definition != marketThread) continue;
                if (thread.State != Pungent.Interaction.PhoneMessageService.ThreadState.Done) return;

                dealSent = true;
                director?.Notify(dealEvent);
                return;
            }
        }

        /// <summary>
        /// Fim do dia. O jogo passa a noite das 02:47, que ate hoje nunca chegava a
        /// comecar.
        /// </summary>
        private void HandOver()
        {
            phase = Phase.Done;

            if (openingQuest != null) openingQuest.enabled = true;
            if (deskOpening == null) return;

            deskOpening.enabled = true;
            deskOpening.Begin();
        }

        private void SetSuppressed(bool value)
        {
            foreach (var component in suppressed)
                if (component != null) component.enabled = !value;
        }

        private void LateUpdate()
        {
            // Reafirmado: outros directores voltam a ligar estes componentes a meio
            // dos seus proprios Updates. E a nota que o `PlayerSleep` ja tinha.
            if (phase == Phase.Opening || phase == Phase.Standing) SetSuppressed(true);
        }

#if UNITY_EDITOR
        /// <summary>Usado pela ferramenta de ligacao, que vive noutra assembly.</summary>
        public void EditorConfigure(PrologueStage stage, PlayerDeskOpening desk,
            GameObject ruiObject, Light[] lights, MonoBehaviour[] toSuppress,
            Transform pivot, Transform tilt,
            Pungent.Interaction.PhoneConversationDefinition market = null,
            GameObject dayContent = null)
        {
            content = dayContent;
            prologue = stage;
            deskOpening = desk;
            marketThread = market;
            rui = ruiObject;
            lightsOffAtDawn = lights;
            suppressed = toSuppress;
            cameraPivot = pivot;
            tiltTransform = tilt;
        }
#endif
    }
}
