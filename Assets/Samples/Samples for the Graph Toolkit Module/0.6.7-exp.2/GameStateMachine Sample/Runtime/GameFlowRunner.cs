using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// Runs a <see cref="GameFlowRuntimeStateMachine"/> imported from a <c>.gameflow</c> asset.
    /// </summary>
    /// <remarks>
    /// The runner is the runtime component of the sample: drop it on a GameObject, assign the authored
    /// <c>.gameflow</c> asset, and it updates the machine every frame. It decides when the game changes state, while what
    /// happens in a state is the job of the <see cref="RuntimeGameState"/> its authoring state type created.
    /// <br/><br/>
    /// Evaluation order each frame:<br/>
    /// 1. The time in the current state advances.<br/>
    /// 2. <see cref="RuntimeGameState.OnUpdate"/> runs on the active state, so a signal it raises is visible to this
    ///    frame's rules.<br/>
    /// 3. The transitions of the current state are considered in the order they were authored.<br/>
    /// 4. Within a transition, the rules are considered in the order they were authored; a disabled rule is skipped.<br/>
    /// 5. The first rule whose condition tree is met wins, and its transition is taken.<br/>
    /// 6. At most one transition is taken per frame, so a chain of satisfied transitions unfolds over several frames
    ///    rather than all at once.<br/>
    /// 7. The signals raised this frame are discarded, whether or not a transition consumed one.
    /// </remarks>
    public class GameFlowRunner : MonoBehaviour
    {
        /// <summary>
        /// The runtime state machine asset to execute.
        /// </summary>
        /// <remarks>
        /// The runtime state machine asset is the same as the authoring state machine asset
        /// because we export the runtime asset object into the same asset and set it as the 'main' asset in our importer.
        /// This allows us to edit the authoring state machine in the editor and drag-drop the same asset into inspector fields
        /// that expect the runtime state machine.
        /// </remarks>
        [Header("State Machine")]
        public GameFlowRuntimeStateMachine StateMachine;

        /// <summary>
        /// The index, into <see cref="GameFlowRuntimeStateMachine.States"/>, of the active state, or <c>-1</c> when
        /// the state machine is not running.
        /// </summary>
        public int CurrentStateIndex => m_CurrentStateIndex;

        /// <summary>
        /// The name of the active state, or <see langword="null"/> when the state machine is not running.
        /// </summary>
        public string CurrentStateName => GetStateName(m_CurrentStateIndex);

        /// <summary>
        /// The number of seconds the active state has been running.
        /// </summary>
        /// <remarks>
        /// Reset whenever a state is entered, including when a self transition re-enters the state it leaves. This is
        /// the value a <c>Time In State</c> condition is compared against.
        /// </remarks>
        public float TimeInState => m_TimeInState;

        /// <summary>
        /// The runtime state driving the active state, or <see langword="null"/> when there is none.
        /// </summary>
        /// <remarks>
        /// This is the instance stored in <see cref="StateMachine"/>, shared by every runner playing the same asset.
        /// </remarks>
        public RuntimeGameState CurrentRuntimeState => GetRuntimeState(m_CurrentStateIndex);

        int m_CurrentStateIndex = -1;
        float m_TimeInState;

        // The signals raised during the current frame. Cleared at the end of every Update, so a signal cannot fire a
        // transition several frames after it was raised.
        readonly HashSet<string> m_Signals = new();

        // Live blackboard values.
        bool[] m_Values;
        readonly Dictionary<string, int> m_VariableIndicesByName = new();

        bool m_Started;

        // Drives the debug view in the state machine editor while the game plays. Null outside play mode and in a
        // player build. See GameFlowDebugVisualizer.
        GameFlowDebugVisualizer m_Visualizer;

        /// <summary>
        /// Reports that a signal was raised, for the current frame only.
        /// </summary>
        /// <param name="signal">The signal name, matching the value authored on a <c>Game Signal</c> condition.</param>
        /// <remarks>
        /// Signals are the sample's model for the discrete events that drive game flow. Names are matched exactly and
        /// are case-sensitive.
        /// <br/><br/>
        /// The signal is discarded at the end of the frame whether or not a transition consumed it, so it cannot linger
        /// and fire a transition later. For something that stays true over time, use a blackboard variable and
        /// <see cref="SetBool"/> instead.
        /// </remarks>
        public void RaiseSignal(string signal)
        {
            if (string.IsNullOrEmpty(signal))
                return;

            m_Signals.Add(signal);
        }

        /// <summary>
        /// Restarts the state machine from its entry state.
        /// </summary>
        /// <remarks>
        /// Exits the active state, resets every blackboard variable to its authored default, clears any pending signals,
        /// and enters the entry state again.
        /// </remarks>
        public void Restart()
        {
            ExitCurrentState();
            Initialize();

            DisposeVisualizer();
            CreateVisualizer();

            EnterState(StateMachine != null ? StateMachine.EntryStateIndex : -1);
        }

        /// <summary>
        /// Sets the live value of a blackboard variable.
        /// </summary>
        /// <param name="name">The variable name, as it appears in the blackboard.</param>
        /// <param name="value">The new value.</param>
        public void SetBool(string name, bool value)
        {
            if (!TryGetVariableIndex(name, out var index))
                return;

            m_Values[index] = value;
        }

        /// <summary>
        /// Gets the live value of a blackboard variable.
        /// </summary>
        /// <param name="name">The variable name, as it appears in the blackboard.</param>
        /// <returns>The current value, or <see langword="false"/> when no variable has that name.</returns>
        public bool GetBool(string name)
        {
            if (!TryGetVariableIndex(name, out var index))
                return false;

            return m_Values[index];
        }

        void OnEnable()
        {
            Initialize();
            CreateVisualizer();

            if (m_Started)
                EnterState(StateMachine != null ? StateMachine.EntryStateIndex : -1);
        }

        void Start()
        {
            m_Started = true;
            EnterState(StateMachine != null ? StateMachine.EntryStateIndex : -1);
        }

        void OnDisable()
        {
            ExitCurrentState();
            m_Signals.Clear();
            DisposeVisualizer();
        }

        void OnDestroy()
        {
            DisposeVisualizer();
        }

        void Update()
        {
            if (StateMachine != null && m_CurrentStateIndex >= 0)
            {
                m_TimeInState += Time.deltaTime;

                CurrentRuntimeState?.OnUpdate(this);

                m_Visualizer?.BeginEvaluation(m_CurrentStateIndex);
                var destinationIndex = FindTransitionDestination(m_CurrentStateIndex, BuildConditionContext(), m_Visualizer?.Trace, out var transitionIndex);
                m_Visualizer?.ApplyEvaluation(m_CurrentStateIndex);

                if (destinationIndex >= 0)
                {
                    var exitedState = CurrentRuntimeState;

                    ExitCurrentState(destinationIndex, transitionIndex);
                    EnterState(destinationIndex, exitedState);
                }
            }

            m_Signals.Clear();
        }

        /// <summary>
        /// Finds the destination of the first transition of a state whose conditions are met.
        /// </summary>
        /// <param name="stateIndex">The index of the state whose transitions are considered.</param>
        /// <param name="context">The values the conditions are evaluated against.</param>
        /// <param name="trace">
        /// Where to record what was evaluated, or <see langword="null"/> to evaluate without recording.
        /// </param>
        /// <param name="transitionIndex">
        /// The index, into the state's <see cref="GameFlowRuntimeState.Transitions"/> list, of the transition that was
        /// taken, or <c>-1</c> in the case none are taken.
        /// </param>
        /// <returns>The destination state index, or <c>-1</c> when no transition should be taken.</returns>
        int FindTransitionDestination(int stateIndex, in GameFlowConditionContext context, GameFlowEvaluationTrace trace, out int transitionIndex)
        {
            transitionIndex = -1;

            var state = StateMachine.States[stateIndex];
            if (state?.Transitions == null)
                return -1;

            for (var i = 0; i < state.Transitions.Count; i++)
            {
                var transition = state.Transitions[i];

                if (transition == null || transition.Rules == null)
                    continue;

                if (transition.ToStateIndex < 0 || transition.ToStateIndex >= StateMachine.States.Count)
                    continue;

                var met = false;

                foreach (var rule in transition.Rules)
                {
                    if (rule == null || !rule.Enabled)
                        continue;

                    if (GameFlowConditionEvaluator.Evaluate(StateMachine, rule.RootConditionIndex, context, trace))
                    {
                        met = true;
                        break;
                    }
                }

                trace?.RecordTransition(i, met);

                if (met)
                {
                    transitionIndex = i;
                    return transition.ToStateIndex;
                }
            }

            return -1;
        }

        /// <summary>
        /// Builds the context this frame's conditions are evaluated against.
        /// </summary>
        GameFlowConditionContext BuildConditionContext() => new(m_TimeInState, m_Signals, m_Values);

        /// <summary>
        /// Copies the imported blackboard defaults into runner-local storage.
        /// </summary>
        /// <remarks>
        /// The live variable values are held here rather than written back into <see cref="StateMachine"/>, which would
        /// dirty the imported sub-asset and leak state from one play session into the next.
        /// </remarks>
        void Initialize()
        {
            m_CurrentStateIndex = -1;
            m_TimeInState = 0f;
            m_Signals.Clear();
            m_VariableIndicesByName.Clear();

            if (StateMachine == null)
            {
                m_Values = Array.Empty<bool>();
                Debug.LogWarning($"'{name}' has no state machine assigned. Assign a .gameflow asset to run.", this);
                return;
            }

            var variableCount = StateMachine.Variables.Count;
            m_Values = new bool[variableCount];

            for (var i = 0; i < variableCount; i++)
            {
                var variable = StateMachine.Variables[i];
                if (variable == null)
                    continue;

                m_Values[i] = variable.DefaultValue;

                if (!string.IsNullOrEmpty(variable.Name))
                    m_VariableIndicesByName[variable.Name] = i;
            }

            if (!StateMachine.IsRunnable)
            {
                Debug.LogWarning(
                    $"'{StateMachine.name}' has no usable entry state, so '{name}' will not run. Open the asset and " +
                    "create an Entry transition on the state the game should start in.", this);
            }
        }

        /// <summary>
        /// Makes a state the active one and runs its <see cref="RuntimeGameState.OnEnter"/>.
        /// </summary>
        /// <param name="stateIndex">The index of the state to enter. Out-of-range indices do nothing.</param>
        /// <param name="previousState">
        /// The runtime state a transition is arriving from, or <see langword="null"/> when the state is not being
        /// entered through a transition. Handed to <see cref="RuntimeGameState.OnEnter"/>.
        /// </param>
        void EnterState(int stateIndex, RuntimeGameState previousState = null)
        {
            if (StateMachine == null || stateIndex < 0 || stateIndex >= StateMachine.States.Count)
                return;

            m_CurrentStateIndex = stateIndex;
            m_TimeInState = 0f;

            CurrentRuntimeState?.OnEnter(this, previousState);
            m_Visualizer?.OnStateEntered(stateIndex);
        }

        /// <summary>
        /// Runs the active state's <see cref="RuntimeGameState.OnExit"/> and leaves the machine with no active state.
        /// </summary>
        /// <param name="destinationIndex">
        /// The index of the state a transition is heading for, or <c>-1</c> when the state is being left without one,
        /// such as the runner being disabled or restarted. Its runtime state is handed to <see cref="RuntimeGameState.OnExit"/>.
        /// </param>
        /// <param name="transitionIndex">
        /// The index, into the leaving state's <see cref="GameFlowRuntimeState.Transitions"/> list, of the transition
        /// being taken, or <c>-1</c> when the state is being left without one.
        /// </param>
        void ExitCurrentState(int destinationIndex = -1, int transitionIndex = -1)
        {
            if (m_CurrentStateIndex < 0)
                return;

            var stateIndex = m_CurrentStateIndex;
            var runtimeState = CurrentRuntimeState;

            m_CurrentStateIndex = -1;

            runtimeState?.OnExit(this, GetRuntimeState(destinationIndex));
            m_Visualizer?.OnStateExited(stateIndex, transitionIndex);
        }

        /// <summary>
        /// Returns the runtime state of the state at <paramref name="stateIndex"/>.
        /// </summary>
        RuntimeGameState GetRuntimeState(int stateIndex)
        {
            if (StateMachine == null || stateIndex < 0 || stateIndex >= StateMachine.States.Count)
                return null;

            return StateMachine.States[stateIndex]?.RuntimeState;
        }

        /// <summary>
        /// Returns the name of the state at <paramref name="stateIndex"/>.
        /// </summary>
        string GetStateName(int stateIndex)
        {
            if (StateMachine == null || stateIndex < 0 || stateIndex >= StateMachine.States.Count)
                return null;

            return StateMachine.States[stateIndex]?.Name;
        }

        /// <summary>
        /// Finds the index of a blackboard variable by name.
        /// </summary>
        /// <param name="name">The variable name, as it appears in the blackboard.</param>
        /// <param name="index">The index into the live variable values.</param>
        /// <returns><see langword="true"/> when a variable has that name.</returns>
        bool TryGetVariableIndex(string name, out int index)
        {
            index = -1;

            if (StateMachine == null || string.IsNullOrEmpty(name))
                return false;

            if (!m_VariableIndicesByName.TryGetValue(name, out var candidate))
            {
                Debug.LogWarning($"'{StateMachine.name}' has no blackboard variable named '{name}'.", this);
                return false;
            }

            index = candidate;
            return true;
        }

        /// <summary>
        /// Attaches the debug view to the state machine editor if the editor is in play mode.
        /// </summary>
        void CreateVisualizer()
        {
            m_Visualizer = GameFlowDebugVisualizer.Create(StateMachine);
        }

        /// <summary>
        /// Detaches the debug view, clearing everything it drew.
        /// </summary>
        void DisposeVisualizer()
        {
            m_Visualizer?.Dispose();
            m_Visualizer = null;
        }
    }
}
