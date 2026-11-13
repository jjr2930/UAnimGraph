using System;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace UAnimGraph.Authoring
{
    [InitializeOnLoad]
    public static class UAnimWindowPaths
    {
        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static double next;
        static UAnimWindowPaths()
        {
            EditorApplication.update += Update;
        }

        static void Update()
        {
            if(EditorApplication.timeSinceStartup < next)
            {
                return;
            }

            next = EditorApplication.timeSinceStartup + .5;
            foreach(var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if(!window)
                {
                    continue;
                }

                if(window is not IGraphWindow graphWindow)
                {
                    continue;
                }

                string path = UAnimSharedBlackboard.SourcePath(window);
                if(string.IsNullOrEmpty(path))
                {
                    continue;
                }

                Apply(window, UAnimEmbeddedAssets.DisplayPath(path));
                // Toolkit toolbar overlays can be siblings of the editor content.
                var surface = window.rootVisualElement.panel?.visualTree ?? window.rootVisualElement;
                ApplyToolbarPath(surface, Path.GetFileNameWithoutExtension(path), UAnimEmbeddedAssets.DisplayPath(path));
            }
        }

        public static void Apply(EditorWindow window, string path)
        {
            string name = path.Substring(path.LastIndexOf('/') + 1);
            if(window.titleContent.text == name && window.titleContent.tooltip == path)
            {
                return;
            }

            window.titleContent = new GUIContent(name, window.titleContent.image, path);
        }

        public static void ApplyToolbarPath(VisualElement root, string sourceName, string path)
        {
            string marker = "uanim-graph-path-" + Hash128.Compute(sourceName);
            foreach(var label in root.Query<TextElement>().ToList())
            {
                if(!label.ClassListContains(marker) && label.text?.Trim() != sourceName)
                {
                    continue;
                }
                bool toolbar = false;
                for(var parent = label.parent; parent != null; parent = parent.parent)
                {
                    if(parent.GetClasses().Any(value => value.IndexOf("toolbar", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        toolbar = true;
                        break;
                    }
                }
                // Some Toolkit versions put the breadcrumb outside the toolbar
                // CSS hierarchy. Its exact cache identifier is still unambiguous.
                bool cacheIdentifier = sourceName.Length == 32 && sourceName.All(Uri.IsHexDigit);
                if(!toolbar && !cacheIdentifier)
                {
                    continue;
                }
                label.AddToClassList(marker);
                label.text = path;
                label.tooltip = path;
            }
        }
    }

    [GraphToolbarElement("UAnimGraph/TransitionConditions", typeof(UAnimStateMachineGraph))]
    public sealed class UAnimTransitionConditionsButton : EditorToolbarButton, IAccessContainerWindow
    {
        public EditorWindow containerWindow
        {
            get;
            set;
        }

        public UAnimTransitionConditionsButton()
        {
            text = "Transition Conditions";
            tooltip = "Choose a function for each State-to-State transition";
            clicked += () =>
            {
                if(containerWindow is IGraphWindow w && w.StateMachine is UAnimStateMachineGraph m)
                {
                    UAnimTransitionConditions.Open(m);
                }
            };
        }
    }

    public sealed class UAnimTransitionConditions : EditorWindow
    {
        [SerializeField]
        string machinePath;
        Vector2 scroll;
        string error;
        string selectedTransitionId;
        [MenuItem("Window/UAnimGraph/Transition Conditions")]
        public static void ShowForActiveMachine()
        {
            var active = EditorWindow.focusedWindow as IGraphWindow;
            var machine = active?.StateMachine as UAnimStateMachineGraph;
            if(machine == null)
            {
                machine = Resources.FindObjectsOfTypeAll<EditorWindow>().OfType<IGraphWindow>().Select(w => w.StateMachine).OfType<UAnimStateMachineGraph>().FirstOrDefault();
            }

            if(machine != null)
            {
                Open(machine);
            }
            else
            {
                GetWindow<UAnimTransitionConditions>("Transition Conditions").Show();
            }
        }

        public static void Open(UAnimStateMachineGraph machine)
        {
            var window = GetWindow<UAnimTransitionConditions>("Transition Conditions");
            window.machinePath = StateMachineDatabase.GetStateMachineAssetPath(machine);
            window.minSize = new Vector2(440, 260);
            window.Show();
        }

        public static bool CanChoose(ITransition transition)
        {
            return transition != null && transition.FromState is not UAnimEntryState && transition is not DefaultStateTransition;
        }

        public static EvaluationFunctionCondition[] Functions(IGroupCondition group)
        {
            return group.Get().SelectMany(c => c is IGroupCondition nested ? Functions(nested) : c is EvaluationFunctionCondition e ? new[] { e } : Array.Empty<EvaluationFunctionCondition>()).ToArray();
        }

        public static void Choose(UAnimStateMachineGraph machine, ITransition transition, string key)
        {
            if(!CanChoose(transition))
            {
                throw new InvalidOperationException("Entry chooses the initial State and has no evaluation function.");
            }

            if(!machine.GetStates().SelectMany(s => s.GetOutgoingTransitions()).Any(t => t.ID == transition.ID))
            {
                throw new InvalidOperationException("Transition no longer exists.");
            }

            UAnimEvaluationCatalog.Refresh();
            var selected = UAnimEvaluationCatalog.Entries.FirstOrDefault(e => e.key == key && e.Valid);
            if(selected == null)
            {
                throw new InvalidOperationException("Choose a valid runtime evaluation function.");
            }

            var rules = transition.GetRules().Where(r => r.Enabled).ToArray();
            if(rules.Length > 1)
            {
                throw new InvalidOperationException("Keep one enabled rule on this transition before choosing a function.");
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Choose transition evaluation function");
            machine.UndoBeginRecordStateMachine("Choose transition evaluation function");
            try
            {
                var rule = rules.Length == 1 ? rules[0] : transition.AddRule();
                // Keep timing/priority, replace incompatible field predicates.
                Clean(rule.RootCondition);
                rule.RootCondition.Add(new EvaluationFunctionCondition { Value = key });
            }
            finally
            {
                machine.UndoEndRecordStateMachine();
            }

            StateMachineDatabase.SaveStateMachine(machine);
            string path = StateMachineDatabase.GetStateMachineAssetPath(machine);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if(UAnimEmbeddedAssets.IsCache(path))
            {
                UAnimEmbeddedAssets.Capture(path);
            }

            Undo.CollapseUndoOperations(undoGroup);
            Undo.IncrementCurrentGroup();
        }

        static void Clean(IGroupCondition group)
        {
            foreach(var condition in group.Get().ToArray())
            {
                if(condition is IGroupCondition nested)
                {
                    Clean(nested);
                    if(nested.Count == 0)
                    {
                        group.Remove(nested);
                    }
                }
                else if(condition is EvaluationFunctionCondition || condition is TransitionFunctionPickerCondition || condition is ParameterCondition || condition is ExitCondition)
                {
                    group.Remove(condition);
                }
            }
        }

        void OnEnable()
        {
            Undo.undoRedoPerformed += Repaint;
        }

        void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
        }

        public void DrawEmbedded(string path, string transitionId = null)
        {
            machinePath = path;
            selectedTransitionId = transitionId;
            OnGUI();
        }

        void OnGUI()
        {
            if(string.IsNullOrEmpty(machinePath))
            {
                EditorGUILayout.HelpBox("Open a StateMachine and click Transition Conditions in its toolbar.", MessageType.Info);
                return;
            }

            var machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(machinePath);
            if(machine == null)
            {
                return;
            }

            EditorGUILayout.LabelField(UAnimEmbeddedAssets.DisplayPath(machinePath), EditorStyles.wordWrappedLabel);
            if(selectedTransitionId == null)
            {
                EditorGUILayout.HelpBox("Entry only selects the initial State. Create another Pose State and connect State → State to choose a function. Choosing a function replaces parameter/Exit Time conditions; duration and priority are retained.", MessageType.Info);
            }

            if(!string.IsNullOrEmpty(error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            int editable = 0;
            foreach(var transition in machine.GetStates().SelectMany(s => s.GetOutgoingTransitions()).Where(t => selectedTransitionId == null || t.ID.ToString() == selectedTransitionId).ToArray())
            {
                using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(transition.FromState.Title + " → " + transition.ToState.Title, EditorStyles.boldLabel);
                    if(!CanChoose(transition))
                    {
                        EditorGUILayout.LabelField("Initial State connection — no condition");
                        continue;
                    }

                    editable++;
                    var functions = transition.GetRules().Where(r => r.Enabled).SelectMany(r => Functions(r.RootCondition)).ToArray();
                    EditorGUILayout.LabelField("Function", functions.Length == 1 ? UAnimEvaluationCatalog.Display(functions[0].Value) : "Choose function…");
                    if(GUILayout.Button("Choose function…"))
                    {
                        string path = machinePath, id = transition.ID.ToString();
                        UAnimEvaluationPicker.Open(key =>
                        {
                            if(!this || machinePath != path)
                            {
                                return;
                            }

                            try
                            {
                                var current = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(path);
                                var edge = current.GetStates().SelectMany(s => s.GetOutgoingTransitions()).FirstOrDefault(t => t.ID.ToString() == id);
                                Choose(current, edge, key);
                                error = null;
                            }
                            catch(Exception e)
                            {
                                error = e.Message;
                            }

                            Repaint();
                        });
                    }

                    if(functions.Length == 1 && GUILayout.Button("Open function source"))
                    {
                        UAnimEvaluationCatalog.OpenSource(functions[0].Value);
                    }
                }
            }

            if(editable == 0 && selectedTransitionId == null)
            {
                EditorGUILayout.HelpBox("No State-to-State transitions yet. Entry connections cannot use conditions.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
