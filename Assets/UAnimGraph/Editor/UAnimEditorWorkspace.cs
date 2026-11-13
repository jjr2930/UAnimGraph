using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using Unity.GraphToolkit.Editor.GraphVisualization;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace UAnimGraph.Authoring
{
    // Companion navigation/parameter panel for the native Graph Toolkit windows.
    // Uses public APIs only, so Unity continues to own graph editing and Undo.
    public sealed class UAnimEditorWorkspace : EditorWindow
    {
        [SerializeField]
        UAnimAsset root;
        [SerializeField]
        UnityEngine.Object current;
        readonly List<UnityEngine.Object> history = new List<UnityEngine.Object>();
        Vector2 scroll;
        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static Context highlight;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static double clearAt;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        public static UAnimAsset CurrentRoot
        {
            get;
            private set;
        }

        [MenuItem("Window/UAnimGraph/Authoring Workspace")]
        public static void ShowWorkspace()
        {
            GetWindow<UAnimEditorWorkspace>("UAnimGraph Workspace").Show();
        }

        public static void Open(UAnimAsset root)
        {
            var w = GetWindow<UAnimEditorWorkspace>("UAnimGraph Workspace");
            w.root = root;
            CurrentRoot = root;
            w.Navigate(root);
            w.Show();
        }

        void OnEnable()
        {
            EditorApplication.update += UpdateHighlight;
            Undo.undoRedoPerformed += Repaint;
        }

        void OnDisable()
        {
            EditorApplication.update -= UpdateHighlight;
            Undo.undoRedoPerformed -= Repaint;
            ClearHighlight();
        }

        void UpdateHighlight()
        {
            if(highlight != null && EditorApplication.timeSinceStartup >= clearAt)
            {
                ClearHighlight();
            }
        }

        static void ClearHighlight()
        {
            if(highlight == null)
            {
                return;
            }

            try
            {
                highlight.Dispose();
            }
            catch(ArgumentException)
            {
            }

            highlight = null;
        }

        public static Graph LoadGraph(string path)
        {
            if(path.EndsWith(".uastate"))
            {
                return GraphDatabase.LoadGraph<UAnimStateGraph>(path);
            }

            if(path.EndsWith(".uanimasset"))
            {
                return GraphDatabase.LoadGraph<UAnimRootGraph>(path);
            }

            if(path.EndsWith(".uapose"))
            {
                return GraphDatabase.LoadGraph<UAnimLayerGraph>(path);
            }

            return null;
        }

        public static void Highlight(Graph graph, INode node)
        {
            AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(GraphDatabase.GetGraphAssetPath(graph)));
            if(UAnimWorkbench.ActiveInstance != null)
            {
                return;
            }

            ClearHighlight();
            highlight = Registry.CreateVisualizationContext(graph.ID);
            var view = highlight.GetNodeReference(node.ID);
            view.FillAmount = 100;
            highlight.Motion.Play(view);
            clearAt = EditorApplication.timeSinceStartup + 1.5;
        }

        public void Navigate(UnityEngine.Object asset)
        {
            if(!asset)
            {
                return;
            }

            current = asset;
            if(history.Count == 0 || history[history.Count - 1] != asset)
            {
                history.Add(asset);
            }

            AssetDatabase.OpenAsset(asset);
            Selection.activeObject = asset;
            Repaint();
        }

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            root = (UAnimAsset)EditorGUILayout.ObjectField("Root", root, typeof(UAnimAsset), false);
            if(EditorGUI.EndChangeCheck())
            {
                history.Clear();
                CurrentRoot = root;
                if(root)
                {
                    Navigate(root);
                }
            }

            CurrentRoot = root;
            if(!root)
            {
                EditorGUILayout.HelpBox("Select a UAnimAsset. Open its root graph to arrange Layers.", MessageType.Info);
                return;
            }

            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Root"))
                {
                    Navigate(root);
                }

                if(GUILayout.Button("Back") && history.Count > 1)
                {
                    history.RemoveAt(history.Count - 1);
                    var previous = history[history.Count - 1];
                    current = previous;
                    AssetDatabase.OpenAsset(previous);
                }

                if(GUILayout.Button("Preview / Debug"))
                {
                    UAnimWorkbench.Open(root);
                }

                if(GUILayout.Button("Mixer Inspector"))
                {
                    UAnimMixerInspector.ShowWindow();
                }
            }

            EditorGUILayout.LabelField(string.Join("  ›  ", history.Where(o => o).Select(o => o.name)), EditorStyles.wordWrappedMiniLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach(var layer in root.layers)
            {
                if(layer == null || !layer.graph)
                {
                    continue;
                }

                EditorGUILayout.LabelField(layer.name + "  /  " + layer.weight.ToString("P0"), EditorStyles.boldLabel);
                foreach(var item in layer.graph.nodes.Where(n => n.kind == UAnimPoseKind.StateMachine && n.machine))
                {
                    if(GUILayout.Button("Open Layer: " + layer.name))
                    {
                        Navigate(item.machine);
                    }

                    foreach(var state in item.machine.states)
                    {
                        using(new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Space(20);
                            if(GUILayout.Button(state.name))
                            {
                                Navigate(state.graph);
                            }
                        }
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Parameters", EditorStyles.boldLabel);
            var live = UAnimWorkbench.ActiveInstance;
            foreach(var p in root.parameters)
            {
                using(new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(p.name + " (" + p.type + ")", GUILayout.Width(150));
                    if(live != null && live.Asset == root)
                    {
                        if(p.type == UAnimParameterType.Vector2)
                        {
                            var v = EditorGUILayout.Vector2Field("", live.GetVector2(p.id));
                            live.SetVector2(p.id, v);
                        }
                        else if(p.type == UAnimParameterType.Trigger)
                        {
                            if(GUILayout.Button("Fire"))
                            {
                                live.Set(p.id, p.type, 1);
                            }
                        }
                        else
                        {
                            double old = live.Get(p.id);
                            double v = p.type == UAnimParameterType.Bool ? EditorGUILayout.Toggle(old != 0) ? 1 : 0 : p.type == UAnimParameterType.Int ? EditorGUILayout.IntField((int)old) : EditorGUILayout.FloatField((float)old);
                            if(v != old)
                            {
                                live.Set(p.id, p.type, v);
                            }
                        }
                    }
                    else
                    {
                        EditorGUILayout.LabelField(p.type == UAnimParameterType.Vector2 ? p.defaultVector2.ToString() : p.defaultValue.ToString());
                    }
                }
            }

            if(GUILayout.Button("Edit root parameters"))
            {
                Selection.activeObject = root;
            }

            var validation = UAnimValidation.Validate(root);
            foreach(var e in validation.Errors)
            {
                EditorGUILayout.HelpBox(e, MessageType.Error);
            }

            foreach(var e in validation.Warnings)
            {
                EditorGUILayout.HelpBox(e, MessageType.Warning);
            }

            if(live != null && live.DefinitionVersion != UAnimBuildValidation.Hash(root))
            {
                EditorGUILayout.HelpBox("Definition changed. Restart Preview or Player.", MessageType.Warning);
            }

            EditorGUILayout.EndScrollView();
        }
    }

    [GraphToolbarElement("UAnimGraph/Workspace", typeof(UAnimRootGraph))]
    public sealed class UAnimRootWorkspaceButton : EditorToolbarButton, IAccessContainerWindow
    {
        public EditorWindow containerWindow
        {
            get;
            set;
        }

        public UAnimRootWorkspaceButton()
        {
            text = "Integrated Editor";
            clicked += () => UAnimUnifiedLayout.Toggle(containerWindow);
        }
    }

    [GraphToolbarElement("UAnimGraph/StateWorkspace", typeof(UAnimStateGraph))]
    public sealed class UAnimStateWorkspaceButton : EditorToolbarButton, IAccessContainerWindow
    {
        public EditorWindow containerWindow
        {
            get;
            set;
        }

        public UAnimStateWorkspaceButton()
        {
            text = "Integrated Editor";
            clicked += () => UAnimUnifiedLayout.Toggle(containerWindow);
        }
    }

    [GraphToolbarElement("UAnimGraph/MachineWorkspace", typeof(UAnimStateMachineGraph))]
    public sealed class UAnimMachineWorkspaceButton : EditorToolbarButton, IAccessContainerWindow
    {
        public EditorWindow containerWindow
        {
            get;
            set;
        }

        public UAnimMachineWorkspaceButton()
        {
            text = "Integrated Editor";
            clicked += () => UAnimUnifiedLayout.Toggle(containerWindow);
        }
    }

    public sealed class PoseStateView : StateView<PoseState>
    {
        public override void OnViewBuilt()
        {
            base.OnViewBuilt();
            string title = null;
            State.GetOptionByName("Name")?.TryGetValue(out title);
            if(!string.IsNullOrEmpty(title) && State.Title != title)
            {
                State.Title = title;
            }

            // State views use a compact native header. Reserve space for the
            // custom body and use measured UI Toolkit controls throughout.
            // An IMGUI field can initially report zero height and overlap the
            // buttons when the native state restores its saved dimensions.
            View.Root.style.minWidth = 300;
            View.Root.style.minHeight = 128;
            var previous = View.Root.Q<VisualElement>("uanim-pose-state-controls");
            previous?.RemoveFromHierarchy();
            var body = new VisualElement
            {
                name = "uanim-pose-state-controls"
            };
            body.style.flexDirection = FlexDirection.Column;
            body.style.flexShrink = 0;
            body.style.minHeight = 62;
            body.style.paddingTop = 4;
            body.style.paddingBottom = 4;
            var nameField = new TextField("Name")
            {
                value = string.IsNullOrEmpty(title) ? State.Title : title,
                isDelayed = true
            };
            nameField.style.height = 24;
            nameField.style.flexShrink = 0;
            nameField.RegisterValueChangedCallback(e =>
            {
                State.StateMachine.UndoBeginRecordStateMachine("Rename State");
                State.GetOptionByName("Name").TrySetValue(e.newValue);
                State.Title = e.newValue;
                State.StateMachine.UndoEndRecordStateMachine();
                StateMachineDatabase.SaveStateMachine(State.StateMachine);
            });
            body.Add(nameField);
            Action open = () =>
            {
                UAnimDefaultStatePose.Ensure(State);
                if(!string.IsNullOrEmpty(State.embeddedGraphId))
                {
                    UAnimEmbeddedAssets.OpenState(State);
                    return;
                }

                UAnimPoseGraph graph = null;
                State.GetOptionByName("Graph")?.TryGetValue(out graph);
                if(graph)
                {
                    AssetDatabase.OpenAsset(graph);
                }
            };
            var openButton = new Button(open)
            {
                text = "Open State Pose Graph"
            };
            openButton.style.height = 28;
            openButton.style.flexShrink = 0;
            openButton.style.marginTop = 2;
            openButton.style.marginBottom = 2;
            body.Add(openButton);
            View.Root.Add(body);
            // Capture before GTK's selection/drag handlers consume body clicks.
            View.Root.RegisterCallback<MouseDownEvent>(e =>
            {
                if(e.button != 0 || e.clickCount != 2)
                {
                    return;
                }

                for(var target = e.target as VisualElement; target != null && target != View.Root; target = target.parent)
                {
                    if(target is TextElement || target is TextField || target is IMGUIContainer || target.ClassListContains("ge-port") || target.ClassListContains("ge-editable-title-part"))
                    {
                        return;
                    }
                }

                e.StopImmediatePropagation();
                open();
            }, TrickleDown.TrickleDown);
        }
    }
}
