using UAnimGraph.Editor.Graphs;
using Unity.GraphToolkit.Editor;

public class EditorOutPoseNode : Node
{
    protected override void OnDefinePorts(IPortDefinitionContext context)
    {
        base.OnDefinePorts(context);

        context.AddInputPort("InPose")
            .WithDataType(typeof(EditorPose));
    }
}
