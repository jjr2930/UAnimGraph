using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// The base type of every condition in an imported <see cref="GameFlowRuntimeStateMachine"/>.
    /// </summary>
    /// <remarks>
    /// Adding a condition type to the sample requires four things:<br/>
    /// 1. An authoring class deriving from <c>Unity.GraphToolkit.Editor.Condition</c> marked with <c>[Condition]</c> in the editor assembly.<br/>
    /// 2. A runtime class deriving from this one.<br/>
    /// 3. A case in <c>GameFlowStateMachineImporter.TryTranslateCondition</c> mapping one to the other.<br/>
    /// 4. A case in
    ///    <see cref="GameFlowConditionEvaluator.Evaluate(GameFlowRuntimeStateMachine,int,in GameFlowConditionContext,GameFlowEvaluationTrace)"/>
    ///    to evaluate it, and whatever it needs to read added to <see cref="GameFlowConditionContext"/>.
    /// </remarks>
    [Serializable]
    public abstract class GameFlowRuntimeCondition
    {
        /// <summary>
        /// The identifier of the authoring condition this one was imported from, used for debug visualization.
        /// </summary>
        public Hash128 ID;
    }

    /// <summary>
    /// A condition that combines nested conditions with a logical <c>And</c> or <c>Or</c>.
    /// </summary>
    [Serializable]
    public class GameFlowRuntimeGroupCondition : GameFlowRuntimeCondition
    {
        /// <summary>
        /// How the nested conditions are combined.
        /// </summary>
        public GameFlowConditionGroupOperation Operation;

        /// <summary>
        /// The indices, into <see cref="GameFlowRuntimeStateMachine.Conditions"/>, of the nested conditions.
        /// </summary>
        /// <remarks>
        /// An empty group evaluates to the identity of its operation: an empty <c>And</c> is met, an empty <c>Or</c> is
        /// not. A rule whose root group is empty therefore always fires.
        /// </remarks>
        public List<int> SubConditionIndices = new();
    }

    /// <summary>
    /// A condition met when gameplay raised a named signal during the current frame.
    /// </summary>
    /// <remarks>
    /// The runtime form of the authored <c>GameSignalCondition</c>.
    /// </remarks>
    [Serializable]
    public class GameFlowRuntimeSignalCondition : GameFlowRuntimeCondition
    {
        /// <summary>
        /// The name of the signal to look for, as authored on the condition.
        /// </summary>
        /// <remarks>
        /// Compared with an exact, case-sensitive string match against the signals raised this frame. An empty or
        /// <see langword="null"/> name never matches.
        /// </remarks>
        public string Signal;
    }

    /// <summary>
    /// A condition that compares how long the current state has been active against an authored number of seconds.
    /// </summary>
    /// <remarks>
    /// The runtime form of the authored <c>TimeInStateCondition</c>.
    /// </remarks>
    [Serializable]
    public class GameFlowRuntimeTimeInStateCondition : GameFlowRuntimeCondition
    {
        /// <summary>
        /// The number of seconds to compare against.
        /// </summary>
        public float Value;

        /// <summary>
        /// The operator applied between the elapsed time and <see cref="Value"/>.
        /// </summary>
        public GameFlowComparison Comparison;
    }

    /// <summary>
    /// A condition that compares a blackboard variable against an authored value.
    /// </summary>
    /// <remarks>
    /// The runtime form of Graph Toolkit's built-in variable condition. The authoring machine offers
    /// <see langword="bool"/> variables only, so only a bool valued is stored.
    /// </remarks>
    [Serializable]
    public class GameFlowRuntimeVariableCondition : GameFlowRuntimeCondition
    {
        /// <summary>
        /// The index into <see cref="GameFlowRuntimeStateMachine.Variables"/> of the variable to test, or <c>-1</c>.
        /// </summary>
        public int VariableIndex = -1;

        /// <summary>
        /// The operator applied between the variable's live value and <see cref="Value"/>.
        /// </summary>
        public GameFlowComparison Comparison;

        /// <summary>
        /// The authored bool value to compare the variable against.
        /// </summary>
        public bool Value;
    }
}
