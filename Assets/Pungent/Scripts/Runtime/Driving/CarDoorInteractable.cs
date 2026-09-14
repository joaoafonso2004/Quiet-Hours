using Pungent.Interaction;
using UnityEngine;

namespace Pungent.Driving
{
    /// <summary>
    /// A porta do condutor: por onde se entra e por onde se sai.
    ///
    /// ---
    ///
    /// **Faltava-lhe metade, e essa metade trancava o Dia 4 inteiro.**
    ///
    /// Ela so sabia entrar. O `Prompt` devolvia vazio com o jogador sentado — o que
    /// esta certo durante a viagem da noite — mas o Dia 4 **comeca** com ele ao
    /// volante, a conduzir ate a oficina. Chegado la, nao havia um unico objecto no
    /// jogo que oferecesse sair: o `CarSeat.Exit` existia e ninguem o chamava. O
    /// capitulo ficava com o jogador sentado num carro parado, a olhar para um
    /// objectivo que mandava procurar uma pessoa a trinta metros.
    ///
    /// **So com o carro parado.** Nao ha prompt de sair a andar — nao por seguranca
    /// de codigo, mas porque nao se sai de um carro em movimento e um prompt que o
    /// oferece e o jogo a dizer que sim.
    ///
    /// Entrar tranca logo a seguir, sem lhe perguntar nada. Quem entra num carro a
    /// fugir de alguem nao decide trancar — tranca. Deixar isso ao jogador dava-lhe
    /// hipotese de se esquecer, e um jogo em que o momento de seguranca depende de
    /// alguem se lembrar de carregar noutra tecla nao e tenso, e injusto. A seguir
    /// e a chave, que ja nao depende dele (ver <see cref="CarIgnition"/>).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CarDoorInteractable : MonoBehaviour, IPlayerInteractable
    {
        [SerializeField] private CarSeat seat;
        [SerializeField] private CarIgnition ignition;
        [SerializeField] private string prompt = "Get in";

        [Tooltip("O de sair. So aparece com ele sentado e o carro parado.")]
        [SerializeField] private string exitPrompt = "Get out";

        [Tooltip("Enquanto isto for falso a porta nao se oferece: e o caso durante "
               + "toda a viagem, e ate ele sair para ver o motor.")]
        [SerializeField] private bool available;

        [Tooltip("Deixar sair. Ligado no Dia 4, onde o capitulo comeca com ele a "
               + "conduzir e continua a pe.\n\n"
               + "Desligado na estrada da noite: la o carro morre e quem manda no "
               + "momento de sair e a encenacao, nao um prompt.")]
        [SerializeField] private bool canExit = true;

        [Tooltip("Velocidade acima da qual nao se sai, em m/s.")]
        [SerializeField, Min(0.05f)] private float stillSpeed = 0.4f;

        [SerializeField] private CarDriver car;

        public string Prompt
        {
            get
            {
                if (!available || seat == null) return string.Empty;
                if (!seat.Seated) return prompt;
                return CanGetOut ? exitPrompt : string.Empty;
            }
        }

        /// <summary>
        /// Sentado, com licenca para sair, e com o carro parado.
        ///
        /// Sem `car` ligado assume-se parado: uma cena de teste sem `CarDriver` nao
        /// pode ficar com o jogador preso la dentro por falta de uma referencia.
        /// </summary>
        private bool CanGetOut =>
            canExit && (car == null || Mathf.Abs(car.Speed) <= stillSpeed);

        public bool HoldToInteract => false;

        private void Awake()
        {
            if (seat == null) seat = FindObjectOfType<CarSeat>();
            if (ignition == null) ignition = FindObjectOfType<CarIgnition>();
            if (car == null) car = FindObjectOfType<CarDriver>();
        }

        /// <summary>Abre a hipotese de entrar. Chamado quando o estranho se aproxima.</summary>
        public void MakeAvailable() => available = true;

        public void BeginInteraction(PlayerInteractor interactor)
        {
            if (!available || seat == null) return;

            if (seat.Seated)
            {
                if (CanGetOut) seat.Exit();
                return;
            }

            seat.Enter();
            seat.SetLocked(true);
            ignition?.Begin();
        }

        public void UpdateInteraction(PlayerInteractor interactor, Vector2 pointerDelta) { }
        public void EndInteraction(PlayerInteractor interactor) { }
    }
}
