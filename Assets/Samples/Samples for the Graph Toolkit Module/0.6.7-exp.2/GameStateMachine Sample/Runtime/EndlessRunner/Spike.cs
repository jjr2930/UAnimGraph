using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// A hazard that kills the player on contact.
    /// </summary>
    /// <remarks>
    /// The prefab needs a <see cref="Collider2D"/> with <c>Is Trigger</c> enabled, and a kinematic
    /// <see cref="Rigidbody2D"/>, because <see cref="ObstacleSpawner"/> moves spikes every frame.
    /// </remarks>
    public class Spike : MonoBehaviour
    {
        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerJumper>();
            if (player == null)
                return;

            player.Kill();
        }
    }
}
