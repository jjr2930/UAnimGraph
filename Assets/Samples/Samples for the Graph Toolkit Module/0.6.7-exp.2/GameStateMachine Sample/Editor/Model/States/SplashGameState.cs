using System;
using Unity.GraphToolkit.Editor;

namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{
    /// <summary>
    /// A <see cref="GameState"/> whose runtime covers the game with the splash card until the state is left.
    /// </summary>
    [Serializable]
    [State(null, null, "Splash")]
    class SplashGameState : GameState
    {
        public override void OnEnable()
        {
            base.OnEnable();
            Tooltip = "Covers the game with the splash card until the state is left.";
        }
        
        public override RuntimeGameState CreateRuntimeState() => new SplashRuntimeGameState();
    }
}
