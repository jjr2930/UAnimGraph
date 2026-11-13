using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security;
using System.IO;
using System.Text.RegularExpressions;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UIElements;

namespace UAnimGraph.Authoring
{
    [InitializeOnLoad]
    public static class UAnimEvaluationCatalog
    {
        public sealed class Entry
        {
            public MethodInfo method;
            public string key, error;
            public bool Valid
            {
                get
                {
                    return error == null;
                }
            }
        }

        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        public static readonly List<Entry> Entries = new List<Entry>();
        static UAnimEvaluationCatalog()
        {
            Refresh();
            AssemblyReloadEvents.afterAssemblyReload += Refresh;
        }

        public static void Refresh()
        {
            Entries.Clear();
            TransitionEvaluationRegistry.Clear();
            foreach(var type in TypeCache.GetTypesWithAttribute<TransitionLibraryAttribute>())
            {
                foreach(var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if(!method.IsDefined(typeof(TransitionEvaluationAttribute), false))
                    {
                        continue;
                    }

                    TransitionEvaluationRegistry.IsValid(method, out var error);
                    if(method.DeclaringType.Assembly.GetReferencedAssemblies().Any(a => a.Name == "UnityEditor" || a.Name.StartsWith("UnityEditor.")))
                    {
                        error = "Editor-only assembly cannot execute in a player.";
                    }

                    Entries.Add(new Entry { method = method, key = TransitionEvaluationRegistry.Key(method), error = error });
                }
            }

            Entries.Sort((a, b) => string.CompareOrdinal(a.method.DeclaringType.FullName + "." + a.method.Name, b.method.DeclaringType.FullName + "." + b.method.Name));
        }

        public static string Display(string key)
        {
            var entry = Entries.FirstOrDefault(e => e.key == key);
            return entry != null ? entry.method.DeclaringType.Name + "." + entry.method.Name : string.IsNullOrEmpty(key) ? "Choose function…" : "Missing function";
        }

        public static void OpenSource(string key)
        {
            var entry = Entries.FirstOrDefault(e => e.key == key);
            if(entry == null)
            {
                return;
            }

            foreach(var guid in AssetDatabase.FindAssets("t:MonoScript"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                string source = File.ReadAllText(path);
                bool declaration = Regex.IsMatch(source, @"\bclass\s+" + Regex.Escape(entry.method.DeclaringType.Name) + @"\b");
                if(script.GetClass() != entry.method.DeclaringType && !declaration)
                {
                    continue;
                }

                int line = 1;
                var text = File.ReadAllLines(path);
                for(int i = 0; i < text.Length; i++)
                {
                    if(text[i].Contains(entry.method.Name + "("))
                    {
                        line = i + 1;
                        break;
                    }
                }

                AssetDatabase.OpenAsset(script, line);
                return;
            }

            Debug.LogWarning("Cannot locate source file for " + Display(key));
        }
    }

    public sealed class UAnimEvaluationPicker : EditorWindow
    {
        Action<string> select;
        string search = "";
        Vector2 scroll;
        public static void Open(Action<string> select)
        {
            var w = CreateInstance<UAnimEvaluationPicker>();
            w.select = select;
            w.titleContent = new GUIContent("Evaluation Function");
            w.position = new Rect(150, 150, 520, 420);
            w.ShowUtility();
        }

        void OnGUI()
        {
            GUI.SetNextControlName("Search");
            search = EditorGUILayout.TextField("Search", search);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            string current = null;
            foreach(var entry in UAnimEvaluationCatalog.Entries)
            {
                var method = entry.method;
                string name = method.DeclaringType.FullName + "." + method.Name;
                if(!string.IsNullOrEmpty(search) && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if(current != method.DeclaringType.FullName)
                {
                    current = method.DeclaringType.FullName;
                    EditorGUILayout.LabelField(current, EditorStyles.boldLabel);
                }

                using(new EditorGUI.DisabledScope(!entry.Valid))
                {
                    if(GUILayout.Button(method.Name))
                    {
                        select(entry.key);
                        Close();
                        GUIUtility.ExitGUI();
                    }
                }

                if(!entry.Valid)
                {
                    EditorGUILayout.HelpBox(entry.error, MessageType.Warning);
                }
            }

            EditorGUILayout.EndScrollView();
            if(UAnimEvaluationCatalog.Entries.Count == 0)
            {
                EditorGUILayout.HelpBox("No evaluation functions found. Add a runtime static library and compile scripts.", MessageType.Info);
            }
        }
    }

    public sealed class EvaluationFunctionConditionView : ConditionView<EvaluationFunctionCondition>
    {
        Button choose, source;
        Label status;
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
            choose = new Button(() => UAnimEvaluationPicker.Open(key =>
            {
                var machine = Condition.StateMachine;
                if(machine == null)
                {
                    return;
                }

                machine.UndoBeginRecordStateMachine("Choose evaluation function", Condition);
                Condition.Value = key;
                machine.UndoEndRecordStateMachine();
                StateMachineDatabase.SaveStateMachine(machine);
                UpdateLabel();
            }));
            choose.style.flexGrow = 1;
            choose.style.minWidth = 0;
            choose.style.whiteSpace = WhiteSpace.Normal;
            source = new Button(() => UAnimEvaluationCatalog.OpenSource(Condition.Value))
            {
                text = "Open source"
            };
            status = new Label();
            View.Root.Add(choose);
            View.Root.Add(source);
            View.Root.Add(status);
            UpdateLabel();
        }

        public override void OnConditionChanged()
        {
            base.OnConditionChanged();
            UpdateLabel();
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

        void UpdateLabel()
        {
            if(choose == null)
            {
                return;
            }

            choose.text = UAnimEvaluationCatalog.Display(Condition.Value);
            choose.tooltip = choose.text;
            var entry = UAnimEvaluationCatalog.Entries.FirstOrDefault(e => e.key == Condition.Value);
            source.SetEnabled(entry != null);
            status.text = entry == null ? "Choose a registered function." : entry.error ?? "Valid evaluation function";
        }
    }

    // A linker descriptor retains user-written static methods for IL2CPP and
    // managed stripping. No runtime source compiler or generated method body.
    public sealed class UAnimEvaluationLinker : IPreprocessBuildWithReport
    {
        public int callbackOrder
        {
            get
            {
                return -100;
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            Generate();
        }

        [MenuItem("UAnimGraph/Refresh Evaluation Libraries")]
        public static void Generate()
        {
            UAnimEvaluationCatalog.Refresh();
            string folder = "Assets/UAnimGraph/Generated";
            Directory.CreateDirectory(folder);
            string xml = "<linker>\n";
            foreach(var assembly in UAnimEvaluationCatalog.Entries.Where(e => e.Valid).GroupBy(e => e.method.DeclaringType.Assembly.GetName().Name))
            {
                xml += "  <assembly fullname=\"" + SecurityElement.Escape(assembly.Key) + "\">\n";
                foreach(var type in assembly.GroupBy(e => e.method.DeclaringType.FullName))
                {
                    xml += "    <type fullname=\"" + SecurityElement.Escape(type.Key) + "\" preserve=\"all\" />\n";
                }

                xml += "  </assembly>\n";
            }

            xml += "</linker>\n";
            string path = folder + "/link.xml";
            if(!File.Exists(path) || File.ReadAllText(path) != xml)
            {
                File.WriteAllText(path, xml);
                AssetDatabase.ImportAsset(path);
            }
        }
    }
}
