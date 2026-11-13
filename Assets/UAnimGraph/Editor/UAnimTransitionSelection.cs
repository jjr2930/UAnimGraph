using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UAnimGraph.Authoring
{
    [Serializable, Condition("UAnimGraph/Choose Transition Function")]
    public sealed class TransitionFunctionPickerCondition : Condition
    {
    }

    // This row belongs to GTK's selected-transition inspector, not the graph.
    public sealed class TransitionFunctionPickerView : ConditionView<TransitionFunctionPickerCondition>
    {
        protected override bool DisplayTitleLabel
        {
            get
            {
                return false;
            }
        }

        protected override bool DisplayValueField
        {
            get
            {
                return false;
            }
        }

        public override void OnViewBuilt()
        {
            base.OnViewBuilt();
            View.Root.Add(new Button(() => UAnimEvaluationPicker.Open(key =>
            {
                var edge = Condition.Rule?.Transition;
                if(edge == null)
                {
                    return;
                }

                UAnimTransitionConditions.Choose((UAnimStateMachineGraph)Condition.StateMachine, edge, key);
            })) { text = "Choose function…" });
        }

        public override void OnViewAttached()
        {
            base.OnViewAttached();
            UAnimTransitionSelection.Attach(this, Condition.StateMachine, Condition.Rule?.Transition, View.Root);
        }

        public override void OnViewDetached()
        {
            UAnimTransitionSelection.Detach(this);
            base.OnViewDetached();
        }
    }

    [InitializeOnLoad]
    public static class UAnimTransitionSelection
    {
        sealed class Selected
        {
            public StateMachine machine;
            public ITransition transition;
            public VisualElement row;
        }

        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly Dictionary<object, Selected> attached = new Dictionary<object, Selected>();
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static Selected active;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static double next;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static bool preparing;
        static UAnimTransitionSelection()
        {
            Selection.selectionChanged += Clear;
            EditorApplication.update += Update;
        }

        public static void Attach(object owner, StateMachine machine, ITransition transition, VisualElement row = null)
        {
            if(!UAnimTransitionConditions.CanChoose(transition))
            {
                return;
            }

            active = new Selected
            {
                machine = machine,
                transition = transition,
                row = row
            };
            attached[owner] = active;
            Repaint();
        }

        public static void Detach(object owner)
        {
            if(attached.TryGetValue(owner, out var value))
            {
                attached.Remove(owner);
                if(ReferenceEquals(active, value))
                {
                    active = attached.Values.LastOrDefault();
                }

                Repaint();
            }
        }

        public static void Clear()
        {
            active = null;
            Repaint();
        }

        static void Repaint()
        {
            foreach(var editor in Resources.FindObjectsOfTypeAll<UnityEditor.Editor>())
            {
                if(editor)
                {
                    editor.Repaint();
                }
            }
        }

        public static ITransition Current(string path)
        {
            if(active == null || StateMachineDatabase.GetStateMachineAssetPath(active.machine) != path)
            {
                return null;
            }

            return active.machine.GetStates().SelectMany(s => s.GetOutgoingTransitions()).FirstOrDefault(t => t.ID == active.transition.ID);
        }

        public static bool EnsurePickers(UAnimStateMachineGraph machine)
        {
            bool changed = false;
            foreach(var edge in machine.GetStates().SelectMany(s => s.GetOutgoingTransitions()).Where(UAnimTransitionConditions.CanChoose).ToArray())
            {
                foreach(var rule in edge.GetRules().Where(r => r.Enabled))
                {
                    if(UAnimTransitionConditions.Functions(rule.RootCondition).Length > 0 || ContainsPicker(rule.RootCondition))
                    {
                        continue;
                    }

                    // Editor metadata only: don't alter the runtime meaning of an
                    // existing unconditional/field transition or create an Undo step.
                    rule.RootCondition.Add(new TransitionFunctionPickerCondition());
                    changed = true;
                }
            }

            return changed;
        }

        static bool ContainsPicker(IGroupCondition group)
        {
            return group.Get().Any(c => c is TransitionFunctionPickerCondition || c is IGroupCondition nested && ContainsPicker(nested));
        }

        static void Update()
        {
            if(EditorWindow.focusedWindow is IGraphWindow focused && focused.StateMachine != null)
            {
                string path = StateMachineDatabase.GetStateMachineAssetPath(focused.StateMachine);
                var visible = attached.Values.LastOrDefault(v => StateMachineDatabase.GetStateMachineAssetPath(v.machine) == path && v.row != null && v.row.panel != null && v.row.resolvedStyle.display != DisplayStyle.None);
                if(visible != null && !ReferenceEquals(active, visible))
                {
                    active = visible;
                    Repaint();
                }
            }

            if(preparing || EditorApplication.timeSinceStartup < next)
            {
                return;
            }

            next = EditorApplication.timeSinceStartup + .5;
            preparing = true;
            try
            {
                foreach(var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w && w is IGraphWindow).ToArray())
                {
                    var machine = ((IGraphWindow)window).StateMachine as UAnimStateMachineGraph;
                    if(machine == null)
                    {
                        continue;
                    }

                    if(window.rootVisualElement.Q("uanim-unified-layout") == null && window.overlayCanvas != null)
                    {
                        foreach(var overlay in window.overlayCanvas.overlays.Where(o => o.displayName.Contains("Inspector")))
                        {
                            if(!overlay.displayed)
                            {
                                overlay.displayed = true;
                            }
                        }
                    }

                    if(!EnsurePickers(machine))
                    {
                        continue;
                    }

                    StateMachineDatabase.SaveStateMachine(machine);
                    string path = StateMachineDatabase.GetStateMachineAssetPath(machine);
                    if(UAnimEmbeddedAssets.IsCache(path))
                    {
                        UAnimEmbeddedAssets.Capture(path);
                    }
                }
            }
            finally
            {
                preparing = false;
            }
        }
    }
}
