using System;
using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// A <see cref="RuntimeGameState"/> that plays the endless runner while the state is active.
    /// </summary>
    [Serializable]
    public class MainRuntimeGameState : RuntimeGameState
    {
        /// <summary>
        /// The signal raised when the player dies.
        /// </summary>
        public string DeathSignal = "Game Over";

        /// <summary>
        /// The points awarded per world unit the level scrolls.
        /// </summary>
        public float PointsPerUnit = 10f;

        /// <summary>
        /// The blackboard variable set to pause the game.
        /// </summary>
        public string PauseVariable = "IsPaused";
        
        public override void OnEnter(GameFlowRunner runner, RuntimeGameState previousState)
        {
            // Do not re-initialize the run if returning from the pause state.
            if (previousState is PauseRuntimeGameState)
                return;

            var player = PlayerJumper.Instance;
            if (player != null)
            {
                player.StartRun();
            }
            else
            {
                Debug.LogWarning(
                    $"'{runner.CurrentStateName}' found no {nameof(PlayerJumper)} in the scene, so the state has no " +
                    "player to run. Add one, or use a different state type for this state.", runner);
            }

            if (LevelScroller.Instance != null)
                LevelScroller.Instance.StartRun();

            if (ObstacleSpawner.Instance != null)
                ObstacleSpawner.Instance.StartRun();

            var ui = GameFlowUI.Instance;
            if (ui != null)
            {
                ui.Show(GameFlowScreen.Score);

                ui.SetScore(0);
            }
        }
        
        public override void OnUpdate(GameFlowRunner runner)
        {
            if (RunnerInput.PausePressed())
                runner.SetBool(PauseVariable, true);

            if (PlayerJumper.Instance is { IsRunning: true, IsAlive: false })
                runner.RaiseSignal(DeathSignal);

            var scroller = LevelScroller.Instance;
            if (scroller == null)
                return;

            GameFlowUI.Instance?.SetScore(Mathf.FloorToInt(scroller.DistanceTravelled * PointsPerUnit));
        }
        
        public override void OnExit(GameFlowRunner runner, RuntimeGameState nextState)
        {
            // Do not end the run if returning from the pause state.
            if (nextState is PauseRuntimeGameState)
                return;

            if (PlayerJumper.Instance != null)
                PlayerJumper.Instance.StopRun();

            if (LevelScroller.Instance != null)
                LevelScroller.Instance.StopRun();

            if (ObstacleSpawner.Instance != null)
                ObstacleSpawner.Instance.StopRun();
        }
    }
}
