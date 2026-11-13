using System;
using System.IO;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UAnimGraph.Authoring
{
    public static class UAnimAuthoringControls
    {
        public static void Layer(RootLayerNode node)
        {
            string name = EditorGUILayout.DelayedTextField(node.Option<string>("Name"));
            if(name != node.Option<string>("Name"))
            {
                Change(node, "Name", name);
            }

            var current = string.IsNullOrEmpty(node.embeddedMachineId) ? node.Option<UAnimMachine>("Machine") : UAnimEmbeddedAssets.RuntimeMachine(node);
            using(new EditorGUI.DisabledScope(!string.IsNullOrEmpty(node.embeddedMachineId)))
            {
                var machine = (UAnimMachine)EditorGUILayout.ObjectField("State Machine", current, typeof(UAnimMachine), false);
                if(machine != current)
                {
                    Change(node, "Machine", machine);
                }
            }

            var mask = (AvatarMask)EditorGUILayout.ObjectField("Avatar Mask", node.Option<AvatarMask>("AvatarMask"), typeof(AvatarMask), false);
            if(mask != node.Option<AvatarMask>("AvatarMask"))
            {
                Change(node, "AvatarMask", mask);
            }

            bool additive = EditorGUILayout.Popup(node.Option<bool>("Additive") ? 1 : 0, new[] { "Override", "Additive" }) == 1;
            if(additive != node.Option<bool>("Additive"))
            {
                Change(node, "Additive", additive);
            }

            float weight = EditorGUILayout.Slider("Weight", node.Option<float>("Weight"), 0, 1);
            if(weight != node.Option<float>("Weight"))
            {
                Change(node, "Weight", weight);
            }

            bool enabled = EditorGUILayout.Toggle("Enabled", node.Option<bool>("Enabled"));
            if(enabled != node.Option<bool>("Enabled"))
            {
                Change(node, "Enabled", enabled);
            }

            if(!current && string.IsNullOrEmpty(node.embeddedMachineId) && !node.Option<UAnimPoseGraph>("PoseGraph") && GUILayout.Button("Create State Machine"))
            {
                CreateMachine(node);
            }
        }

        static void Change<T>(RootLayerNode node, string option, T value)
        {
            node.Graph.UndoBeginRecordGraph("Edit Layer " + option, node);
            node.GetNodeOptionByName(option).TrySetValue(value);
            if(option == "Name")
            {
                node.Title = value?.ToString();
            }

            node.Graph.UndoEndRecordGraph();
            GraphDatabase.SaveGraph(node.Graph);
        }

        public static void CreateMachine(RootLayerNode node)
        {
            if(node.Graph is UAnimRootGraph)
            {
                UAnimEmbeddedAssets.CreateMachine(node);
                return;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(GraphDatabase.GetGraphAssetPath(node.Graph), null) + "-Layer.uamachine");
            var graph = StateMachineDatabase.CreateStateMachine<UAnimStateMachineGraph>(path);
            graph.requireEvaluation = true;
            var entry = new UAnimEntryState();
            var state = new PoseState();
            graph.AddState(entry);
            graph.AddState(state);
            entry.Title = "Entry";
            state.Title = "State";
            state.Position = new Vector2(300, 0);
            graph.Connect(entry, state);
            StateMachineDatabase.SaveStateMachine(graph);
            AssetDatabase.ImportAsset(path);
            Change(node, "Machine", AssetDatabase.LoadAssetAtPath<UAnimMachine>(path));
            node.OpenContents();
        }

        public static void CreateStateGraph(PoseState state, bool open = true)
        {
            state.defaultPoseInitialized = true;
            if(UAnimEmbeddedAssets.IsCache(StateMachineDatabase.GetStateMachineAssetPath(state.StateMachine)))
            {
                UAnimEmbeddedAssets.CreateStateGraph(state, open);
                return;
            }

            UAnimPoseGraph existing = null;
            state.GetOptionByName("Graph")?.TryGetValue(out existing);
            if(existing)
            {
                if(open)
                {
                    AssetDatabase.OpenAsset(existing);
                }

                return;
            }

            string directory = Path.GetDirectoryName(AssetDatabase.GUIDToAssetPath(state.StateMachine.AssetGuid.ToString()));
            string path = AssetDatabase.GenerateUniqueAssetPath(directory + "/State.uastate");
            var graph = GraphDatabase.CreateGraph<UAnimStateGraph>(path);
            var output = new StateOutputNode();
            var clip = new ClipNode();
            graph.AddNode(output);
            graph.AddNode(clip);
            output.Position = new Vector2(400, 0);
            graph.Connect(clip.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(path);
            state.StateMachine.UndoBeginRecordStateMachine("Assign State graph");
            state.GetOptionByName("Graph").TrySetValue(AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(path));
            state.StateMachine.UndoEndRecordStateMachine();
            StateMachineDatabase.SaveStateMachine(state.StateMachine);
            if(open)
            {
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(path));
            }
        }
    }

    public sealed class UAnimEntryStateView : StateView<UAnimEntryState>
    {
        public override void OnViewBuilt()
        {
            base.OnViewBuilt();
            View.Root.style.backgroundColor = new Color(.38f, .58f, .35f);
        }
    }
}
