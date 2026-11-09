using System;
using Unity.GraphToolkit.Editor;

namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{
    /// <summary>
    /// A condition that compares how long the current state has been active against a number of seconds.
    /// </summary>
    /// <remarks>
    /// This condition displays the comparison operator dropdown, so a transition can be
    /// restricted to either the beginning or the end of a state.
    /// </remarks>
    [Serializable]
    [Condition("Time In State")]
    class TimeInStateCondition : Condition<float>
    {
        /// <summary>
        /// The tooltip displayed when hovering over the condition in the transition inspector.
        /// </summary>
        public override string Tooltip =>
            "Compares the seconds the current state has been active against this value.";

        /// <summary>
        /// Shows the comparison operator dropdown in the transition inspector.
        /// </summary>
        protected override bool DisplayComparisonDropdown => true;
    }
}
