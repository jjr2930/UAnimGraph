using System;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// A <see cref="RuntimeGameState"/> that puts the countdown screen up and counts it down.
    /// </summary>
    [Serializable]
    public class CountdownRuntimeGameState : RuntimeGameState
    {
        /// <summary>
        /// The number of seconds to count down from.
        /// </summary>
        public float Duration = 3f;
        
        public override void OnEnter(GameFlowRunner runner, RuntimeGameState previousState)
        {
            var ui = GameFlowUI.Instance;
            if (ui == null)
                return;

            ui.Show(GameFlowScreen.Countdown);

            ui.SetCountdown(Duration);
        }
        
        public override void OnUpdate(GameFlowRunner runner)
        {
            GameFlowUI.Instance?.SetCountdown(Duration - runner.TimeInState);
        }
    }
}
