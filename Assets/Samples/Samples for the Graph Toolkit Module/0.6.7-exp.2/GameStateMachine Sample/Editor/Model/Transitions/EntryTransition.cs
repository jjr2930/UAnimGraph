using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{
    /// <summary>
    /// Marks the state a Game Flow state machine starts in.
    /// </summary>
    /// <remarks>
    /// A state machine has no built-in notion of an entry point, so the sample marks one with a self transition of its
    /// own. <see cref="GameFlowStateMachineImporter"/> looks for the state this transition is attached to, and that
    /// state is where <see cref="GameFlowRunner"/> starts. The transition is only a marker: it is not imported as a
    /// transition the machine can take, and the rules stacked on it are ignored.
    /// </remarks>
    [Serializable]
    [Transition(null, GameFlowStateMachine.ToolPath + "Assets/Icons/EntryTransition.png", "Entry")]
    class EntryTransition : SelfTransition
    {
        /// <summary>
        /// Initializes the transition with a custom tooltip and fill color.
        /// </summary>
        public override void OnEnable()
        {
            Tooltip = "The game starts in this state.";
            FillColor = EditorGUIUtility.isProSkin ? new Color(0.82f, 0.82f, 0.82f) : new Color(0.247f, 0.247f, 0.247f);
        }
    }
}
