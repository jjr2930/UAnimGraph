using UAnimGraph;
using UnityEngine;

public class TestUIScript : MonoBehaviour
{
    [SerializeField]
    UAnimGraphPlayer uAnimPlayer;
    public void OnSpeedChange(float speed)
    {
        var blackboard = uAnimPlayer.Instance.SharedBlackboard;
        blackboard.SetFloat("MovingSpeed", speed);
    }
}
