using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using Unity.GraphToolkit.Editor.GraphVisualization;
#endif

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
#if UNITY_EDITOR
    /// <summary>
    /// Displays what a running <see cref="GameFlowRunner"/> is doing onto the state machine it was authored in.
    /// </summary>
    /// <remarks>
    /// While the game plays, the active state animates, the outgoing transitions of that state are colored
    /// by whether their conditions were met, and every condition in those transitions gets a true, false or pending icon
    /// on its row in the transition inspector.
    /// <br/><br/>
    /// Everything here is <c>Unity.GraphToolkit.Editor.GraphVisualization</c>, which is editor-only API, so this
    /// implementation sits inside <c>#if UNITY_EDITOR</c>. A player build compiles a no-op stand-in with the same
    /// members instead, so the runner calls into the debug view without guards of its own.
    /// <br/><br/>
    /// This class never evaluates the state machine itself. It reads a <see cref="GameFlowEvaluationTrace"/> that the runner's
    /// own evaluation filled in, so what is drawn is what the machine actually did.
    /// </remarks>
    sealed class GameFlowDebugVisualizer : IDisposable
    {
        /// <summary>
        /// How much of a transition's line is left showing when it does not leave the active state.
        /// </summary>
        const float k_IdleOpacity = 0.5f;

        /// <summary>
        /// How long the transition that passed is held green, so that the flash outlives the frame it reports.
        /// </summary>
        const float k_FlashDuration = 0.5f;

        readonly GameFlowRuntimeStateMachine m_Machine;
        readonly Context m_Context;

        readonly HashSet<int> m_VisitedConditions = new();

        /// <summary>
        /// The transition the machine last passed through, held green until m_FlashExpiryTime. Null when there is
        /// nothing flashing.
        /// </summary>
        GameFlowRuntimeTransition m_PassedTransition;
        
        float m_FlashExpiryTime;

        /// <summary>
        /// Where the runner's evaluation records what it did, and where this class reads it back from.
        /// </summary>
        public GameFlowEvaluationTrace Trace { get; } = new();

        /// <summary>
        /// Attaches a debug view to the state machine editor for a machine, if the editor is in play mode and one can be
        /// attached.
        /// </summary>
        /// <param name="machine">The imported machine being run.</param>
        /// <returns>
        /// The visualizer, or <see langword="null"/> when the editor is not in play mode or <paramref name="machine"/>
        /// carries no usable identifier.
        /// </returns>
        /// <remarks>
        /// A context can be created before the state machine editor is open. The visualization data is buffered and
        /// applied when a window for the asset attaches, so there is no need to check if an instance of the window already exists.
        /// </remarks>
        public static GameFlowDebugVisualizer Create(GameFlowRuntimeStateMachine machine)
        {
            if (!Application.isPlaying || machine == null || !machine.ID.isValid)
                return null;

            return new GameFlowDebugVisualizer(machine);
        }

        /// <summary>
        /// Creates the visualization context and fades every transition.
        /// </summary>
        /// <param name="machine">The imported machine being run.</param>
        GameFlowDebugVisualizer(GameFlowRuntimeStateMachine machine)
        {
            m_Machine = machine;
            m_Context = Registry.CreateVisualizationContext(machine.ID);

            foreach (var state in m_Machine.States)
            {
                if (state?.Transitions == null)
                    continue;

                foreach (var transition in state.Transitions)
                    GetTransition(transition).Opacity = k_IdleOpacity;
            }
        }

        /// <summary>
        /// Clears the trace, and removes any passed transition that has run its course, before the runner evaluates a state's
        /// transitions.
        /// </summary>
        /// <param name="stateIndex">The index of the state about to be evaluated.</param>
        public void BeginEvaluation(int stateIndex)
        {
            if (!m_Context.IsValid)
                return;

            if (m_PassedTransition != null && Time.unscaledTime >= m_FlashExpiryTime)
                ClearPassedTransition();

            Trace.BeginState(m_Machine, stateIndex);
        }

        /// <summary>
        /// Draws the evaluation the runner has just finished.
        /// </summary>
        /// <param name="stateIndex">The index of the state that was evaluated.</param>
        /// <remarks>
        /// Called every frame while a state is active and before the machine transitions, so the visualization describes
        /// the state that is being exited rather than the one being entered.
        /// </remarks>
        public void ApplyEvaluation(int stateIndex)
        {
            if (!m_Context.IsValid || !TryGetState(stateIndex, out var state))
                return;

            m_VisitedConditions.Clear();

            for (var i = 0; i < state.Transitions.Count; i++)
            {
                var transition = state.Transitions[i];

                if (transition == m_PassedTransition)
                    continue;

                var result = Trace.GetTransition(i);

                var transitionReference = GetTransition(transition);
                transitionReference.Opacity = 1f;
                transitionReference.LineColor = ColorFor(result);
                transitionReference.Icon = IconFor(result);

                ApplyConditionIcons(transition, clear: false);
            }
        }

        /// <summary>
        /// Starts the animation on the state the machine has entered.
        /// </summary>
        /// <param name="stateIndex">The index of the state that was entered.</param>
        public void OnStateEntered(int stateIndex)
        {
            if (!m_Context.IsValid || !TryGetState(stateIndex, out var state))
                return;

            m_Context.Motion.Play(m_Context.GetStateReference(state.ID));
        }

        /// <summary>
        /// Stops the animation on the state the machine has left and resets everything it drew for that state.
        /// </summary>
        /// <param name="stateIndex">The index of the state that was left.</param>
        /// <param name="takenTransitionIndex">
        /// The index, into that state's transitions, of the transition that was taken, or <c>-1</c> when the state was
        /// left without one.
        /// </param>
        /// <remarks>
        /// The transition that was taken is turned green instead of being reset with the rest and, along with its inspector
        /// icon and the icons on its conditions, are all held for <see cref="k_FlashDuration"/> seconds. A transition
        /// fires on the frame its conditions are first met and the machine instantly moves on to the next state,
        /// so without the hold there would be nothing to see.
        /// </remarks>
        public void OnStateExited(int stateIndex, int takenTransitionIndex)
        {
            if (!m_Context.IsValid || !TryGetState(stateIndex, out var state))
                return;

            ClearPassedTransition();

            var stateReference = m_Context.GetStateReference(state.ID);
            m_Context.Motion.Stop(stateReference);
            stateReference.ClearCustomization();

            m_VisitedConditions.Clear();

            for (var i = 0; i < state.Transitions.Count; i++)
            {
                var transition = state.Transitions[i];
                var transitionReference = GetTransition(transition);

                transitionReference.ClearCustomization();

                if (i == takenTransitionIndex)
                {
                    transitionReference.LineColor = DebugStyles.True;
                    transitionReference.Icon = DebugStyles.TrueIcon;
                    m_Context.Motion.Play(transitionReference, 3f);

                    ApplyConditionIcons(transition, clear: false);

                    m_PassedTransition = transition;
                    m_FlashExpiryTime = Time.unscaledTime + k_FlashDuration;
                }
                else
                {
                    transitionReference.Opacity = k_IdleOpacity;

                    ApplyConditionIcons(transition, clear: true);
                }
            }
        }

        /// <summary>
        /// Detaches the debug view, which clears everything it drew.
        /// </summary>
        public void Dispose()
        {
            m_Context.Dispose();
        }

        /// <summary>
        /// Resets the visualization of the passed transition and its conditions.
        /// </summary>
        void ClearPassedTransition()
        {
            if (m_PassedTransition == null)
                return;

            var transitionReference = GetTransition(m_PassedTransition);
            transitionReference.ClearCustomization();
            transitionReference.Opacity = k_IdleOpacity;

            m_VisitedConditions.Clear();
            ApplyConditionIcons(m_PassedTransition, clear: true);

            m_PassedTransition = null;
        }

        /// <summary>
        /// Puts a true, false or pending icon on every condition of a transition's rules, or clears the icons.
        /// </summary>
        /// <param name="transition">The transition whose rules are evaluated.</param>
        /// <param name="clear">
        /// <see langword="true"/> to remove the overrides, so each condition shows whatever icon it carries itself
        /// again.
        /// </param>
        /// <remarks>
        /// Because evaluation stops at the first transition whose conditions are met, transitions the evaluator never
        /// reached are drawn as pending rather than as false.
        /// </remarks>
        void ApplyConditionIcons(GameFlowRuntimeTransition transition, bool clear)
        {
            if (transition?.Rules == null)
                return;

            foreach (var rule in transition.Rules)
            {
                if (rule != null)
                    UpdateConditions(rule.RootConditionIndex, clear);
            }
        }

        /// <summary>
        /// Applies an icon to a condition and everything nested inside it.
        /// </summary>
        /// <param name="conditionIndex">The index into <see cref="GameFlowRuntimeStateMachine.Conditions"/>.</param>
        /// <param name="clear">Whether to remove the icon overrides instead of applying them.</param>
        void UpdateConditions(int conditionIndex, bool clear)
        {
            if (conditionIndex < 0 || conditionIndex >= m_Machine.Conditions.Count)
                return;

            if (!m_VisitedConditions.Add(conditionIndex))
                return;

            var condition = m_Machine.Conditions[conditionIndex];
            if (condition == null)
                return;

            if (condition.ID.isValid)
                m_Context.GetConditionReference(condition.ID).Icon = clear ? null : IconFor(Trace.GetCondition(conditionIndex));

            if (condition is not GameFlowRuntimeGroupCondition group || group.SubConditionIndices == null)
                return;

            foreach (var subConditionIndex in group.SubConditionIndices)
                UpdateConditions(subConditionIndex, clear);
        }
        
        bool TryGetState(int stateIndex, out GameFlowRuntimeState state)
        {
            state = stateIndex >= 0 && stateIndex < m_Machine.States.Count ? m_Machine.States[stateIndex] : null;
            return state?.Transitions != null && state.ID.isValid;
        }
        
        TransitionReference GetTransition(GameFlowRuntimeTransition transition) =>
            transition != null && transition.ID.isValid ? m_Context.GetTransitionReference(transition.ID) : default;

        /// <summary>
        /// Gets the line color that reports an evaluation result.
        /// </summary>
        static Color ColorFor(GameFlowEvaluationResult result) => result switch
        {
            GameFlowEvaluationResult.True => DebugStyles.True,
            GameFlowEvaluationResult.False => DebugStyles.False,
            _ => DebugStyles.Pending
        };

        /// <summary>
        /// Gets the icon that reports an evaluation result.
        /// </summary>
        static Texture2D IconFor(GameFlowEvaluationResult result) => result switch
        {
            GameFlowEvaluationResult.True => DebugStyles.TrueIcon,
            GameFlowEvaluationResult.False => DebugStyles.FalseIcon,
            _ => DebugStyles.PendingIcon
        };
    }
#else
    /// <summary>
    /// Stands in for the debug view in a player build, where the editor-only visualization API does not exist.
    /// </summary>
    /// <remarks>
    /// <see cref="Create"/> always returns <see langword="null"/>, so every call the runner makes into the debug view
    /// does nothing.
    /// </remarks>
    sealed class GameFlowDebugVisualizer : IDisposable
    {
        public GameFlowEvaluationTrace Trace => null;

        public static GameFlowDebugVisualizer Create(GameFlowRuntimeStateMachine machine) => null;

        public void BeginEvaluation(int stateIndex) { }

        public void ApplyEvaluation(int stateIndex) { }

        public void OnStateEntered(int stateIndex) { }

        public void OnStateExited(int stateIndex, int takenTransitionIndex) { }

        public void Dispose() { }
    }
#endif
}
