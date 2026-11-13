using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public enum MixerBinding
    {
        GraphPort,
        FloatParameter,
        Vector2Parameter,
        XYParameters,
        Constant
    }

    [Serializable]
    public sealed class MixerPoseInput
    {
        public string port;
        public float threshold;
        public Vector2 position;
        public MixerPoseInput(string port, float threshold, Vector2 position)
        {
            this.port = port;
            this.threshold = threshold;
            this.position = position;
        }
    }

    [Serializable]
    public sealed class MixerSettings
    {
        public List<MixerPoseInput> inputs;
        public MixerBinding binding;
        public string parameter, xParameter, yParameter;
        public string displayName;
        public Vector2 constant;
        public bool independentTime;
    }

    public interface IMixerAuthoring
    {
        MixerSettings Settings
        {
            get;
        }
    }

    [Serializable, Node("UAnimGraph", null, "Mixer2D")]
    public sealed class Blend2DNode : PoseNode, IMixerAuthoring
    {
        [SerializeField]
        MixerSettings settings = new MixerSettings
        {
            binding = MixerBinding.Constant,
            inputs = new List<MixerPoseInput>
            {
                new MixerPoseInput("Pose0", 0, Vector2.zero),
                new MixerPoseInput("Pose1", 1, Vector2.up)
            }
        };
        public MixerSettings Settings
        {
            get
            {
                return settings;
            }
        }

        protected override void OnDefinePorts(IPortDefinitionContext c)
        {
            int index = 0;
            foreach(var entry in settings.inputs)
            {
                c.AddInputPort<UAnimPose>(entry.port).WithDisplayName("Pose" + (++index)).Build();
            }

            c.AddOutputPort<UAnimPose>("Pose").Build();
        }
    }

    public static class MixerAuthoring
    {
        public static List<MixerPoseInput> Inputs(PoseNode node)
        {
            var data = ((IMixerAuthoring)node).Settings;
            if(data.inputs != null)
            {
                return data.inputs;
            }

            return LegacyInputs(node);
        }

        internal static List<MixerPoseInput> LegacyInputs(PoseNode node)
        {
            var list = new List<MixerPoseInput>();
            for(int i = 0; i < Mathf.Clamp(node.Option<int>("Count"), 1, 8); i++)
            {
                list.Add(new MixerPoseInput("Pose" + i, node.Option<float>("Threshold" + i), Vector2.zero));
            }

            return list;
        }

        public static void Materialize(PoseNode node)
        {
            var data = ((IMixerAuthoring)node).Settings;
            if(data.inputs == null)
            {
                data.inputs = Inputs(node);
            }
        }
    }
}
