using UnityEngine;

namespace Pungent.Narrative
{
    /// <summary>
    /// Uma coisa da casa que muda quando o jogador nao esta a ver.
    ///
    /// **Porque e que isto existe.**
    ///
    /// O jogo tinha uma estrutura completa e nenhuma tensao, e a razao e simples:
    /// nada no apartamento acontecia sem o jogador mandar. Ele carregava numa
    /// coisa, saia um pensamento, aparecia o objectivo seguinte. Um thriller
    /// domestico em que a casa so reage nunca assusta ninguem, por melhor que
    /// esteja escrito — porque o medo neste genero nao vem do que se ve acontecer,
    /// vem de **encontrar uma coisa ja mudada e nao saber quando**.
    ///
    /// A seccao 5 pede isto literalmente para o Dia 2 — *"no regresso, um objeto ou
    /// porta mudou discretamente"* — e o comentario do `HomeTaskInteractable` ja
    /// tinha escrito a intencao ha meses: *"e nesses segundos, encostado a varanda,
    /// que ele pode reparar que a luz do Rui se acendeu."* Nunca houve quem o
    /// fizesse.
    ///
    /// **As regras, e nenhuma delas e negociavel.**
    ///
    /// 1. **Nunca a ver.** Ver a mudanca acontecer transforma-a num truque de
    ///    guiao. Encontra-la feita e o que fica a doer. E a mesma licao que o
    ///    `InvasionSigns` ja tinha aprendido com as palpebras fechadas.
    /// 2. **Ninguem comenta.** Sem pensamento, sem HUD, sem musica. Se o Tomas diz
    ///    "isso nao estava assim", o jogo confirmou ao jogador que ele viu bem — e
    ///    a duvida era a unica coisa que a mudanca tinha para dar. O
    ///    <see cref="ProximityThought"/> daquele sitio pode mudar de fase depois, e
    ///    isso chega.
    /// 3. **Sempre explicavel.** Uma porta um palmo mais aberta, uma cadeira
    ///    rodada, uma luz acesa. Nada que so possa ter uma explicacao sinistra:
    ///    uma corrente de ar faz a primeira, um cotovelo faz a segunda. A duvida
    ///    razoavel e o que faz o jogador nao ter a certeza **de si proprio**, que e
    ///    o efeito que este jogo quer.
    /// 4. **Uma vez so.** Repetida, vira mecanica, e o jogador comeca a olhar para
    ///    tras de proposito a espera dela.
    ///
    /// O som e opcional e serve para uma coisa so: **trazer o jogador de volta.**
    /// Uma mudanca que ele nunca chegue a encontrar nao aconteceu.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HouseChange : MonoBehaviour
    {
        public enum Kind
        {
            /// <summary>Roda o alvo. Uma cadeira virada para a porta.</summary>
            Rotate,
            /// <summary>Desloca o alvo. Uma gaveta um palmo fora.</summary>
            Move,
            /// <summary>Liga o alvo. Os chinelos dele no corredor.</summary>
            Appear,
            /// <summary>Desliga o alvo. A chavena que estava na mesa.</summary>
            Vanish,
            /// <summary>Acende ou apaga uma luz.</summary>
            Light
        }

        [Header("O que muda")]
        [Tooltip("O objecto que muda. Vazio = este.")]
        [SerializeField] private Transform target;

        [SerializeField] private Kind kind = Kind.Rotate;

        [Tooltip("Para Rotate: graus em torno do eixo local. Para Move: metros na "
               + "direccao local.\n\nPequeno. Uma cadeira a 90 graus e um susto; a "
               + "25 e uma pessoa a perguntar-se se a deixou assim.")]
        [SerializeField] private Vector3 amount = new Vector3(0f, 25f, 0f);

        [Tooltip("Para Light: o estado em que ela fica.")]
        [SerializeField] private bool lightOn = true;
        [SerializeField] private Light lamp;

        [Header("Quando")]
        [Tooltip("So depois deste acontecimento. Vazio = desde o inicio do capitulo.")]
        [SerializeField] private string requiresEvent;

        [Tooltip("Deixa de poder acontecer depois deste. Uma mudanca do Dia 2 que "
               + "dispare no Dia 3 chega tarde e ja nao quer dizer nada.")]
        [SerializeField] private string silencedByEvent;

        [Tooltip("Distancia minima a que o jogador tem de estar. De perto, mesmo de "
               + "costas, a periferia e o som da propria mudanca denunciam-na.")]
        [SerializeField, Min(1f)] private float minimumDistance = 4.5f;

        [Tooltip("Quanto tempo seguido tem de estar fora de vista. Curto de mais e "
               + "a mudanca acontece enquanto o jogador vira a cabeca.")]
        [SerializeField, Min(0.2f)] private float unseenSeconds = 1.2f;

        [Header("Trazer de volta (opcional)")]
        [Tooltip("Tocado no momento da mudanca. Baixo e curto: uma dobradica, um "
               + "estalido de madeira. E o convite para o jogador voltar atras — sem "
               + "ele, uma mudanca que ninguem encontra nao aconteceu.")]
        [SerializeField] private AudioSource sound;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.35f;

        [SerializeField] private ChapterDirector director;

        private Transform player;
        private Camera eye;
        private Renderer[] renderers;
        private float unseenSince = -1f;
        private bool done;

        /// <summary>Ja aconteceu. Util para inspeccionar e para os testes.</summary>
        public bool HasHappened => done;

        private void Awake()
        {
            if (target == null) target = transform;
            if (lamp == null && target != null) lamp = target.GetComponentInChildren<Light>(true);
            renderers = target != null ? target.GetComponentsInChildren<Renderer>(true) : null;
        }

        private void Update()
        {
            if (done) return;
            if (!Allowed()) { unseenSince = -1f; return; }
            if (!ResolvePlayer()) return;

            if (!Unseen()) { unseenSince = -1f; return; }

            if (unseenSince < 0f) unseenSince = Time.time;
            if (Time.time - unseenSince < unseenSeconds) return;

            Apply();
        }

        /// <summary>
        /// Fora de vista e longe o suficiente.
        ///
        /// O teste e o tronco da piramide da camara e nao um raycast: um raycast
        /// diz se ha parede pelo meio, e uma parede nao chega. Se o objecto esta
        /// dentro do campo de visao, mesmo tapado, o jogador pode estar a andar na
        /// direccao dele e a chegar a tempo de ver.
        ///
        /// Conservador de proposito: na duvida, **nao muda**. Uma mudanca que o
        /// jogador apanha a acontecer perde-se para sempre; uma que demora mais um
        /// minuto nao perde nada.
        /// </summary>
        private bool Unseen()
        {
            if (Vector3.Distance(player.position, target.position) < minimumDistance)
                return false;

            if (eye == null) return true;

            var planes = GeometryUtility.CalculateFrustumPlanes(eye);

            if (renderers != null && renderers.Length > 0)
            {
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    if (GeometryUtility.TestPlanesAABB(planes, r.bounds)) return false;
                }
                return true;
            }

            // Sem malha (um objecto desligado a espera de aparecer): testa-se um
            // cubo pequeno a volta do sitio onde ele vai estar.
            var bounds = new Bounds(target.position, Vector3.one * 0.6f);
            return !GeometryUtility.TestPlanesAABB(planes, bounds);
        }

        private void Apply()
        {
            done = true;

            switch (kind)
            {
                case Kind.Rotate:
                    target.localRotation *= Quaternion.Euler(amount);
                    break;

                case Kind.Move:
                    target.localPosition += amount;
                    break;

                case Kind.Appear:
                    target.gameObject.SetActive(true);
                    break;

                case Kind.Vanish:
                    target.gameObject.SetActive(false);
                    break;

                case Kind.Light:
                    if (lamp != null) lamp.enabled = lightOn;
                    break;
            }

            if (sound != null && clip != null) sound.PlayOneShot(clip, volume);
        }

        private bool ResolvePlayer()
        {
            if (player == null)
            {
                var motor = FindObjectOfType<Pungent.Player.PlayerMotor>();
                if (motor == null) return false;
                player = motor.transform;
                eye = motor.GetComponentInChildren<Camera>(true);
            }

            // O jogador e outro objecto em cada cena; a camara pode ter sido
            // trocada por um corte. Ambos comparam como nulos depois de destruidos.
            if (eye == null && player != null)
                eye = player.GetComponentInChildren<Camera>(true);

            return player != null;
        }

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
        public void EditorConfigure(Transform what, Kind how, Vector3 howMuch,
            string requires, string silencedBy, float distance = 4.5f, Light light = null,
            bool on = true)
        {
            target = what;
            kind = how;
            amount = howMuch;
            requiresEvent = requires;
            silencedByEvent = silencedBy;
            minimumDistance = distance;
            lamp = light;
            lightOn = on;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            var t = target != null ? target : transform;
            Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.85f);
            Gizmos.DrawWireCube(t.position, Vector3.one * 0.4f);
            Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.18f);
            Gizmos.DrawWireSphere(t.position, minimumDistance);
        }
    }
}
