namespace UAnimGraph
{
    // Minimal examples: no root parameters or character-specific code needed.
    [TransitionLibrary]
    public static class SampleTransitionConditions
    {
        [TransitionEvaluation]
        public static bool AlwaysTrue(in TransitionContext context)
        {
            return true;
        }

        [TransitionEvaluation]
        public static bool AfterOneCycle(in TransitionContext context)
        {
            return context.From.TotalNormalizedTime >= 1;
        }
    }
}

