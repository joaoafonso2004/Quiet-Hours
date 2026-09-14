using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Uma coisa que acontece **com o jogador na sala**.
    ///
    /// ---
    ///
    /// **O que isto e, e porque nao e a <see cref="HouseChange"/>.**
    ///
    /// A `HouseChange` exige duas coisas: o jogador **longe** e o objecto fora do
    /// enquadramento. E o medo de encontrar uma coisa ja mudada e nao saber quando.
    /// Funciona, e e metade do genero.
    ///
    /// A outra metade e o oposto exacto: o jogador esta **ali**, a dois metros, e a
    /// coisa acontece na mesma. Ele nao pode dizer que foi enquanto estava noutra
    /// divisao, porque nao esteve noutra divisao. E a diferenca entre "isto nao estava
    /// assim" e "isto acabou de acontecer".
    ///
    /// Por isso a condicao aqui e a inversa — uma distancia **maxima** — e por isso
    /// isto e uma classe separada e nao um campo novo naquela. Duas regras contrarias
    /// dentro do mesmo componente sao duas oportunidades de armar uma pela outra e
    /// nao ter aviso nenhum a dizer qual.
    ///
    /// ---
    ///
    /// **As tres testemunhas**, e cada uma e um efeito diferente:
    ///
    /// - <see cref="Witness.MustNotSee"/> — ele esta na sala e esta virado para outro
    ///   lado. Deixou o comando na mesa; volta-se e ele esta no chao. **Nao ha
    ///   distancia que o proteja**, e a diferenca para a `HouseChange`.
    /// - <see cref="Witness.MustSee"/> — ele esta a olhar. A luz da rua apaga-se com
    ///   ele encostado ao lava-loica a ver a rua. Nao ha duvida nenhuma sobre se
    ///   aconteceu: aconteceu, ele viu. A duvida passa a ser sobre porque.
    /// - <see cref="Witness.Ignores"/> — para efeitos que se ouvem e nao se veem.
    ///
    /// ---
    ///
    /// **As regras da <see cref="HouseChange"/> continuam todas de pe**, e nao sao
    /// negociaveis: pequeno, nunca comentado, sempre com uma explicacao inocente
    /// possivel, e **uma vez so**. Uma lampada funde-se. Um comando cai da mesa. Um
    /// candeeiro com mau contacto pisca. Nada disto prova nada — e por isso que
    /// funciona.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WitnessedCue : MonoBehaviour
    {
        public enum Witness
        {
            /// <summary>Perto, mas virado para outro lado.</summary>
            MustNotSee,
            /// <summary>Perto e a olhar mesmo para aquilo.</summary>
            MustSee,
            /// <summary>Perto, e o resto e indiferente.</summary>
            Ignores
        }

        public enum Effect
        {
            /// <summary>Apaga a luz.</summary>
            LightOff,
            /// <summary>Acende a luz.</summary>
            LightOn,
            /// <summary>Desloca o alvo, em metros no espaco local dele.</summary>
            Move,
            /// <summary>Roda o alvo, em graus.</summary>
            Rotate,
            /// <summary>Liga o alvo.</summary>
            Appear,
            /// <summary>Desliga o alvo.</summary>
            Vanish,
            /// <summary>Encosta ou abre uma porta pelo componente dela.</summary>
            DoorAjar,
            /// <summary>Nada muda de sitio. So o som.</summary>
            SoundOnly
        }

        [Header("O que acontece")]
        [SerializeField] private Effect effect = Effect.LightOff;

        [Tooltip("O objecto afectado. Vazio = este.")]
        [SerializeField] private Transform target;

        [Tooltip("Para Move: metros no espaco local. Para Rotate: graus.")]
        [SerializeField] private Vector3 amount = new Vector3(0f, 0f, -0.2f);

        [SerializeField] private Light lamp;

        [SerializeField] private DoorDragInteractable door;

        [Tooltip("Para DoorAjar, quando o que abre e o frigorifico. Tem o seu proprio "
               + "componente porque tem a sua propria dobradica, o seu proprio angulo "
               + "e uma luz la dentro.")]
        [SerializeField] private FridgeDoor fridge;

        [Tooltip("Para DoorAjar: se a porta fica aberta ou fechada.")]
        [SerializeField] private bool doorOpen = true;

        [Header("Quem esta a ver")]
        [SerializeField] private Witness witness = Witness.MustNotSee;

        [Tooltip("O jogador tem de estar **mais perto** do que isto. E a condicao "
               + "inversa da HouseChange e e o que torna este componente outra coisa.")]
        [SerializeField, Min(0.5f)] private float maximumDistance = 6f;

        [Tooltip("E nao mais perto do que isto. Serve para o efeito nao acontecer "
               + "colado a cara do jogador, onde a periferia o denuncia mesmo de "
               + "costas. Zero = sem minimo.")]
        [SerializeField, Min(0f)] private float minimumDistance = 1.2f;

        [Tooltip("Ponto a partir do qual a distancia e medida. Vazio = este objecto. "
               + "Serve para a condicao ser 'ele esta na cozinha' e o efeito "
               + "acontecer na rua, a vinte metros.")]
        [SerializeField] private Transform measureFrom;

        [Tooltip("Quanto tempo seguido a condicao tem de se manter. Curto de mais e "
               + "o efeito dispara enquanto o jogador vira a cabeca.")]
        [SerializeField, Min(0.1f)] private float dwellSeconds = 1.2f;

        [SerializeField] private LayerMask sightBlockers = ~0;
        [Tooltip("Altura do alvo, para os testes de visibilidade.")]
        [SerializeField, Min(0.1f)] private float targetHeight = 1.2f;

        [Tooltip("O que conta como 'estar a olhar', quando nao e o proprio alvo.\n\n"
               + "Existe porque a condicao e o efeito nem sempre estao no mesmo sitio: "
               + "a luz da rua que se funde esta dezoito metros abaixo da cozinha, e "
               + "perguntar se o jogador a esta a ver e perguntar por um poste atras "
               + "de um vidro. A pergunta certa e outra — ele esta encostado ao "
               + "lava-loica a olhar pela janela? — e o alvo dela e a janela.")]
        [SerializeField] private Transform sightTarget;

        [Tooltip("Angulo maximo entre o olhar e a direccao do alvo de vista. Zero = "
               + "sem teste de direccao.\n\n"
               + "Faz o trabalho que o raio nao consegue fazer atraves de um vidro: "
               + "nao pergunta se ha parede pelo meio, pergunta para onde e que ele "
               + "esta virado. Barato, e nao ha geometria que o parta.")]
        [SerializeField, Range(0f, 180f)] private float maximumFacingAngle = 0f;

        [Header("Janela")]
        [SerializeField] private string requiresEvent;
        [SerializeField] private string silencedByEvent;

        [Header("Som (opcional)")]
        [Tooltip("Baixo e curto. Num efeito MustNotSee este som e o convite para o "
               + "jogador se virar — sem ele, uma mudanca que ninguem encontra nao "
               + "aconteceu.")]
        [SerializeField] private AudioSource sound;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.4f;

        [Header("Depois")]
        [Tooltip("Quase sempre vazio. Se o Tomas diz 'isso nao estava assim', o jogo "
               + "confirmou ao jogador que ele viu bem — e a duvida era a unica coisa "
               + "que a mudanca tinha para dar.")]
        [SerializeField, TextArea] private string thought;

        [SerializeField] private string raisesEvent;

        [SerializeField] private ChapterDirector director;
        [SerializeField] private PlayerThoughtDirector thoughts;

        private Transform player;
        private Camera eye;
        private float heldSince = -1f;
        private bool done;

        /// <summary>Ja aconteceu. Util para inspeccionar e para os testes.</summary>
        public bool HasHappened => done;

        private void Awake()
        {
            if (target == null) target = transform;
            if (lamp == null && target != null) lamp = target.GetComponentInChildren<Light>(true);
            if (thoughts == null) thoughts = FindObjectOfType<PlayerThoughtDirector>();
        }

        private void Update()
        {
            if (done) return;
            if (!Allowed()) { heldSince = -1f; return; }
            if (!ResolvePlayer()) return;

            if (!Conditions()) { heldSince = -1f; return; }

            if (heldSince < 0f) heldSince = Time.time;
            if (Time.time - heldSince < dwellSeconds) return;

            Apply();
        }

        private bool Conditions()
        {
            Transform anchor = measureFrom != null ? measureFrom : transform;
            float distance = Vector3.Distance(player.position, anchor.position);
            if (distance > maximumDistance) return false;
            if (minimumDistance > 0f && distance < minimumDistance) return false;

            if (eye == null) return true;

            Transform watched = sightTarget != null ? sightTarget : target;

            // O teste de direccao corre sempre que estiver ligado, seja qual for a
            // testemunha: e uma condicao de enquadramento e nao uma alternativa ao
            // teste de visibilidade.
            if (maximumFacingAngle > 0f && watched != null)
            {
                Vector3 toWatched = watched.position - eye.transform.position;
                if (toWatched.sqrMagnitude > 0.0001f &&
                    Vector3.Angle(eye.transform.forward, toWatched) > maximumFacingAngle)
                    return false;
            }

            if (witness == Witness.Ignores) return true;

            // O teste completo — enquadramento **e** linha de vista — e o mesmo que o
            // Rui usa para saber se foi apanhado. Uma parede pelo meio nao chega para
            // dizer que nao viu, e estar no ecra tapado por um armario tambem nao.
            bool sees = Pungent.NPC.PlayerSight.CanPlayerSee(
                eye, watched, targetHeight, sightBlockers, 0.04f, maximumDistance + 30f);

            return witness == Witness.MustSee ? sees : !sees;
        }

        private void Apply()
        {
            done = true;

            switch (effect)
            {
                case Effect.LightOff:
                    if (lamp != null) lamp.enabled = false;
                    break;

                case Effect.LightOn:
                    if (lamp != null) lamp.enabled = true;
                    break;

                case Effect.Move:
                    if (target != null) target.localPosition += amount;
                    break;

                case Effect.Rotate:
                    if (target != null) target.localRotation *= Quaternion.Euler(amount);
                    break;

                case Effect.Appear:
                    if (target != null) target.gameObject.SetActive(true);
                    break;

                case Effect.Vanish:
                    if (target != null) target.gameObject.SetActive(false);
                    break;

                case Effect.DoorAjar:
                    // Pelo componente e nao pelo transform: a porta tem `Rigidbody` e
                    // `HingeJoint`, e escrever na rotacao dela e brigar com a fisica e
                    // ganhar um movel a tremer. Licao ja escrita no `DailyPressureWiring`.
                    if (door != null) door.SetOpen(doorOpen, true);

                    // O frigorifico anima-se sozinho a partir daqui, e traz a luz de
                    // dentro com ele — que e metade do efeito.
                    if (fridge != null) fridge.SetOpen(doorOpen);
                    break;
            }

            if (sound != null && clip != null) sound.PlayOneShot(clip, volume);

            if (!string.IsNullOrWhiteSpace(thought))
                thoughts?.Think($"cue_{GetInstanceID()}", thought, 3, true, 3.2f);

            if (!string.IsNullOrWhiteSpace(raisesEvent))
            {
                director = ChapterDirector.Resolve(director);
                director?.Notify(raisesEvent);
            }
        }

        private bool ResolvePlayer()
        {
            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor == null) return false;
                player = motor.transform;
            }

            // A camara e outra em cada cena e pode ser trocada por um corte; depois de
            // destruida compara como nula e deixa-se substituir.
            if (eye == null) eye = player.GetComponentInChildren<Camera>(true);
            if (eye == null) eye = Camera.main;

            return true;
        }

        /// <summary>Na duvida cala-se, como o resto do jogo.</summary>
        private bool Allowed()
        {
            bool gated = !string.IsNullOrWhiteSpace(requiresEvent)
                      || !string.IsNullOrWhiteSpace(silencedByEvent);
            if (!gated) return true;

            director = ChapterDirector.Resolve(director);
            if (director == null) return false;

            if (!string.IsNullOrWhiteSpace(requiresEvent) && !director.HasSeen(requiresEvent))
                return false;
            if (!string.IsNullOrWhiteSpace(silencedByEvent) && director.HasSeen(silencedByEvent))
                return false;

            return true;
        }

#if UNITY_EDITOR
        /// <summary>Usado pelas ferramentas de ligacao, que vivem noutra assembly.</summary>
        public void EditorConfigure(Effect how, Transform what, Vector3 howMuch,
            Witness who, float maxDistance, string requires, string silencedBy,
            Transform measure = null, Light light = null, DoorDragInteractable theDoor = null,
            AudioSource source = null, AudioClip audio = null, float dwell = 1.2f,
            string thoughtText = null, string raises = null, float minDistance = 1.2f,
            Transform lookedAt = null, float facingAngle = 0f, FridgeDoor theFridge = null)
        {
            sightTarget = lookedAt;
            maximumFacingAngle = facingAngle;
            fridge = theFridge;
            effect = how;
            target = what;
            amount = howMuch;
            witness = who;
            maximumDistance = maxDistance;
            requiresEvent = requires;
            silencedByEvent = silencedBy;
            measureFrom = measure;
            lamp = light;
            door = theDoor;
            sound = source;
            clip = audio;
            dwellSeconds = dwell;
            thought = thoughtText;
            raisesEvent = raises;
            minimumDistance = minDistance;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            Transform anchor = measureFrom != null ? measureFrom : transform;
            var t = target != null ? target : transform;

            Gizmos.color = new Color(0.4f, 1f, 0.55f, 0.25f);
            Gizmos.DrawWireSphere(anchor.position, maximumDistance);
            if (minimumDistance > 0f)
            {
                Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.25f);
                Gizmos.DrawWireSphere(anchor.position, minimumDistance);
            }

            Gizmos.color = new Color(0.4f, 1f, 0.55f, 0.9f);
            Gizmos.DrawWireCube(t.position, Vector3.one * 0.35f);
            if (anchor != t) Gizmos.DrawLine(anchor.position, t.position);
        }
    }
}
