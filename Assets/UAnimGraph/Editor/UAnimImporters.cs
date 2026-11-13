using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimGraphCompiler
    {
        public static UAnimPoseGraph Compile(Graph graph, bool stateGraph, Action<UnityEngine.Object> dependency)
        {
            var result = ScriptableObject.CreateInstance<UAnimPoseGraph>();
            result.id = graph.ID.ToString();
            result.stateGraph = stateGraph;
            var list = new List<UAnimPoseNode>();
            var indices = new Dictionary<INode, int>();
            var visiting = new HashSet<INode>();
            try
            {
                var outputs = graph.GetNodes().Where(n => n is LayerOutputNode || n is StateOutputNode).ToArray();
                if(outputs.Length != 1 || (stateGraph ? outputs[0] is not StateOutputNode : outputs[0] is not LayerOutputNode))
                {
                    throw new Exception("Exactly one matching LayerOutput/StateOutput is required");
                }

                if(outputs[0] is StateOutputNode so)
                {
                    UAnimStateDuration.Synchronize(graph);
                    result.stateDuration = so.Option<float>("Duration");
                    result.stateSpeed = so.Option<float>("Speed");
                }

                result.output = Visit(Input(outputs[0], "Pose"));
                int unused = graph.GetNodes().Count(n => n is PoseNode && n is not LayerOutputNode && n is not StateOutputNode && n is not ParameterNode && n is not ConstantNode && !indices.ContainsKey(n));
                if(unused > 0)
                {
                    Debug.LogWarning("UAnimGraph: " + unused + " pose nodes do not contribute to output");
                }
            }
            catch(Exception e)
            {
                result.importError = e.Message;
            }

            result.nodes = list.ToArray();
            return result;
            int Visit(INode source)
            {
                if(source == null)
                {
                    throw new Exception("Missing pose input");
                }

                if(visiting.Contains(source))
                {
                    throw new Exception("Pose cycle at " + source.Title);
                }

                if(indices.TryGetValue(source, out int index))
                {
                    return index;
                }

                visiting.Add(source);
                var n = new UAnimPoseNode
                {
                    id = source.ID.ToString()
                };
                index = list.Count;
                list.Add(n);
                indices.Add(source, index);
                switch(source)
                {
                    case ClipNode clip:
                        n.kind = UAnimPoseKind.Clip;
                        n.clip = clip.Option<AnimationClip>("Clip");
                        n.speed = clip.Option<float>("Speed");
                        n.loop = clip.Option<bool>("Loop");
                        n.offset = clip.Option<float>("Offset");
                        dependency(n.clip);
                        break;
                    case PoseNode blend when blend is IMixerAuthoring:
                        var settings = ((IMixerAuthoring)blend).Settings;
                        var entries = MixerAuthoring.Inputs(blend);
                        int count = entries.Count;
                        n.kind = blend is Blend2DNode ? UAnimPoseKind.Blend2D : UAnimPoseKind.Blend1D;
                        n.inputIds = entries.Select(e => e.port).ToArray();
                        n.inputs = new int[count];
                        n.thresholds = new float[count];
                        n.samples = new Vector2[count];
                        n.independentTime = settings.independentTime;
                        if(settings.binding == MixerBinding.GraphPort)
                        {
                            n.value = Value(blend.GetInputPortByName("Value"));
                        }
                        else if(settings.binding == MixerBinding.Vector2Parameter)
                        {
                            n.value = new UAnimValue
                            {
                                type = UAnimParameterType.Vector2,
                                parameterId = settings.parameter
                            };
                        }
                        else if(settings.binding == MixerBinding.XYParameters)
                        {
                            n.value = new UAnimValue
                            {
                                parameterId = settings.xParameter
                            };
                            n.valueY = new UAnimValue
                            {
                                parameterId = settings.yParameter
                            };
                        }
                        else if(settings.binding == MixerBinding.FloatParameter)
                        {
                            n.value = new UAnimValue
                            {
                                parameterId = settings.parameter
                            };
                        }
                        else
                        {
                            n.value = new UAnimValue
                            {
                                type = blend is Blend2DNode ? UAnimParameterType.Vector2 : UAnimParameterType.Float,
                                constant = settings.constant.x,
                                vector = settings.constant
                            };
                        }

                        for(int i = 0; i < count; i++)
                        {
                            n.inputs[i] = Visit(Input(blend, entries[i].port));
                            n.thresholds[i] = entries[i].threshold;
                            n.samples[i] = entries[i].position;
                        }

                        break;
                    case MixerNode mixer:
                        n.kind = UAnimPoseKind.Mixer;
                        int size = Mathf.Clamp(mixer.Option<int>("Count"), 1, 8);
                        n.inputs = new int[size];
                        n.weights = new UAnimValue[size];
                        for(int i = 0; i < size; i++)
                        {
                            n.inputs[i] = Visit(Input(mixer, "Pose" + i));
                            n.weights[i] = Value(mixer.GetInputPortByName("Weight" + i));
                        }

                        break;
                    case StateMachineNode machine:
                        if(stateGraph)
                        {
                            throw new Exception("State graphs cannot contain a StateMachine node");
                        }

                        n.kind = UAnimPoseKind.StateMachine;
                        n.machine = machine.Option<UAnimMachine>("Machine");
                        dependency(n.machine);
                        break;
                    default:
                        throw new Exception("Unsupported pose node: " + source.Title);
                }

                visiting.Remove(source);
                return index;
            }
        }

        static INode Input(INode node, string name)
        {
            var p = node.GetInputPortByName(name);
            var ports = new List<IPort>();
            p?.GetConnectedPorts(ports);
            if(ports.Count != 1 || ports[0].DataType != typeof(UAnimPose))
            {
                throw new Exception(node.Title + "/" + name + ": exactly one pose connection required");
            }

            return ports[0].GetNode();
        }

        static UAnimValue Value(IPort p)
        {
            if(p == null)
            {
                throw new Exception("Missing value port");
            }

            if(p.IsConnected)
            {
                var source = p.FirstConnectedPort.GetNode();
                if(source is ParameterNode pn)
                {
                    return new UAnimValue
                    {
                        parameterId = pn.Option<string>("ParameterId"),
                        type = pn.Option<UAnimParameterType>("Type")
                    };
                }

                if(source is ConstantNode cn)
                {
                    return new UAnimValue
                    {
                        type = cn.Option<UAnimParameterType>("Type"),
                        constant = cn.Option<float>("Value"),
                        vector = cn.Option<Vector2>("Vector2")
                    };
                }

                throw new Exception("Value input only accepts Parameter/Constant");
            }

            p.TryGetValue(out float value);
            return new UAnimValue
            {
                constant = value
            };
        }
    }
}
