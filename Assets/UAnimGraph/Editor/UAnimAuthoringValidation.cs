using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimAuthoringValidation
    {
        public static bool IsIncomplete(Graph graph, string message)
        {
            return message.StartsWith("Assign ") || message.StartsWith("Copy ") || message.Contains("connect exactly one Pose") || (message.StartsWith("Exactly one ") && !graph.GetNodes().Any(n => n is LayerOutputNode || n is StateOutputNode));
        }

        public static void Graph(Graph graph, bool stateGraph, GraphLogger logger)
        {
            if(graph is UAnimRootGraph root && root.layerChain)
            {
                try
                {
                    UAnimRootChainCompiler.Ordered(root);
                }
                catch(Exception e)
                {
                    logger.LogWarning(e.Message, root.GetNodes().FirstOrDefault());
                }

                return;
            }

            foreach(var issue in GetIssues(graph, stateGraph))
            {
                bool incomplete = IsIncomplete(graph, issue.message);
                if(incomplete)
                {
                    logger.LogWarning(issue.message, issue.element);
                }
                else
                {
                    logger.LogError(issue.message, issue.element);
                }
            }
        }

        public static List<(string message, object element)> GetIssues(Graph graph, bool stateGraph)
        {
            var issues = new List<(string, object)>();
            var nodes = graph.GetNodes().ToArray();
            var outputs = nodes.Where(n => n is LayerOutputNode || n is StateOutputNode).ToArray();
            if(outputs.Length != 1)
            {
                issues.Add(("Exactly one " + (stateGraph ? "StateOutput" : "LayerOutput") + " is required", outputs.FirstOrDefault() ?? nodes.FirstOrDefault()));
            }

            var stack = new HashSet<INode>();
            var visited = new HashSet<INode>();
            foreach(var n in nodes)
            {
                if((stateGraph && n is LayerOutputNode) || (!stateGraph && n is StateOutputNode))
                {
                    issues.Add(("Wrong output kind for this graph", n));
                }

                if(n is ClipNode c && !c.Option<AnimationClip>("Clip"))
                {
                    issues.Add(("Assign an AnimationClip", n));
                }

                if(n is StateMachineNode m && (stateGraph || !m.Option<UAnimMachine>("Machine")))
                {
                    issues.Add((stateGraph ? "State graphs cannot contain state machines" : "Assign a StateMachine asset", n));
                }

                if(n is ParameterNode p && string.IsNullOrWhiteSpace(p.Option<string>("ParameterId")))
                {
                    issues.Add(("Copy a parameter ID from the root asset", n));
                }

                if(n is IMixerAuthoring)
                {
                    var samples = MixerAuthoring.Inputs((PoseNode)n);
                    for(int i = 0; i < samples.Count; i++)
                    {
                        if(!UAnimValidation.Finite(samples[i].threshold) || !UAnimValidation.Finite(samples[i].position.x) || !UAnimValidation.Finite(samples[i].position.y))
                        {
                            issues.Add(("Mixer coordinates must be finite", n));
                        }

                        for(int j = 0; j < i; j++)
                        {
                            if(n is Blend1DNode ? samples[j].threshold == samples[i].threshold : samples[j].position.Equals(samples[i].position))
                            {
                                issues.Add(("Duplicate Mixer sample location", n));
                            }
                        }
                    }
                }

                foreach(var input in n.GetInputPorts())
                {
                    if(input.DataType != typeof(UAnimPose))
                    {
                        continue;
                    }

                    if(n is RootLayerNode && input.Name == "PreviousPose" && !input.IsConnected)
                    {
                        continue;
                    }

                    var ports = new List<IPort>();
                    input.GetConnectedPorts(ports);
                    if(ports.Count != 1 || ports[0].DataType != typeof(UAnimPose))
                    {
                        issues.Add((input.DisplayName + ": connect exactly one Pose", n));
                    }
                }

                Visit(n);
            }

            return issues;
            void Visit(INode n)
            {
                if(stack.Contains(n))
                {
                    issues.Add(("Pose cycle: remove this cyclic connection", n));
                    return;
                }

                if(!visited.Add(n))
                {
                    return;
                }

                stack.Add(n);
                foreach(var input in n.GetInputPorts())
                {
                    if(input.DataType == typeof(UAnimPose) && input.FirstConnectedPort?.GetNode()is INode source)
                    {
                        Visit(source);
                    }
                }

                stack.Remove(n);
            }
        }

        public static void Machine(UAnimStateMachineGraph machine, StateMachineLogger logger)
        {
            var states = machine.GetStates().ToArray();
            int defaults = 0;
            foreach(var state in states)
            {
                if(state is UAnimEntryState)
                {
                    defaults++;
                    if(state.GetIncomingTransitions().Any() || state.GetOutgoingTransitions().Count() != 1)
                    {
                        logger.LogWarning("Entry needs one outgoing transition and no incoming transitions", state);
                    }

                    continue;
                }

                bool entry = false;
                state.GetOptionByName("Default")?.TryGetValue(out entry);
                if(entry)
                {
                    defaults++;
                }

                foreach(var t in state.GetOutgoingTransitions())
                {
                    if(t is DefaultStateTransition)
                    {
                        defaults++;
                    }
                }

                UAnimPoseGraph pose = null;
                if(state is ISubgraphState sub)
                {
                    if(sub.GetSubgraphAsGraph()is not UAnimStateGraph)
                    {
                        logger.LogWarning("Link a UAnimGraph State Pose Graph", state);
                    }
                }
                else
                {
                    if(state is PoseState embedded && !string.IsNullOrEmpty(embedded.embeddedGraphId))
                    {
                        pose = UAnimEmbeddedAssets.CachedPose(StateMachineDatabase.GetStateMachineAssetPath(machine), embedded.embeddedGraphId);
                    }
                    else
                    {
                        state.GetOptionByName("Graph")?.TryGetValue(out pose);
                    }

                    if(!pose || !pose.stateGraph)
                    {
                        logger.LogWarning("Assign a State Pose Graph", state);
                    }
                }
            }

            if(defaults == 0)
            {
                logger.LogWarning("Choose one Default State before running", states.FirstOrDefault());
            }
            else if(defaults > 1)
            {
                logger.LogError("Exactly one Default State is required", states.FirstOrDefault());
            }
        }
    }
}
