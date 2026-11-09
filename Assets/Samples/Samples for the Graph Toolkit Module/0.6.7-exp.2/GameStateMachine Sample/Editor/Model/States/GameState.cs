using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{
    /// <summary>
    /// Game Flow Base State model.
    /// </summary>
    /// <remarks>
    /// Every state of a <see cref="GameFlowStateMachine"/> derives from this class, one subclass per behavior. Each
    /// subclass declares the values it needs as state options in <see cref="State.OnDefineOptions"/>, and converts
    /// itself into the matching <see cref="RuntimeGameState"/> subclass in <see cref="CreateRuntimeState"/>, which
    /// <see cref="GameFlowStateMachineImporter"/> calls for every state it imports.
    /// <br/><br/>
    /// The class is abstract and has no <see cref="StateAttribute"/>, so it is not offered in the state machine
    /// editor; only its concrete subclasses are.
    /// </remarks>
    [Serializable]
    abstract class GameState : State
    {
        /// <summary>
        /// Initializes the state with the color shared by every Game Flow state.
        /// </summary>
        public override void OnEnable()
        {
            DefaultColor = new Color(0.30f, 0.55f, 0.85f);
        }

        /// <summary>
        /// Creates the runtime state that runs while this state is active.
        /// </summary>
        /// <returns>A new runtime state holding the values of this state's options.</returns>
        public abstract RuntimeGameState CreateRuntimeState();

        /// <summary>
        /// Gets the value of one of this state's options.
        /// </summary>
        /// <typeparam name="T">The data type the option was declared with.</typeparam>
        /// <param name="optionName">The name the option was declared with in <see cref="State.OnDefineOptions"/>.</param>
        /// <returns>The option's value, or the default value of <typeparamref name="T"/> when there is no such option.</returns>
        protected T GetOptionValue<T>(string optionName)
        {
            T value = default;
            GetOptionByName(optionName)?.TryGetValue(out value);
            return value;
        }
    }
}
