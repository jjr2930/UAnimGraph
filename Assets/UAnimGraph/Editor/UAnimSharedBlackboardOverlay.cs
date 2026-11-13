using System;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace UAnimGraph.Authoring
{
    [Overlay(defaultDisplay = true)]
    public sealed class UAnimSharedBlackboardOverlay : Overlay
    {
        [NonSerialized]
        public bool initialized;
        string search = "";
        string error;
        Vector2 scroll;
        public override VisualElement CreatePanelContent()
        {
            var content = new IMGUIContainer(Draw);
            content.style.width = 300;
            content.style.minHeight = 90;
            content.style.maxHeight = 480;
            return content;
        }

        void Draw()
        {
            string sourcePath = UAnimSharedBlackboard.SourcePath(containerWindow);
            string rootPath = UAnimSharedBlackboard.ResolveRoot(sourcePath);
            if(string.IsNullOrEmpty(rootPath))
            {
                EditorGUILayout.HelpBox("This graph needs one owning UAnimGraph to use a shared blackboard.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(System.IO.Path.GetFileNameWithoutExtension(rootPath), EditorStyles.boldLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                if(GUILayout.Button("+", GUILayout.Width(28)))
                {
                    var menu = new GenericMenu();
                    foreach(UAnimParameterType type in Enum.GetValues(typeof(UAnimParameterType)))
                    {
                        var selectedType = type;
                        menu.AddItem(new GUIContent(type.ToString()), false, () => UAnimSharedBlackboard.Add(rootPath, selectedType));
                    }

                    menu.ShowAsContext();
                }
            }

            if(error != null)
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            var parameters = UAnimSharedBlackboard.Parameters(rootPath);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(370));
            foreach(var parameter in parameters)
            {
                if(parameter.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUI.BeginChangeCheck();
                string name = EditorGUILayout.DelayedTextField("Name", parameter.name);
                EditorGUILayout.LabelField("Type", parameter.type.ToString());
                double value = parameter.defaultValue;
                Vector2 vector = parameter.defaultVector2;
                switch(parameter.type)
                {
                    case UAnimParameterType.Float:
                        value = EditorGUILayout.DelayedDoubleField("Default", value);
                        break;
                    case UAnimParameterType.Int:
                        value = EditorGUILayout.DelayedIntField("Default", (int)value);
                        break;
                    case UAnimParameterType.Bool:
                        value = EditorGUILayout.Toggle("Default", value != 0) ? 1 : 0;
                        break;
                    case UAnimParameterType.Vector2:
                        vector = EditorGUILayout.Vector2Field("Default", vector);
                        break;
                    case UAnimParameterType.Trigger:
                        EditorGUILayout.LabelField("Default", "Not set");
                        break;
                }

                if(EditorGUI.EndChangeCheck())
                {
                    try
                    {
                        UAnimSharedBlackboard.Update(rootPath, parameter.id, name, value, vector);
                        error = null;
                    }
                    catch(ArgumentException exception)
                    {
                        error = exception.Message;
                    }
                }

                using(new EditorGUILayout.HorizontalScope())
                {
                    if(GUILayout.Button("Copy ID"))
                    {
                        EditorGUIUtility.systemCopyBuffer = parameter.id;
                    }

                    if(containerWindow is IGraphWindow window && window.Graph != null)
                    {
                        if(GUILayout.Button("Create Node"))
                        {
                            UAnimSharedBlackboard.CreateNode(window.Graph, rootPath, parameter.id);
                            GUIUtility.ExitGUI();
                        }
                    }

                    if(GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        UAnimSharedBlackboard.Remove(rootPath, parameter.id);
                        GUIUtility.ExitGUI();
                    }
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndScrollView();
            if(parameters.Length == 0)
            {
                EditorGUILayout.HelpBox("Add a parameter with +. All graphs in this UAnimGraph use this list.", MessageType.Info);
            }
        }
    }

    [InitializeOnLoad]
    public static class UAnimSharedBlackboardWindows
    {
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static double nextUpdate;
        static UAnimSharedBlackboardWindows()
        {
            EditorApplication.update += Update;
        }

        static void Update()
        {
            if(Application.isBatchMode || EditorApplication.timeSinceStartup < nextUpdate)
            {
                return;
            }

            nextUpdate = EditorApplication.timeSinceStartup + 0.5;
            foreach(var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                Ensure(window);
            }
        }

        public static UAnimSharedBlackboardOverlay Ensure(EditorWindow window)
        {
            if(!window || window is not IGraphWindow || window.overlayCanvas == null)
            {
                return null;
            }

            string rootPath = UAnimSharedBlackboard.ResolveRoot(UAnimSharedBlackboard.SourcePath(window));
            if(string.IsNullOrEmpty(rootPath))
            {
                return null;
            }

            var existing = window.overlayCanvas.overlays.OfType<UAnimSharedBlackboardOverlay>().FirstOrDefault();
            if(existing != null)
            {
                Initialize(window, existing);
                window.Repaint();
                return existing;
            }

            var overlay = new UAnimSharedBlackboardOverlay
            {
                displayName = "Shared Blackboard"
            };
            window.overlayCanvas.Add(overlay);
            Initialize(window, overlay);
            return overlay;
        }

        static void Initialize(EditorWindow window, UAnimSharedBlackboardOverlay overlay)
        {
            if(overlay.initialized)
            {
                return;
            }

            overlay.initialized = true;
            window.overlayCanvas.overlaysEnabled = true;
            overlay.displayed = false;
            overlay.displayed = true;
            overlay.Undock();
            overlay.floatingPosition = new Vector2(12, 40);
            overlay.collapsed = false;
            foreach(var local in window.overlayCanvas.overlays.ToArray())
            {
                if(local != overlay && local.displayName.Contains("Blackboard"))
                {
                    local.displayed = false;
                }
            }
        }

        public static void Show(EditorWindow window)
        {
            if(!window)
            {
                window = EditorWindow.focusedWindow;
            }

            var overlay = Ensure(window);
            if(overlay == null)
            {
                Debug.Log("UAnimGraph shared blackboard owner could not be resolved. Window: " + window?.GetType().FullName + "; source: " + UAnimSharedBlackboard.SourcePath(window));
            }

            if(overlay != null)
            {
                window.overlayCanvas.overlaysEnabled = true;
                overlay.displayed = false;
                overlay.displayed = true;
                overlay.Undock();
                overlay.collapsed = false;
            }
        }
    }

    public class UAnimSharedBlackboardButton : EditorToolbarButton, IAccessContainerWindow
    {
        public EditorWindow containerWindow
        {
            get;
            set;
        }

        public UAnimSharedBlackboardButton()
        {
            text = "Shared Blackboard";
            clicked += () => UAnimSharedBlackboardWindows.Show(containerWindow);
        }
    }

    [GraphToolbarElement("UAnimGraph/SharedRootBlackboard", typeof(UAnimRootGraph))]
    public sealed class UAnimSharedRootBlackboardButton : UAnimSharedBlackboardButton
    {
    }

    [GraphToolbarElement("UAnimGraph/SharedStateBlackboard", typeof(UAnimStateGraph))]
    public sealed class UAnimSharedStateBlackboardButton : UAnimSharedBlackboardButton
    {
    }

    [GraphToolbarElement("UAnimGraph/SharedMachineBlackboard", typeof(UAnimStateMachineGraph))]
    public sealed class UAnimSharedMachineBlackboardButton : UAnimSharedBlackboardButton
    {
    }
}
