using System;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// The base class for the runtime behavior of one state of a game flow state machine.
    /// </summary>
    /// <remarks>
    /// Each authoring state type, a subclass of <c>GameState</c>, is converted into one of these at import time by
    /// <c>GameState.CreateRuntimeState</c>. The result is serialized by reference into
    /// <see cref="GameFlowRuntimeState.RuntimeState"/>, so a subclass carries the values read from the authoring
    /// state's options as plain serialized fields.
    /// </remarks>
    [Serializable]
    public abstract class RuntimeGameState
    {
        /// <summary>
        /// Called once when the machine enters this state.
        /// </summary>
        /// <param name="runner">The runner that entered the state.</param>
        /// <param name="previousState">
        /// The runtime state the machine is transitioning from, or <see langword="null"/> when no transition led here, such as
        /// the entry state or a <see cref="GameFlowRunner.Restart"/>. It is also <see langword="null"/> when the state
        /// being left has no runtime state.
        /// </param>
        /// <remarks>
        /// Anything set up here should be undone in <see cref="OnExit"/>.
        /// </remarks>
        public virtual void OnEnter(GameFlowRunner runner, RuntimeGameState previousState) { }

        /// <summary>
        /// Called every frame while the state is active.
        /// </summary>
        /// <param name="runner">The runner running the state.</param>
        /// <remarks>
        /// This runs before the runner evaluates the state's transitions, so a signal raised here with
        /// <see cref="GameFlowRunner.RaiseSignal"/> is visible to this frame's rules and can move the machine on
        /// immediately. It never runs in the same frame as this state's <see cref="OnEnter"/>.
        /// </remarks>
        public virtual void OnUpdate(GameFlowRunner runner) { }

        /// <summary>
        /// Called once when the machine leaves this state.
        /// </summary>
        /// <param name="runner">The runner that left the state.</param>
        /// <param name="nextState">
        /// The runtime state the machine is transitioning into, or <see langword="null"/> when the state is being left without
        /// a transition, such as the runner being disabled, destroyed or restarted. It is also <see langword="null"/>
        /// when the state being entered has no runtime state.
        /// </param>
        public virtual void OnExit(GameFlowRunner runner, RuntimeGameState nextState) { }
    }
}
