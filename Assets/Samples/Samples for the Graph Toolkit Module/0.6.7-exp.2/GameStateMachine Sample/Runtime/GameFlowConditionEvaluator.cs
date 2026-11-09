using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// The values a condition is evaluated against.
    /// </summary>
    /// <remarks>
    /// <see cref="GameFlowRunner"/> builds a condition context each frame.
    /// </remarks>
    public readonly struct GameFlowConditionContext
    {
        /// <summary>
        /// The number of seconds the current state has been active.
        /// </summary>
        /// <remarks>
        /// Accumulated from <see cref="Time.deltaTime"/> and reset every time a state is entered, including when a self
        /// transition re-enters the state it leaves.
        /// </remarks>
        public readonly float TimeInState;

        /// <summary>
        /// The signals raised during the current frame, or <see langword="null"/> when none can be raised.
        /// </summary>
        public readonly HashSet<string> Signals;

        /// <summary>
        /// The live values of the blackboard variables, indexed like <see cref="GameFlowRuntimeStateMachine.Variables"/> is.
        /// </summary>
        /// <remarks>
        /// Can be <see langword="null"/> when the machine declares no variables.
        /// </remarks>
        public readonly bool[] Values;

        /// <summary>
        /// Initializes a new context.
        /// </summary>
        /// <param name="timeInState">The number of seconds the current state has been active.</param>
        /// <param name="signals">The signals raised this frame, or <see langword="null"/>.</param>
        /// <param name="values">The live blackboard variable values, or <see langword="null"/>.</param>
        public GameFlowConditionContext(float timeInState, HashSet<string> signals, bool[] values)
        {
            TimeInState = timeInState;
            Signals = signals;
            Values = values;
        }
    }

    /// <summary>
    /// The evaluated value of a transition or a condition.
    /// </summary>
    /// <remarks>
    /// <see cref="NotEvaluated"/> signifies that the evaluator never checked the transition or condition, because either 
    /// an enclosing group short-circuited, an earlier transition had already been taken, or the element does not
    /// belong to the state being evaluated. The debug visualization draws it as pending.
    /// </remarks>
    public enum GameFlowEvaluationResult
    {
        /// <summary>The evaluator did not reach this element.</summary>
        NotEvaluated,

        /// <summary>The element was evaluated and was not met.</summary>
        False,

        /// <summary>The element was evaluated and was met.</summary>
        True
    }

    /// <summary>
    /// Records what the evaluator did while considering the transitions of one state.
    /// </summary>
    /// <remarks>
    /// Evaluation reports a single <see langword="bool"/> as a result, which is all the runner
    /// needs, but it does not provide the full reasoning for why a transition was or was not taken.
    /// Passing a trace into <see cref="GameFlowConditionEvaluator.Evaluate(GameFlowRuntimeStateMachine,int,in GameFlowConditionContext,GameFlowEvaluationTrace)"/>
    /// allows for tracking the full result of a transition evaluation.
    /// <br/><br/>
    /// A condition from an <c>And</c> group that was never evaluated is tracked as <see cref="GameFlowEvaluationResult.NotEvaluated"/>.
    /// </remarks>
    public sealed class GameFlowEvaluationTrace
    {
        /// <summary>
        /// The index of the state whose transitions the trace describes, or <c>-1</c> before the first
        /// <see cref="BeginState"/>.
        /// </summary>
        public int StateIndex { get; private set; } = -1;

        GameFlowEvaluationResult[] m_ConditionResults = Array.Empty<GameFlowEvaluationResult>();

        GameFlowEvaluationResult[] m_TransitionResults = Array.Empty<GameFlowEvaluationResult>();

        /// <summary>
        /// Clears the trace and prepares it to record one state's transitions.
        /// </summary>
        /// <param name="stateMachine">The machine being evaluated.</param>
        /// <param name="stateIndex">The index of the state whose transitions are about to be considered.</param>
        /// <remarks>
        /// Condition results are indexed within an array and stored in <see cref="GameFlowRuntimeStateMachine.Conditions"/>.
        /// Transition results are indexed as the current state's <see cref="GameFlowRuntimeState.Transitions"/>.
        /// </remarks>
        public void BeginState(GameFlowRuntimeStateMachine stateMachine, int stateIndex)
        {
            StateIndex = stateIndex;

            var conditionCount = stateMachine != null ? stateMachine.Conditions.Count : 0;
            var transitionCount = 0;

            if (stateMachine != null && stateIndex >= 0 && stateIndex < stateMachine.States.Count)
                transitionCount = stateMachine.States[stateIndex]?.Transitions?.Count ?? 0;

            Reset(ref m_ConditionResults, conditionCount);
            Reset(ref m_TransitionResults, transitionCount);
        }

        /// <summary>
        /// Records the outcome of one condition.
        /// </summary>
        /// <param name="conditionIndex">The index into <see cref="GameFlowRuntimeStateMachine.Conditions"/>.</param>
        /// <param name="result">Whether the condition was met.</param>
        public void RecordCondition(int conditionIndex, bool result)
        {
            if (conditionIndex < 0 || conditionIndex >= m_ConditionResults.Length)
                return;

            m_ConditionResults[conditionIndex] = result ? GameFlowEvaluationResult.True : GameFlowEvaluationResult.False;
        }

        /// <summary>
        /// Records the outcome of one transition of the state named by <see cref="StateIndex"/>.
        /// </summary>
        /// <param name="transitionIndex">The index into that state's transitions.</param>
        /// <param name="result">Whether the transition was taken.</param>
        public void RecordTransition(int transitionIndex, bool result)
        {
            if (transitionIndex < 0 || transitionIndex >= m_TransitionResults.Length)
                return;

            m_TransitionResults[transitionIndex] = result ? GameFlowEvaluationResult.True : GameFlowEvaluationResult.False;
        }

        /// <summary>
        /// Gets the outcome of a condition during the last pass.
        /// </summary>
        /// <param name="conditionIndex">The index into <see cref="GameFlowRuntimeStateMachine.Conditions"/>.</param>
        /// <returns>
        /// The recorded outcome, or <see cref="GameFlowEvaluationResult.NotEvaluated"/> when the condition was not
        /// reached or the index names no condition.
        /// </returns>
        public GameFlowEvaluationResult GetCondition(int conditionIndex) =>
            conditionIndex >= 0 && conditionIndex < m_ConditionResults.Length
                ? m_ConditionResults[conditionIndex]
                : GameFlowEvaluationResult.NotEvaluated;

        /// <summary>
        /// Gets the outcome of a transition of the state named by <see cref="StateIndex"/> during the last pass.
        /// </summary>
        /// <param name="transitionIndex">The index into that state's transitions.</param>
        /// <returns>
        /// The recorded outcome, or <see cref="GameFlowEvaluationResult.NotEvaluated"/> when the transition was not
        /// reached or the index names no transition.
        /// </returns>
        public GameFlowEvaluationResult GetTransition(int transitionIndex) =>
            transitionIndex >= 0 && transitionIndex < m_TransitionResults.Length
                ? m_TransitionResults[transitionIndex]
                : GameFlowEvaluationResult.NotEvaluated;

        /// <summary>
        /// Grows the array to hold <paramref name="count"/> results, and clears whatever it held.
        /// </summary>
        static void Reset(ref GameFlowEvaluationResult[] results, int count)
        {
            if (results.Length < count)
                results = new GameFlowEvaluationResult[count];
            else
                Array.Clear(results, 0, results.Length);
        }
    }

    /// <summary>
    /// Evaluates the condition trees of a <see cref="GameFlowRuntimeStateMachine"/>.
    /// </summary>
    public static class GameFlowConditionEvaluator
    {
        /// <summary>
        /// Evaluates the condition at the given index against the values in <paramref name="context"/>.
        /// </summary>
        /// <param name="stateMachine">The machine that owns the condition list.</param>
        /// <param name="conditionIndex">The index into <see cref="GameFlowRuntimeStateMachine.Conditions"/>.</param>
        /// <param name="context">The values to evaluate against.</param>
        /// <returns>
        /// <see langword="true"/> when the condition is met. An out-of-range index, a <see langword="null"/> entry, or
        /// an unrecognized condition type all evaluate to <see langword="false"/>.
        /// </returns>
        public static bool Evaluate(GameFlowRuntimeStateMachine stateMachine, int conditionIndex, in GameFlowConditionContext context) =>
            Evaluate(stateMachine, conditionIndex, context, null);

        /// <summary>
        /// Evaluates the condition at the given index, recording what it did into a trace.
        /// </summary>
        /// <param name="stateMachine">The machine that owns the condition list.</param>
        /// <param name="conditionIndex">The index into <see cref="GameFlowRuntimeStateMachine.Conditions"/>.</param>
        /// <param name="context">The values to evaluate against.</param>
        /// <param name="trace">
        /// Where to record the outcome of every condition that is reached, or <see langword="null"/> to evaluate
        /// without recording anything.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when the condition is met. An out-of-range index, a <see langword="null"/> entry, or
        /// an unrecognized condition type all evaluate to <see langword="false"/>, which keeps a partially imported
        /// asset running instead of throwing once per frame.
        /// </returns>
        public static bool Evaluate(GameFlowRuntimeStateMachine stateMachine, int conditionIndex, in GameFlowConditionContext context, GameFlowEvaluationTrace trace)
        {
            if (stateMachine == null || conditionIndex < 0 || conditionIndex >= stateMachine.Conditions.Count)
                return false;

            var condition = stateMachine.Conditions[conditionIndex];

            bool result;

            switch (condition)
            {
                case GameFlowRuntimeGroupCondition group:
                    result = EvaluateGroup(stateMachine, group, context, trace);
                    break;

                case GameFlowRuntimeSignalCondition signal:
                    result = EvaluateSignal(signal, context);
                    break;

                case GameFlowRuntimeTimeInStateCondition timeInState:
                    result = Compare(context.TimeInState, timeInState.Comparison, timeInState.Value);
                    break;

                case GameFlowRuntimeVariableCondition variable:
                    result = EvaluateVariable(stateMachine, variable, context);
                    break;

                default:
                    result = false;
                    break;
            }

            trace?.RecordCondition(conditionIndex, result);

            return result;
        }

        /// <summary>
        /// Evaluates a group condition by combining its nested conditions.
        /// </summary>
        /// <param name="stateMachine">The machine that owns the condition list.</param>
        /// <param name="group">The group to evaluate.</param>
        /// <param name="context">The values to evaluate against.</param>
        /// <param name="trace">Where to record what was evaluated, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when the group is met.</returns>
        /// <remarks>
        /// An empty group returns the identity of its operation: an empty <c>And</c> evaluates as <see langword="true"/>, an empty <c>Or</c> evaluates as <see langword="false"/>.
        /// </remarks>
        static bool EvaluateGroup(GameFlowRuntimeStateMachine stateMachine, GameFlowRuntimeGroupCondition group, in GameFlowConditionContext context, GameFlowEvaluationTrace trace)
        {
            var subConditions = group.SubConditionIndices;
            if (subConditions == null || subConditions.Count == 0)
                return group.Operation == GameFlowConditionGroupOperation.And;

            if (group.Operation == GameFlowConditionGroupOperation.And)
            {
                for (var i = 0; i < subConditions.Count; i++)
                {
                    if (!Evaluate(stateMachine, subConditions[i], context, trace))
                        return false;
                }

                return true;
            }

            for (var i = 0; i < subConditions.Count; i++)
            {
                if (Evaluate(stateMachine, subConditions[i], context, trace))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Evaluates a signal condition against the signals raised this frame.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="context">The values to evaluate against.</param>
        /// <returns><see langword="true"/> when the signal was raised this frame.</returns>
        /// <remarks>
        /// A condition with no signal name never passes.
        /// </remarks>
        static bool EvaluateSignal(GameFlowRuntimeSignalCondition condition, in GameFlowConditionContext context)
        {
            if (context.Signals == null || string.IsNullOrEmpty(condition.Signal))
                return false;

            return context.Signals.Contains(condition.Signal);
        }

        /// <summary>
        /// Evaluates a blackboard variable condition against the runner's live variable values.
        /// </summary>
        /// <param name="stateMachine">The machine that owns the variable declarations.</param>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="context">The values to evaluate against.</param>
        /// <returns><see langword="true"/> when the comparison holds.</returns>
        static bool EvaluateVariable(GameFlowRuntimeStateMachine stateMachine, GameFlowRuntimeVariableCondition condition, in GameFlowConditionContext context)
        {
            var variableIndex = condition.VariableIndex;
            if (variableIndex < 0 || variableIndex >= stateMachine.Variables.Count)
                return false;

            if (stateMachine.Variables[variableIndex] == null)
                return false;

            if (context.Values == null || variableIndex >= context.Values.Length)
                return false;

            return Compare(context.Values[variableIndex], condition.Comparison, condition.Value);
        }

        /// <summary>
        /// Applies a comparison operator between two floats.
        /// </summary>
        /// <param name="left">The sensed value.</param>
        /// <param name="comparison">The operator to apply.</param>
        /// <param name="right">The authored value.</param>
        /// <returns><see langword="true"/> when the comparison holds.</returns>
        public static bool Compare(float left, GameFlowComparison comparison, float right)
        {
            switch (comparison)
            {
                case GameFlowComparison.Equal: return Mathf.Approximately(left, right);
                case GameFlowComparison.NotEqual: return !Mathf.Approximately(left, right);
                case GameFlowComparison.Less: return left < right;
                case GameFlowComparison.LessOrEqual: return left <= right;
                case GameFlowComparison.Greater: return left > right;
                case GameFlowComparison.GreaterOrEqual: return left >= right;
                default: return false;
            }
        }

        /// <summary>
        /// Applies a comparison operator between two booleans.
        /// </summary>
        /// <param name="left">The sensed value.</param>
        /// <param name="comparison">The operator to apply.</param>
        /// <param name="right">The authored value.</param>
        /// <returns><see langword="true"/> when the comparison holds.</returns>
        public static bool Compare(bool left, GameFlowComparison comparison, bool right)
        {
            switch (comparison)
            {
                case GameFlowComparison.Equal: return left == right;
                case GameFlowComparison.NotEqual: return left != right;
                default: return false;
            }
        }
    }
}
