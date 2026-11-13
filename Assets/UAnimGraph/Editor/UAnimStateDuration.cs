using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimStateDuration
    {
        // A single connected clip defines a natural cycle. Mixers retain their
        // explicit duration because their inputs may have different cycle lengths.
        public static void Synchronize(Graph graph)
        {
            foreach(var output in graph.GetNodes().OfType<StateOutputNode>())
            {
                var input = output.GetInputPortByName("Pose");
                if(input == null || !input.IsConnected || input.FirstConnectedPort.GetNode()is not ClipNode player)
                {
                    continue;
                }

                var clip = player.Option<AnimationClip>("Clip");
                if(!clip || clip.length <= 0)
                {
                    continue;
                }

                if(output.Option<float>("Duration") != clip.length)
                {
                    output.GetNodeOptionByName("Duration").TrySetValue(clip.length);
                }
            }
        }
    }
}
