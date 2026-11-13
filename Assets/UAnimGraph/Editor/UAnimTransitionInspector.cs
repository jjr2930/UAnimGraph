using System;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    // Toolkit's selected GraphObject has a native default Inspector. Extend its
    // public header hook instead of targeting Unity's private GraphObject type.
    [InitializeOnLoad]
    public static class UAnimTransitionInspector
    {
        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static UAnimTransitionConditions panel;
        static UAnimTransitionInspector()
        {
            Editor.finishedDefaultHeaderGUI += Draw;
            AssemblyReloadEvents.beforeAssemblyReload += Cleanup;
            EditorApplication.quitting += Cleanup;
        }

        static void Cleanup()
        {
            if(panel)
            {
                UnityEngine.Object.DestroyImmediate(panel);
            }

            panel = null;
        }

        public static string Resolve(UnityEngine.Object target)
        {
            if(!target || target is not ScriptableObject)
            {
                return null;
            }

            string path = AssetDatabase.GetAssetPath(target);
            if(path.EndsWith(".uamachine", StringComparison.Ordinal))
            {
                return path;
            }

            if(target is UAnimMachine runtime && path.EndsWith(".uanimasset", StringComparison.Ordinal))
            {
                var root = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                if(root != null)
                {
                    foreach(var entry in root.embeddedSources.Where(e => e.extension == "uamachine"))
                    {
                        string cache = UAnimEmbeddedAssets.CachePath(path, entry);
                        var candidate = AssetDatabase.LoadAssetAtPath<UAnimMachine>(cache);
                        if(candidate && candidate.id == runtime.id)
                        {
                            return cache;
                        }
                    }
                }
            }

            // Native GraphObjects can be transient and have no AssetDatabase
            // path. Their name is the source file name, including cache IDs.
            string name = target.name.Replace(" ", "");
            if(name.Length == 0)
            {
                return null;
            }

            foreach(string source in AssetDatabase.GetAllAssetPaths())
            {
                if(source.EndsWith(".uamachine", StringComparison.Ordinal) && Path.GetFileNameWithoutExtension(source).Replace(" ", "") == name)
                {
                    return source;
                }
            }

            return null;
        }

        static void Draw(UnityEditor.Editor editor)
        {
            if(editor.targets.Length != 1)
            {
                return;
            }

            string path = Resolve(editor.target);
            if(string.IsNullOrEmpty(path))
            {
                return;
            }

            var selected = UAnimTransitionSelection.Current(path);
            if(selected == null)
            {
                return;
            }

            var machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(path);
            if(machine == null)
            {
                return;
            }

            if(!panel)
            {
                panel = ScriptableObject.CreateInstance<UAnimTransitionConditions>();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UAnimGraph Transition Functions", EditorStyles.boldLabel);
            panel.DrawEmbedded(path, selected.ID.ToString());
            EditorGUILayout.Space();
        }

        public static ITransition SelectedFor(UnityEngine.Object target)
        {
            string path = Resolve(target);
            return string.IsNullOrEmpty(path) ? null : UAnimTransitionSelection.Current(path);
        }
    }
}
