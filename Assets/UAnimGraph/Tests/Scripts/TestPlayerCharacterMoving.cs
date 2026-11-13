using UAnimGraph;
using UnityEngine;
using UnityEngine.InputSystem;

public class TestPlayerCharacterMoving : MonoBehaviour
{
    [SerializeField]
    Transform camera;

    [SerializeField]
    float speed = 10f;

    [SerializeField] UAnimGraphPlayer uAnimPlayer;

    public void Update()
    {
        Vector2 input = new Vector2();
        if (Keyboard.current.aKey.isPressed)
        {
            input.x = -1f;
        }

        if (Keyboard.current.dKey.isPressed)
        {
            input.x = 1f;
        }

        if (Keyboard.current.wKey.isPressed)
        {
            input.y = 1f;
        }

        if (Keyboard.current.sKey.isPressed)
        {
            input.y = -1f;
        }

        Vector3 PlanarRight = Vector3.ProjectOnPlane(camera.right, Vector3.up);
        Vector3 PlanarForward = Vector3.ProjectOnPlane(camera.forward, Vector3.up);
        Vector3 Direction = PlanarRight * input.x + PlanarForward * input.y;
        Direction.Normalize();
        Vector3 velocity = Direction * speed * Time.deltaTime;

        transform.position += velocity;

        uAnimPlayer.Instance.SharedBlackboard.SetFloat("MovingSpeed", velocity.magnitude);

        Vector2 currentDirection = uAnimPlayer.Instance.SharedBlackboard.GetVector2("Direction");
        Vector2 nextDirection = Vector2.MoveTowards(currentDirection, input, 0.1f);
        uAnimPlayer.Instance.SharedBlackboard.SetVector2("Direction", nextDirection);
    }
}
