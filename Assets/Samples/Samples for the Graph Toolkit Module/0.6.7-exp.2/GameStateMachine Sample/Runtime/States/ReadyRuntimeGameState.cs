using System;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// A <see cref="RuntimeGameState"/> that shows the title screen and waits for the player to start the game.
    /// </summary>
    [Serializable]
    public class ReadyRuntimeGameState : RuntimeGameState
    {
        /// <summary>
        /// The signal raised when the start button is pressed.
        /// </summary>
        public string StartSignal = "Start";
        
        public override void OnEnter(GameFlowRunner runner, RuntimeGameState previousState)
        {
            GameFlowUI.Instance?.Show(GameFlowScreen.Title);
        }
        
        public override void OnUpdate(GameFlowRunner runner)
        {
            if (RunnerInput.JumpPressed())
                runner.RaiseSignal(StartSignal);
        }
    }
}
