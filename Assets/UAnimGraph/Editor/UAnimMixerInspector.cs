using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UAnimGraph.Authoring
{
    public sealed class Blend2DNodeView : NodeView<Blend2DNode>
    {
        public override void OnViewBuilt()
        {
            base.OnViewBuilt();
            Blend1DNodeView.BuildControls(Node, View.Root);
        }
    }

    public sealed class UAnimMixerInspector : EditorWindow
    {
        [SerializeField]
        string graphPath, nodeId;
        [SerializeField]
        UAnimAsset root;
        PoseNode node;
        Vector2 scroll, pan, previewInput;
        float viewRange = 1.25f;
        string selected;
        bool testInput, sortView, dragging;
        int dragUndo;
        [MenuItem("Window/UAnimGraph/Mixer Inspector")]
        public static void ShowWindow()
        {
            var w = GetWindow<UAnimMixerInspector>("Mixer Inspector");
            w.EnsureSize();
            w.Show();
        }

        public static void Open(PoseNode node)
        {
            var w = GetWindow<UAnimMixerInspector>("Mixer Inspector");
            w.Bind(node);
            w.EnsureSize();
            w.Show();
        }

        public static void BindIfOpen(PoseNode node)
        {
            foreach(var w in Resources.FindObjectsOfTypeAll<UAnimMixerInspector>())
            {
                w.Bind(node);
            }
        }

        void EnsureSize()
        {
            minSize = new Vector2(500, 500);
            if(position.width < 560)
            {
                position = new Rect(120, 80, 640, 800);
            }
        }

        void Bind(PoseNode value)
        {
            ClearOverrides();
            node = value;
            graphPath = GraphDatabase.GetGraphAssetPath(value.Graph);
            nodeId = value.ID.ToString();
            selected = null;
            testInput = false;
            root = UAnimEditorWorkspace.CurrentRoot ? UAnimEditorWorkspace.CurrentRoot : root;
            Repaint();
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed += Reload;
            EditorApplication.update += Repaint;
            Reload();
        }

        void OnDisable()
        {
            EndDrag();
            ClearOverrides();
            Undo.undoRedoPerformed -= Reload;
            EditorApplication.update -= Repaint;
        }

        void Reload()
        {
            if(string.IsNullOrEmpty(graphPath))
            {
                return;
            }

            var graph = UAnimEditorWorkspace.LoadGraph(graphPath);
            node = graph?.GetNodes().OfType<PoseNode>().FirstOrDefault(n => n.ID.ToString() == nodeId);
            Repaint();
        }

        void Edit(string title, Action action)
        {
            if(node is IMixerAuthoring)
            {
                UAnimMixerInputEditing.Edit(node, title, action);
                return;
            }

            var graph = node.Graph;
            graph.UndoBeginRecordGraph(title, node);
            try
            {
                MixerAuthoring.Materialize(node);
                action();
                node.DefineNode();
            }
            finally
            {
                graph.UndoEndRecordGraph();
            }

            GraphDatabase.SaveGraph(graph);
        }

        public static void AddInput(PoseNode node)
        {
            MixerAuthoring.Materialize(node);
            var s = ((IMixerAuthoring)node).Settings;
            float threshold = 0;
            while(s.inputs.Any(e => node is Blend2DNode ? e.position.Equals(new Vector2(threshold, 0)) : e.threshold == threshold))
            {
                threshold += 1;
            }

            s.inputs.Add(new MixerPoseInput("Pose" + Guid.NewGuid().ToString("N"), threshold, new Vector2(threshold, 0)));
        }

        public static bool CanRemoveInput(PoseNode node, string port)
        {
            int index = MixerAuthoring.Inputs(node).FindIndex(i => i.port == port);
            return index >= 2;
        }

        public static bool RemoveInput(PoseNode node, string port)
        {
            if(!CanRemoveInput(node, port))
            {
                return false;
            }

            MixerAuthoring.Materialize(node);
            return ((IMixerAuthoring)node).Settings.inputs.RemoveAll(e => e.port == port) > 0;
        }

        void ClearOverrides()
        {
            var live = UAnimWorkbench.PreviewInstance;
            if(live != null)
            {
                foreach(var p in live.Poses)
                {
                    if(p.Id == nodeId)
                    {
                        p.HasPreviewOverride = false;
                    }
                }
            }
        }

        UAnimInstance.Pose LivePose
        {
            get
            {
                return UAnimWorkbench.ActiveInstance?.Poses.FirstOrDefault(p => p.Id == nodeId && p.Activity > 0);
            }
        }

        Vector2 CurrentInput()
        {
            if(testInput)
            {
                return previewInput;
            }

            var p = LivePose;
            if(p != null)
            {
                return node is Blend2DNode ? p.InputVector : new Vector2(p.InputValue, 0);
            }

            var s = ((IMixerAuthoring)node).Settings;
            if(s.binding == MixerBinding.Constant)
            {
                return s.constant;
            }

            if(root)
            {
                var param = root.parameters.FirstOrDefault(v => v.id == s.parameter);
                if(param != null)
                {
                    return param.type == UAnimParameterType.Vector2 ? param.defaultVector2 : new Vector2((float)param.defaultValue, 0);
                }

                if(s.binding == MixerBinding.XYParameters)
                {
                    return new Vector2((float)(root.parameters.FirstOrDefault(v => v.id == s.xParameter)?.defaultValue ?? 0), (float)(root.parameters.FirstOrDefault(v => v.id == s.yParameter)?.defaultValue ?? 0));
                }
            }

            return Vector2.zero;
        }

        void Parameter(string label, string current, UAnimParameterType type, Action<MixerSettings, string> assign)
        {
            var item = UAnimParameterPicker.Search(root, type, "").FirstOrDefault(p => p.id == current);
            if(item == null && !string.IsNullOrEmpty(current))
            {
                EditorGUILayout.HelpBox("Missing parameter: " + current, MessageType.Warning);
            }

            using(new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                using(new EditorGUI.DisabledScope(!root))
                {
                    if(GUILayout.Button(item?.name ?? "Search parameters…", EditorStyles.popup))
                    {
                        string path = graphPath, id = nodeId;
                        var parameterRoot = root;
                        UAnimParameterPicker.Open(root, type, key =>
                        {
                            if(!this || path != graphPath || id != nodeId || root != parameterRoot)
                            {
                                return;
                            }

                            Reload();
                            if(node == null)
                            {
                                return;
                            }

                            if(!UAnimParameterPicker.Search(root, type, "").Any(p => p.id == key))
                            {
                                return;
                            }

                            Edit("Bind Mixer parameter", () => assign(((IMixerAuthoring)node).Settings, key));
                            Repaint();
                        });
                    }
                }
            }
        }

        void OnGUI()
        {
            if(node == null)
            {
                EditorGUILayout.HelpBox("Select a Mixer node and click its Mixer Inspector button.", MessageType.Info);
                return;
            }

            bool two = node is Blend2DNode;
            var data = ((IMixerAuthoring)node).Settings;
            root = (UAnimAsset)EditorGUILayout.ObjectField("Parameter root", root, typeof(UAnimAsset), false);
            using(new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(two ? "Mixer2D" : "Mixer1D", EditorStyles.boldLabel);
                if(GUILayout.Button("Open Graph"))
                {
                    AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(graphPath));
                }
            }

            var issues = UAnimAuthoringValidation.GetIssues(node.Graph, node.Graph is UAnimStateGraph).Where(i => ReferenceEquals(i.element, node)).ToArray();
            int warnings = issues.Count(i => UAnimAuthoringValidation.IsIncomplete(node.Graph, i.message));
            EditorGUILayout.LabelField("Diagnostics", (issues.Length - warnings) + " errors / " + warnings + " warnings");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            string name = EditorGUILayout.DelayedTextField("Name", node.Title);
            if(name != node.Title)
            {
                Edit("Rename Mixer", () =>
                {
                    data.displayName = name;
                    node.Title = name;
                });
            }

            var bindings = two ? new[]
            {
                MixerBinding.Vector2Parameter,
                MixerBinding.XYParameters,
                MixerBinding.Constant
            }

            : new[]
            {
                MixerBinding.GraphPort,
                MixerBinding.FloatParameter,
                MixerBinding.Constant
            };
            int binding = Array.IndexOf(bindings, data.binding);
            int nextBinding = EditorGUILayout.Popup("Input source", Mathf.Max(0, binding), bindings.Select(v => v.ToString()).ToArray());
            if(nextBinding != binding && (binding >= 0 || Event.current.type != EventType.Repaint))
            {
                var port = node.GetInputPortByName("Value");
                bool connected = port != null && port.IsConnected;
                if(!connected || EditorUtility.DisplayDialog("Change Mixer input", "The old Value wire will be removed. Continue?", "Change", "Cancel"))
                {
                    Edit("Change Mixer binding", () => data.binding = bindings[nextBinding]);
                }
            }

            if(data.binding == MixerBinding.FloatParameter || data.binding == MixerBinding.Vector2Parameter)
            {
                Parameter("Parameter", data.parameter, two ? UAnimParameterType.Vector2 : UAnimParameterType.Float, (s, key) => s.parameter = key);
            }

            if(data.binding == MixerBinding.XYParameters)
            {
                Parameter("X", data.xParameter, UAnimParameterType.Float, (s, key) => s.xParameter = key);
                Parameter("Y", data.yParameter, UAnimParameterType.Float, (s, key) => s.yParameter = key);
            }

            if(data.binding == MixerBinding.Constant)
            {
                Vector2 value = two ? EditorGUILayout.Vector2Field("Constant", data.constant) : new Vector2(EditorGUILayout.FloatField("Constant", data.constant.x), 0);
                if(value != data.constant && Finite(value))
                {
                    Edit("Set Mixer constant", () => data.constant = value);
                }
            }

            bool independent = EditorGUILayout.Popup("Time", data.independentTime ? 1 : 0, new[] { "State phase synchronization", "Independent playback" }) == 1;
            if(independent != data.independentTime)
            {
                Edit("Set Mixer time", () => data.independentTime = independent);
            }

            if(!two)
            {
                EditorGUILayout.LabelField("Outside range", "Clamp to end samples");
            }
            else
            {
                EditorGUILayout.LabelField("Interpolation", "Directional / speed gradient bands");
            }

            var stateSettings = UAnimStateSettingsNavigation.Find(root, AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(graphPath));
            using(new EditorGUI.DisabledScope(stateSettings.Length == 0))
            {
                if(GUILayout.Button("Open State duration settings"))
                {
                    if(stateSettings.Length == 1)
                    {
                        UAnimStateSettingsNavigation.Open(stateSettings[0]);
                    }
                    else
                    {
                        var menu = new GenericMenu();
                        foreach(var target in stateSettings)
                        {
                            var selectedTarget = target;
                            menu.AddItem(new GUIContent(target.LayerName + " / " + target.State.name + " [" + target.State.id + "]"), false, () => UAnimStateSettingsNavigation.Open(selectedTarget));
                        }

                        menu.ShowAsContext();
                    }
                }
            }

            if(stateSettings.Length == 0)
            {
                EditorGUILayout.HelpBox("This graph is not used by a State in the selected root. Assign it to a State to edit its duration.", MessageType.Info);
            }

            foreach(var target in stateSettings)
            {
                EditorGUILayout.LabelField(target.LayerName + " / " + target.State.name, target.State.duration + " s");
            }

            testInput = EditorGUILayout.Toggle("Test input (preview only)", testInput);
            if(testInput)
            {
                previewInput = two ? EditorGUILayout.Vector2Field("Temporary input", previewInput) : new Vector2(EditorGUILayout.FloatField("Temporary input", previewInput.x), 0);
                if(!Finite(previewInput))
                {
                    previewInput = Vector2.zero;
                }
            }

            var preview = UAnimWorkbench.PreviewInstance;
            if(preview != null)
            {
                foreach(var p in preview.Poses)
                {
                    if(p.Id == nodeId)
                    {
                        p.HasPreviewOverride = testInput;
                        p.PreviewInput = previewInput;
                    }
                }
            }

            var inputs = MixerAuthoring.Inputs(node);
            var valueNow = CurrentInput();
            var weights = new float[inputs.Count];
            if(inputs.Count > 0)
            {
                if(two)
                {
                    UAnimBlendWeights.Two(valueNow, inputs.Select(p => p.position).ToArray(), weights);
                }
                else
                {
                    UAnimBlendWeights.One(valueNow.x, inputs.Select(p => p.threshold).ToArray(), weights);
                }
            }

            DrawPlot(two, inputs, valueNow);
            EditorGUILayout.LabelField("Input", two ? valueNow.ToString("F2") : valueNow.x.ToString("F3"));
            if(!two)
            {
                sortView = EditorGUILayout.Toggle("View by threshold", sortView);
            }

            IEnumerable<MixerPoseInput> view = inputs;
            if(sortView && !two)
            {
                view = inputs.OrderBy(p => p.threshold);
            }

            foreach(var input in view.ToArray())
            {
                int index = inputs.IndexOf(input);
                var source = node.GetInputPortByName(input.port)?.FirstConnectedPort?.GetNode();
                using(new EditorGUILayout.HorizontalScope())
                {
                    GUI.backgroundColor = selected == input.port ? new Color(.6f, .85f, 1) : Color.white;
                    if(GUILayout.Button(source?.Title ?? "Unconnected", GUILayout.MinWidth(100)))
                    {
                        selected = input.port;
                        if(source != null)
                        {
                            UAnimEditorWorkspace.Highlight(node.Graph, source);
                        }
                    }

                    GUI.backgroundColor = Color.white;
                    if(two)
                    {
                        var v = EditorGUILayout.Vector2Field("", input.position);
                        if(v != input.position && Finite(v))
                        {
                            Edit("Set sample coordinate", () => input.position = v);
                        }
                    }
                    else
                    {
                        float v = EditorGUILayout.DelayedFloatField(input.threshold);
                        if(v != input.threshold && UAnimValidation.Finite(v))
                        {
                            Edit("Set threshold", () => input.threshold = v);
                        }
                    }

                    GUILayout.Label(weights[index].ToString("P1"), GUILayout.Width(55));
                    using(new EditorGUI.DisabledScope(!CanRemoveInput(node, input.port)))
                    {
                        if(GUILayout.Button("−", GUILayout.Width(25)))
                        {
                            if(source == null || EditorUtility.DisplayDialog("Remove input", "Remove this Pose input and its wire?", "Remove", "Cancel"))
                            {
                                Edit("Remove Mixer input", () => RemoveInput(node, input.port));
                            }

                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }

            if(GUILayout.Button("+ Pose input"))
            {
                Edit("Add Mixer input", () => AddInput(node));
            }

            foreach(var issue in issues)
            {
                EditorGUILayout.HelpBox(issue.message, UAnimAuthoringValidation.IsIncomplete(node.Graph, issue.message) ? MessageType.Warning : MessageType.Error);
            }

            EditorGUILayout.HelpBox("Blue points edit stored sample locations. Test input changes only the preview. Sorting never rewires ports. Drag with Shift for 0.1 snapping.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        static bool Finite(Vector2 v)
        {
            return UAnimValidation.Finite(v.x) && UAnimValidation.Finite(v.y);
        }

        Vector2 Point(Rect r, Vector2 value, bool two)
        {
            if(two)
            {
                return r.center + new Vector2((value.x - pan.x) / viewRange * r.width * .4f, -(value.y - pan.y) / viewRange * r.height * .4f);
            }

            return new Vector2(r.center.x + (value.x - pan.x) / viewRange * r.width * .4f, r.yMax - 25);
        }

        Vector2 Value(Rect r, Vector2 point, bool two)
        {
            return new Vector2((point.x - r.center.x) / (r.width * .4f) * viewRange + pan.x, two ? -(point.y - r.center.y) / (r.height * .4f) * viewRange + pan.y : 0);
        }

        void DrawPlot(bool two, List<MixerPoseInput> inputs, Vector2 current)
        {
            Rect r = GUILayoutUtility.GetRect(250, two ? 280 : 180, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(.12f, .15f, .20f));
            if(Event.current.type == EventType.Repaint)
            {
                Handles.BeginGUI();
                Handles.color = new Color(.35f, .4f, .48f);
                var zero = Point(r, Vector2.zero, two);
                Handles.DrawLine(new Vector3(r.x, zero.y), new Vector3(r.xMax, zero.y));
                if(two)
                {
                    Handles.DrawLine(new Vector3(zero.x, r.y), new Vector3(zero.x, r.yMax));
                }

                if(!two && inputs.Count > 1)
                {
                    var sorted = inputs.OrderBy(p => p.threshold).ToArray();
                    Handles.color = new Color(.5f, .65f, .8f);
                    for(int i = 0; i < sorted.Length; i++)
                    {
                        var center = Point(r, new Vector2(sorted[i].threshold, 0), false);
                        center.y = r.y + 25;
                        if(i > 0)
                        {
                            Handles.DrawLine(Point(r, new Vector2(sorted[i - 1].threshold, 0), false), center);
                        }

                        if(i + 1 < sorted.Length)
                        {
                            Handles.DrawLine(center, Point(r, new Vector2(sorted[i + 1].threshold, 0), false));
                        }
                    }
                }

                foreach(var input in inputs)
                {
                    var point = Point(r, two ? input.position : new Vector2(input.threshold, 0), two);
                    if(!r.Contains(point))
                    {
                        continue;
                    }

                    Handles.color = selected == input.port ? Color.yellow : new Color(.4f, .65f, 1);
                    Handles.DrawSolidDisc(point, Vector3.forward, 6);
                    GUI.Label(new Rect(point.x + 6, point.y - 20, 95, 20), two ? input.position.ToString("F1") : input.threshold.ToString("F2"), EditorStyles.whiteMiniLabel);
                }

                Handles.color = Color.green;
                var cursor = Point(r, current, two);
                if(r.Contains(cursor))
                {
                    if(two)
                    {
                        Handles.DrawWireDisc(cursor, Vector3.forward, 10);
                    }
                    else
                    {
                        Handles.DrawLine(new Vector3(cursor.x, r.y), new Vector3(cursor.x, r.yMax));
                    }
                }

                Handles.EndGUI();
            }

            var evt = Event.current;
            if(r.Contains(evt.mousePosition))
            {
                if(evt.type == EventType.ScrollWheel)
                {
                    viewRange = Mathf.Clamp(viewRange * Mathf.Pow(1.1f, evt.delta.y), .01f, 100000);
                    evt.Use();
                }

                if(evt.type == EventType.MouseDrag && evt.button == 2)
                {
                    pan -= new Vector2(evt.delta.x / (r.width * .4f), -evt.delta.y / (r.height * .4f)) * viewRange;
                    evt.Use();
                }

                if(evt.type == EventType.MouseDown && evt.button == 0)
                {
                    if(testInput)
                    {
                        previewInput = Value(r, evt.mousePosition, two);
                        evt.Use();
                    }
                    else
                    {
                        var nearest = inputs.OrderBy(p => (Point(r, two ? p.position : new Vector2(p.threshold, 0), two) - evt.mousePosition).sqrMagnitude).FirstOrDefault();
                        if(nearest != null && Vector2.Distance(Point(r, two ? nearest.position : new Vector2(nearest.threshold, 0), two), evt.mousePosition) < 15)
                        {
                            selected = nearest.port;
                            node.Graph.UndoBeginRecordGraph("Drag Mixer sample", node);
                            MixerAuthoring.Materialize(node);
                            dragging = true;
                            dragUndo = Undo.GetCurrentGroup();
                            evt.Use();
                        }
                    }
                }
            }

            if(evt.type == EventType.MouseDrag && evt.button == 0 && (dragging || testInput) && r.Contains(evt.mousePosition))
            {
                var v = Value(r, evt.mousePosition, two);
                if(evt.shift)
                {
                    v = new Vector2(Mathf.Round(v.x * 10) / 10, Mathf.Round(v.y * 10) / 10);
                }

                if(testInput)
                {
                    previewInput = v;
                }
                else
                {
                    var input = ((IMixerAuthoring)node).Settings.inputs.First(e => e.port == selected);
                    if(two)
                    {
                        input.position = v;
                    }
                    else
                    {
                        input.threshold = v.x;
                    }
                }

                evt.Use();
                Repaint();
            }

            if(evt.type == EventType.MouseUp && dragging)
            {
                EndDrag();
                evt.Use();
            }

            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Fit samples"))
                {
                    pan = Vector2.zero;
                    viewRange = Mathf.Max(.1f, inputs.Count == 0 ? 1 : inputs.Max(e => two ? Mathf.Max(Mathf.Abs(e.position.x), Mathf.Abs(e.position.y)) : Mathf.Abs(e.threshold))) * 1.2f;
                }

                if(GUILayout.Button("Origin"))
                {
                    pan = Vector2.zero;
                    viewRange = 1.25f;
                }
            }
        }

        void EndDrag()
        {
            if(!dragging || node == null)
            {
                return;
            }

            dragging = false;
            node.DefineNode();
            node.Graph.UndoEndRecordGraph();
            Undo.CollapseUndoOperations(dragUndo);
            GraphDatabase.SaveGraph(node.Graph);
        }
    }
}
