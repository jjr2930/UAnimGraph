namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// The screens <see cref="GameFlowUI"/> can show.
    /// </summary>
    public enum GameFlowScreen
    {
        /// <summary>No screen. The game is shown with nothing over it.</summary>
        None,

        /// <summary>The card shown before the game starts.</summary>
        Splash,

        /// <summary>The title of the game and the prompt to start it.</summary>
        Title,

        /// <summary>The seconds counting down to the start of a run.</summary>
        Countdown,

        /// <summary>The score of the current run.</summary>
        Score,

        /// <summary>The end of a run, and the score it finished on.</summary>
        GameOver,
    }
}
