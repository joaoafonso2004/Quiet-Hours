using UnityEngine;
using UnityEngine.InputSystem;

namespace Pungent.Player
{
    /// <summary>Lê teclado/rato e expõe um estado neutro aos restantes sistemas.</summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField, Min(0.001f)] private float mouseSensitivity = 0.075f;

        public Vector2 Move { get; private set; }
        public Vector2 LookDeltaDegrees { get; private set; }
        public float Lean { get; private set; }
        
        public Vector2 PointerDelta { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool InteractHeld { get; private set; }
        public bool InteractReleased { get; private set; }
        public bool FlashlightPressed { get; private set; }
        public bool PhonePressed { get; private set; }
        public bool ChoiceOnePressed { get; private set; }
        public bool ChoiceTwoPressed { get; private set; }
        public int NumberPressed { get; private set; }

        private bool lookSuppressed;
        private bool moveSuppressed;
        private bool uiPointerActive;
        public bool CursorCaptured { get; private set; }

        private void OnEnable()
        {
            CaptureCursor();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard == null)
            {
                Move = Vector2.zero;
                Lean = 0f;
                FlashlightPressed = false;
                PhonePressed = false;
                ChoiceOnePressed = false;
                ChoiceTwoPressed = false;
                NumberPressed = 0;
            }
            else
            {
                float horizontal = moveSuppressed ? 0f : (keyboard.dKey.isPressed ? 1f : 0f) -
                                   (keyboard.aKey.isPressed ? 1f : 0f);
                float vertical = moveSuppressed ? 0f : (keyboard.wKey.isPressed ? 1f : 0f) -
                                 (keyboard.sKey.isPressed ? 1f : 0f);
                Move = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
                Lean = moveSuppressed ? 0f : ((keyboard.eKey.isPressed ? 1f : 0f) -
                       (keyboard.qKey.isPressed ? 1f : 0f));
                FlashlightPressed = keyboard.fKey.wasPressedThisFrame;
                PhonePressed = keyboard.tabKey.wasPressedThisFrame;
                NumberPressed = ReadNumberPressed(keyboard);
                ChoiceOnePressed = NumberPressed == 1;
                ChoiceTwoPressed = NumberPressed == 2;

                // **O Escape nao larga o cursor aqui.** Quem precisa do rato e que
                // o pede, pelo `SetUiPointerActive`: o menu de pausa ao abrir, as
                // escolhas de dialogo enquanto duram. Largar por tecla fazia isto
                // ao lado deles e sem saber deles — com o telemovel aberto, o
                // Escape fecha-o e deixava o rato solto no meio do jogo sem menu
                // nenhum a justifica-lo; e a fechar a pausa, o `Close` agarrava o
                // cursor e esta linha largava-o outra vez, ou nao, conforme a
                // ordem em que os dois `Update` calhassem correr.
            }

            PointerDelta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            InteractPressed = CursorCaptured && mouse != null && mouse.leftButton.wasPressedThisFrame;
            InteractHeld = CursorCaptured && mouse != null && mouse.leftButton.isPressed;
            InteractReleased = mouse != null && mouse.leftButton.wasReleasedThisFrame;

            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !CursorCaptured && !uiPointerActive)
                CaptureCursor();

            LookDeltaDegrees = CursorCaptured && mouse != null && !lookSuppressed
                ? ApplyLookSettings(PointerDelta)
                : Vector2.zero;
        }

        /// <summary>
        /// Poe as opcoes do jogador em cima do delta do rato.
        ///
        /// ---
        ///
        /// **`mouseSensitivity` continua a mandar, e o slider e um multiplicador
        /// por cima.** O campo do Inspector e a afinacao do jogo — o valor com que
        /// as cenas foram testadas — e nao um valor por omissao a espera de ser
        /// substituido. Meio da barra da exactamente esse; quem nao abrir as
        /// opcoes joga o jogo como ele foi afinado.
        ///
        /// **O `RawInput` desligado suaviza, e nao o contrario.** O Input System ja
        /// entrega o delta cru; nao ha "modo raw" para ligar. O que ha e a escolha
        /// de o passar como esta — que e o que quem joga com mira quer — ou de o
        /// misturar com o frame anterior, que tira o tremor a ratos baratos e a
        /// maos nervosas ao preco de um atraso de meio frame.
        ///
        /// **O Y invertido e uma soma e nao uma opiniao.** Quem joga assim nao
        /// consegue jogar de outra maneira, e nao ha nada a ganhar em discutir.
        /// </summary>
        private Vector2 ApplyLookSettings(Vector2 delta)
        {
            if (!Pungent.Menu.GameSettings.RawInput)
            {
                delta = Vector2.Lerp(previousDelta, delta, 0.5f);
                previousDelta = delta;
            }
            else
            {
                previousDelta = delta;
            }

            Vector2 look = delta * (mouseSensitivity * Pungent.Menu.GameSettings.SensitivityScale);
            if (Pungent.Menu.GameSettings.InvertY) look.y = -look.y;
            return look;
        }

        private Vector2 previousDelta;

        private static int ReadNumberPressed(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) return 1;
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) return 2;
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) return 3;
            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) return 4;
            return 0;
        }

        public void CaptureCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            CursorCaptured = true;
        }

        public void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            CursorCaptured = false;
            LookDeltaDegrees = Vector2.zero;
        }

        public void SetUiPointerActive(bool active)
        {
            if (uiPointerActive == active)
                return;

            uiPointerActive = active;
            if (active)
            {
                ReleaseCursor();
            }
            else
            {
                CaptureCursor();
            }
        }

        private void OnDisable()
        {
            ReleaseCursor();
            Move = Vector2.zero;
            Lean = 0f;
            PointerDelta = Vector2.zero;
            InteractPressed = false;
            InteractHeld = false;
            InteractReleased = false;
            FlashlightPressed = false;
            PhonePressed = false;
            ChoiceOnePressed = false;
            ChoiceTwoPressed = false;
            NumberPressed = 0;
            lookSuppressed = false;
            moveSuppressed = false;
            uiPointerActive = false;
        }

        public void SetLookSuppressed(bool suppressed)
        {
            lookSuppressed = suppressed;
            if (suppressed)
                LookDeltaDegrees = Vector2.zero;
        }

        public void SetMoveSuppressed(bool suppressed)
        {
            moveSuppressed = suppressed;
            if (suppressed)
            {
                Move = Vector2.zero;
                Lean = 0f;
                GetComponent<PlayerMotor>()?.StopImmediately();
            }
        }
    }
}
