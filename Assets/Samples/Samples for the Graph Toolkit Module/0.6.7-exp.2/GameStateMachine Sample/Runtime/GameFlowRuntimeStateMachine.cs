using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine
{
    /// <summary>
    /// The comparison operator applied by a runtime condition.
    /// </summary>
    /// <remarks>
    /// This mirrors the authoring-side <c>Unity.GraphToolkit.Editor.ConditionComparison</c>.
    /// </remarks>
    public enum GameFlowComparison
    {
        Equal,
        NotEqual,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual
    }

    /// <summary>
    /// The logical operation a <see cref="GameFlowRuntimeGroupCondition"/> applies to its nested conditions.
    /// </summary>
    /// <remarks>
    /// This mirrors the authoring-side <c>Unity.GraphToolkit.Editor.GroupConditionOperation</c>.
    /// </remarks>
    public enum GameFlowConditionGroupOperation
    {
        And,
        Or
    }

    /// <summary>
    /// The runtime, player-build-friendly form of a <c>GameFlowStateMachine</c> authoring asset.
    /// </summary>
    /// <remarks>
    /// <c>GameFlowStateMachineImporter</c> builds this object at import time and makes it the main object of the
    /// <c>.gameflow</c> asset file, so the authored asset drops directly into the
    /// <see cref="GameFlowRunner.StateMachine"/> field while double-clicking it still opens the state machine editor.
    /// <br/><br/>
    /// Each state carries the <see cref="RuntimeGameState"/> its authoring state type created at import time. Evaluation
    /// lives in <see cref="GameFlowConditionEvaluator"/> and <see cref="GameFlowRunner"/>.
    /// </remarks>
    public class GameFlowRuntimeStateMachine : ScriptableObject
    {
        /// <summary>
        /// The identifier of the authoring state machine this data was imported from. 
        /// </summary>
        /// <remarks>
        /// Used for correlating the runtime state machine with the authoring state machine during debug visualization.
        /// </remarks>
        public Hash128 ID;

        /// <summary>
        /// The index into <see cref="States"/> of the state the machine starts in, or <c>-1</c> if there is none.
        /// </summary>
        /// <remarks>
        /// Resolved at import time by finding the state that carries the authored <c>EntryTransition</c>. That
        /// transition only marks where execution begins, so it is not part of the state's
        /// <see cref="GameFlowRuntimeState.Transitions"/>.
        /// </remarks>
        public int EntryStateIndex = -1;

        /// <summary>
        /// The states of the machine, in the order they were authored.
        /// </summary>
        public List<GameFlowRuntimeState> States = new();

        /// <summary>
        /// The blackboard variables declared on the authoring state machine, in the order they were created.
        /// </summary>
        public List<GameFlowRuntimeVariable> Variables = new();

        /// <summary>
        /// Every condition in the machine, flattened into one list and referenced by index.
        /// </summary>
        [SerializeReference]
        public List<GameFlowRuntimeCondition> Conditions = new();

        /// <summary>
        /// Whether the machine holds at least one state and a valid entry point.
        /// </summary>
        public bool IsRunnable => EntryStateIndex >= 0 && EntryStateIndex < States.Count;
    }

    /// <summary>
    /// The runtime form of a <c>GameState</c> authored in a <c>GameFlowStateMachine</c>.
    /// </summary>
    [Serializable]
    public class GameFlowRuntimeState
    {
        /// <summary>
        /// The identifier of the authoring state this one was imported from.
        /// </summary>
        /// <remarks>
        /// Used for correlating the runtime state with the authoring state during debug visualization.
        /// </remarks>
        public Hash128 ID;

        /// <summary>
        /// The name of the state, taken from the title displayed on the state in the state machine editor.
        /// </summary>
        public string Name;

        /// <summary>
        /// The behavior to run while this state is active, or <see langword="null"/> when there is none.
        /// </summary>
        /// <remarks>
        /// Created by <c>GameState.CreateRuntimeState</c> on the authoring state, and serialized by reference so the
        /// field keeps the concrete <see cref="RuntimeGameState"/> subclass and its values.
        /// </remarks>
        [SerializeReference]
        public RuntimeGameState RuntimeState;

        /// <summary>
        /// The transitions leaving this state, in the order they were authored.
        /// </summary>
        /// <remarks>
        /// Self transitions are included here, with <see cref="GameFlowRuntimeTransition.IsSelfTransition"/> set.
        /// </remarks>
        public List<GameFlowRuntimeTransition> Transitions = new();
    }

    /// <summary>
    /// The runtime form of a transition leaving a <see cref="GameFlowRuntimeState"/>.
    /// </summary>
    /// <remarks>
    /// A transition holds a stack of rules. It is taken when the first enabled rule whose conditions are met is found;
    /// see <see cref="GameFlowRunner"/> for the exact evaluation order.
    /// </remarks>
    [Serializable]
    public class GameFlowRuntimeTransition
    {
        /// <summary>
        /// The identifier of the authoring transition this one was imported from.
        /// </summary>
        /// <remarks>
        /// Used for correlating the runtime condition with the authoring condition during debug visualization.
        /// </remarks>
        public Hash128 ID;

        /// <summary>
        /// The index into <see cref="GameFlowRuntimeStateMachine.States"/> of the destination state, or <c>-1</c>.
        /// </summary>
        /// <remarks>
        /// A value of <c>-1</c> means the destination could not be resolved at import time, for example because the
        /// target state was deleted. The runner never takes such a transition.
        /// </remarks>
        public int ToStateIndex = -1;

        /// <summary>
        /// Whether this transition is anchored on a single state rather than connecting two states.
        /// </summary>
        /// <remarks>
        /// A self transition re-enters its own state, so <see cref="ToStateIndex"/> is the index of the state that owns
        /// the transition. Taking it calls <see cref="RuntimeGameState.OnExit"/> followed by
        /// <see cref="RuntimeGameState.OnEnter"/> for the same state, and resets the time in state, which is what makes
        /// it useful for restarting a state's behavior from the beginning.
        /// <br/><br/>
        /// The authored <c>EntryTransition</c> is a self transition too, but it is a marker rather than something the
        /// machine takes, so the importer leaves it out of the runtime data entirely.
        /// </remarks>
        public bool IsSelfTransition;

        /// <summary>
        /// The rules stacked on this transition, in the order they were authored.
        /// </summary>
        public List<GameFlowRuntimeTransitionRule> Rules = new();
    }

    /// <summary>
    /// The runtime form of a single rule stacked on a <see cref="GameFlowRuntimeTransition"/>.
    /// </summary>
    [Serializable]
    public class GameFlowRuntimeTransitionRule
    {
        /// <summary>
        /// The authored title of the rule.
        /// </summary>
        public string Title;

        /// <summary>
        /// Whether the rule is evaluated. A disabled rule never fires.
        /// </summary>
        public bool Enabled = true;

        /// <summary>
        /// The index into <see cref="GameFlowRuntimeStateMachine.Conditions"/> of the rule's root condition, or <c>-1</c>.
        /// </summary>
        /// <remarks>
        /// A value of <c>-1</c> means the rule's condition tree could not be imported.
        /// </remarks>
        public int RootConditionIndex = -1;
    }

    /// <summary>
    /// The runtime form of a blackboard variable declared on the authoring state machine.
    /// </summary>
    /// <remarks>
    /// The authoring machine offers <see langword="bool"/> variables only, so this type needs no type discriminator.
    /// </remarks>
    [Serializable]
    public class GameFlowRuntimeVariable
    {
        /// <summary>
        /// The name of the variable as it appears in the blackboard.
        /// </summary>
        public string Name;

        /// <summary>
        /// The authored default value.
        /// </summary>
        public bool DefaultValue;
    }
}
