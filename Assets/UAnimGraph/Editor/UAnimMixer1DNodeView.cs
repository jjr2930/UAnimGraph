using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UAnimGraph.Authoring
{
    [InitializeOnLoad]
    public static class UAnimMixerInputEditing
    {
        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly HashSet<string> editedGraphs = new HashSet<string>();
        static UAnimMixerInputEditing()
        {
            Undo.undoRedoPerformed += RestorePortDefinitions;
        }

        static void RestorePortDefinitions()
        {
            foreach(string path in editedGraphs.Where(File.Exists).ToArray())
            {
                var graph = UAnimEditorWorkspace.LoadGraph(path);
                if(graph == null)
                {
                    continue;
                }

                foreach(var node in graph.GetNodes().OfType<PoseNode>().Where(node => node is IMixerAuthoring))
                {
                    node.DefineNode();
                }

                GraphDatabase.SaveGraph(graph);
                if(UAnimEmbeddedAssets.IsCache(path))
                {
                    UAnimEmbeddedAssets.Capture(path);
                }
            }
        }

        public static void Edit(PoseNode node, string title, Action action)
        {
            editedGraphs.Add(GraphDatabase.GetGraphAssetPath(node.Graph));
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(title);
            node.Graph.UndoBeginRecordGraph(title);
            try
            {
                MixerAuthoring.Materialize(node);
                action();
                node.DefineNode();
            }
            finally
            {
                node.Graph.UndoEndRecordGraph();
            }

            GraphDatabase.SaveGraph(node.Graph);
            string path = GraphDatabase.GetGraphAssetPath(node.Graph);
            if(UAnimEmbeddedAssets.IsCache(path))
            {
                UAnimEmbeddedAssets.Capture(path);
            }

            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
        }

        public static void Add(PoseNode node)
        {
            Edit(node, "Add Mixer input Pose", () => UAnimMixerInspector.AddInput(node));
        }

        public static bool Remove(PoseNode node, string port)
        {
            if(!UAnimMixerInspector.CanRemoveInput(node, port))
            {
                return false;
            }

            Edit(node, "Remove Mixer input Pose", () => UAnimMixerInspector.RemoveInput(node, port));
            return true;
        }

        public static void SetThreshold(Blend1DNode node, string port, float threshold)
        {
            if(!UAnimValidation.Finite(threshold))
            {
                return;
            }

            Edit(node, "Set Mixer1D threshold", () => node.Settings.inputs.First(e => e.port == port).threshold = threshold);
        }

        public static void SetPosition(Blend2DNode node, string port, Vector2 position)
        {
            if(!UAnimValidation.Finite(position.x) || !UAnimValidation.Finite(position.y))
            {
                return;
            }
            Edit(node, "Set Mixer2D sample position", () => node.Settings.inputs.First(entry => entry.port == port).position = position);
        }
    }

    public sealed class Blend1DNodeView : NodeView<Blend1DNode>
    {
        public override void OnViewBuilt()
        {
            base.OnViewBuilt();
            BuildControls(Node, View.Root);
        }

        public static void BuildControls(PoseNode node, VisualElement root)
        {
            var settings = ((IMixerAuthoring)node).Settings;
            if(!string.IsNullOrEmpty(settings.displayName) && node.Title != settings.displayName)
            {
                node.Title = settings.displayName;
            }

            root.style.minWidth = node is Blend2DNode ? 440 : 360;
            root.Q("uanim-add-input-pose")?.RemoveFromHierarchy();
            var add = new Button(() => UAnimMixerInputEditing.Add(node))
            {
                name = "uanim-add-input-pose",
                text = "Add Input Pose"
            };
            add.style.height = 28;
            add.style.flexShrink = 0;
            add.style.marginTop = 4;
            root.Add(add);
            string inspector = node is Blend2DNode ? "Mixer2D Inspector" : "Mixer1D Inspector";
            root.AddManipulator(new ContextualMenuManipulator(e => e.menu.AppendAction(inspector, _ => UAnimMixerInspector.Open(node))));
            root.RegisterCallback<PointerDownEvent>(e =>
            {
                if(e.button == 0)
                {
                    UAnimMixerInspector.BindIfOpen(node);
                }
            });
            DecoratePorts(node, root);
            root.schedule.Execute(() => DecoratePorts(node, root)).Every(200);
        }

        public static void DecoratePorts(PoseNode node, VisualElement root)
        {
            var inputs = MixerAuthoring.Inputs(node);
            for(int index = 0; index < inputs.Count; index++)
            {
                var input = inputs[index];
                string labelText = "Pose" + (index + 1);
                var label = root.Query<Label>().ToList().FirstOrDefault(l => l.text == labelText);
                if(label == null)
                {
                    continue;
                }

                var port = label.parent;
                while(port != null && !port.ClassListContains("ge-port"))
                {
                    port = port.parent;
                }

                if(port == null)
                {
                    continue;
                }

                float rowWidth = node is Blend2DNode ? 325 : 245;
                port.style.minWidth = rowWidth;
                port.style.minHeight = 28;
                port.style.flexShrink = 0;
                port.style.flexDirection = FlexDirection.Row;
                port.style.alignItems = Align.Center;
                var connector = port.Q<VisualElement>("connector-container");
                if(connector != null)
                {
                    connector.style.width = 80;
                    connector.style.minWidth = 80;
                    connector.style.flexGrow = 0;
                    connector.style.flexShrink = 0;
                }

                var constant = port.Q<VisualElement>("constant-editor");
                if(constant != null)
                {
                    constant.style.display = DisplayStyle.None;
                }

                if(port.parent != null)
                {
                    port.parent.style.minWidth = rowWidth;
                    port.parent.style.flexGrow = 1;
                }

                label.style.minWidth = 45;
                label.style.flexShrink = 0;
                string key = "uanim-input-" + input.port;
                var controls = port.Q<VisualElement>(key);
                if(controls == null)
                {
                    controls = new VisualElement
                    {
                        name = key
                    };
                    controls.style.flexDirection = FlexDirection.Row;
                    controls.style.alignItems = Align.Center;
                    controls.style.flexGrow = 1;
                    controls.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
                    string id = input.port;
                    VisualElement field;
                    if(node is Blend2DNode two)
                    {
                        var position = new Vector2Field
                        {
                            value = input.position,
                            name = "position",
                            tooltip = "Sample position (X, Y)"
                        };
                        foreach(var component in position.Query<FloatField>().ToList())
                        {
                            component.isDelayed = true;
                        }
                        position.RegisterValueChangedCallback(e => UAnimMixerInputEditing.SetPosition(two, id, e.newValue));
                        field = position;
                    }
                    else
                    {
                        var threshold = new FloatField
                        {
                            value = input.threshold,
                            isDelayed = true,
                            name = "threshold"
                        };
                        threshold.RegisterValueChangedCallback(e => UAnimMixerInputEditing.SetThreshold((Blend1DNode)node, id, e.newValue));
                        field = threshold;
                    }
                    field.style.width = node is Blend2DNode ? 156 : 76;
                    field.style.minWidth = node is Blend2DNode ? 156 : 76;
                    field.style.flexGrow = 1;
                    field.style.marginLeft = 4;
                    var remove = new Button(() => UAnimMixerInputEditing.Remove(node, id))
                    {
                        text = "X",
                        name = "remove"
                    };
                    remove.style.width = 24;
                    remove.style.flexShrink = 0;
                    remove.style.marginLeft = 4;
                    controls.Add(field);
                    controls.Add(remove);
                    port.Add(controls);
                }

                var editor = controls.Q<VisualElement>(node is Blend2DNode ? "position" : "threshold");
                var focused = editor.focusController?.focusedElement as VisualElement;
                if(focused == null || !editor.Contains(focused))
                {
                    if(editor is Vector2Field position)
                    {
                        position.SetValueWithoutNotify(input.position);
                    }
                    else if(editor is FloatField threshold)
                    {
                        threshold.SetValueWithoutNotify(input.threshold);
                    }
                }

                editor.tooltip = node is Blend2DNode ? labelText + " sample position (X, Y)" : labelText + " threshold: Value at which this Pose has full weight";
                var button = controls.Q<Button>("remove");
                button.SetEnabled(index >= 2);
                button.tooltip = index < 2 ? "The first two Pose inputs cannot be deleted" : "Delete this input Pose and its connection";
            }
        }
    }
}
