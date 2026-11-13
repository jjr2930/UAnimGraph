using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.UIElements;

namespace UAnimGraph.Authoring
{
    // Compose the native Toolkit surface with navigation and preview using only
    // public UI Toolkit containers. Toolkit still owns its graph and Inspector.
    public static class UAnimUnifiedLayout
    {
        sealed class Layout
        {
            public EditorWindow window;
            public VisualElement shell, center;
            public UAnimWorkbench preview;
            public UAnimAsset root;
            public Vector2 scroll;
            public bool wasMaximized;
            public readonly Dictionary<Overlay, bool> overlays = new Dictionary<Overlay, bool>();
        }

        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly Dictionary<EditorWindow, Layout> layouts = new Dictionary<EditorWindow, Layout>();
        static UAnimUnifiedLayout()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.update += Cleanup;
        }

        static void Cleanup()
        {
            foreach(var item in layouts.Keys.Where(w => !w).ToArray())
            {
                var l = layouts[item];
                if(l.preview)
                {
                    UnityEngine.Object.DestroyImmediate(l.preview);
                }

                layouts.Remove(item);
            }
        }

        static void Clear()
        {
            foreach(var window in layouts.Keys.ToArray())
            {
                Restore(window);
            }
        }

        public static void Toggle(EditorWindow window)
        {
            if(!window)
            {
                return;
            }

            if(layouts.ContainsKey(window))
            {
                Restore(window);
                return;
            }

            var graphWindow = window as IGraphWindow;
            UAnimAsset root = UAnimEditorWorkspace.CurrentRoot;
            if(graphWindow?.Graph is UAnimRootGraph rootGraph)
            {
                root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(GraphDatabase.GetGraphAssetPath(rootGraph));
            }

            if(!root)
            {
                UAnimEditorWorkspace.ShowWorkspace();
                return;
            }

            bool wasMaximized = window.maximized;
            window.maximized = true;
            var children = window.rootVisualElement.Children().ToArray();
            var l = new Layout
            {
                wasMaximized = wasMaximized,
                window = window,
                root = root,
                preview = ScriptableObject.CreateInstance<UAnimWorkbench>()
            };
            l.preview.BindRoot(root);
            l.shell = new VisualElement
            {
                name = "uanim-unified-layout"
            };
            l.shell.style.flexGrow = 1;
            l.shell.style.flexDirection = FlexDirection.Row;
            l.center = new VisualElement();
            l.center.style.flexGrow = 1;
            l.center.style.flexShrink = 1;
            l.center.style.minWidth = 200;
            foreach(var child in children)
            {
                child.RemoveFromHierarchy();
                l.center.Add(child);
            }

            var navigation = new IMGUIContainer(() => DrawNavigation(l));
            navigation.style.width = 176;
            navigation.style.flexShrink = 0;
            var right = new VisualElement();
            right.style.width = 300;
            right.style.flexShrink = 0;
            if(window.overlayCanvas != null)
            {
                foreach(var overlay in window.overlayCanvas.overlays.ToArray())
                {
                    if(overlay is not UAnimSharedBlackboardOverlay && (overlay.displayName.Contains("Inspector") || overlay.displayName.Contains("Blackboard") || overlay.displayName.Contains("MiniMap")))
                    {
                        l.overlays[overlay] = overlay.displayed;
                        overlay.displayed = false;
                    }

                    if(overlay.displayName.Contains("Inspector"))
                    {
                        right.Add(new Label("Selection Inspector"));
                        var content = overlay.CreatePanelContent();
                        content.style.display = DisplayStyle.Flex;
                        content.style.position = Position.Relative;
                        content.style.minHeight = 140;
                        content.style.flexGrow = 1;
                        content.style.maxHeight = 350;
                        right.Add(content);
                    }
                }
            }

            var preview = new IMGUIContainer(() => l.preview.DrawEmbedded());
            preview.style.flexGrow = 1;
            right.Add(preview);
            l.shell.Add(navigation);
            l.shell.Add(l.center);
            l.shell.Add(right);
            window.rootVisualElement.Add(l.shell);
            layouts.Add(window, l);
            window.Repaint();
        }

        public static void Restore(EditorWindow window)
        {
            if(!layouts.TryGetValue(window, out var l))
            {
                return;
            }

            layouts.Remove(window);
            if(window)
            {
                var children = l.center.Children().ToArray();
                l.shell.RemoveFromHierarchy();
                foreach(var child in children)
                {
                    child.RemoveFromHierarchy();
                    window.rootVisualElement.Add(child);
                }
            }

            if(window)
            {
                foreach(var pair in l.overlays)
                {
                    pair.Key.displayed = pair.Value;
                }

                window.maximized = l.wasMaximized;
            }

            if(l.preview)
            {
                UnityEngine.Object.DestroyImmediate(l.preview);
            }
        }

        static void DrawNavigation(Layout l)
        {
            if(!l.root)
            {
                return;
            }

            EditorGUILayout.LabelField("UAnimGraph", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(l.root.name, EditorStyles.wordWrappedMiniLabel);
            if(GUILayout.Button("Root Graph"))
            {
                AssetDatabase.OpenAsset(l.root);
            }

            if(GUILayout.Button("Parameters / Root Settings"))
            {
                Selection.activeObject = l.root;
            }

            l.scroll = EditorGUILayout.BeginScrollView(l.scroll);
            foreach(var layer in l.root.layers)
            {
                EditorGUILayout.LabelField(layer.name, EditorStyles.boldLabel);
                if(!layer.graph)
                {
                    continue;
                }

                foreach(var n in layer.graph.nodes.Where(n => n.machine))
                {
                    if(GUILayout.Button("Open Layer"))
                    {
                        AssetDatabase.OpenAsset(n.machine);
                    }

                    foreach(var state in n.machine.states)
                    {
                        if(GUILayout.Button("    " + state.name))
                        {
                            AssetDatabase.OpenAsset(state.graph);
                        }
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
            if(GUILayout.Button("Mixer Inspector"))
            {
                UAnimMixerInspector.ShowWindow();
            }

            if(GUILayout.Button("Function Libraries"))
            {
                UAnimEvaluationPicker.Open(_ =>
                {
                });
            }

            if(GUILayout.Button("Validate"))
            {
                UAnimBuildValidation.Compile(l.root);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
