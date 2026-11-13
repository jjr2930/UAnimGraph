using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UAnimGraph
{
    public sealed class UAnimInstance : IDisposable
    {
        public readonly UAnimAsset Asset;
        // Ownership follows live instances and Dispose, including editor preview graphs.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly HashSet<Animator> owners = new HashSet<Animator>();
        readonly Animator animator;
        readonly GameObject graphOwner;
        public readonly SharedBlackboard SharedBlackboard;
        public GameObject GraphOwner
        {
            get
            {
                return graphOwner;
            }
        }
        bool ownsAnimator;
        public readonly string DefinitionVersion;
        public PlayableGraph Graph
        {
            get;
            private set;
        }

        readonly Dictionary<string, int> lookup = new Dictionary<string, int>();
        readonly Dictionary<string, int> names = new Dictionary<string, int>();
        readonly double[] values, snapshot;
        readonly Vector2[] vectorValues, vectorSnapshot;
        readonly bool[] triggerWrites;
        bool evaluating;
        public string LastParameterError
        {
            get;
            private set;
        }

        readonly bool[] enabled;
        readonly float[] layerWeights;
        readonly Pose[] layers;
        readonly AnimationLayerMixerPlayable mixer;
        readonly bool manual;
        public readonly List<Machine> Machines = new List<Machine>();
        public readonly List<Pose> Poses = new List<Pose>();
        public UAnimInstance(UAnimAsset asset, Animator animator, bool manual = false, GameObject graphOwner = null)
        {
            var validation = UAnimValidation.Validate(asset);
            if(validation.Errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", validation.Errors));
            }

            if(!animator)
            {
                throw new ArgumentNullException(nameof(animator));
            }

            if(animator.runtimeAnimatorController)
            {
                throw new InvalidOperationException("Remove the Animator Controller before using UAnimGraphPlayer.");
            }

            this.animator = animator;
            this.graphOwner = graphOwner ? graphOwner : animator.gameObject;
            SharedBlackboard = new SharedBlackboard(this);
            Asset = asset;
            DefinitionVersion = asset.compiledDependencyHash;
            this.manual = manual;
            vectorValues = new Vector2[asset.parameters.Length];
            vectorSnapshot = new Vector2[asset.parameters.Length];
            triggerWrites = new bool[asset.parameters.Length];
            values = new double[asset.parameters.Length];
            snapshot = new double[values.Length];
            for(int i = 0; i < values.Length; i++)
            {
                var p = asset.parameters[i];
                lookup.Add(p.id, i);
                names.Add(p.name, i);
                values[i] = p.type == UAnimParameterType.Trigger ? 0 : p.defaultValue;
                vectorValues[i] = p.defaultVector2;
            }

            layers = new Pose[asset.layers.Length];
            enabled = new bool[layers.Length];
            layerWeights = new float[layers.Length];
            if(!owners.Add(animator))
            {
                throw new InvalidOperationException("This Animator already has a UAnimGraph output owner.");
            }

            ownsAnimator = true;
            try
            {
                Graph = PlayableGraph.Create("UAnimGraph: " + animator.name);
                Graph.SetTimeUpdateMode(manual ? DirectorUpdateMode.Manual : DirectorUpdateMode.GameTime);
                mixer = AnimationLayerMixerPlayable.Create(Graph, layers.Length);
                for(int i = 0; i < layers.Length; i++)
                {
                    var l = asset.layers[i];
                    layers[i] = Build(l.graph, l.graph.output);
                    enabled[i] = l.enabled;
                    layerWeights[i] = l.weight;
                    Graph.Connect(layers[i].Playable, 0, mixer, i);
                    mixer.SetInputWeight(i, l.enabled ? l.weight : 0);
                    mixer.SetLayerAdditive((uint)i, l.additive);
                    if(l.mask)
                    {
                        mixer.SetLayerMaskFromAvatarMask((uint)i, l.mask);
                    }
                }

                var output = AnimationPlayableOutput.Create(Graph, "UAnimGraph", animator);
                output.SetSourcePlayable(mixer);
                Graph.Play();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public double Get(string idOrName)
        {
            return values[Index(idOrName)];
        }

        int Index(string key)
        {
            return lookup.TryGetValue(key, out int i) || names.TryGetValue(key, out i) ? i : throw new ArgumentException("Unknown parameter: " + key);
        }

        public void Set(string idOrName, UAnimParameterType type, double value)
        {
            int i = Index(idOrName);
            if(Asset.parameters[i].type != type || type == UAnimParameterType.Vector2)
            {
                throw new ArgumentException("Parameter type mismatch: " + idOrName);
            }

            if(!UAnimValidation.Finite(value))
            {
                LastParameterError = "Non-finite parameter ignored: " + idOrName;
                return;
            }

            LastParameterError = null;
            if(type == UAnimParameterType.Trigger && evaluating)
            {
                triggerWrites[i] = value != 0;
                return;
            }

            values[i] = value;
        }

        internal double ContextValue(string key, UAnimParameterType type)
        {
            int i = Index(key);
            if(Asset.parameters[i].type != type)
            {
                throw new ArgumentException("Parameter type mismatch: " + key);
            }

            return evaluating ? snapshot[i] : values[i];
        }

        internal Vector2 ContextVector(string key)
        {
            int i = Index(key);
            if(Asset.parameters[i].type != UAnimParameterType.Vector2)
            {
                throw new ArgumentException("Vector2 parameter required: " + key);
            }

            return evaluating ? vectorSnapshot[i] : vectorValues[i];
        }

        public Vector2 GetVector2(string key)
        {
            return vectorValues[Index(key)];
        }

        public void SetVector2(string key, Vector2 value)
        {
            int i = Index(key);
            if(Asset.parameters[i].type != UAnimParameterType.Vector2)
            {
                throw new ArgumentException("Vector2 parameter required");
            }

            if(UAnimValidation.Finite(value.x) && UAnimValidation.Finite(value.y))
            {
                vectorValues[i] = value;
                LastParameterError = null;
            }
            else
            {
                LastParameterError = "Non-finite Vector2 ignored: " + key;
            }
        }

        public void SetLayer(int index, bool active, float weight)
        {
            if(!UAnimValidation.Finite(weight) || weight < 0 || weight > 1 || (index == 0 && weight != 1))
            {
                throw new ArgumentOutOfRangeException(nameof(weight));
            }

            if(active && !enabled[index])
            {
                layers[index].Reset();
            }

            enabled[index] = active;
            layerWeights[index] = weight;
        }

        internal float Read(UAnimValue v)
        {
            return string.IsNullOrEmpty(v.parameterId) ? v.constant : (float)snapshot[lookup[v.parameterId]];
        }

        internal Vector2 ReadVector(UAnimValue v)
        {
            return string.IsNullOrEmpty(v.parameterId) ? v.vector : vectorSnapshot[lookup[v.parameterId]];
        }

        public void Tick(float dt)
        {
            if(!Graph.IsValid())
            {
                return;
            }

            if(!UAnimValidation.Finite(dt) || dt < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dt));
            }

            Array.Copy(values, snapshot, values.Length);
            Array.Copy(vectorValues, vectorSnapshot, values.Length);
            evaluating = true;
            foreach(var pose in Poses)
            {
                pose.ClearActivity();
            }

            foreach(var machine in Machines)
            {
                machine.ClearActivity();
            }

            for(int i = 0; i < layers.Length; i++)
            {
                mixer.SetInputWeight(i, enabled[i] ? layerWeights[i] : 0);
                if(enabled[i])
                {
                    layers[i].Tick(dt, -1);
                    layers[i].MarkActive(layerWeights[i]);
                }
            }

            evaluating = false;
            for(int i = 0; i < values.Length; i++)
            {
                if(Asset.parameters[i].type == UAnimParameterType.Trigger)
                {
                    values[i] = triggerWrites[i] ? 1 : 0;
                    triggerWrites[i] = false;
                }
            }

            if(manual)
            {
                Graph.Evaluate(0);
            }
        }

        public void SeekPreview(float seconds)
        {
            if(!manual)
            {
                throw new InvalidOperationException("Seeking is only available on a manual preview instance.");
            }

            if(!UAnimValidation.Finite(seconds) || seconds < 0 || seconds > 60)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }

            foreach(var layer in layers)
            {
                layer.Reset();
            }

            float elapsed = 0;
            Tick(0);
            while(elapsed < seconds)
            {
                float step = Math.Min(1f / 60, seconds - elapsed);
                Tick(step);
                elapsed += step;
            }
        }

        public void Dispose()
        {
            if(Graph.IsValid())
            {
                Graph.Destroy();
            }

            if(ownsAnimator)
            {
                owners.Remove(animator);
                ownsAnimator = false;
            }
        }

        Pose Build(UAnimPoseGraph graph, int index)
        {
            var n = graph.nodes[index];
            if(n.kind == UAnimPoseKind.StateMachine)
            {
                var m = new Machine(this, n.machine)
                {
                    GraphId = graph.id,
                    NodeId = n.id
                };
                Machines.Add(m);
                return m;
            }

            var children = new Pose[n.inputs.Length];
            for(int i = 0; i < children.Length; i++)
            {
                children[i] = Build(graph, n.inputs[i]);
            }

            var pose = new Pose(this, n, children)
            {
                GraphId = graph.id,
                NodeId = n.id
            };
            Poses.Add(pose);
            return pose;
        }

        public class Pose
        {
            protected readonly UAnimInstance owner;
            readonly UAnimPoseNode node;
            readonly Pose[] children;
            double time;
            public Playable Playable
            {
                get;
                protected set;
            }

            public readonly float[] Weights;
            public string GraphId
            {
                get;
                internal set;
            }
            public string NodeId
            {
                get;
                internal set;
            }

            public string Id
            {
                get
                {
                    return NodeId;
                }
            }

            public float Activity
            {
                get;
                protected set;
            }
            public float InputValue
            {
                get;
                private set;
            }
            public Vector2 InputVector
            {
                get;
                private set;
            }
            public bool HasPreviewOverride
            {
                get;
                set;
            }
            public Vector2 PreviewInput
            {
                get;
                set;
            }

            public UAnimPoseNode PoseDefinition
            {
                get
                {
                    return node;
                }
            }

            public virtual void MarkActive(float weight)
            {
                Activity = Mathf.Max(Activity, weight);
                if(children != null)
                {
                    for(int i = 0; i < children.Length; i++)
                    {
                        children[i].MarkActive(weight * Weights[i]);
                    }
                }
            }

            internal void ClearActivity()
            {
                Activity = 0;
            }

            protected Pose(UAnimInstance owner)
            {
                this.owner = owner;
            }

            public Pose(UAnimInstance owner, UAnimPoseNode node, Pose[] children) : this(owner)
            {
                this.node = node;
                this.children = children;
                Weights = new float[children.Length];
                if(node.kind == UAnimPoseKind.Clip)
                {
                    var clip = AnimationClipPlayable.Create(owner.Graph, node.clip);
                    clip.SetApplyFootIK(false);
                    clip.SetApplyPlayableIK(false);
                    Playable = clip;
                }
                else
                {
                    var mix = AnimationMixerPlayable.Create(owner.Graph, children.Length);
                    Playable = mix;
                    for(int i = 0; i < children.Length; i++)
                    {
                        owner.Graph.Connect(children[i].Playable, 0, mix, i);
                    }
                }

                Playable.SetSpeed(0);
            }

            public virtual void Reset()
            {
                time = 0;
                if(children != null)
                {
                    foreach(var c in children)
                    {
                        c.Reset();
                    }
                }
            }

            public virtual void Tick(float dt, double phase)
            {
                time += dt * node.speed;
                if(node.kind == UAnimPoseKind.Clip)
                {
                    double t = (phase >= 0 ? phase * node.speed * node.clip.length : time) + node.offset * node.clip.length;
                    t = node.loop && node.clip.length > 0 ? t % node.clip.length : Math.Min(t, node.clip.length);
                    Playable.SetTime(t);
                    return;
                }

                Array.Clear(Weights, 0, Weights.Length);
                if(node.kind == UAnimPoseKind.Blend1D)
                {
                    InputValue = HasPreviewOverride ? PreviewInput.x : owner.Read(node.value);
                    UAnimBlendWeights.One(InputValue, node.thresholds, Weights);
                }
                else if(node.kind == UAnimPoseKind.Blend2D)
                {
                    InputVector = HasPreviewOverride ? PreviewInput : node.value.type == UAnimParameterType.Vector2 ? owner.ReadVector(node.value) : new Vector2(owner.Read(node.value), owner.Read(node.valueY));
                    UAnimBlendWeights.Two(InputVector, node.samples, Weights, node.inputIds);
                }
                else
                {
                    float total = 0;
                    for(int i = 0; i < Weights.Length; i++)
                    {
                        Weights[i] = Mathf.Max(0, owner.Read(node.weights[i]));
                        total += Weights[i];
                    }

                    if(total > 0)
                    {
                        for(int i = 0; i < Weights.Length; i++)
                        {
                            Weights[i] /= total;
                        }
                    }
                }

                for(int i = 0; i < children.Length; i++)
                {
                    Playable.SetInputWeight(i, Weights[i]);
                    children[i].Tick(dt, node.independentTime ? -1 : phase);
                }
            }
        }

        public sealed class Machine : Pose
        {
            public readonly UAnimMachine Definition;
            readonly Pose[] states;
            readonly UAnimTransition[] transitions;
            readonly TransitionEvaluator[] evaluators;
            readonly bool[] reported;
            public string LastEvaluationError
            {
                get;
                private set;
            }
            public bool LastEvaluationResult
            {
                get;
                private set;
            }

            int current, bank, destination = -1, destinationBank;
            double elapsed, destinationElapsed;
            float blendTime;
            UAnimTransition transition;
            public TransitionContext? ActiveTransitionContext
            {
                get;
                private set;
            }
            public int CurrentState
            {
                get
                {
                    return current;
                }
            }

            public int DestinationState
            {
                get
                {
                    return destination;
                }
            }

            public string ActiveTransitionId
            {
                get
                {
                    return transition?.id;
                }
            }

            public float ActivityWeight
            {
                get
                {
                    return Activity;
                }
            }

            public double Elapsed
            {
                get
                {
                    return elapsed;
                }
            }

            public double NormalizedTime
            {
                get
                {
                    return Definition.states[current].duration > 0 ? elapsed / Definition.states[current].duration : 0;
                }
            }

            public float TransitionProgress
            {
                get
                {
                    return transition == null ? 0 : transition.duration <= 0 ? 1 : Mathf.Clamp01(blendTime / transition.duration);
                }
            }

            public Machine(UAnimInstance owner, UAnimMachine definition) : base(owner)
            {
                Definition = definition;
                states = new Pose[definition.states.Length * 2];
                Playable = AnimationMixerPlayable.Create(owner.Graph, states.Length);
                Playable.SetSpeed(0);
                for(int i = 0; i < states.Length; i++)
                {
                    var g = definition.states[i / 2].graph;
                    states[i] = owner.Build(g, g.output);
                    owner.Graph.Connect(states[i].Playable, 0, Playable, i);
                }

                transitions = (UAnimTransition[])definition.transitions.Clone();
                Array.Sort(transitions, (a, b) => a.priority != b.priority ? b.priority.CompareTo(a.priority) : string.CompareOrdinal(a.id, b.id));
                evaluators = new TransitionEvaluator[transitions.Length];
                reported = new bool[transitions.Length];
                for(int i = 0; i < transitions.Length; i++)
                {
                    if(!string.IsNullOrEmpty(transitions[i].evaluation))
                    {
                        evaluators[i] = TransitionEvaluationRegistry.Resolve(transitions[i].evaluation);
                    }
                }

                Reset();
            }

            public override void Reset()
            {
                current = Definition.defaultState;
                bank = 0;
                destination = -1;
                elapsed = 0;
                transition = null;
                ActiveTransitionContext = null;
                blendTime = 0;
                foreach(var s in states)
                {
                    s.Reset();
                }

                for(int i = 0; i < states.Length; i++)
                {
                    Playable.SetInputWeight(i, i == current * 2 ? 1 : 0);
                }
            }

            public override void MarkActive(float weight)
            {
                Activity = Mathf.Max(Activity, weight);
                float blend = transition == null ? 0 : TransitionProgress;
                states[current * 2 + bank].MarkActive(weight * (1 - blend));
                if(destination >= 0)
                {
                    states[destination * 2 + destinationBank].MarkActive(weight * blend);
                }
            }

            public override void Tick(float dt, double unused)
            {
                bool started = false;
                double previous = elapsed;
                elapsed += dt * Definition.states[current].speed;
                if(transition == null)
                {
                    for(int evaluationIndex = 0; evaluationIndex < transitions.Length; evaluationIndex++)
                    {
                        var t = transitions[evaluationIndex];
                        if(t.from != current || (t.to == current && !t.allowSelf))
                        {
                            continue;
                        }

                        double length = Definition.states[current].duration;
                        if(string.IsNullOrEmpty(t.evaluation) && t.hasExitTime && !(previous <= t.exitTime * length && elapsed >= t.exitTime * length))
                        {
                            continue;
                        }

                        bool pass = true;
                        var context = new TransitionContext(owner,
                            new State(Definition.states[current], elapsed),
                            new State(Definition.states[t.to], 0), t.duration);
                        if(evaluators[evaluationIndex] != null)
                        {
                            try
                            {
                                pass = evaluators[evaluationIndex](in context);
                                LastEvaluationError = null;
                            }
                            catch(Exception e)
                            {
                                pass = false;
                                LastEvaluationError = e.Message;
                                if(!reported[evaluationIndex])
                                {
                                    Debug.LogError("UAnimGraph transition " + t.id + ": " + e.Message);
                                    reported[evaluationIndex] = true;
                                }
                            }

                            LastEvaluationResult = pass;
                        }
                        else
                        {
                            foreach(var c in t.conditions)
                            {
                                double actual = owner.snapshot[owner.lookup[c.parameterId]], b = c.value;
                                bool ok = c.comparison switch
                                {
                                    UAnimComparison.Equal => actual == b,
                                    UAnimComparison.NotEqual => actual != b,
                                    UAnimComparison.Less => actual < b,
                                    UAnimComparison.LessOrEqual => actual <= b,
                                    UAnimComparison.Greater => actual > b,
                                    _ => actual >= b
                                };
                                if(!ok)
                                {
                                    pass = false;
                                    break;
                                }
                            }
                        }

                        if(!pass)
                        {
                            continue;
                        }

                        transition = t;
                        ActiveTransitionContext = context;
                        destination = t.to;
                        destinationBank = 1 - bank;
                        destinationElapsed = 0;
                        blendTime = 0;
                        states[destination * 2 + destinationBank].Reset();
                        started = true;
                        break;
                    }
                }

                TickState(current, bank, dt, elapsed);
                if(transition == null)
                {
                    return;
                }

                // A newly entered state starts at zero; subsequent ticks advance both sides.
                if(!started)
                {
                    destinationElapsed += dt * Definition.states[destination].speed;
                }

                TickState(destination, destinationBank, started ? 0 : dt, destinationElapsed);
                if(!started)
                {
                    blendTime += dt;
                }
                float a = TransitionProgress;
                Playable.SetInputWeight(current * 2 + bank, 1 - a);
                Playable.SetInputWeight(destination * 2 + destinationBank, a);
                if(a >= 1)
                {
                    current = destination;
                    bank = destinationBank;
                    elapsed = destinationElapsed;
                    destination = -1;
                    transition = null;
                    ActiveTransitionContext = null;
                }
            }

            void TickState(int state, int stateBank, float dt, double t)
            {
                double length = Definition.states[state].duration;
                states[state * 2 + stateBank].Tick(dt, length > 0 ? t / length : -1);
            }
        }
    }
}
