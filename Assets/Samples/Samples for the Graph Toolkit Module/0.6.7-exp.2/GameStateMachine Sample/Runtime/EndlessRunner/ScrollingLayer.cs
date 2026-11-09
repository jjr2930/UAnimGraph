using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// Scrolls a strip of repeating pieces leftwards and wraps each one around when it leaves the screen.
    /// </summary>
    public class ScrollingLayer : MonoBehaviour
    {
        /// <summary>
        /// The fraction of the level speed this layer moves at.
        /// </summary>
        [Header("Level Speed")]
        [Tooltip("Fraction of the level speed this layer moves at. 1 keeps pace with the obstacles; smaller values read as further away.")]
        [Min(0f)]
        public float SpeedMultiplier = 1f;

        /// <summary>
        /// The pieces of the strip.
        /// </summary>
        [Header("Looping")]
        [Tooltip("The pieces of the strip, laid out left to right and exactly touching. Two or more are needed for a seamless loop.")]
        public Transform[] Pieces;

        /// <summary>
        /// The width of one piece in world units, or zero to measure it from the first piece's renderer.
        /// </summary>
        [Tooltip("The width of one piece, in world units. Leave at 0 to measure it from the first piece's renderer.")]
        [Min(0f)]
        public float PieceWidth;

        /// <summary>
        /// The world X coordinate a piece must scroll past before it moves to the back of the strip.
        /// </summary>
        [Tooltip("The world X a piece must scroll past before it is moved to the back of the strip.")]
        public float WrapX = -2.5f;

        float m_LoopWidth;

        void Awake()
        {
            m_LoopWidth = PieceWidth > 0f ? PieceWidth : MeasurePieceWidth();

            if (m_LoopWidth <= 0f)
            {
                Debug.LogWarning(
                    $"'{name}' could not work out how wide one piece is, so it will not scroll. Assign the pieces " +
                    $"and give them a renderer, or set {nameof(PieceWidth)} by hand.", this);
                enabled = false;
            }
        }

        void Update()
        {
            if (Pieces == null || Pieces.Length == 0)
                return;

            var scroller = LevelScroller.Instance;

            var step = (scroller != null ? scroller.CurrentSpeed : 0f) * SpeedMultiplier * Time.deltaTime;

            if (step <= 0f)
                return;

            var span = m_LoopWidth * Pieces.Length;

            foreach (var piece in Pieces)
            {
                if (piece == null)
                    continue;

                var position = piece.position;
                position.x -= step;

                if (position.x <= WrapX)
                    position.x += span;

                piece.position = position;
            }
        }

        /// <summary>
        /// Measures one piece's width from its renderer.
        /// </summary>
        float MeasurePieceWidth()
        {
            if (Pieces == null)
                return 0f;

            foreach (var piece in Pieces)
            {
                if (piece == null)
                    continue;

                var renderer2d = piece.GetComponentInChildren<Renderer>();
                if (renderer2d != null)
                    return renderer2d.bounds.size.x;
            }

            return 0f;
        }
    }
}
