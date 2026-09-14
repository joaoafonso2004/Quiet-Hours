using Pungent.Player;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// Quem senta o Tomas ao volante e quem o deixa sair.
    ///
    /// A §8.12 pede entrar e sair so em pontos controlados. Sentado, o corpo dele
    /// fica desligado e o carro leva-o; e o unico sitio do jogo onde o jogador nao
    /// anda pelo proprio pe.
    ///
    /// **A supressao e reafirmada todos os frames**, e nao so uma vez ao sentar. O
    /// `ChapterDirector` suspende o `PlayerMotor` enquanto o cartao do capitulo esta
    /// no ecra e volta a liga-lo quando ele sai — sem reafirmar, o motor acordava a
    /// meio da viagem. E a mesma armadilha que a sequencia de abertura ja tinha
    /// custado uma vez, e que o plano ja lista.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarSeat : MonoBehaviour
    {
        [Tooltip("Onde ficam os olhos de quem conduz.")]
        [SerializeField] private Transform seat;

        [Tooltip("Onde ele poe os pes ao sair. Ao lado do carro, fora da estrada.")]
        [SerializeField] private Transform exitPoint;

        [Header("Portas")]
        [SerializeField] private AudioSource doorAudio;
        [SerializeField] private AudioClip lockClip;
        [SerializeField] private AudioClip unlockClip;

        private Transform player;
        private PlayerMotor motor;
        private CharacterController controller;
        private Vector3 seatOffset;
        private Pungent.Interaction.PrototypeHUD hud;

        public bool Seated { get; private set; }

        /// <summary>Portas trancadas. So conta depois de ele voltar a entrar.</summary>
        public bool Locked { get; private set; }

        private void Awake()
        {
            // Pelo componente e nao pela tag: nenhum builder poe tag "Player" no
            // prefab, e uma tag em falta falhava aqui sem uma linha de erro.
            motor = FindObjectOfType<PlayerMotor>(true);
            if (motor == null) return;

            player = motor.transform;
            controller = motor.GetComponent<CharacterController>();

            // Ja vem agarrado ao banco do editor: e assim que o capitulo comeca, com
            // ele a conduzir. Nao ha porta nenhuma para abrir antes.
            Seated = seat != null && player.IsChildOf(seat);

            // Guardar o desvio com que a ferramenta o sentou. Nao e zero: o banco
            // marca a altura dos olhos e o prefab tem a camara 1,65 m acima da base,
            // por isso a raiz fica abaixo do banco. Sem guardar isto, voltar a
            // entrar punha-o de pe com a cabeca fora do tejadilho.
            if (Seated) seatOffset = player.localPosition;
        }

        private void LateUpdate()
        {
            // Reafirmado todos os frames como o resto: o `ChapterDirector` mexe no
            // HUD quando o cartao de capitulo entra e sai.
            if (hud == null) hud = FindObjectOfType<Pungent.Interaction.PrototypeHUD>();
            if (hud != null) hud.SetReticleHidden(Seated);

            if (!Seated) return;

            // Depois do `ChapterDirector`, que corre no seu proprio tempo.
            if (motor != null && motor.enabled) motor.enabled = false;
            if (controller != null && controller.enabled) controller.enabled = false;
        }

        /// <summary>
        /// Volta a por o Tomas la dentro.
        ///
        /// "O sedan e a unica capsula de seguranca: entrar, trancar, tentar ligar"
        /// (§5). E o mesmo banco, o mesmo desligar do corpo — a diferenca e que
        /// desta vez ele entra a fugir de alguem, e o que o carro tem de melhor ja
        /// nao e andar, e ter portas.
        /// </summary>
        public void Enter()
        {
            if (Seated || player == null || seat == null) return;
            Seated = true;

            // **O salto acontece com os olhos fechados.**
            //
            // Nao ha animacao de entrar num carro e nao vai haver: e um clip caro,
            // dificil de alinhar com uma porta que tem dobradica propria, e nao
            // acrescenta nada a historia. O que nao se pode e deixar o corpo saltar do
            // passeio para o banco a vista — isso nao le como uma pessoa a sentar-se,
            // le como o jogo a partir.
            //
            // Um piscar de dois decimos resolve-o inteiro, e resolve todos os outros
            // saltos sem animacao pelo mesmo preco. Ver `ScreenEyelid.Blink`.
            Blink(() =>
            {
                player.SetParent(seat, false);
                player.localPosition = seatOffset;
                player.localRotation = Quaternion.identity;

                // O motor primeiro, ao contrario do sair: com o corpo ainda ligado, o
                // `CharacterController` empurrava-o para fora do carro no mesmo frame.
                if (motor != null) motor.enabled = false;
                if (controller != null) controller.enabled = false;
            });
        }

        /// <summary>
        /// Pisca, se houver palpebras. Se nao houver, faz a troca na mesma.
        ///
        /// Sem o `?:` isto era uma dependencia dura: uma cena de teste sem
        /// `ScreenEyelid` deixava de conseguir entrar no carro, e o sintoma seria o
        /// prompt a responder e nao acontecer nada.
        /// </summary>
        private void Blink(System.Action swap)
        {
            if (eyelid == null) eyelid = FindObjectOfType<Pungent.Narrative.ScreenEyelid>();

            if (eyelid != null && isActiveAndEnabled) eyelid.Blink(swap);
            else swap?.Invoke();
        }

        private Pungent.Narrative.ScreenEyelid eyelid;

        /// <summary>
        /// Tranca ou destranca as portas.
        ///
        /// Nao muda nada na fisica: nao ha ninguem a abrir portas neste jogo. O que
        /// muda e o som e o que ele sabe — o estalido e a unica coisa que ele
        /// consegue fazer contra o que esta la fora, e ouvi-lo vale mais do que
        /// qualquer barreira que o jogador nunca veria a funcionar.
        /// </summary>
        public void SetLocked(bool value)
        {
            if (Locked == value) return;
            Locked = value;

            var clip = value ? lockClip : unlockClip;
            if (clip != null && doorAudio != null) doorAudio.PlayOneShot(clip);
        }

        /// <summary>
        /// Deixa-o sair. Chamado quando o carro morre — a partir dai o capitulo e
        /// a pe.
        /// </summary>
        public void Exit()
        {
            if (!Seated || player == null) return;
            Seated = false;

            Blink(() =>
            {
                player.SetParent(null, true);

                if (exitPoint != null)
                    player.SetPositionAndRotation(exitPoint.position, exitPoint.rotation);
                else
                    FallOutOfTheCar();

                // O controlador primeiro: ligar o motor com o corpo ainda desligado dava
                // o erro de sempre no primeiro frame.
                if (controller != null) controller.enabled = true;
                if (motor != null) motor.enabled = true;
            });

            // "O jogador sai usando a lanterna do telemovel" (§5). Acesa por ele e
            // nao pelo jogo: e o gesto de quem sai para o escuro e ja sabe que vai
            // precisar dela. Deixa-se apagar a seguir, se ele quiser.
            var torch = FindObjectOfType<Pungent.Interaction.PhoneLightController>(true);
            if (torch != null && !torch.IsOn) torch.SetState(true);
        }

        /// <summary>
        /// A rede para quando ninguem ligou o <see cref="exitPoint"/>.
        ///
        /// ---
        ///
        /// **Sem isto, sair do carro punha o jogador debaixo do alcatrao.**
        ///
        /// O `SetParent(null, true)` conserva a posicao no mundo, e a posicao no
        /// mundo de quem esta sentado **nao e a do banco**: a raiz do jogador fica
        /// 1,65 m abaixo dele, porque o prefab tem a camara a essa altura e o banco
        /// marca os olhos. Largado assim, o corpo aparece a um metro e meio abaixo
        /// do chao, com o `CharacterController` a ser religado dentro da geometria.
        ///
        /// Nao dava erro nenhum. Media-se assim: cena do Dia 4, `exitPoint` a nulo,
        /// jogador em y = -0,56 com o chao a 0. O capitulo ficava impossivel e a
        /// consola calada.
        ///
        /// Isto nao substitui o `exitPoint` — **avisa**, e poe-o de pe ao lado da
        /// porta do condutor para o capitulo poder continuar. Um sitio escolhido a
        /// mao e sempre melhor do que um calculado, mas nenhum dos dois e tao mau
        /// como o chao.
        /// </summary>
        private void FallOutOfTheCar()
        {
            Debug.LogWarning("[Banco] O `exitPoint` esta por ligar. Sair do carro sem ele " +
                             "deixa o jogador abaixo do chao, porque a raiz dele fica 1,65 m " +
                             "por baixo do banco. Ligar o ponto de saida ao lado da porta.", this);

            // Ao lado da porta do condutor, ao nivel a que o carro assenta.
            Vector3 beside = transform.position - transform.right * 1.2f;
            beside.y = transform.position.y;

            player.SetPositionAndRotation(beside, Quaternion.LookRotation(transform.forward, Vector3.up));
        }
    }
}
