using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.GraphToolkit.Editor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Unity.GraphToolkit.Samples.GameStateMachine.Editor
{
    /// <summary>
    /// GameFlowStateMachineImporter is a <see cref="ScriptedImporter"/> that imports the
    /// <see cref="GameFlowStateMachine"/> and builds the corresponding <see cref="GameFlowRuntimeStateMachine"/>.
    /// </summary>
    /// <remarks>
    /// The conversion flattens the authored state machine such that:<br/>
    /// - Each <see cref="GameState"/> becomes a <see cref="GameFlowRuntimeState"/>, carrying the
    ///   <see cref="RuntimeGameState"/> returned by its <see cref="GameState.CreateRuntimeState"/>.<br/>
    /// - Each authored transition becomes a <see cref="GameFlowRuntimeTransition"/> on its source state, holding a
    ///   <see cref="GameFlowRuntimeTransitionRule"/> per authored rule.<br/>
    /// - <see cref="GameSignalCondition"/>, <see cref="TimeInStateCondition"/>, the built-in variable condition and the
    ///   built-in group condition become the matching <see cref="GameFlowRuntimeCondition"/> types, appended to a list.<br/>
    /// - <see cref="EntryTransition"/> is not imported as a transition; it only resolves
    ///   <see cref="GameFlowRuntimeStateMachine.EntryStateIndex"/>.
    /// </remarks>
    [ScriptedImporter(1, GameFlowStateMachine.AssetExtension)]
    class GameFlowStateMachineImporter : ScriptedImporter
    {
        /// <summary>
        /// Unity calls this method when the editor imports the asset. This method then processes the imported
        /// <see cref="GameFlowStateMachine"/>.
        /// </summary>
        /// <param name="ctx">The asset import context.</param>
        /// <remarks>
        /// The runtime object is exported into the state machine asset and set as its main asset, which allows the
        /// same asset to be used in inspectors wherever a <see cref="GameFlowRuntimeStateMachine"/> is expected, while
        /// double-clicking it still opens the state machine editor.
        /// </remarks>
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var stateMachine = StateMachineDatabase.LoadStateMachineForImporter<GameFlowStateMachine>(ctx.assetPath);

            // The state machine may be null when the asset does not exist at that path, holds a different state
            // machine type, or is corrupt.
            if (stateMachine == null)
            {
                ctx.LogImportError($"Failed to load the game flow state machine asset: {ctx.assetPath}");
                return;
            }

            var runtimeAsset = ScriptableObject.CreateInstance<GameFlowRuntimeStateMachine>();
            runtimeAsset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            runtimeAsset.ID = stateMachine.ID;

            BuildRuntimeStateMachine(ctx, stateMachine, runtimeAsset);

            // Add the runtime object to the state machine asset and make it the main asset, so the same asset can be
            // used wherever a GameFlowRuntimeStateMachine is expected.
            ctx.AddObjectToAsset("RuntimeAsset", runtimeAsset);
            ctx.SetMainObject(runtimeAsset);
        }

        /// <summary>
        /// Populates a runtime state machine from an authoring state machine.
        /// </summary>
        /// <param name="ctx">The asset import context, used to report recoverable problems against the asset.</param>
        /// <param name="stateMachine">The authoring state machine to convert.</param>
        /// <param name="runtimeAsset">The runtime asset to populate.</param>
        static void BuildRuntimeStateMachine(AssetImportContext ctx, GameFlowStateMachine stateMachine, GameFlowRuntimeStateMachine runtimeAsset)
        {
            var variableIndicesByName = BuildVariables(ctx, stateMachine, runtimeAsset);
            var stateIndicesByID = BuildStates(ctx, stateMachine, runtimeAsset);

            BuildTransitions(ctx, stateMachine, runtimeAsset, stateIndicesByID, variableIndicesByName);

            runtimeAsset.EntryStateIndex = ResolveEntryStateIndex(ctx, stateMachine, runtimeAsset, stateIndicesByID);
        }

        /// <summary>
        /// Imports the blackboard variables declared on the state machine.
        /// </summary>
        /// <param name="ctx">The asset import context.</param>
        /// <param name="stateMachine">The authoring state machine to read the variables from.</param>
        /// <param name="runtimeAsset">The runtime asset to populate.</param>
        /// <returns>A map from variable name to runtime variable index.</returns>
        static Dictionary<string, int> BuildVariables(AssetImportContext ctx, GameFlowStateMachine stateMachine, GameFlowRuntimeStateMachine runtimeAsset)
        {
            var variableIndicesByName = new Dictionary<string, int>();

            foreach (var variable in stateMachine.GetVariables())
            {
                if (variable == null)
                    continue;

                if (variable.DataType != typeof(bool))
                {
                    ctx.LogImportWarning(
                        $"Blackboard variable '{variable.Name}' has the unsupported type {variable.DataType}. " +
                        "A game flow state machine supports bool variables, so this variable was skipped.");
                    continue;
                }

                var runtimeVariable = new GameFlowRuntimeVariable { Name = variable.Name };
                variable.TryGetDefaultValue(out runtimeVariable.DefaultValue);

                if (!string.IsNullOrEmpty(runtimeVariable.Name))
                {
                    if (variableIndicesByName.ContainsKey(runtimeVariable.Name))
                    {
                        ctx.LogImportWarning(
                            $"More than one blackboard variable is named '{runtimeVariable.Name}'. Conditions using " +
                            "that name resolve to the first one.");
                    }
                    else
                    {
                        variableIndicesByName[runtimeVariable.Name] = runtimeAsset.Variables.Count;
                    }
                }

                runtimeAsset.Variables.Add(runtimeVariable);
            }

            return variableIndicesByName;
        }

        /// <summary>
        /// Imports the states of the state machine.
        /// </summary>
        /// <param name="ctx">The asset import context.</param>
        /// <param name="stateMachine">The authoring state machine to read the states from.</param>
        /// <param name="runtimeAsset">The runtime asset to populate.</param>
        /// <returns>A map from authoring state ID to runtime state index.</returns>
        static Dictionary<Hash128, int> BuildStates(AssetImportContext ctx, GameFlowStateMachine stateMachine, GameFlowRuntimeStateMachine runtimeAsset)
        {
            var stateIndicesByID = new Dictionary<Hash128, int>();
            var stateNames = new HashSet<string>();

            foreach (var state in stateMachine.GetStates())
            {
                if (state == null)
                    continue;

                var runtimeState = new GameFlowRuntimeState
                {
                    ID = state.ID,
                    Name = state.Title,
                    RuntimeState = CreateRuntimeState(ctx, state)
                };

                // Several states can share a state type, so states require unique titles in order to be identifiable
                if (!string.IsNullOrEmpty(runtimeState.Name) && !stateNames.Add(runtimeState.Name))
                {
                    ctx.LogImportWarning(
                        $"More than one state is named '{runtimeState.Name}'. Anything that looks a state up by name " +
                        "resolves to the first one; rename one of the states to tell them apart.");
                }

                stateIndicesByID[state.ID] = runtimeAsset.States.Count;
                runtimeAsset.States.Add(runtimeState);
            }

            if (runtimeAsset.States.Count == 0)
                ctx.LogImportWarning($"'{runtimeAsset.name}' contains no states, so there is nothing to run.");

            return stateIndicesByID;
        }

        /// <summary>
        /// Converts an authoring state into the <see cref="RuntimeGameState"/> that runs while it is active.
        /// </summary>
        /// <param name="ctx">The asset import context.</param>
        /// <param name="state">The authoring state to convert.</param>
        /// <returns>The runtime state, or <see langword="null"/> when the state is not a <see cref="GameState"/>.</returns>
        static RuntimeGameState CreateRuntimeState(AssetImportContext ctx, IState state)
        {
            if (state is GameState gameState)
                return gameState.CreateRuntimeState();

            ctx.LogImportWarning($"'{state.Title}' is not a Game State, so it does nothing while it is active.");
            return null;
        }

        /// <summary>
        /// Imports the transitions of every state, along with their rules and condition trees.
        /// </summary>
        /// <param name="ctx">The asset import context.</param>
        /// <param name="stateMachine">The authoring state machine to read the transitions from.</param>
        /// <param name="runtimeAsset">The runtime asset to populate.</param>
        /// <param name="stateIndicesByID">Maps an authoring state ID to its runtime state index.</param>
        /// <param name="variableIndicesByName">Maps a blackboard variable name to its runtime variable index.</param>
        static void BuildTransitions(
            AssetImportContext ctx,
            GameFlowStateMachine stateMachine,
            GameFlowRuntimeStateMachine runtimeAsset,
            Dictionary<Hash128, int> stateIndicesByID,
            Dictionary<string, int> variableIndicesByName)
        {
            foreach (var state in stateMachine.GetStates())
            {
                if (state == null)
                    continue;

                if (!stateIndicesByID.TryGetValue(state.ID, out var fromStateIndex))
                    continue;

                var runtimeState = runtimeAsset.States[fromStateIndex];
                
                foreach (var transition in state.GetOutgoingTransitions())
                {
                    if (transition == null)
                        continue;

                    // The Entry transition only marks where the runner starts. It is not a transition the machine is supposed
                    // to take, so it is left out of the runtime data.
                    if (transition is EntryTransition)
                        continue;

                    var isSelfTransition = transition is ISelfTransition;
                    var toStateIndex = ResolveDestination(ctx, transition, stateIndicesByID, fromStateIndex, isSelfTransition, state.Title);

                    var runtimeTransition = new GameFlowRuntimeTransition
                    {
                        ID = transition.ID,
                        ToStateIndex = toStateIndex,
                        IsSelfTransition = isSelfTransition
                    };

                    foreach (var rule in transition.GetRules())
                    {
                        if (rule == null)
                            continue;

                        var runtimeRule = new GameFlowRuntimeTransitionRule
                        {
                            Title = rule.Title,
                            Enabled = rule.Enabled
                        };

                        if (rule.RootCondition == null)
                        {
                            ctx.LogImportWarning(
                                $"The rule '{rule.Title}' on a transition leaving '{state.Title}' has no condition " +
                                "group and was dropped.");
                        }
                        else
                        {
                            var conditionCountBefore = runtimeAsset.Conditions.Count;

                            if (TryTranslateCondition(ctx, runtimeAsset, rule.RootCondition, variableIndicesByName, out var rootConditionIndex))
                            {
                                runtimeRule.RootConditionIndex = rootConditionIndex;
                            }
                            else
                            {
                                runtimeAsset.Conditions.RemoveRange(conditionCountBefore, runtimeAsset.Conditions.Count - conditionCountBefore);

                                ctx.LogImportWarning(
                                    $"The rule '{rule.Title}' on a transition leaving '{state.Title}' contains a condition " +
                                    "that could not be imported, so the whole rule was dropped. The rule will never fire.");
                            }
                        }

                        runtimeTransition.Rules.Add(runtimeRule);
                    }

                    runtimeState.Transitions.Add(runtimeTransition);
                }
            }
        }

        /// <summary>
        /// Resolves the runtime index of a transition's destination state.
        /// </summary>
        /// <param name="ctx">The asset import context.</param>
        /// <param name="transition">The transition whose destination is resolved.</param>
        /// <param name="stateIndicesByID">Maps an authoring state ID to its runtime state index.</param>
        /// <param name="fromStateIndex">The runtime index of the state the transition leaves.</param>
        /// <param name="isSelfTransition">Whether the transition is anchored on a single state.</param>
        /// <param name="fromStateName">The title of the state the transition leaves, used in warnings.</param>
        /// <returns>The destination state index, or <c>-1</c> when the destination cannot be used at runtime.</returns>
        /// <remarks>
        /// <see cref="ITransition.ToState"/> can be <see langword="null"/> even for a connected transition, for
        /// instance when the destination's state type was renamed or removed and the state no longer resolves while
        /// its transitions survive.
        /// </remarks>
        static int ResolveDestination(
            AssetImportContext ctx,
            ITransition transition,
            Dictionary<Hash128, int> stateIndicesByID,
            int fromStateIndex,
            bool isSelfTransition,
            string fromStateName)
        {
            if (isSelfTransition)
                return fromStateIndex;

            var toState = transition.ToState;

            if (toState == null)
            {
                ctx.LogImportWarning(
                    $"A transition leaving '{fromStateName}' has no destination state, so it will never be taken. " +
                    "This usually means the destination state no longer resolves in the state machine.");
                return -1;
            }

            if (!stateIndicesByID.TryGetValue(toState.ID, out var toStateIndex))
            {
                ctx.LogImportWarning(
                    $"The transition from '{fromStateName}' to '{toState.Title}' was dropped, because that destination " +
                    "is not one of the imported states.");
                return -1;
            }

            return toStateIndex;
        }

        /// <summary>
        /// Determines which state the runner starts in.
        /// </summary>
        /// <param name="ctx">The asset import context.</param>
        /// <param name="stateMachine">The authoring state machine to search for an entry point.</param>
        /// <param name="runtimeAsset">The runtime asset whose states have already been imported.</param>
        /// <param name="stateIndicesByID">Maps an authoring state ID to its runtime state index.</param>
        /// <returns>The entry state index, or <c>-1</c> when the machine has no states at all.</returns>
        /// <remarks>
        /// The entry point is the state that carries an <see cref="EntryTransition"/>. Every other case falls back to
        /// the first authored state and warns, so that an asset with no Entry transition still runs.
        /// <br/><br/>
        /// The rules stacked on an Entry transition are ignored: the runner has to start somewhere, so the state the
        /// transition is attached to is entered unconditionally.
        /// </remarks>
        static int ResolveEntryStateIndex(AssetImportContext ctx, GameFlowStateMachine stateMachine, GameFlowRuntimeStateMachine runtimeAsset, Dictionary<Hash128, int> stateIndicesByID)
        {
            if (runtimeAsset.States.Count == 0)
                return -1;

            var entryStateIndices = new List<int>();

            foreach (var state in stateMachine.GetStates())
            {
                if (state == null || !HasEntryTransition(state))
                    continue;

                if (stateIndicesByID.TryGetValue(state.ID, out var stateIndex))
                    entryStateIndices.Add(stateIndex);
            }

            if (entryStateIndices.Count == 0)
            {
                ctx.LogImportWarning(
                    $"'{runtimeAsset.name}' has no Entry transition, so the runner starts in " +
                    $"'{runtimeAsset.States[0].Name}'. Open the asset, right-click the state the game should start " +
                    "in, and create an Entry transition on it.");
                return 0;
            }

            if (entryStateIndices.Count > 1)
            {
                ctx.LogImportWarning(
                    $"'{runtimeAsset.name}' has {entryStateIndices.Count} states carrying an Entry transition. The " +
                    $"runner starts in '{runtimeAsset.States[entryStateIndices[0]].Name}'; remove the other Entry " +
                    "transitions to make the entry point unambiguous.");
            }

            return entryStateIndices[0];
        }

        /// <summary>
        /// Gets whether a state carries the <see cref="EntryTransition"/> that marks it as the entry point.
        /// </summary>
        /// <param name="state">The state to inspect.</param>
        /// <returns><see langword="true"/> when the state carries an Entry transition.</returns>
        static bool HasEntryTransition(IState state)
        {
            foreach (var transition in state.GetOutgoingTransitions())
            {
                if (transition is EntryTransition)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Converts an authoring condition, and everything nested inside it, into runtime conditions.
        /// </summary>
        /// <param name="ctx">The asset import context.</param>
        /// <param name="runtimeAsset">The runtime asset whose condition list is appended to.</param>
        /// <param name="condition">The authoring condition to convert.</param>
        /// <param name="variableIndicesByName">Maps a blackboard variable name to its runtime variable index.</param>
        /// <param name="conditionIndex">The index of the converted condition in the runtime condition list.</param>
        /// <returns>
        /// <see langword="true"/> when the condition converts successfully, otherwise <see langword="false"/> when
        /// the condition is unsupported.
        /// </returns>
        static bool TryTranslateCondition(
            AssetImportContext ctx,
            GameFlowRuntimeStateMachine runtimeAsset,
            ICondition condition,
            Dictionary<string, int> variableIndicesByName,
            out int conditionIndex)
        {
            conditionIndex = -1;

            switch (condition)
            {
                case IGroupCondition groupCondition:
                {
                    var runtimeGroup = new GameFlowRuntimeGroupCondition
                    {
                        Operation = groupCondition.Operation == GroupConditionOperation.Or
                            ? GameFlowConditionGroupOperation.Or
                            : GameFlowConditionGroupOperation.And
                    };

                    conditionIndex = AppendCondition(runtimeAsset, runtimeGroup, groupCondition);

                    foreach (var subCondition in groupCondition.Get())
                    {
                        if (!TryTranslateCondition(ctx, runtimeAsset, subCondition, variableIndicesByName, out var subConditionIndex))
                            return false;

                        runtimeGroup.SubConditionIndices.Add(subConditionIndex);
                    }

                    return true;
                }

                case GameSignalCondition signalCondition:
                {
                    if (string.IsNullOrEmpty(signalCondition.Value))
                    {
                        ctx.LogImportWarning(
                            "A Game Signal condition has no signal name, so it can never be met. Type the name that " +
                            "gameplay passes to GameFlowRunner.RaiseSignal.");
                    }

                    conditionIndex = AppendCondition(runtimeAsset, new GameFlowRuntimeSignalCondition
                    {
                        Signal = signalCondition.Value
                    }, signalCondition);

                    return true;
                }

                case TimeInStateCondition timeInStateCondition:
                {
                    if (!TryConvertComparison(ctx, timeInStateCondition.Comparison, out var comparison))
                        return false;

                    conditionIndex = AppendCondition(runtimeAsset, new GameFlowRuntimeTimeInStateCondition
                    {
                        Value = timeInStateCondition.Value,
                        Comparison = comparison
                    }, timeInStateCondition);

                    return true;
                }

                case IVariableCondition variableCondition:
                    return TryTranslateVariableCondition(ctx, runtimeAsset, variableCondition, variableIndicesByName, out conditionIndex);

                default:
                    ctx.LogImportWarning($"Skipped a condition of an unsupported kind: {condition?.GetType().Name ?? "null"}.");
                    return false;
            }
        }

        /// <summary>
        /// Converts the built-in condition that compares a blackboard variable against a constant.
        /// </summary>
        /// <param name="ctx">The asset import context.</param>
        /// <param name="runtimeAsset">The runtime asset whose condition list is appended to.</param>
        /// <param name="variableCondition">The authoring variable condition to convert.</param>
        /// <param name="variableIndicesByName">Maps a blackboard variable name to its runtime variable index.</param>
        /// <param name="conditionIndex">The index of the converted condition in the runtime condition list.</param>
        /// <returns><see langword="true"/> when the condition converted.</returns>
        static bool TryTranslateVariableCondition(
            AssetImportContext ctx,
            GameFlowRuntimeStateMachine runtimeAsset,
            IVariableCondition variableCondition,
            Dictionary<string, int> variableIndicesByName,
            out int conditionIndex)
        {
            conditionIndex = -1;

            // Variable and Value are both null when no variable is assigned to the condition, or when the variable it
            // referred to was deleted from the blackboard.
            var variable = variableCondition.Variable;
            if (variable == null)
            {
                ctx.LogImportWarning("A variable condition has no variable assigned to it and was skipped.");
                return false;
            }

            if (!variableIndicesByName.TryGetValue(variable.Name, out var variableIndex))
            {
                ctx.LogImportWarning(
                    $"A condition refers to the blackboard variable '{variable.Name}', which was not imported. " +
                    "A game flow state machine supports bool variables.");
                return false;
            }

            if (!TryConvertComparison(ctx, variableCondition.Comparison, out var comparison))
                return false;

            var runtimeCondition = new GameFlowRuntimeVariableCondition
            {
                VariableIndex = variableIndex,
                Comparison = comparison
            };

            // Value is boxed as object, and its runtime type follows the variable's data type.
            var value = variableCondition.Value;

            try
            {
                runtimeCondition.Value = value != null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (exception is InvalidCastException or FormatException)
            {
                ctx.LogImportWarning(
                    $"The value compared against the blackboard variable '{variable.Name}' is not a boolean " +
                    $"({exception.Message}), so the condition was skipped.");
                return false;
            }

            conditionIndex = AppendCondition(runtimeAsset, runtimeCondition, variableCondition);
            return true;
        }

        /// <summary>
        /// Maps a Graph Toolkit comparison operator onto its runtime counterpart.
        /// </summary>
        /// <param name="ctx">The asset import context.</param>
        /// <param name="comparison">The authoring comparison operator to map.</param>
        /// <param name="runtimeComparison">The matching runtime comparison operator.</param>
        /// <returns><see langword="true"/> when the operator has a runtime equivalent.</returns>
        static bool TryConvertComparison(AssetImportContext ctx, ConditionComparison comparison, out GameFlowComparison runtimeComparison)
        {
            switch (comparison)
            {
                case ConditionComparison.Equal: runtimeComparison = GameFlowComparison.Equal; return true;
                case ConditionComparison.NotEqual: runtimeComparison = GameFlowComparison.NotEqual; return true;
                case ConditionComparison.Less: runtimeComparison = GameFlowComparison.Less; return true;
                case ConditionComparison.LessOrEqual: runtimeComparison = GameFlowComparison.LessOrEqual; return true;
                case ConditionComparison.Greater: runtimeComparison = GameFlowComparison.Greater; return true;
                case ConditionComparison.GreaterOrEqual: runtimeComparison = GameFlowComparison.GreaterOrEqual; return true;
                default:
                    ctx.LogImportWarning($"The comparison operator '{comparison}' has no runtime equivalent, so the condition was skipped.");
                    runtimeComparison = default;
                    return false;
            }
        }

        /// <summary>
        /// Sets the ID of a runtime condition and appends it to the runtime condition list, and returns its index.
        /// </summary>
        /// <param name="runtimeAsset">The runtime asset whose condition list is appended to.</param>
        /// <param name="runtimeCondition">The translated condition.</param>
        /// <param name="authoringCondition">The condition it was translated from, whose ID is copied across.</param>
        /// <returns>The index of the appended condition.</returns>
        static int AppendCondition(GameFlowRuntimeStateMachine runtimeAsset, GameFlowRuntimeCondition runtimeCondition, ICondition authoringCondition)
        {
            runtimeCondition.ID = authoringCondition.ID;

            runtimeAsset.Conditions.Add(runtimeCondition);
            return runtimeAsset.Conditions.Count - 1;
        }
    }
}
