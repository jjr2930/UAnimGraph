using UAnimGraph;
using UnityEngine;

[TransitionLibrary]
public static class TestSceneTransitionMethods
{
    [TransitionEvaluation]
    public static bool IsSpeedBiggerThenZero(in TransitionContext context)
    {
        return 0 < context.SharedBlackboard.GetFloat("MovingSpeed");
    }

    [TransitionEvaluation]
    public static bool IsSpeedZero(in TransitionContext context)
    {
        float speed = context.SharedBlackboard.GetFloat("MovingSpeed");
        float epsilon = 0.0001f;
        return Mathf.Abs(speed - 0f) <= epsilon;
    }
}
