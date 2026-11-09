using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{
    /// <summary>
    /// Represents the Game Flow state machine.
    /// </summary>
    /// <remarks>
    /// This is the entry point for the Game Flow tool, it represents a state machine that models the top-level flow of
    /// a game (e.g. splash, title, countdown, playing, paused, game over).
    /// This class extends the Graph Toolkit <see cref="StateMachine"/> class, which provides the states, transitions,
    /// rules and blackboard of a state machine.
    /// It also provides customization by allowing you to override specific methods and can be decorated by attributes
    /// to tailor the tool's functionality.
    /// For example, in this class, we override <see cref="BuildAvailableVariableTypes"/> to restrict the blackboard to
    /// boolean variables, and <see cref="OnStateMachineChanged"/> to report authoring mistakes in the editor. We also
    /// decorated it with the <see cref="StateMachineAttribute"/>, which specifies the asset extension.
    /// </remarks>
    [Serializable]
    [StateMachine(AssetExtension)]
    class GameFlowStateMachine : StateMachine
    {
        /// <summary>
        /// The file extension for Game Flow state machines.
        /// </summary>
        /// <remarks>
        /// In Unity, the extension is used to select the right importer, so it must be unique.
        /// </remarks>
        internal const string AssetExtension = "gameflow";

        /// <summary>
        /// The path to the Game Flow tool.
        /// </summary>
        internal const string ToolPath = SamplePathInfo.SamplePath;

        /// <summary>
        /// Declares the variable types a user can add to the blackboard.
        /// </summary>
        /// <returns>The types offered when creating a blackboard variable.</returns>
        protected override IEnumerable<Type> BuildAvailableVariableTypes()
        {
            yield return typeof(bool);
        }

        /// <summary>
        /// Creates a new Game Flow state machine asset file in the project window.
        /// </summary>
        /// <remarks>This is also where we add the shortcut to create a new state machine from the editor Asset menu.</remarks>
        [MenuItem("Assets/Create/Graph Toolkit Samples/Game Flow State Machine")]
        static void CreateAssetFile()
        {
            StateMachineDatabase.PromptInProjectBrowserToCreateNewAsset<GameFlowStateMachine>("New Game Flow");
        }

        /// <summary>
        /// Called when the state machine changes.
        /// </summary>
        /// <param name="stateMachineLogger">The StateMachineLogger object to which errors and warnings are added.</param>
        /// <remarks>
        /// This method is triggered whenever the state machine is modified. It calls `CheckStateMachineErrors` to
        /// validate the state machine and report any issues.
        /// </remarks>
        public override void OnStateMachineChanged(StateMachineLogger stateMachineLogger)
        {
            base.OnStateMachineChanged(stateMachineLogger);

            CheckStateMachineErrors(stateMachineLogger);
        }

        /// <summary>
        /// Checks the state machine for errors and warnings and adds them to the logger.
        /// </summary>
        /// <param name="stateMachineLogger">Object implementing <see cref="StateMachineLogger"/> interface and
        /// containing collected errors and warnings.</param>
        /// <remarks>
        /// Errors and warnings are reported by adding them to the StateMachineLogger object, which is the default
        /// reporting mechanism for a state machine tool. Passing a transition as the context puts the warning marker on
        /// that transition in the editor.
        /// </remarks>
        void CheckStateMachineErrors(StateMachineLogger stateMachineLogger)
        {
            EntryTransition firstEntryTransition = null;
            var entryStateTitle = string.Empty;

            foreach (var state in GetStates())
            {
                if (state == null)
                    continue;

                foreach (var transition in state.GetOutgoingTransitions())
                {
                    if (transition is not EntryTransition entryTransition)
                        continue;

                    // GameFlowStateMachineImporter starts the runner in the first state carrying an Entry transition,
                    // so the first one found is the one that wins and every other one is reported.
                    if (firstEntryTransition == null)
                    {
                        firstEntryTransition = entryTransition;
                        entryStateTitle = state.Title;
                        continue;
                    }

                    stateMachineLogger.LogWarning(
                        "A Game Flow state machine only supports one Entry transition. The game starts in " +
                        $"'{entryStateTitle}'; remove this Entry transition to make the entry point unambiguous.",
                        entryTransition);
                }
            }
        }
    }
}
