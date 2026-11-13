using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UAnimGraph.Authoring
{
    [Serializable, Node("UAnimGraph/Root", null, "Layer")]
    public sealed class RootLayerNode : PoseNode
    {
        [SerializeField]
        internal string embeddedMachineId;
        [SerializeField]
        internal bool defaultMachineInitialized;
        protected override void OnDefineOptions(IOptionDefinitionContext c)
        {
            c.AddOption<string>("Name").WithDefaultValue("Layer").ShowInInspectorOnly().Build();
            c.AddOption<UAnimMachine>("Machine").ShowInInspectorOnly().Build();
            // Optional compatibility source for existing authored Pose graphs.
            c.AddOption<UAnimPoseGraph>("PoseGraph").ShowInInspectorOnly().Build();
            c.AddOption<AvatarMask>("AvatarMask").ShowInInspectorOnly().Build();
            c.AddOption<bool>("Additive").ShowInInspectorOnly().Build();
            c.AddOption<float>("Weight").WithDefaultValue(1).ShowInInspectorOnly().Build();
            c.AddOption<bool>("Enabled").WithDefaultValue(true).ShowInInspectorOnly().Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext c)
        {
            c.AddInputPort<UAnimPose>("PreviousPose").Build();
            c.AddOutputPort<UAnimPose>("Pose").Build();
            if(Graph is UAnimRootGraph root && root.layerChain && !defaultMachineInitialized)
            {
                defaultMachineInitialized = true;
                UAnimDefaultLayerMachine.Queue(this);
            }
        }

        public void OpenContents()
        {
            if(!string.IsNullOrEmpty(embeddedMachineId))
            {
                UAnimEmbeddedAssets.OpenMachine(this);
                return;
            }

            UnityEngine.Object source = Option<UAnimMachine>("Machine");
            if(!source)
            {
                source = Option<UAnimPoseGraph>("PoseGraph");
            }

            if(source)
            {
                AssetDatabase.OpenAsset(source);
            }
            else
            {
                Debug.LogWarning("Assign a State Machine in this Layer's options first.");
            }
        }
    }

    public sealed class RootLayerNodeView : NodeView<RootLayerNode>
    {
        Button open;
        IMGUIContainer controls;
        public override void OnViewBuilt()
        {
            base.OnViewBuilt();
            if(Node.Title != Node.Option<string>("Name"))
            {
                Node.Title = Node.Option<string>("Name");
            }

            if(open == null)
            {
                open = new Button(() => Node.OpenContents())
                {
                    text = "Open Layer / State Machine"
                };
            }

            if(controls == null)
            {
                controls = new IMGUIContainer(() => UAnimAuthoringControls.Layer(Node));
                controls.style.minWidth = 280;
            }

            View.Root.Add(controls);
            View.Root.Add(open);
            View.Root.RegisterCallback<MouseDownEvent>(e =>
            {
                if(e.button != 0 || e.clickCount != 2)
                {
                    return;
                }

                for(var target = e.target as VisualElement; target != null && target != View.Root; target = target.parent)
                {
                    if(target is TextElement || target is TextField || target is IMGUIContainer || target.ClassListContains("ge-port") || target.ClassListContains("ge-editable-title-part"))
                    {
                        return;
                    }
                }

                e.StopImmediatePropagation();
                Node.OpenContents();
            }, TrickleDown.TrickleDown);
        }
    }

    // A root is a linear accumulation chain. Flatten it to the runtime's ordered
    // AnimationLayerMixer inputs; positions in the editor never determine order.
    public static class UAnimRootChainCompiler
    {
        public static RootLayerNode[] Ordered(UAnimRootGraph graph)
        {
            var nodes = graph.GetNodes().ToArray();
            var outputs = nodes.OfType<LayerOutputNode>().ToArray();
            if(outputs.Length != 1 || nodes.Any(n => n is StateOutputNode))
            {
                throw new InvalidOperationException("Root requires exactly one Output Pose (LayerOutput).");
            }

            var chain = new List<RootLayerNode>();
            var visited = new HashSet<INode>();
            INode next = Source(outputs[0].GetInputPortByName("Pose"), false);
            while(next != null)
            {
                if(!visited.Add(next))
                {
                    throw new InvalidOperationException("Root Layer chain contains a cycle.");
                }

                if(next is not RootLayerNode layer)
                {
                    throw new InvalidOperationException("Root Output/PreviousPose must connect to a Layer node, not a Clip or Mixer.");
                }

                chain.Add(layer);
                next = Source(layer.GetInputPortByName("PreviousPose"), true);
            }

            chain.Reverse();
            if(chain.Count == 0)
            {
                throw new InvalidOperationException("Connect a Layer to Output Pose.");
            }

            if(nodes.OfType<RootLayerNode>().Count() != chain.Count)
            {
                throw new InvalidOperationException("Every Layer must belong to the single chain ending at Output Pose.");
            }

            foreach(var layer in chain)
            {
                var connections = new List<IPort>();
                layer.GetOutputPortByName("Pose").GetConnectedPorts(connections);
                if(connections.Count != 1)
                {
                    throw new InvalidOperationException("A Layer output must have one consumer; branching is not supported.");
                }

                bool machine = !string.IsNullOrEmpty(layer.embeddedMachineId) || layer.Option<UAnimMachine>("Machine");
                bool pose = layer.Option<UAnimPoseGraph>("PoseGraph");
                if(machine == pose)
                {
                    throw new InvalidOperationException("Assign exactly one Machine or PoseGraph on Layer " + layer.Option<string>("Name"));
                }

                float weight = layer.Option<float>("Weight");
                if(!UAnimValidation.Finite(weight) || weight < 0 || weight > 1)
                {
                    throw new InvalidOperationException("Layer Weight must be finite and within 0..1.");
                }
            }

            var first = chain[0];
            if(first.Option<float>("Weight") != 1 || first.Option<bool>("Additive") || first.Option<AvatarMask>("AvatarMask"))
            {
                throw new InvalidOperationException("First Layer requires Weight 1, Override, no AvatarMask.");
            }

            return chain.ToArray();
        }

        static INode Source(IPort port, bool optional)
        {
            var sources = new List<IPort>();
            port?.GetConnectedPorts(sources);
            if(optional && sources.Count == 0)
            {
                return null;
            }

            if(sources.Count != 1 || sources[0].DataType != typeof(UAnimPose))
            {
                throw new InvalidOperationException("Connect exactly one Pose to " + port?.Name);
            }

            return sources[0].GetNode();
        }
    }
}
