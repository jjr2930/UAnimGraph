using System;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// A <see cref="RuntimeGameState"/> that covers the game with the splash card until the state is left.
    /// </summary>
    [Serializable]
    public class SplashRuntimeGameState : RuntimeGameState
    {
        public override void OnEnter(GameFlowRunner runner, RuntimeGameState previousState)
        {
            GameFlowUI.Instance?.Show(GameFlowScreen.Splash);
        }
        
        public override void OnExit(GameFlowRunner runner, RuntimeGameState nextState)
        {
            GameFlowUI.Instance?.Hide(GameFlowScreen.Splash);
        }
    }
}
