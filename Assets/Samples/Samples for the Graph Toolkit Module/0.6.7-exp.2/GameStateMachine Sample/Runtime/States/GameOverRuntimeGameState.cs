using System;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// A <see cref="RuntimeGameState"/> that shows the game over screen and waits for the player to start again.
    /// </summary>
    [Serializable]
    public class GameOverRuntimeGameState : RuntimeGameState
    {
        /// <summary>
        /// The signal raised when the player asks to play again.
        /// </summary>
        public string ResetSignal = "Reset";
        
        public override void OnEnter(GameFlowRunner runner, RuntimeGameState previousState)
        {
            GameFlowUI.Instance?.Show(GameFlowScreen.GameOver);

            PlayerJumper.Instance?.SetSprite(PlayerJumper.SpriteState.Dead);
        }
        
        public override void OnUpdate(GameFlowRunner runner)
        {
            if (RunnerInput.JumpPressed())
                runner.RaiseSignal(ResetSignal);
        }
        
        public override void OnExit(GameFlowRunner runner, RuntimeGameState nextState)
        {
            PlayerJumper.Instance?.SetSprite(PlayerJumper.SpriteState.Alive);
        }
    }
}
