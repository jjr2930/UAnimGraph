using TMPro;
using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// Holds the game's UI screens, and the singleton a <see cref="RuntimeGameState"/> uses to in order to change them.
    /// </summary>
    /// <remarks>
    /// Every screen lives on the same <see cref="Canvas"/>, one <see cref="CanvasGroup"/> per screen, and showing a
    /// screen takes one group's <see cref="CanvasGroup.alpha"/> to one and the rest to zero.
    /// </remarks>
    [DisallowMultipleComponent]
    public class GameFlowUI : MonoBehaviour
    {
        /// <summary>
        /// The UI singleton instance of the loaded scene, or <see langword="null"/> when the scene has none.
        /// </summary>
        public static GameFlowUI Instance { get; private set; }
        
        [Header("Screens")]
        [Tooltip("The group holding the splash card and its opaque background.")]
        public CanvasGroup SplashScreen;
        
        [Tooltip("The group holding the title and the start prompt.")]
        public CanvasGroup TitleScreen;
        
        [Tooltip("The group holding the countdown.")]
        public CanvasGroup CountdownScreen;
        
        [Tooltip("The group holding the score readout.")]
        public CanvasGroup ScoreScreen;

        [Tooltip("The group holding the game over message and the final score.")]
        public CanvasGroup GameOverScreen;
        
        [Header("Overlays")]
        [Tooltip("The group holding the paused message. Shown over the current screen.")]
        public CanvasGroup PauseOverlay;
        
        [Header("Labels")]
        [Tooltip("The label the countdown is written into.")]
        public TMP_Text CountdownLabel;
        
        [Tooltip("The label the score is written into.")]
        public TMP_Text ScoreLabel;
        
        [Tooltip("The label the final score is written into on the game over screen.")]
        public TMP_Text FinalScoreLabel;
        
        [Header("Startup")]
        [Tooltip("The screen shown at the start of the game.")]
        public GameFlowScreen InitialScreen = GameFlowScreen.Splash;

        /// <summary>
        /// The screen currently on show.
        /// </summary>
        public GameFlowScreen CurrentScreen { get; private set; }

        int m_LastCountdownSeconds = int.MinValue;
        int m_LastScore = int.MinValue;

        /// <summary>
        /// Shows one screen and hides the others.
        /// </summary>
        /// <param name="screen">The screen to show. <see cref="GameFlowScreen.None"/> hides all of them.</param>
        public void Show(GameFlowScreen screen)
        {
            CurrentScreen = screen;

            SetVisible(SplashScreen, screen == GameFlowScreen.Splash);
            SetVisible(TitleScreen, screen == GameFlowScreen.Title);
            SetVisible(CountdownScreen, screen == GameFlowScreen.Countdown);
            SetVisible(ScoreScreen, screen == GameFlowScreen.Score);
            SetVisible(GameOverScreen, screen == GameFlowScreen.GameOver);
        }

        /// <summary>
        /// Hides a screen.
        /// </summary>
        /// <param name="screen">The screen to take down.</param>
        public void Hide(GameFlowScreen screen)
        {
            if (CurrentScreen == screen)
                Show(GameFlowScreen.None);
        }

        /// <summary>
        /// Shows or hides the paused overlay, without replacing the screen underneath it.
        /// </summary>
        /// <param name="visible">Whether the overlay is on show.</param>
        /// <remarks>
        /// Separate from <see cref="Show"/> because the overlay stacks rather than replaces.
        /// </remarks>
        public void SetPauseOverlayVisible(bool visible)
        {
            SetVisible(PauseOverlay, visible);
        }

        /// <summary>
        /// Writes the seconds left of a countdown into the countdown label.
        /// </summary>
        /// <param name="secondsRemaining">The time left in seconds.</param>
        public void SetCountdown(float secondsRemaining)
        {
            if (CountdownLabel == null)
                return;

            // Rounded up, so anything above zero reads at least 1 and every number of the
            // countdown is on screen for a full second.
            var seconds = Mathf.CeilToInt(secondsRemaining);
            if (seconds == m_LastCountdownSeconds)
                return;

            m_LastCountdownSeconds = seconds;
            CountdownLabel.text = seconds.ToString();
        }

        /// <summary>
        /// Writes a score into both the score readout and the game over screen.
        /// </summary>
        /// <param name="score">The score to show.</param>
        /// <remarks>
        /// Both labels are written every time, so the game over screen is already showing the right number before it is
        /// put up.
        /// </remarks>
        public void SetScore(int score)
        {
            if (score == m_LastScore)
                return;

            m_LastScore = score;

            if (ScoreLabel != null)
                ScoreLabel.text = score.ToString();

            if (FinalScoreLabel != null)
                FinalScoreLabel.text = score.ToString();
        }

        void Awake()
        {
            Instance = this;

            Show(InitialScreen);
            
            SetPauseOverlayVisible(false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Fades a group in or out, and stops a hidden one from consuming clicks meant for what is behind it.
        /// </summary>
        static void SetVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;

            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
    }
}
