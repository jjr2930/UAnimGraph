using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// Spawns spikes at random intervals and scrolls them past the player.
    /// </summary>
    /// <remarks>
    /// Spikes appear at this GameObject's position, travel left at <see cref="LevelScroller.CurrentSpeed"/>, and are
    /// recycled once they pass <see cref="DespawnX"/>.
    /// </remarks>
    public class ObstacleSpawner : MonoBehaviour
    {
        /// <summary>
        /// The scene's obstacle spawner, or <see langword="null"/> when the scene has none.
        /// </summary>
        public static ObstacleSpawner Instance { get; private set; }

        /// <summary>
        /// The prefab spawned and recycled.
        /// </summary>
        [Header("Obstacles")]
        [Tooltip("The prefab pooled and spawned. Needs a trigger Collider2D and a kinematic Rigidbody2D.")]
        public Spike SpikePrefab;

        /// <summary>
        /// The smallest distance between two spikes, in world units.
        /// </summary>
        [Tooltip("Smallest distance, in world units, between two spikes. Must exceed how far the level moves while the player is airborne.")]
        [Min(0f)]
        public float MinGap = 1.4f;

        /// <summary>
        /// The largest distance between two spikes, in world units.
        /// </summary>
        [Tooltip("Largest distance, in world units, between two spikes.")]
        [Min(0f)]
        public float MaxGap = 2.6f;

        /// <summary>
        /// The world X coordinate at which a spike that has scrolled past the camera is recycled.
        /// </summary>
        [Tooltip("The world X at which a spike that has scrolled past the camera is returned to the pool.")]
        public float DespawnX = -2.5f;

        /// <summary>
        /// How many spikes the pool makes room for up front.
        /// </summary>
        [Header("Pool")]
        [Tooltip("How many spikes the pool allocates room for up front.")]
        [Min(1)]
        public int PoolCapacity = 8;

        /// <summary>
        /// The most spikes the pool keeps. Beyond this, a released spike is destroyed instead.
        /// </summary>
        [Tooltip("Above this, a released spike is destroyed instead of kept.")]
        [Min(1)]
        public int PoolMaxSize = 32;

        /// <summary>
        /// Whether a run is in progress.
        /// </summary>
        public bool IsRunning => m_IsRunning;

        ObjectPool<Spike> m_Pool;
        readonly List<Spike> m_Active = new();
        float m_DistanceToNextSpawn;
        bool m_IsRunning;

        /// <summary>
        /// Starts a run on an empty screen.
        /// </summary>
        public void StartRun()
        {
            m_IsRunning = true;

            ReleaseAll();
            m_DistanceToNextSpawn = NextGap();
        }

        /// <summary>
        /// Stops the run and clears every spike still on screen.
        /// </summary>
        public void StopRun()
        {
            m_IsRunning = false;
            ReleaseAll();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"'{name}' is a second {nameof(ObstacleSpawner)} in the scene, and will be ignored. Keep only " +
                    "one, because the game state reaches the spawner through a single shared instance.", this);
                enabled = false;
                return;
            }

            Instance = this;

            if (SpikePrefab == null)
            {
                Debug.LogWarning(
                    $"'{name}' has no spike prefab assigned, so it will not spawn anything. Assign one to the " +
                    $"{nameof(SpikePrefab)} field.", this);
                enabled = false;
                return;
            }

            m_Pool = new ObjectPool<Spike>(
                CreateSpike,
                OnGetSpike,
                OnReleaseSpike,
                OnDestroySpike,
                collectionCheck: true,
                PoolCapacity,
                PoolMaxSize);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            m_Pool?.Dispose();
        }

        void Update()
        {
            if (!m_IsRunning)
                return;

            var scroller = LevelScroller.Instance;
            var step = (scroller != null ? scroller.CurrentSpeed : 0f) * Time.deltaTime;

            for (var i = m_Active.Count - 1; i >= 0; i--)
            {
                var spike = m_Active[i];
                if (spike == null)
                {
                    m_Active.RemoveAt(i);
                    continue;
                }

                var position = spike.transform.position;
                position.x -= step;
                spike.transform.position = position;

                if (position.x > DespawnX)
                    continue;

                m_Active.RemoveAt(i);
                m_Pool.Release(spike);
            }

            m_DistanceToNextSpawn -= step;

            if (m_DistanceToNextSpawn <= 0f)
                Spawn();
        }

        /// <summary>
        /// Takes a spike from the pool and places it at the spawn point.
        /// </summary>
        void Spawn()
        {
            var spike = m_Pool.Get();
            spike.transform.position = transform.position;
            m_Active.Add(spike);

            m_DistanceToNextSpawn += NextGap();
        }

        /// <summary>
        /// Returns every spike on screen to the pool.
        /// </summary>
        void ReleaseAll()
        {
            for (var i = m_Active.Count - 1; i >= 0; i--)
            {
                var spike = m_Active[i];
                if (spike != null)
                    m_Pool.Release(spike);
            }

            m_Active.Clear();
        }

        Spike CreateSpike() => Instantiate(SpikePrefab, transform.position, Quaternion.identity, transform);

        void OnGetSpike(Spike spike) => spike.gameObject.SetActive(true);

        void OnReleaseSpike(Spike spike) => spike.gameObject.SetActive(false);

        void OnDestroySpike(Spike spike)
        {
            if (spike != null)
                Destroy(spike.gameObject);
        }

        /// <summary>
        /// Picks the distance to leave before the next spike.
        /// </summary>
        /// <remarks>
        /// The upper bound is clamped rather than trusted, so a <see cref="MaxGap"/> typed in below
        /// <see cref="MinGap"/> produces a fixed gap instead of an exception.
        /// </remarks>
        float NextGap() => Random.Range(MinGap, Mathf.Max(MinGap, MaxGap));

        /// <summary>
        /// Draws the spawn point and the line spikes are recycled at.
        /// </summary>
        void OnDrawGizmosSelected()
        {
            var origin = transform.position;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(origin, 0.05f);
            Gizmos.DrawLine(origin, new Vector3(DespawnX, origin.y, origin.z));
        }
    }
}
