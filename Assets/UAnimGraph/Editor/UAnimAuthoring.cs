using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    [Serializable, Graph("uapose", GraphOptions.SupportsSubgraphs)]
    public class UAnimLayerGraph : Graph
    {
        public override void OnGraphChanged(GraphLogger logger)
        {
            base.OnGraphChanged(logger);
            UAnimAuthoringValidation.Graph(this, false, logger);
        }

        [MenuItem("Assets/Create/UAnimGraph/Layer Graph")]
        static void Create()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<UAnimLayerGraph>();
        }
    }

    [Serializable, Graph("uastate", GraphOptions.SupportsSubgraphs)]
    public class UAnimStateGraph : Graph
    {
        public override void OnGraphChanged(GraphLogger logger)
        {
            base.OnGraphChanged(logger);
            UAnimStateDuration.Synchronize(this);
            UAnimAuthoringValidation.Graph(this, true, logger);
        }

        [MenuItem("Assets/Create/UAnimGraph/State Pose Graph")]
        static void Create()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<UAnimStateGraph>();
        }
    }

    [Serializable, StateMachine("uamachine", StateMachineOptions.SupportsSubgraphs)]
    public class UAnimStateMachineGraph : StateMachine
    {
        [SerializeField]
        internal bool requireEvaluation;
        public override void OnStateMachineChanged(StateMachineLogger logger)
        {
            base.OnStateMachineChanged(logger);
            UAnimAuthoringValidation.Machine(this, logger);
        }

        [MenuItem("Assets/Create/UAnimGraph/State Machine")]
        static void Create()
        {
            CreateUAnimStateMachine.Prompt();
        }
    }

    [Serializable]
    public struct UAnimPose
    {
    }

    [Serializable]
    public abstract class PoseNode : Node
    {
        public T Option<T>(string name)
        {
            T value = default;
            GetNodeOptionByName(name)?.TryGetValue(out value);
            return value;
        }
    }

    [Serializable, Node("UAnimGraph", null, "Animation Player")]
    public sealed class ClipNode : PoseNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext c)
        {
            c.AddOption<AnimationClip>("Clip").Build();
            c.AddOption<float>("Speed").WithDefaultValue(1).Build();
            c.AddOption<bool>("Loop").WithDefaultValue(true).Build();
            c.AddOption<float>("Offset").Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext c)
        {
            c.AddOutputPort<UAnimPose>("Pose").Build();
        }
    }

    [Serializable, Node("UAnimGraph", null, "Mixer1D")]
    public sealed class Blend1DNode : PoseNode, IMixerAuthoring
    {
        [SerializeField]
        MixerSettings settings = new MixerSettings();
        public MixerSettings Settings
        {
            get
            {
                return settings;
            }
        }

        protected override void OnDefineOptions(IOptionDefinitionContext c)
        {
            if(settings.inputs != null)
            {
                return;
            }

            c.AddOption<int>("Count").WithDefaultValue(2).ShowInInspectorOnly().Build();
            for(int i = 0; i < 8; i++)
            {
                c.AddOption<float>("Threshold" + i).WithDefaultValue(i).ShowInInspectorOnly().Build();
            }
        }

        protected override void OnDefinePorts(IPortDefinitionContext c)
        {
            MixerAuthoring.Materialize(this);
            if(settings.binding == MixerBinding.GraphPort)
            {
                c.AddInputPort<float>("Value").Build();
            }

            int index = 0;
            foreach(var entry in MixerAuthoring.Inputs(this))
            {
                c.AddInputPort<UAnimPose>(entry.port).WithDisplayName("Pose" + (++index)).Build();
            }

            c.AddOutputPort<UAnimPose>("Pose").Build();
        }
    }

    [Serializable, Node("UAnimGraph")]
    public sealed class MixerNode : PoseNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext c)
        {
            c.AddOption<int>("Count").WithDefaultValue(2).Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext c)
        {
            for(int i = 0; i < Mathf.Clamp(Option<int>("Count"), 1, 8); i++)
            {
                c.AddInputPort<UAnimPose>("Pose" + i).Build();
                c.AddInputPort<float>("Weight" + i).WithDefaultValue(i == 0 ? 1 : 0).Build();
            }

            c.AddOutputPort<UAnimPose>("Pose").Build();
        }
    }

    [Serializable, Node("UAnimGraph")]
    public sealed class StateMachineNode : PoseNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext c)
        {
            c.AddOption<UAnimMachine>("Machine").Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext c)
        {
            c.AddOutputPort<UAnimPose>("Pose").Build();
        }
    }

    [Serializable, Node("UAnimGraph")]
    public sealed class ParameterNode : PoseNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext c)
        {
            c.AddOption<string>("ParameterId").Build();
            c.AddOption<UAnimParameterType>("Type").Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext c)
        {
            var t = Option<UAnimParameterType>("Type");
            c.AddOutputPort("Value").WithDataType(t == UAnimParameterType.Float ? typeof(float) : t == UAnimParameterType.Int ? typeof(int) : t == UAnimParameterType.Vector2 ? typeof(Vector2) : typeof(bool)).Build();
        }
    }

    [Serializable, Node("UAnimGraph")]
    public sealed class ConstantNode : PoseNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext c)
        {
            c.AddOption<UAnimParameterType>("Type").Build();
            c.AddOption<float>("Value").Build();
            c.AddOption<Vector2>("Vector2").Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext c)
        {
            var t = Option<UAnimParameterType>("Type");
            c.AddOutputPort("Value").WithDataType(t == UAnimParameterType.Float ? typeof(float) : t == UAnimParameterType.Int ? typeof(int) : t == UAnimParameterType.Vector2 ? typeof(Vector2) : typeof(bool)).Build();
        }
    }

    [Serializable, Node("UAnimGraph", null, "Output Pose (Layer / Root)")]
    public sealed class LayerOutputNode : PoseNode
    {
        protected override void OnDefinePorts(IPortDefinitionContext c)
        {
            c.AddInputPort<UAnimPose>("Pose").Build();
        }
    }

    [Serializable, Node("UAnimGraph", null, "Output Pose (State)")]
    public sealed class StateOutputNode : PoseNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext c)
        {
            // Keep cycle timing for synchronized mixers and transitions without
            // exposing an automatically calculated clip length on the node.
            c.AddOption<float>("Duration").WithDefaultValue(1).ShowInInspectorOnly().Build();
            c.AddOption<float>("Speed").WithDefaultValue(1).Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext c)
        {
            c.AddInputPort<UAnimPose>("Pose").Build();
        }
    }

    // Native subgraph states can be used directly. This explicit reference state is also
    // provided for authoring workflows where the native subgraph asset menu is unavailable.
    [Serializable, State("UAnimGraph", null, "Pose State")]
    public sealed class PoseState : Unity.GraphToolkit.Editor.State
    {
        [SerializeField]
        internal string embeddedGraphId;
        [SerializeField]
        internal bool defaultPoseInitialized;
        protected override void OnDefineOptions(IOptionDefinitionContext c)
        {
            c.AddOption<string>("Name").Build();
            c.AddOption<UAnimPoseGraph>("Graph").Build();
            c.AddOption<bool>("Default").Build();
            if(!defaultPoseInitialized && StateMachine != null)
            {
                defaultPoseInitialized = true;
                UAnimDefaultStatePose.Queue(this);
            }
        }
    }

    [Serializable, State("UAnimGraph", null, "Entry")]
    public sealed class UAnimEntryState : Unity.GraphToolkit.Editor.State
    {
    }

    [Serializable, Transition("UAnimGraph", null, "Default State")]
    public sealed class DefaultStateTransition : SelfTransition
    {
    }

    [Serializable, Transition("UAnimGraph", null, "Self Transition")]
    public sealed class EvaluationSelfTransition : SelfTransition
    {
    }

    [Serializable, Condition("UAnimGraph/Evaluation Function")]
    public sealed class EvaluationFunctionCondition : Condition<string>
    {
        protected override bool DisplayComparisonDropdown
        {
            get
            {
                return false;
            }
        }
    }

    [Serializable, Condition("UAnimGraph/Duration Seconds")]
    public sealed class DurationCondition : Condition<float>
    {
    }

    [Serializable, Condition("UAnimGraph/Priority")]
    public sealed class PriorityCondition : Condition<int>
    {
    }

    [Serializable, Condition("UAnimGraph/Legacy/Exit Normalized Time")]
    public sealed class ExitCondition : Condition<float>
    {
    }

    [Serializable, Condition("UAnimGraph/Legacy/Allow Self Transition")]
    public sealed class AllowSelfCondition : Condition<bool>
    {
    }

    [Serializable, Condition("UAnimGraph/Legacy/Parameter ID | Type | Comparison | Value")]
    public sealed class ParameterCondition : Condition<string>
    {
        public override string Tooltip
        {
            get
            {
                return "Root parameter ID|Float/Int/Bool/Trigger|Equal/NotEqual/Less/LessOrEqual/Greater/GreaterOrEqual|number. Example: speed-id|Float|Greater|0.1";
            }
        }
    }
}
