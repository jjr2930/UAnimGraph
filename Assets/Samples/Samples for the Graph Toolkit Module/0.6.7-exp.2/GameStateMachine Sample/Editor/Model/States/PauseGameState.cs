using System;
using Unity.GraphToolkit.Editor;

namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{
    /// <summary>
    /// A <see cref="GameState"/> whose runtime freezes the game and shows the paused overlay while it is active.
    /// </summary>
    [Serializable]
    [State(null, null, "Pause")]
    class PauseGameState : GameState
    {
        const string PauseVariableOptionName = "PauseVariable";
        
        public override void OnEnable()
        {
            base.OnEnable();
            Tooltip = "Freezes the game and shows the paused overlay while the state is active.";
        }
        
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>(PauseVariableOptionName)
                .WithDisplayName("Pause Variable")
                .WithTooltip("The blackboard variable cleared to unpause. Must match the variable this state's transition tests.")
                .WithDefaultValue("IsPaused")
                .Build();
        }
        
        public override RuntimeGameState CreateRuntimeState() => new PauseRuntimeGameState
        {
            PauseVariable = GetOptionValue<string>(PauseVariableOptionName)
        };
    }
}
