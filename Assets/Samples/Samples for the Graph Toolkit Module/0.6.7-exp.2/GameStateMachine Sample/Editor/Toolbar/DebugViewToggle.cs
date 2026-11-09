using Unity.GraphToolkit.Editor;
using Unity.GraphToolkit.Editor.GraphVisualization;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{
    /// <summary>
    /// A toolbar toggle that turns the debug view on and off for the state machine shown in its window.
    /// </summary>
    /// <remarks>
    /// The customization flags of a <see cref="Context"/> hide the overrides that <c>GameFlowDebugVisualizer</c> has
    /// applied without discarding them, so switching the toggle back on restores the visuals rather than redrawing them.
    /// The toggle is on by default. The toggle is hidden outside play mode.
    /// </remarks>
    [GraphToolbarElement(ToggleID, typeof(GameFlowStateMachine))]
    class DebugViewToggle : EditorToolbarToggle, IAccessContainerWindow
    {
        const string ToggleID = "GameStateMachine/DebugView";

        /// <summary>
        /// The window this toolbar element belongs to.
        /// </summary>
        public EditorWindow containerWindow { get; set; }

        /// <summary>
        /// Initializes the toggle and starts tracking play mode.
        /// </summary>
        public DebugViewToggle()
        {
            text = "Debug View";

            SetValueWithoutNotify(true);
            this.RegisterValueChangedCallback(OnValueChanged);

            SetVisibility();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        void OnValueChanged(ChangeEvent<bool> evt)
        {
            var context = GetActiveContext();
            if (context == null)
                return;

            context.StateCustomizationEnabled = evt.newValue;
            context.TransitionCustomizationEnabled = evt.newValue;
            context.ConditionCustomizationEnabled = evt.newValue;
        }

        /// <summary>
        /// Finds the visualization context a running <see cref="GameFlowRunner"/> has attached to this window's asset.
        /// </summary>
        /// <returns>The context, or <see langword="null"/> when nothing is driving the state machine.</returns>
        Context GetActiveContext()
        {
            if (containerWindow is not IGraphWindow graphWindow)
                return null;

            var stateMachine = graphWindow.StateMachine;
            if (stateMachine == null)
                return null;

            return Registry.GetActiveContext(stateMachine.ID);
        }

        /// <summary>
        /// Shows the toggle in play mode only, as the debug view is not relevant otherwise.
        /// </summary>
        void SetVisibility()
        {
            style.display = EditorApplication.isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            SetVisibility();
        }
    }
}
