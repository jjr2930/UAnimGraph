using System;
using Unity.GraphToolkit.Editor;

namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{
    /// <summary>
    /// A condition that passes when gameplay raises a named signal during the current frame.
    /// </summary>
    /// <remarks>
    /// Gameplay code raises a signal with <see cref="GameFlowRunner.RaiseSignal"/>, which will result in this condition
    /// passing if the name of the signal raised matches the string value stored in this condition.
    /// </remarks>
    [Serializable]
    [Condition("Game Signal")]
    class GameSignalCondition : Condition<string>
    {
        /// <summary>
        /// The tooltip displayed when hovering over the condition in the transition inspector.
        /// </summary>
        public override string Tooltip =>
            "Passes when this signal was raised during the current frame, by GameFlowRunner.RaiseSignal.";
    }
}
