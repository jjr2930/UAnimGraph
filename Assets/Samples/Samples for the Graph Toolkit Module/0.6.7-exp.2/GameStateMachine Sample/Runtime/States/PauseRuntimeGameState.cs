using System;
using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// A <see cref="RuntimeGameState"/> that freezes the game and shows the paused overlay while it is active.
    /// </summary>
    [Serializable]
    public class PauseRuntimeGameState : RuntimeGameState
    {
        /// <summary>
        /// The blackboard variable cleared to unpause the game.
        /// </summary>
        public string PauseVariable = "IsPaused";
        
        public override void OnEnter(GameFlowRunner runner, RuntimeGameState previousState)
        {
            Time.timeScale = 0f;

            GameFlowUI.Instance?.SetPauseOverlayVisible(true);
        }
        
        public override void OnUpdate(GameFlowRunner runner)
        {
            if (RunnerInput.PausePressed())
                runner.SetBool(PauseVariable, false);
        }
        
        public override void OnExit(GameFlowRunner runner, RuntimeGameState nextState)
        {
            Time.timeScale = 1f;

            GameFlowUI.Instance?.SetPauseOverlayVisible(false);
        }
    }
}
