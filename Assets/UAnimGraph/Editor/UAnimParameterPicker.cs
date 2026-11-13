using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public sealed class UAnimParameterPicker : EditorWindow
    {
        UAnimAsset root;
        UAnimParameterType type;
        Action<string> select;
        string search = "";
        Vector2 scroll;
        public static UAnimParameter[] Search(UAnimAsset root, UAnimParameterType type, string query)
        {
            if(!root)
            {
                return Array.Empty<UAnimParameter>();
            }

            return root.parameters.Where(p => p != null && p.type == type && (string.IsNullOrWhiteSpace(query) || p.name.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)).OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.id, StringComparer.Ordinal).ToArray();
        }

        public static void Open(UAnimAsset root, UAnimParameterType type, Action<string> select)
        {
            var window = CreateInstance<UAnimParameterPicker>();
            window.root = root;
            window.type = type;
            window.select = select;
            window.titleContent = new GUIContent("Select " + type + " Parameter");
            window.position = new Rect(150, 150, 400, 360);
            window.ShowUtility();
        }

        void OnGUI()
        {
            if(!root)
            {
                EditorGUILayout.HelpBox("Select a parameter root first.", MessageType.Info);
                return;
            }

            GUI.SetNextControlName("ParameterSearch");
            search = EditorGUILayout.TextField("Search by name", search);
            if(Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
                return;
            }

            var matches = Search(root, type, search);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach(var parameter in matches)
            {
                if(GUILayout.Button(new GUIContent(parameter.name, parameter.id)))
                {
                    select?.Invoke(parameter.id);
                    Close();
                    GUIUtility.ExitGUI();
                }
            }

            EditorGUILayout.EndScrollView();
            if(matches.Length == 0)
            {
                EditorGUILayout.HelpBox("No matching " + type + " parameters.", MessageType.Info);
            }
        }
    }

    public static class UAnimStateSettingsNavigation
    {
        public readonly struct Target
        {
            public readonly UAnimMachine Machine;
            public readonly UAnimState State;
            public readonly string LayerName;
            public Target(UAnimMachine machine, UAnimState state, string layerName)
            {
                Machine = machine;
                State = state;
                LayerName = layerName;
            }
        }

        public static Target[] Find(UAnimAsset root, UAnimPoseGraph pose)
        {
            if(!root || !pose)
            {
                return Array.Empty<Target>();
            }

            return root.layers.Where(l => l != null && l.graph).SelectMany(l => l.graph.nodes.Where(n => n != null && n.machine).SelectMany(n => n.machine.states.Where(s => s != null && s.graph == pose).Select(s => new Target(n.machine, s, l.name)))).GroupBy(t => (t.Machine, t.State.id)).Select(g => g.First()).ToArray();
        }

        public static void Open(Target target)
        {
            Selection.activeObject = target.Machine;
            AssetDatabase.OpenAsset(target.Machine);
            EditorGUIUtility.PingObject(target.Machine);
        }
    }
}
