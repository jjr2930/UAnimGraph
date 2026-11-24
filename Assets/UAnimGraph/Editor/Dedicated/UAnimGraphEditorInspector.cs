using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public sealed partial class UAnimGraphEditor
    {
        void DrawInspector()
        {
            if(document == null || canvas == null)
            {
                EditorGUILayout.HelpBox("Open or create a UAnimGraph to begin.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(document.IsMachine ? "StateMachine" : document.Graph is UAnimRootGraph ? "Layer Chain" : "Pose Graph", EditorStyles.boldLabel);
            if(!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.HelpBox(status, MessageType.Info);
            }

            if(canvas.TransitionSelected)
            {
                var transition = document.Transitions.FirstOrDefault(t => t.ID.ToString() == canvas.Selected);
                if(transition != null)
                {
                    DrawTransition(transition);
                }

                return;
            }

            if(document.IsMachine)
            {
                var state = document.States.FirstOrDefault(s => s.ID.ToString() == canvas.Selected);
                if(state != null)
                {
                    DrawState(state);
                }
                else
                {
                    EditorGUILayout.HelpBox("Select a State or a transition arrow. Right click the canvas to add a State.", MessageType.Info);
                }

                return;
            }

            var node = document.Nodes.FirstOrDefault(n => n.ID.ToString() == canvas.Selected);
            if(node == null)
            {
                EditorGUILayout.HelpBox("Select a node. Drag between matching ports to connect them. Right click a wire or port to disconnect it.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(UAnimEditorDocument.Friendly(node.GetType()), EditorStyles.boldLabel);
            if(node is RootLayerNode layer)
            {
                DrawOptions(node, new[] { "Name", "AvatarMask", "Additive", "Weight", "Enabled" });
                if(GUILayout.Button("Open StateMachine"))
                {
                    OpenChild(node.ID.ToString());
                }
            }
            else if(node is PoseNode pose && node is IMixerAuthoring mixer)
            {
                DrawMixer(pose, mixer);
            }
            else if(node is ParameterNode parameter)
            {
                DrawParameterNode(parameter);
            }
            else
            {
                DrawOptions(node);
                if(node is StateMachineNode && GUILayout.Button("Open StateMachine"))
                {
                    OpenChild(node.ID.ToString());
                }
            }

            foreach(var port in node.GetInputPorts().Where(p => !p.IsConnected && p.DataType != typeof(UAnimPose)))
            {
                DrawPortDefault(port);
            }

            if(node is not StateOutputNode && node is not LayerOutputNode && GUILayout.Button("Delete Node"))
            {
                Run(() => document.Delete(node.ID.ToString()));
            }
        }

        void DrawOptions(INode node, string[] names = null)
        {
            for(int i = 0; i < node.NodeOptionCount; i++)
            {
                var option = node.GetNodeOption(i);
                if((names != null && !names.Contains(option.Name)) || (node is StateOutputNode && option.Name == "Duration"))
                {
                    continue;
                }

                object old = UAnimEditorDocument.ReadOption(option, option.DataType);
                EditorGUI.BeginChangeCheck();
                object value = DrawValue(option.DisplayName ?? option.Name, option.DataType, old);
                if(EditorGUI.EndChangeCheck())
                {
                    Run(() => document.SetOption(option, option.DataType, value, node as Node));
                }
            }
        }

        static object DrawValue(string label, Type type, object value)
        {
            if(type == typeof(string))
            {
                return EditorGUILayout.DelayedTextField(label, (string)value ?? "");
            }

            if(type == typeof(float))
            {
                return EditorGUILayout.DelayedFloatField(label, value == null ? 0 : (float)value);
            }

            if(type == typeof(int))
            {
                return EditorGUILayout.DelayedIntField(label, value == null ? 0 : (int)value);
            }

            if(type == typeof(bool))
            {
                return EditorGUILayout.Toggle(label, value != null && (bool)value);
            }

            if(type == typeof(Vector2))
            {
                return EditorGUILayout.Vector2Field(label, value == null ? Vector2.zero : (Vector2)value);
            }

            if(type.IsEnum)
            {
                return EditorGUILayout.EnumPopup(label, (Enum)value);
            }

            if(typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                return EditorGUILayout.ObjectField(label, (UnityEngine.Object)value, type, false);
            }

            EditorGUILayout.LabelField(label, value?.ToString() ?? "None");
            return value;
        }

        void DrawPortDefault(IPort port)
        {
            var values = new object[]
            {
                port.DataType.IsValueType ? Activator.CreateInstance(port.DataType) : null
            };
            var read = typeof(IPort).GetMethod("TryGetValue").MakeGenericMethod(port.DataType);
            if(!(bool)read.Invoke(port, values))
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            object next = DrawValue(port.DisplayName ?? port.Name, port.DataType, values[0]);
            if(EditorGUI.EndChangeCheck())
            {
                Run(() => document.Edit("Edit port value", () => typeof(IPort).GetMethod("TrySetValue").MakeGenericMethod(port.DataType).Invoke(port, new[] { next })));
            }
        }

        void DrawState(IState state)
        {
            if(state is UAnimEntryState)
            {
                EditorGUILayout.HelpBox("Entry selects the initial State. Use its Transition button and then click the destination State.", MessageType.Info);
                return;
            }

            string name = EditorGUILayout.DelayedTextField("Name", state.Title);
            if(name != state.Title)
            {
                Run(() => document.Edit("Rename State", () =>
                {
                    state.Title = name;
                    state.GetOptionByName("Name")?.TrySetValue(name);
                }));
            }

            if(GUILayout.Button("Open State Pose Graph"))
            {
                OpenChild(state.ID.ToString());
            }

            EditorGUILayout.LabelField("Outgoing transitions", EditorStyles.boldLabel);
            foreach(var edge in state.GetOutgoingTransitions())
            {
                if(GUILayout.Button(state.Title + " → " + edge.ToState.Title))
                {
                    canvas.Select(edge.ID.ToString(), true);
                }
            }

            if(GUILayout.Button("Delete State"))
            {
                Run(() => document.Delete(state.ID.ToString()));
            }
        }

        static IEnumerable<ICondition> Conditions(IGroupCondition group)
        {
            foreach(var condition in group.Get())
            {
                yield return condition;
                if(condition is IGroupCondition nested)
                {
                    foreach(var child in Conditions(nested))
                    {
                        yield return child;
                    }
                }
            }
        }

        void DrawTransition(ITransition edge)
        {
            EditorGUILayout.LabelField(edge.FromState.Title + " → " + edge.ToState.Title, EditorStyles.boldLabel);
            if(!UAnimTransitionConditions.CanChoose(edge))
            {
                EditorGUILayout.HelpBox("Initial State connection — no evaluation function.", MessageType.Info);
            }
            else
            {
                var rules = edge.GetRules().Where(r => r.Enabled).ToArray();
                if(rules.Length > 1)
                {
                    EditorGUILayout.HelpBox("This legacy transition has multiple enabled rules. Keep one rule before editing a single evaluation function.", MessageType.Warning);
                    return;
                }

                var all = rules.SelectMany(r => Conditions(r.RootCondition)).ToArray();
                var functions = all.OfType<EvaluationFunctionCondition>().ToArray();
                string key = functions.FirstOrDefault()?.Value;
                if(GUILayout.Button(string.IsNullOrEmpty(key) ? "Choose function…" : UAnimEvaluationCatalog.Display(key)))
                {
                    var menu = new GenericMenu();
                    UAnimEvaluationCatalog.Refresh();
                    foreach(var entry in UAnimEvaluationCatalog.Entries.Where(e => e.Valid))
                    {
                        string selected = entry.key;
                        menu.AddItem(new GUIContent(UAnimEvaluationCatalog.Display(selected).Replace('/', '.')), selected == key, () => Run(() =>
                        {
                            var machine = document.Machine;
                            UAnimTransitionConditions.Choose(machine, edge, selected);
                            QueueRefresh();
                        }));
                    }

                    menu.ShowAsContext();
                }

                if(!string.IsNullOrEmpty(key) && GUILayout.Button("Open Function Source"))
                {
                    UAnimEvaluationCatalog.OpenSource(key);
                }

                float duration = all.OfType<DurationCondition>().FirstOrDefault()?.Value ?? .2f;
                int priority = all.OfType<PriorityCondition>().FirstOrDefault()?.Value ?? 0;
                EditorGUI.BeginChangeCheck();
                float nextDuration = Mathf.Max(0, EditorGUILayout.DelayedFloatField("Duration (seconds)", duration));
                int nextPriority = EditorGUILayout.DelayedIntField("Priority", priority);
                bool self = all.OfType<AllowSelfCondition>().FirstOrDefault()?.Value ?? false;
                bool nextSelf = EditorGUILayout.Toggle("Allow Self", self);
                if(EditorGUI.EndChangeCheck())
                {
                    Run(() => document.Edit("Edit transition timing", () =>
                    {
                        var rule = rules.FirstOrDefault() ?? edge.AddRule();
                        var timing = all.OfType<DurationCondition>().FirstOrDefault();
                        if(timing == null)
                        {
                            rule.RootCondition.Add(new DurationCondition { Value = nextDuration });
                        }
                        else
                        {
                            timing.Value = nextDuration;
                        }

                        var order = all.OfType<PriorityCondition>().FirstOrDefault();
                        if(order == null)
                        {
                            rule.RootCondition.Add(new PriorityCondition { Value = nextPriority });
                        }
                        else
                        {
                            order.Value = nextPriority;
                        }

                        var allow = all.OfType<AllowSelfCondition>().FirstOrDefault();
                        if(allow == null)
                        {
                            rule.RootCondition.Add(new AllowSelfCondition { Value = nextSelf });
                        }
                        else
                        {
                            allow.Value = nextSelf;
                        }
                    }));
                }

                if(all.Any(c => c is ParameterCondition || c is ExitCondition))
                {
                    EditorGUILayout.HelpBox("Legacy parameter / exit conditions are preserved. Choosing a function replaces them.", MessageType.Info);
                }
            }

            if(GUILayout.Button("Delete Transition"))
            {
                Run(() => document.Delete(edge.ID.ToString(), true));
            }
        }

        string ParameterPopup(string label, string value, UAnimParameterType type)
        {
            var parameters = string.IsNullOrEmpty(document.RootPath) ? Array.Empty<UAnimParameter>() : UAnimSharedBlackboard.Parameters(document.RootPath).Where(p => p.type == type).ToArray();
            string[] names = new[]
            {
                "None"
            }.Concat(parameters.Select(p => p.name)).ToArray();
            int current = Array.FindIndex(parameters, p => p.id == value) + 1;
            int next = EditorGUILayout.Popup(label, current, names);
            return next == 0 ? null : parameters[next - 1].id;
        }

        void DrawParameterNode(ParameterNode node)
        {
            var type = (UAnimParameterType)EditorGUILayout.EnumPopup("Type", node.Option<UAnimParameterType>("Type"));
            string id = ParameterPopup("Parameter", node.Option<string>("ParameterId"), type);
            if(type != node.Option<UAnimParameterType>("Type") || id != node.Option<string>("ParameterId"))
            {
                Run(() => document.Edit("Bind Parameter", () =>
                {
                    node.GetNodeOptionByName("Type").TrySetValue(type);
                    node.GetNodeOptionByName("ParameterId").TrySetValue(id);
                    node.DefineNode();
                }));
            }
        }

        void DrawMixer(PoseNode node, IMixerAuthoring mixer)
        {
            var settings = mixer.Settings;
            EditorGUI.BeginChangeCheck();
            var binding = (MixerBinding)EditorGUILayout.EnumPopup("Input", settings.binding);
            string parameter = settings.parameter, x = settings.xParameter, y = settings.yParameter;
            Vector2 constant = settings.constant;
            if(binding == MixerBinding.FloatParameter || binding == MixerBinding.Vector2Parameter)
            {
                parameter = ParameterPopup("Parameter", parameter, binding == MixerBinding.Vector2Parameter ? UAnimParameterType.Vector2 : UAnimParameterType.Float);
            }

            if(binding == MixerBinding.XYParameters)
            {
                x = ParameterPopup("X", x, UAnimParameterType.Float);
                y = ParameterPopup("Y", y, UAnimParameterType.Float);
            }

            if(binding == MixerBinding.Constant)
            {
                constant = node is Blend2DNode ? EditorGUILayout.Vector2Field("Value", constant) : new Vector2(EditorGUILayout.FloatField("Value", constant.x), 0);
            }

            bool independent = EditorGUILayout.Toggle("Natural Clip Timing", settings.independentTime);
            if(EditorGUI.EndChangeCheck())
            {
                Run(() => UAnimMixerInputEditing.Edit(node, "Edit Mixer", () =>
                {
                    settings.binding = binding;
                    settings.parameter = parameter;
                    settings.xParameter = x;
                    settings.yParameter = y;
                    settings.constant = constant;
                    settings.independentTime = independent;
                }));
                QueueRefresh();
            }

            EditorGUILayout.HelpBox("Edit thresholds / positions on the node. The first two inputs are required.", MessageType.None);
            if(GUILayout.Button("Add Input Pose"))
            {
                Run(() => UAnimMixerInputEditing.Add(node));
                QueueRefresh();
            }

            var live = LiveInstance?.Poses.FirstOrDefault(p => p.Id == node.ID.ToString());
            if(live?.Weights != null)
            {
                for(int i = 0; i < live.Weights.Length; i++)
                {
                    EditorGUILayout.LabelField("Pose " + (i + 1), live.Weights[i].ToString("P1"));
                }
            }
        }

        void DrawBlackboard()
        {
            if(document == null || string.IsNullOrEmpty(document.RootPath))
            {
                EditorGUILayout.HelpBox("Shared parameters belong to the root UAnimGraph.", MessageType.Info);
                return;
            }

            if(GUILayout.Button("+ Parameter"))
            {
                var menu = new GenericMenu();
                foreach(UAnimParameterType type in Enum.GetValues(typeof(UAnimParameterType)))
                {
                    var captured = type;
                    menu.AddItem(new GUIContent(type.ToString()), false, () => Run(() =>
                    {
                        UAnimSharedBlackboard.Add(document.RootPath, captured);
                        QueueRefresh();
                    }));
                }

                menu.ShowAsContext();
            }

            blackboardScroll = EditorGUILayout.BeginScrollView(blackboardScroll, GUILayout.MaxHeight(280));
            var live = LiveInstance;
            foreach(var parameter in UAnimSharedBlackboard.Parameters(document.RootPath))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                bool remove = false;
                using(new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(parameter.name + " / " + parameter.type, EditorStyles.boldLabel);
                    remove = live == null && GUILayout.Button("×", GUILayout.Width(23));
                }

                if(remove)
                {
                    Run(() => UAnimSharedBlackboard.Remove(document.RootPath, parameter.id));
                    QueueRefresh();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUI.BeginChangeCheck();
                string name = live == null ? EditorGUILayout.DelayedTextField(parameter.name) : parameter.name;
                double value = live == null ? parameter.defaultValue : live.Get(parameter.id);
                Vector2 vector = live == null ? parameter.defaultVector2 : live.GetVector2(parameter.id);
                string label = live == null ? "Default" : "Live";
                switch(parameter.type)
                {
                    case UAnimParameterType.Float:
                        value = EditorGUILayout.FloatField(label, (float)value);
                        break;
                    case UAnimParameterType.Int:
                        value = EditorGUILayout.IntField(label, (int)value);
                        break;
                    case UAnimParameterType.Vector2:
                        vector = EditorGUILayout.Vector2Field(label, vector);
                        break;
                    case UAnimParameterType.Bool:
                        value = EditorGUILayout.Toggle(label, value != 0) ? 1 : 0;
                        break;
                    case UAnimParameterType.Trigger:
                        if(live != null && GUILayout.Button("Fire"))
                        {
                            live.SharedBlackboard.SetTrigger(parameter.id);
                        }

                        break;
                }

                if(EditorGUI.EndChangeCheck())
                {
                    if(live != null)
                    {
                        if(parameter.type == UAnimParameterType.Vector2)
                        {
                            live.SetVector2(parameter.id, vector);
                        }
                        else
                        {
                            live.Set(parameter.id, parameter.type, value);
                        }
                    }
                    else
                    {
                        Run(() => UAnimSharedBlackboard.Update(document.RootPath, parameter.id, name, value, vector));
                        QueueRefresh();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
