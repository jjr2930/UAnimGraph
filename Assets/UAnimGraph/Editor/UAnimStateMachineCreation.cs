using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public sealed class CreateUAnimStateMachine : AssetCreationEndAction
    {
        public static void Prompt()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(default(EntityId), CreateInstance<CreateUAnimStateMachine>(), "StateMachine.uamachine", null, null);
        }

        public override void Action(EntityId instanceId, string pathName, string resourceFile)
        {
            var graph = StateMachineDatabase.CreateStateMachine<UAnimStateMachineGraph>(pathName);
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
            AssetDatabase.ImportAsset(pathName);
            ProjectWindowUtil.ShowCreatedAsset(AssetDatabase.LoadAssetAtPath<UAnimMachine>(pathName));
        }
    }
}
