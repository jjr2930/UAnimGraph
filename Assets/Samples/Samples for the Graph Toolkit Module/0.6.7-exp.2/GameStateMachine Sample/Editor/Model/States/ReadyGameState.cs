using System;
using Unity.GraphToolkit.Editor;

namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{
    /// <summary>
    /// A <see cref="GameState"/> whose runtime shows the title screen and waits for the player to start the game.
    /// </summary>
    [Serializable]
    [State(null, null, "Ready")]
    class ReadyGameState : GameState
    {
        const string StartSignalOptionName = "StartSignal";
        
        public override void OnEnable()
        {
            base.OnEnable();
            Tooltip = "Shows the title screen and raises a signal when the player presses start.";
        }
        
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>(StartSignalOptionName)
                .WithDisplayName("Start Signal")
                .WithTooltip("The signal raised when the start button is pressed. Must match a Game Signal condition on one of this state's transitions.")
                .WithDefaultValue("Start")
                .Build();
        }
        
        public override RuntimeGameState CreateRuntimeState() => new ReadyRuntimeGameState
        {
            StartSignal = GetOptionValue<string>(StartSignalOptionName)
        };
    }
}
