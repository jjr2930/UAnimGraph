using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{
    /// <summary>
    /// A <see cref="GameState"/> who runtime plays the endless runner while the state is active.
    /// </summary>
    [Serializable]
    [State(null, null, "Main Game")]
    class MainGameState : GameState
    {
        const string DeathSignalOptionName = "DeathSignal";
        const string PointsPerUnitOptionName = "PointsPerUnit";
        const string PauseVariableOptionName = "PauseVariable";
        
        public override void OnEnable()
        {
            base.OnEnable();
            Tooltip = "Plays and scores the endless runner, and raises a signal when the player dies.";
            DefaultColor = new Color(0.30f, 0.8f, 0.85f);
        }
        
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>(DeathSignalOptionName)
                .WithDisplayName("Death Signal")
                .WithTooltip("The signal raised when the player dies. Must match a Game Signal condition on one of this state's transitions.")
                .WithDefaultValue("Game Over")
                .Build();

            context.AddOption<float>(PointsPerUnitOptionName)
                .WithDisplayName("Points Per Unit")
                .WithTooltip("Points awarded per world unit the level scrolls.")
                .WithDefaultValue(10f)
                .Build();

            context.AddOption<string>(PauseVariableOptionName)
                .WithDisplayName("Pause Variable")
                .WithTooltip("The blackboard variable set to pause. Must match the variable this state's transition tests.")
                .WithDefaultValue("IsPaused")
                .Build();
        }
        
        public override RuntimeGameState CreateRuntimeState() => new MainRuntimeGameState
        {
            DeathSignal = GetOptionValue<string>(DeathSignalOptionName),
            PointsPerUnit = Mathf.Max(0f, GetOptionValue<float>(PointsPerUnitOptionName)),
            PauseVariable = GetOptionValue<string>(PauseVariableOptionName)
        };
    }
}
