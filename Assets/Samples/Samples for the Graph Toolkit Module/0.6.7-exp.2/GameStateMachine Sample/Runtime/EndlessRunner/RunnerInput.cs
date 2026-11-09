using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// Reads the two inputs the endless runner needs: a jump press and a pause press.
    /// </summary>
    /// <remarks>
    /// Keeping input handling here means the <c>#if</c> that picks between Unity's two input systems appears once, and
    /// a <see cref="RuntimeGameState"/> that reacts to the same press reads it the same way the player does. Which
    /// branch compiles depends on whether the project has the Input System package installed;
    /// </remarks>
    public static class RunnerInput
    {
        /// <summary>
        /// Reports whether the player asked to jump during the current frame.
        /// </summary>
        /// <returns><see langword="true"/> on the frame the jump input is pressed, otherwise <see langword="false"/>.</returns>
        /// <remarks>Space, the up arrow, a left mouse click and a screen tap all count.</remarks>
        public static bool JumpPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame))
                return true;

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;

            var touchscreen = Touchscreen.current;

            return touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.UpArrow)
                || Input.GetMouseButtonDown(0)
                || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
#endif
        }

        /// <summary>
        /// Reports whether the player asked to pause or unpause during the current frame.
        /// </summary>
        /// <returns><see langword="true"/> on the frame the pause input is pressed, otherwise <see langword="false"/>.</returns>
        public static bool PausePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;

            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }
}
