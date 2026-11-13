using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimTransitionFunctionSample
    {
        public const string Folder = "Assets/UAnimGraph/TransitionFunctionSample";
        public const string RootPath = Folder + "/SampleGraph.uanimasset";
        [MenuItem("UAnimGraph/Open Transition Function Sample")]
        public static void Open()
        {
            Build();
            var graph = GraphDatabase.LoadGraph<UAnimRootGraph>(RootPath);
            var layer = graph.GetNodes().OfType<RootLayerNode>().Single();
            UAnimEmbeddedAssets.OpenMachine(layer);
            Selection.activeObject = UAnimEmbeddedAssets.RuntimeMachine(layer);
        }

        public static UAnimAsset Build()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UAnimAsset>(RootPath);
            if(existing)
            {
                return existing;
            }

            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var clip = new AnimationClip
            {
                name = "Sample Pose"
            };
            clip.SetCurve("Body", typeof(Transform), "localPosition.y", AnimationCurve.Linear(0, 0, 1, 1));
            AssetDatabase.CreateAsset(clip, Folder + "/SamplePose.anim");
            CreateUAnimAsset.CreateAtPath(RootPath);
            var root = GraphDatabase.LoadGraph<UAnimRootGraph>(RootPath);
            var layer = root.GetNodes().OfType<RootLayerNode>().Single();
            layer.GetNodeOptionByName("Name").TrySetValue("Layer1");
            UAnimEmbeddedAssets.CreateMachine(layer);
            root = GraphDatabase.LoadGraph<UAnimRootGraph>(RootPath);
            string machinePath = UAnimEmbeddedAssets.CachePath(RootPath, root.embeddedSources.Single(e => e.extension == "uamachine"));
            var machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(machinePath);
            var first = machine.GetStates().OfType<PoseState>().Single();
            UAnimEmbeddedAssets.CreateStateGraph(first, false);
            root = GraphDatabase.LoadGraph<UAnimRootGraph>(RootPath);
            string posePath = UAnimEmbeddedAssets.CachePath(RootPath, root.embeddedSources.Single(s => s.extension == "uastate"));
            var pose = GraphDatabase.LoadGraph<UAnimStateGraph>(posePath);
            pose.GetNodes().OfType<ClipNode>().Single().GetNodeOptionByName("Clip").TrySetValue(clip);
            GraphDatabase.SaveGraph(pose);
            AssetDatabase.ImportAsset(posePath, ImportAssetOptions.ForceSynchronousImport);
            UAnimEmbeddedAssets.Capture(posePath);
            machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(machinePath);
            first = machine.GetStates().OfType<PoseState>().Single();
            machine.UndoBeginRecordStateMachine("Create sample transitions");
            first.Title = "Idle";
            first.GetOptionByName("Name").TrySetValue("Idle");
            var second = new PoseState
            {
                embeddedGraphId = first.embeddedGraphId
            };
            machine.AddState(second);
            second.Title = "Move";
            second.GetOptionByName("Name").TrySetValue("Move");
            second.Position = new Vector2(650, 0);
            var go = machine.Connect(first, second);
            var back = machine.Connect(second, first);
            machine.UndoEndRecordStateMachine();
            string always = TransitionEvaluationRegistry.Key(typeof(SampleTransitionConditions).GetMethod(nameof(SampleTransitionConditions.AlwaysTrue)));
            string after = TransitionEvaluationRegistry.Key(typeof(SampleTransitionConditions).GetMethod(nameof(SampleTransitionConditions.AfterOneCycle)));
            UAnimTransitionConditions.Choose(machine, go, always);
            UAnimTransitionConditions.Choose(machine, back, after);
            return AssetDatabase.LoadAssetAtPath<UAnimAsset>(RootPath);
        }
    }
}
