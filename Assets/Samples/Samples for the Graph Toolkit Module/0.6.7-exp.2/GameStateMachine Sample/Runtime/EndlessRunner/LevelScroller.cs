using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// Owns the speed the level moves past the player at, and ramps it up over a run.
    /// </summary>
    public class LevelScroller : MonoBehaviour
    {
        /// <summary>
        /// The scene's level scroller, or <see langword="null"/> when the scene has none.
        /// </summary>
        /// <remarks>
        /// A single shared instance, so a <see cref="RuntimeGameState"/> can reach the gameplay without holding a
        /// reference to it.
        /// </remarks>
        public static LevelScroller Instance { get; private set; }

        /// <summary>
        /// The speed a run begins at, in world units per second.
        /// </summary>
        [Header("Speed")]
        [Tooltip("World units per second at the start of a run.")]
        [Min(0f)]
        public float StartSpeed = 1f;

        /// <summary>
        /// The speed the ramp climbs to and never exceeds, in world units per second.
        /// </summary>
        [Tooltip("The speed the ramp climbs to and never exceeds.")]
        [Min(0f)]
        public float MaxSpeed = 1.8f;

        /// <summary>
        /// How quickly the level speeds up, in world units per second, per second.
        /// </summary>
        [Tooltip("World units per second, per second, that the level speeds up while a run is in progress. Set to 0 for a constant speed.")]
        [Min(0f)]
        public float Acceleration = 0.03f;

        /// <summary>
        /// The speed the level is moving at right now, in world units per second, or zero between runs.
        /// </summary>
        public float CurrentSpeed => m_CurrentSpeed;

        /// <summary>
        /// How far the level has moved during the current run, in world units.
        /// </summary>
        /// <remarks><see cref="MainRuntimeGameState"/> derives the score from this.</remarks>
        public float DistanceTravelled => m_DistanceTravelled;

        /// <summary>
        /// Whether a run is in progress.
        /// </summary>
        public bool IsRunning => m_IsRunning;

        float m_CurrentSpeed;
        float m_DistanceTravelled;
        bool m_IsRunning;

        /// <summary>
        /// Starts a run, from <see cref="StartSpeed"/> with the distance travelled back at zero.
        /// </summary>
        public void StartRun()
        {
            m_IsRunning = true;
            m_CurrentSpeed = StartSpeed;
            m_DistanceTravelled = 0f;
        }

        /// <summary>
        /// Stops the run and brings the level to a halt.
        /// </summary>
        public void StopRun()
        {
            m_IsRunning = false;
            m_CurrentSpeed = 0f;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"'{name}' is a second {nameof(LevelScroller)} in the scene, and will be ignored. Keep only one, " +
                    "because everything that scrolls takes its speed from a single shared instance.", this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (!m_IsRunning)
                return;

            m_CurrentSpeed = Mathf.MoveTowards(m_CurrentSpeed, MaxSpeed, Acceleration * Time.deltaTime);
            m_DistanceTravelled += m_CurrentSpeed * Time.deltaTime;
        }
    }
}
