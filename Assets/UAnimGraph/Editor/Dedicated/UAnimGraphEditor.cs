using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.UIElements;
using ObjectField = UnityEditor.UIElements.ObjectField;

namespace UAnimGraph.Authoring
{
    public sealed partial class UAnimGraphEditor : EditorWindow
    {
        [SerializeField]
        string sourcePath;
        [SerializeField]
        GameObject previewModel;
        [SerializeField]
        bool previewWholeGraph;
        [SerializeField]
        List<string> history = new List<string>();
        UAnimEditorDocument document;
        UAnimEditorCanvas canvas;
        VisualElement navigation;
        IMGUIContainer inspector, previewPanel, blackboard;
        Label breadcrumb, message;
        UAnimWorkbench preview;
        UAnimGraphPreviewSource previewSource;
        string status;
        bool refreshQueued;
        Vector2 blackboardScroll;
        public UAnimEditorDocument Document
        {
            get
            {
                return document;
            }
        }

        public UAnimInstance LiveInstance
        {
            get
            {
                return preview ? preview.CurrentInstance : null;
            }
        }

        [MenuItem("Window/UAnimGraph/Graph Editor", priority = 0)]
        public static void ShowEditor()
        {
            var window = GetWindow<UAnimGraphEditor>("UAnimGraph");
            if(Selection.activeObject)
            {
                string path = UAnimEditorDocument.ResolvePath(Selection.activeObject);
                if(Supported(path))
                {
                    window.Navigate(path);
                }
            }

            window.Show();
        }

        [MenuItem("Assets/UAnimGraph/Open Graph Editor")]
        static void OpenSelectedGraph()
        {
            ShowEditor();
        }

        [MenuItem("Assets/UAnimGraph/Open Graph Editor", true)]
        static bool CanOpenSelectedGraph()
        {
            return Supported(AssetDatabase.GetAssetPath(Selection.activeObject));
        }
        public static UAnimGraphEditor Open(UnityEngine.Object asset)
        {
            return OpenPath(UAnimEditorDocument.ResolvePath(asset));
        }

        public static UAnimGraphEditor OpenPath(string path)
        {
            var window = GetWindow<UAnimGraphEditor>("UAnimGraph");
            window.Navigate(path);
            window.Show();
            return window;
        }

        [OnOpenAsset(-1000)]
        static bool OnOpenAsset(EntityId id, int line)
        {
            // Existing batch regressions explicitly exercise the compatibility
            // Toolkit windows; interactive asset opening uses this editor.
            if(Application.isBatchMode)
            {
                return false;
            }

            var asset = EditorUtility.EntityIdToObject(id);
            string path = UAnimEditorDocument.ResolvePath(asset);
            if(!Supported(path))
            {
                return false;
            }

            OpenPath(path);
            return true;
        }

        static bool Supported(string path)
        {
            return !string.IsNullOrEmpty(path) && new[]
            {
                ".uanimasset",
                ".uamachine",
                ".uastate",
                ".uapose"
            }.Any(extension => path.EndsWith(extension, StringComparison.Ordinal));
        }

        void OnEnable()
        {
            minSize = new Vector2(1050, 640);
            Undo.undoRedoPerformed += UndoChanged;
            EditorApplication.playModeStateChanged += PlayModeChanged;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= UndoChanged;
            EditorApplication.playModeStateChanged -= PlayModeChanged;
            if(document != null)
            {
                document.Changed -= QueueRefresh;
            }

            DisposePreview();
        }

        void PlayModeChanged(PlayModeStateChange change)
        {
            if(preview)
            {
                preview.BindRoot(null);
            }

            previewSource?.Dispose();
            previewSource = null;
            QueueRefresh();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 30;
            toolbar.Add(new Button(NewGraph) { text = "New" });
            toolbar.Add(new Button(Back) { text = "← Back" });
            toolbar.Add(new Button(() =>
            {
                if(document != null && !string.IsNullOrEmpty(document.RootPath))
                {
                    Navigate(document.RootPath);
                }
            }) { text = "Root" });
            toolbar.Add(new Button(() => Run(() => document?.Save())) { text = "Save" });
            toolbar.Add(new Button(() => canvas?.FrameAll()) { text = "Frame All (F)" });
            toolbar.Add(new Button(() => ShowCreateMenu(canvas.ToGraph(new Vector2(150, 150)))) { text = "+ Add" });
            var assetField = new ObjectField
            {
                objectType = typeof(UAnimAsset),
                allowSceneObjects = false
            };
            assetField.style.width = 220;
            assetField.RegisterValueChangedCallback(e =>
            {
                if(e.newValue)
                {
                    Navigate(UAnimEditorDocument.ResolvePath(e.newValue));
                }
            });
            toolbar.Add(assetField);
            toolbar.Add(new Button(() => Selection.activeObject = document == null ? null : AssetDatabase.LoadAssetAtPath<UAnimAsset>(document.RootPath ?? document.Path)) { text = "Select Asset" });
            toolbar.Add(new Button(Validate) { text = "Validate" });
            rootVisualElement.Add(toolbar);
            breadcrumb = new Label("Open a UAnimAsset or create a new graph.")
            {
                name = "uanim-editor-path"
            };
            breadcrumb.style.height = 25;
            breadcrumb.style.paddingLeft = 10;
            rootVisualElement.Add(breadcrumb);
            var body = new TwoPaneSplitView(0, 220, TwoPaneSplitViewOrientation.Horizontal);
            body.style.flexGrow = 1;
            var left = new VisualElement();
            left.style.minWidth = 180;
            var tree = new ScrollView();
            tree.style.flexGrow = 1;
            navigation = new VisualElement
            {
                name = "uanim-editor-navigation"
            };
            tree.Add(navigation);
            left.Add(tree);
            var parameterFoldout = new Foldout
            {
                text = "Shared Blackboard",
                value = true
            };
            blackboard = new IMGUIContainer(DrawBlackboard);
            blackboard.style.maxHeight = 340;
            parameterFoldout.Add(blackboard);
            left.Add(parameterFoldout);
            body.Add(left);
            var main = new TwoPaneSplitView(1, 360, TwoPaneSplitViewOrientation.Horizontal);
            canvas = new UAnimEditorCanvas(this);
            main.Add(canvas);
            var side = new ScrollView
            {
                name = "uanim-editor-side"
            };
            side.style.minWidth = 300;
            side.Add(new Label("Inspector") { style = { unityFontStyleAndWeight = FontStyle.Bold, height = 25 } });
            inspector = new IMGUIContainer(DrawInspector);
            side.Add(inspector);
            var previewFoldout = new Foldout
            {
                text = "Animation Preview / Live Debug",
                value = true
            };
            previewPanel = new IMGUIContainer(DrawPreview);
            previewFoldout.Add(previewPanel);
            side.Add(previewFoldout);
            main.Add(side);
            body.Add(main);
            rootVisualElement.Add(body);
            message = new Label("Middle mouse / Alt+drag: pan   •   Wheel: zoom   •   Right click: add   •   Double click: open   •   Delete: remove");
            message.style.whiteSpace = WhiteSpace.Normal;
            message.style.minHeight = 25;
            rootVisualElement.Add(message);
            if(Supported(sourcePath) && File.Exists(sourcePath))
            {
                Navigate(sourcePath, false);
            }
        }

        public void Navigate(string path, bool addHistory = true)
        {
            if(!Supported(path))
            {
                status = "Choose a UAnimGraph source asset.";
                RefreshInspector();
                return;
            }

            Run(() =>
            {
                if(document != null)
                {
                    document.Changed -= QueueRefresh;
                }

                string root = UAnimSharedBlackboard.ResolveRoot(path);
                if(!string.IsNullOrEmpty(root))
                {
                    UAnimEmbeddedAssets.Restore(root);
                }

                document = new UAnimEditorDocument(path);
                UAnimEditorWorkspace.CurrentRoot = string.IsNullOrEmpty(document.RootPath) ? null : AssetDatabase.LoadAssetAtPath<UAnimAsset>(document.RootPath);
                document.Changed += QueueRefresh;
                sourcePath = path;
                canvas?.BeginNavigation();
                if(addHistory && (history.Count == 0 || history[history.Count - 1] != path))
                {
                    history.Add(path);
                }

                status = null;
                canvas?.Select(null);
                Refresh();
                canvas?.schedule.Execute(() => canvas.FrameOnOpen());
            });
        }

        void Back()
        {
            if(history.Count < 2)
            {
                return;
            }

            history.RemoveAt(history.Count - 1);
            Navigate(history[history.Count - 1], false);
        }

        public void OpenChild(string id)
        {
            if(document == null)
            {
                return;
            }

            object item = document.IsMachine ? (object)document.States.FirstOrDefault(s => s.ID.ToString() == id) : document.Nodes.FirstOrDefault(n => n.ID.ToString() == id);
            Run(() =>
            {
                string path = document.ChildPath(item);
                if(!string.IsNullOrEmpty(path))
                {
                    Navigate(path);
                }
            });
        }

        public void Run(Action action)
        {
            try
            {
                action();
                status = null;
                QueueRefresh();
            }
            catch(Exception exception)
            {
                status = exception is System.Reflection.TargetInvocationException && exception.InnerException != null ? exception.InnerException.Message : exception.Message;
            }

            RefreshInspector();
        }

        void UndoChanged()
        {
            Run(() =>
            {
                document?.Reload();
                document?.Save();
                QueueRefresh();
            });
        }

        void QueueRefresh()
        {
            if(refreshQueued)
            {
                return;
            }

            refreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                refreshQueued = false;
                if(this)
                {
                    Refresh();
                }
            };
        }

        void Refresh()
        {
            if(canvas == null || document == null)
            {
                return;
            }

            breadcrumb.text = UAnimEmbeddedAssets.DisplayPath(document.Path);
            titleContent = new GUIContent(System.IO.Path.GetFileName(breadcrumb.text.Replace('\\', '/')), breadcrumb.text);
            canvas.Rebuild();
            RefreshNavigation();
            BindPreview();
            RefreshInspector();
        }

        public void RefreshInspector()
        {
            inspector?.MarkDirtyRepaint();
            blackboard?.MarkDirtyRepaint();
            if(message != null)
            {
                message.text = string.IsNullOrEmpty(status) ? "Middle mouse / Alt+drag: pan   •   Wheel: zoom   •   Right click: add   •   Double click: open   •   Delete: remove" : status;
            }

            Repaint();
        }

        void RefreshNavigation()
        {
            navigation.Clear();
            if(document == null || string.IsNullOrEmpty(document.RootPath))
            {
                return;
            }

            string rootPath = document.RootPath;
            var root = GraphDatabase.LoadGraph<UAnimRootGraph>(rootPath);
            navigation.Add(new Button(() => Navigate(rootPath)) { text = System.IO.Path.GetFileNameWithoutExtension(rootPath) });
            foreach(var layer in root.GetNodes().OfType<RootLayerNode>())
            {
                string machinePath = new UAnimEditorDocument(rootPath).ChildPath(layer);
                if(string.IsNullOrEmpty(machinePath) || !machinePath.EndsWith(".uamachine", StringComparison.Ordinal))
                {
                    continue;
                }

                var foldout = new Foldout
                {
                    text = layer.Option<string>("Name"),
                    value = true
                };
                foldout.Add(new Button(() => Navigate(machinePath)) { text = "StateMachine" });
                var machine = new UAnimEditorDocument(machinePath);
                foreach(var state in machine.States.Where(s => s is not UAnimEntryState))
                {
                    var captured = state;
                    foldout.Add(new Button(() => Run(() => Navigate(machine.ChildPath(captured)))) { text = state.Title });
                }

                navigation.Add(foldout);
            }
        }

        void BindPreview()
        {
            if(preview)
            {
                previewModel = preview.PreviewModel;
                preview.BindRoot(null);
            }
            else
            {
                preview = CreateInstance<UAnimWorkbench>();
                preview.hideFlags = HideFlags.HideAndDontSave;
                preview.visualizeToolkit = false;
                preview.repaintRequested = () => previewPanel?.MarkDirtyRepaint();
                preview.PreviewModel = previewModel;
            }

            previewSource?.Dispose();
            previewSource = new UAnimGraphPreviewSource(document.Path, previewWholeGraph);
            if(previewSource.Asset && !EditorUtility.IsPersistent(previewSource.Asset))
            {
                previewSource.Asset.name = System.IO.Path.GetFileName(UAnimEmbeddedAssets.DisplayPath(document.Path));
            }

            preview.BindRoot(previewSource.Asset);
        }

        void DisposePreview()
        {
            if(preview)
            {
                previewModel = preview.PreviewModel;
                DestroyImmediate(preview);
            }

            preview = null;
            previewSource?.Dispose();
            previewSource = null;
        }

        void DrawPreview()
        {
            if(!preview)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            previewWholeGraph = EditorGUILayout.Toggle("Whole UAnimGraph", previewWholeGraph);
            if(EditorGUI.EndChangeCheck())
            {
                BindPreview();
            }

            preview.DrawEmbedded(true);
            if(preview)
            {
                previewModel = preview.PreviewModel;
            }
        }

        void NewGraph()
        {
            string path = EditorUtility.SaveFilePanelInProject("Create UAnimGraph", "UAnimGraph", "uanimasset", "Choose the root graph location.");
            if(string.IsNullOrEmpty(path))
            {
                return;
            }

            Run(() =>
            {
                var root = GraphDatabase.CreateGraph<UAnimRootGraph>(path);
                root.layerChain = true;
                var layer = new RootLayerNode();
                var output = new LayerOutputNode();
                root.AddNode(layer);
                root.AddNode(output);
                layer.GetNodeOptionByName("Name").TrySetValue("Base Layer");
                layer.Title = "Base Layer";
                output.Position = new Vector2(420, 0);
                root.Connect(layer.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
                GraphDatabase.SaveGraph(root);
                UAnimDefaultLayerMachine.Ensure(layer);
                Navigate(path);
            });
        }

        public void ShowCreateMenu(Vector2 position)
        {
            if(document == null)
            {
                return;
            }

            var menu = new GenericMenu();
            if(document.IsMachine)
            {
                menu.AddItem(new GUIContent("Pose State"), false, () => Run(() => document.AddState(position)));
                if(!document.States.Any(s => s is UAnimEntryState))
                {
                    menu.AddItem(new GUIContent("Entry"), false, () => Run(() => document.Edit("Create Entry", () =>
                    {
                        var entry = new UAnimEntryState();
                        document.Machine.AddState(entry);
                        entry.Position = position;
                        entry.Title = "Entry";
                    })));
                }
            }
            else
            {
                var types = document.Graph is UAnimRootGraph rootGraph && rootGraph.layerChain ? new[]
                {
                    typeof(RootLayerNode)
                }

                : new[]
                {
                    typeof(ClipNode),
                    typeof(Blend1DNode),
                    typeof(Blend2DNode),
                    typeof(MixerNode),
                    typeof(ParameterNode),
                    typeof(ConstantNode),
                    typeof(StateMachineNode)
                };
                if(document.Graph is UAnimStateGraph)
                {
                    types = types.Where(type => type != typeof(StateMachineNode)).ToArray();
                }
                foreach(var type in types)
                {
                    var captured = type;
                    menu.AddItem(new GUIContent(UAnimEditorDocument.Friendly(type)), false, () => Run(() => document.AddNode(captured, position)));
                }

                Type output = document.Graph is UAnimStateGraph ? typeof(StateOutputNode) : typeof(LayerOutputNode);
                if(!document.Nodes.Any(n => n.GetType() == output))
                {
                    menu.AddItem(new GUIContent("Output Pose"), false, () => Run(() => document.AddNode(output, position)));
                }
            }

            menu.ShowAsContext();
        }

        void Validate()
        {
            Run(() =>
            {
                document.Save();
                UAnimEmbeddedAssets.Flush();
                var root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(document.RootPath ?? document.Path);
                var report = UAnimValidation.Validate(root);
                status = report.Errors.Count == 0 ? "Validation passed." : string.Join(" • ", report.Errors);
            });
            // Run clears status only after successful actions; retain the report.
            if(document != null)
            {
                var report = UAnimValidation.Validate(AssetDatabase.LoadAssetAtPath<UAnimAsset>(document.RootPath ?? document.Path));
                status = report.Errors.Count == 0 ? "Validation passed." : string.Join(" • ", report.Errors);
                RefreshInspector();
            }
        }
    }
}
