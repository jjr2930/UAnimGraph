using System;
using Unity.GraphToolkit.Editor;

namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{
    /// <summary>
    /// A <see cref="GameState"/> whose runtime shows the game over screen and waits for the player to start again.
    /// </summary>
    [Serializable]
    [State(null, null, "Game Over")]
    class GameOverGameState : GameState
    {
        const string ResetSignalOptionName = "ResetSignal";
        
        public override void OnEnable()
        {
            base.OnEnable();
            Tooltip = "Shows the game over screen and raises a signal when the player asks to play again.";
        }
        
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>(ResetSignalOptionName)
                .WithDisplayName("Reset Signal")
                .WithTooltip("The signal raised when the player asks to play again. Must match a Game Signal condition on one of this state's transitions.")
                .WithDefaultValue("Reset")
                .Build();
        }
        
        public override RuntimeGameState CreateRuntimeState() => new GameOverRuntimeGameState
        {
            ResetSignal = GetOptionValue<string>(ResetSignalOptionName)
        };
    }
}
