using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{
    /// <summary>
    /// A <see cref="GameState"/> whose runtime puts the countdown screen up and counts it down.
    /// </summary>
    [Serializable]
    [State(null, null, "Countdown")]
    class CountdownGameState : GameState
    {
        const string DurationOptionName = "Duration";
        
        public override void OnEnable()
        {
            base.OnEnable();
            Tooltip = "Shows the countdown screen and counts it down from the time spent in the state.";
        }
        
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<float>(DurationOptionName)
                .WithDisplayName("Duration")
                .WithTooltip("Seconds to count down from. Match it with the Time In State condition on the transition out of this state.")
                .WithDefaultValue(3f)
                .Build();
        }
        
        public override RuntimeGameState CreateRuntimeState() => new CountdownRuntimeGameState
        {
            Duration = Mathf.Max(0f, GetOptionValue<float>(DurationOptionName))
        };
    }
}
