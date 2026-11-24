using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    // Compatibility adapter only: the dedicated canvas does not instantiate a
    // Toolkit view, window, Inspector, overlay or visualization context.
    public sealed class UAnimEditorDocument
    {
        public string Path
        {
            get;
            private set;
        }
        public string RootPath
        {
            get;
            private set;
        }
        public Graph Graph
        {
            get;
            private set;
        }
        public UAnimStateMachineGraph Machine
        {
            get;
            private set;
        }

        public bool IsMachine
        {
            get
            {
                return Machine != null;
            }
        }

        public event Action Changed;
        public UAnimEditorDocument(string path)
        {
            Path = path.Replace('\\', '/');
            Reload();
        }

        public void Reload()
        {
            RootPath = UAnimSharedBlackboard.ResolveRoot(Path);
            Machine = Path.EndsWith(".uamachine", StringComparison.Ordinal) ? StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(Path) : null;
            Graph = Machine == null ? UAnimEditorWorkspace.LoadGraph(Path) : null;
            if(Graph == null && Machine == null)
            {
                throw new InvalidOperationException("Cannot open UAnimGraph source: " + Path);
            }
        }

        public INode[] Nodes
        {
            get
            {
                return Graph?.GetNodes().ToArray() ?? Array.Empty<INode>();
            }
        }

        public IState[] States
        {
            get
            {
                return Machine?.GetStates().ToArray() ?? Array.Empty<IState>();
            }
        }

        public ITransition[] Transitions
        {
            get
            {
                return States.SelectMany(s => s.GetOutgoingTransitions()).ToArray();
            }
        }

        public void Edit(string label, Action action)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(label);
            if(IsMachine)
            {
                Machine.UndoBeginRecordStateMachine(label);
            }
            else
            {
                Graph.UndoBeginRecordGraph(label, Nodes.OfType<Node>().ToArray());
            }

            try
            {
                action();
            }
            finally
            {
                if(IsMachine)
                {
                    Machine.UndoEndRecordStateMachine();
                }
                else
                {
                    Graph.UndoEndRecordGraph();
                }
            }

            Save();
            Undo.CollapseUndoOperations(group);
            Undo.IncrementCurrentGroup();
        }

        public void Save()
        {
            if(IsMachine)
            {
                StateMachineDatabase.SaveStateMachine(Machine);
            }
            else
            {
                UAnimStateDuration.Synchronize(Graph);
                GraphDatabase.SaveGraph(Graph);
            }

            AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceSynchronousImport);
            if(UAnimEmbeddedAssets.IsCache(Path))
            {
                UAnimEmbeddedAssets.Capture(Path);
            }

            UAnimEmbeddedAssets.Queue(Path);
            Changed?.Invoke();
        }

        public Node AddNode(Type type, Vector2 position)
        {
            if(IsMachine || !typeof(PoseNode).IsAssignableFrom(type))
            {
                throw new InvalidOperationException("Choose a Pose node in a Pose graph.");
            }

            Node node = null;
            Edit("Create " + type.Name, () =>
            {
                node = (Node)Activator.CreateInstance(type);
                Graph.AddNode(node);
                node.Position = position;
                node.Title = Friendly(type);
            });
            if(node is RootLayerNode layer && Graph is UAnimRootGraph)
            {
                UAnimDefaultLayerMachine.Ensure(layer);
            }

            return node;
        }

        public PoseState AddState(Vector2 position)
        {
            if(!IsMachine)
            {
                throw new InvalidOperationException("States belong to a StateMachine.");
            }

            var state = new PoseState();
            Edit("Create Pose State", () =>
            {
                Machine.AddState(state);
                state.Position = position;
                state.Title = "State";
                state.GetOptionByName("Name").TrySetValue("State");
            });
            UAnimDefaultStatePose.Ensure(state);
            return state;
        }

        public void Move(string id, Vector2 position)
        {
            Edit("Move node", () =>
            {
                if(IsMachine)
                {
                    States.First(s => s.ID.ToString() == id).Position = position;
                }
                else
                {
                    Nodes.First(n => n.ID.ToString() == id).Position = position;
                }
            });
        }

        public bool Connect(IPort output, IPort input)
        {
            if(output == null || input == null || output.Direction == input.Direction || output.DataType != input.DataType)
            {
                throw new InvalidOperationException("Connect an output to an input of the same type.");
            }

            if(!Nodes.Any(n => n.GetOutputPorts().Any(p => p.ID == output.ID)) || !Nodes.Any(n => n.GetInputPorts().Any(p => p.ID == input.ID)))
            {
                throw new InvalidOperationException("The ports must belong to the currently open graph.");
            }

            var from = Nodes.First(n => n.GetOutputPorts().Any(p => p.ID == output.ID));
            var to = Nodes.First(n => n.GetInputPorts().Any(p => p.ID == input.ID));
            if(from.ID == to.ID || Reaches(to, from.ID.ToString(), new HashSet<string>()))
            {
                throw new InvalidOperationException("Pose graphs cannot contain a cycle.");
            }

            bool connected = false;
            Edit("Connect ports", () =>
            {
                var old = new List<IPort>();
                input.GetConnectedPorts(old);
                foreach(var port in old)
                {
                    Graph.Disconnect(port, input);
                }

                connected = Graph.Connect(output, input);
            });
            return connected;
        }

        bool Reaches(INode node, string target, HashSet<string> visited)
        {
            if(!visited.Add(node.ID.ToString()))
            {
                return false;
            }

            foreach(var port in node.GetOutputPorts())
            {
                var connected = new List<IPort>();
                port.GetConnectedPorts(connected);
                foreach(var input in connected)
                {
                    var next = Nodes.FirstOrDefault(n => n.GetInputPorts().Any(p => p.ID == input.ID));
                    if(next != null && (next.ID.ToString() == target || Reaches(next, target, visited)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public ITransition ConnectStates(IState from, IState to)
        {
            if(!IsMachine || to is UAnimEntryState || from.StateMachine != Machine || to.StateMachine != Machine)
            {
                throw new InvalidOperationException("Choose two States in this machine. Entry cannot be a destination.");
            }

            ITransition result = null;
            Edit("Create transition", () =>
            {
                if(from is UAnimEntryState)
                {
                    foreach(var existing in from.GetOutgoingTransitions().ToArray())
                    {
                        Machine.Disconnect(from, existing.ToState);
                    }
                }

                result = Machine.Connect(from, to);
                if(from is not UAnimEntryState && result.RuleCount == 0)
                {
                    result.AddRule().RootCondition.Add(new DurationCondition { Value = .2f });
                }
            });
            return result;
        }

        public void Delete(string id, bool transition = false)
        {
            Edit("Delete selection", () =>
            {
                if(IsMachine)
                {
                    if(transition)
                    {
                        var edge = Transitions.FirstOrDefault(t => t.ID.ToString() == id);
                        if(edge != null)
                        {
                            Machine.Disconnect(edge.FromState, edge.ToState);
                        }
                    }
                    else
                    {
                        var state = States.FirstOrDefault(s => s.ID.ToString() == id);
                        if(state != null && state is not UAnimEntryState)
                        {
                            Machine.RemoveState(state);
                        }
                    }
                }
                else
                {
                    var node = Nodes.FirstOrDefault(n => n.ID.ToString() == id);
                    if(node != null && node is not LayerOutputNode && node is not StateOutputNode)
                    {
                        Graph.RemoveNode(node);
                    }
                }
            });
        }

        public string ChildPath(object item)
        {
            if(item is RootLayerNode layer)
            {
                UAnimDefaultLayerMachine.Ensure(layer);
                if(!string.IsNullOrEmpty(layer.embeddedMachineId))
                {
                    return UAnimEmbeddedAssets.CacheFolder + "/" + AssetDatabase.AssetPathToGUID(Path) + "/" + layer.embeddedMachineId + ".uamachine";
                }

                return ResolvePath((UnityEngine.Object)layer.Option<UAnimMachine>("Machine") ?? layer.Option<UAnimPoseGraph>("PoseGraph"));
            }

            if(item is PoseState state)
            {
                UAnimDefaultStatePose.Ensure(state);
                if(!string.IsNullOrEmpty(state.embeddedGraphId))
                {
                    return UAnimEmbeddedAssets.CachedPosePath(Path, state.embeddedGraphId);
                }

                state.GetOptionByName("Graph").TryGetValue(out UAnimPoseGraph pose);
                return ResolvePath(pose);
            }

            if(item is StateMachineNode machineNode)
            {
                return ResolvePath(machineNode.Option<UAnimMachine>("Machine"));
            }

            if(item is ISubgraphState sub)
            {
                var graph = sub.GetSubgraphAsGraph();
                return graph == null ? null : GraphDatabase.GetGraphAssetPath(graph);
            }

            return null;
        }

        public static string ResolvePath(UnityEngine.Object asset)
        {
            if(!asset)
            {
                return null;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            if(path.EndsWith(".uanimasset", StringComparison.Ordinal) && asset is UAnimMachine or UAnimPoseGraph)
            {
                UAnimEmbeddedAssets.Restore(path);
                var root = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                string id = asset is UAnimMachine m ? m.id : ((UAnimPoseGraph)asset).id;
                foreach(var entry in root.embeddedSources)
                {
                    string candidate = UAnimEmbeddedAssets.CachePath(path, entry);
                    var runtime = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(candidate);
                    var machine = AssetDatabase.LoadAssetAtPath<UAnimMachine>(candidate);
                    if((runtime && runtime.id == id) || (machine && machine.id == id))
                    {
                        return candidate;
                    }
                }
            }

            return path;
        }

        public static string Friendly(Type type)
        {
            if(type == typeof(ClipNode))
            {
                return "Animation Player";
            }

            if(type == typeof(Blend1DNode) || type == typeof(Blend2DNode))
            {
                return type == typeof(Blend1DNode) ? "Mixer1D" : "Mixer2D";
            }

            return ObjectNames.NicifyVariableName(type.Name.Replace("Node", ""));
        }

        public static object ReadOption(object option, Type type)
        {
            Type contract = option is INodeOption ? typeof(INodeOption) : typeof(IStateOption);
            var values = new object[]
            {
                type.IsValueType ? Activator.CreateInstance(type) : null
            };
            contract.GetMethod("TryGetValue").MakeGenericMethod(type).Invoke(option, values);
            return values[0];
        }

        public void SetOption(object option, Type type, object value, Node node = null)
        {
            Edit("Edit graph option", () =>
            {
                Type contract = option is INodeOption ? typeof(INodeOption) : typeof(IStateOption);
                contract.GetMethod("TrySetValue").MakeGenericMethod(type).Invoke(option, new[] { value });
                node?.DefineNode();
            });
        }
    }
}
