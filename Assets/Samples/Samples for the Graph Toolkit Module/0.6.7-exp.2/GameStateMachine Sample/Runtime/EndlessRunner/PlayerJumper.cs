using System.Collections.Generic;
using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// The endless runner's player: it jumps in place while the level scrolls past, and dies when something kills it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerJumper : MonoBehaviour
    {
        /// <summary>
        /// The appearances the player can be shown with.
        /// </summary>
        public enum SpriteState
        {
            Alive,
            Dead,
        }

        /// <summary>
        /// The scene's player, or <see langword="null"/> when the scene has none.
        /// </summary>
        public static PlayerJumper Instance { get; private set; }

        /// <summary>
        /// The upward velocity applied the instant a jump starts, in world units per second.
        /// </summary>
        [Header("Jump")]
        [Tooltip("Upward velocity, in world units per second, applied the instant a jump starts.")]
        [Min(0f)]
        public float JumpVelocity = 2.2f;

        /// <summary>
        /// How long a jump press is buffered in seconds.
        /// </summary>
        [Tooltip("How long a jump press is remembered. A press made just before landing still jumps.")]
        [Min(0f)]
        public float JumpBufferTime = 0.1f;

        /// <summary>
        /// The radius of the ground probe, in world units.
        /// </summary>
        [Tooltip("Radius of the ground probe, in world units.")]
        [Min(0f)]
        public float GroundCheckRadius = 0.04f;

        /// <summary>
        /// The layers that count as ground.
        /// </summary>
        /// <remarks>
        /// Everything by default, which works without setting up a single layer: the probe skips the player's own
        /// collider and ignores triggers.
        /// </remarks>
        [Tooltip("The layers that count as ground.")]
        public LayerMask GroundLayers = ~0;

        /// <summary>
        /// The sprite shown for <see cref="SpriteState.Alive"/>.
        /// </summary>
        [Header("Sprites")]
        [Tooltip("The sprite shown while the player is alive. Leave empty to keep the renderer's authored sprite.")]
        public Sprite AliveSprite;

        /// <summary>
        /// The sprite shown for <see cref="SpriteState.Dead"/>.
        /// </summary>
        [Tooltip("The sprite shown once the player is dead. Leave empty to keep the renderer's authored sprite.")]
        public Sprite DeadSprite;

        /// <summary>
        /// Whether the ground probe found something solid during the last physics step.
        /// </summary>
        public bool IsGrounded => m_IsGrounded;

        /// <summary>
        /// Whether the player is alive.
        /// </summary>
        public bool IsAlive => m_IsAlive;

        /// <summary>
        /// Whether a run is in progress.
        /// </summary>
        public bool IsRunning => m_IsRunning;

        Rigidbody2D m_Rigidbody;
        Collider2D m_Collider;
        SpriteRenderer m_SpriteRenderer;

        ContactFilter2D m_GroundFilter;
        readonly List<Collider2D> m_GroundHits = new();

        Vector2 m_StartPosition;

        float m_JumpBufferRemaining;
        bool m_IsGrounded;
        bool m_IsAlive;
        bool m_IsRunning;

        /// <summary>
        /// Starts a run, returning the player to the start line alive.
        /// </summary>
        public void StartRun()
        {
            m_IsRunning = true;
            m_IsAlive = true;
            m_IsGrounded = false;
            m_JumpBufferRemaining = 0f;

            m_Rigidbody.linearVelocity = Vector2.zero;

            m_Rigidbody.position = m_StartPosition;
            transform.position = m_StartPosition;
        }

        /// <summary>
        /// Stops the run, so the player no longer responds to input.
        /// </summary>
        public void StopRun()
        {
            m_IsRunning = false;
            m_JumpBufferRemaining = 0f;
        }

        /// <summary>
        /// Kills the player, clearing <see cref="IsAlive"/>.
        /// </summary>
        public void Kill()
        {
            if (!m_IsAlive)
                return;

            m_IsAlive = false;
            m_JumpBufferRemaining = 0f;
        }

        /// <summary>
        /// Shows the player with the sprite for <paramref name="state"/>.
        /// </summary>
        /// <param name="state">The appearance to show.</param>
        public void SetSprite(SpriteState state)
        {
            if (m_SpriteRenderer == null)
                return;

            var sprite = state == SpriteState.Dead ? DeadSprite : AliveSprite;

            if (sprite != null)
                m_SpriteRenderer.sprite = sprite;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning(
                    $"'{name}' is a second {nameof(PlayerJumper)} in the scene, and will be ignored. Keep only one, " +
                    "because the game state reaches the player through a single shared instance.", this);
                enabled = false;
                return;
            }

            Instance = this;

            m_Rigidbody = GetComponent<Rigidbody2D>();
            m_Collider = GetComponent<Collider2D>();
            m_SpriteRenderer = GetComponent<SpriteRenderer>();

            if (m_Collider == null)
            {
                Debug.LogWarning(
                    $"'{name}' has no Collider2D, so it cannot stand on anything or be hit. Add one, for instance a " +
                    "Box Collider 2D.", this);
                enabled = false;
                return;
            }

            m_StartPosition = transform.position;

            m_GroundFilter.useTriggers = false;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (!m_IsRunning || !m_IsAlive)
                return;

            m_JumpBufferRemaining -= Time.deltaTime;

            if (RunnerInput.JumpPressed())
                m_JumpBufferRemaining = JumpBufferTime;
        }

        void FixedUpdate()
        {
            if (!m_IsRunning || !m_IsAlive)
                return;

            m_IsGrounded = CheckGrounded();

            if (m_JumpBufferRemaining <= 0f || !m_IsGrounded)
                return;

            m_JumpBufferRemaining = 0f;

            m_Rigidbody.linearVelocity = new Vector2(m_Rigidbody.linearVelocity.x, JumpVelocity);
        }

        /// <summary>
        /// Reports whether the player is standing on something solid.
        /// </summary>
        /// <returns><see langword="true"/> when the probe overlaps a collider other than the player's own.</returns>
        bool CheckGrounded()
        {
            m_GroundFilter.SetLayerMask(GroundLayers);
            var bounds = m_Collider.bounds;

            Physics2D.OverlapCircle(new Vector2(bounds.center.x, bounds.min.y), GroundCheckRadius, m_GroundFilter, m_GroundHits);

            for (var i = 0; i < m_GroundHits.Count; i++)
            {
                if (m_GroundHits[i] != m_Collider)
                    return true;
            }

            return false;
        }
    }
}
