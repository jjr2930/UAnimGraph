using System;
using System.Collections.Generic;
using UnityEngine;

namespace UAnimGraph
{
    public sealed class UAnimValidation
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        readonly Dictionary<string, UAnimParameter> parameters = new Dictionary<string, UAnimParameter>();
        readonly HashSet<UnityEngine.Object> stack = new HashSet<UnityEngine.Object>();
        public static UAnimValidation Validate(UAnimAsset asset)
        {
            var v = new UAnimValidation();
            if(!asset)
            {
                v.Errors.Add("Root: missing UAnimAsset");
                return v;
            }

            if(!string.IsNullOrEmpty(asset.importError))
            {
                v.Errors.Add("Root: " + asset.importError);
            }

            if(asset.schemaVersion != 1 || string.IsNullOrEmpty(asset.id))
            {
                v.Errors.Add("Root: invalid schema or missing ID");
            }

            var names = new HashSet<string>();
            foreach(var p in asset.parameters)
            {
                if(p == null || string.IsNullOrEmpty(p.id) || !v.parameters.TryAdd(p.id, p))
                {
                    v.Errors.Add("Parameters: missing or duplicate ID");
                    continue;
                }

                if(string.IsNullOrEmpty(p.name) || !names.Add(p.name))
                {
                    v.Errors.Add("Parameters: missing or duplicate name");
                }

                if(p.type == UAnimParameterType.Vector2 && (!Finite(p.defaultVector2.x) || !Finite(p.defaultVector2.y)))
                {
                    v.Errors.Add(p.name + ": non-finite Vector2 default");
                }

                if(!Finite(p.defaultValue))
                {
                    v.Errors.Add(p.name + ": non-finite default");
                }
            }

            if(asset.layers.Length == 0)
            {
                v.Errors.Add("Root: at least one layer required");
            }

            var ids = new HashSet<string>();
            for(int i = 0; i < asset.layers.Length; i++)
            {
                var l = asset.layers[i];
                var path = asset.name + "/Layer " + i;
                if(l == null)
                {
                    v.Errors.Add(path + ": missing layer");
                    continue;
                }

                if(string.IsNullOrEmpty(l.id) || !ids.Add(l.id))
                {
                    v.Errors.Add(path + ": missing or duplicate ID");
                }

                if(!Finite(l.weight) || l.weight < 0 || l.weight > 1)
                {
                    v.Errors.Add(path + ": weight must be 0..1");
                }

                if(i == 0 && (l.weight != 1 || l.additive || l.mask))
                {
                    v.Errors.Add(path + ": base layer requires weight 1, Override, no mask");
                }

                v.Graph(l.graph, false, path);
            }

            return v;
        }

        public static bool Finite(float f)
        {
            return !float.IsNaN(f) && !float.IsInfinity(f);
        }

        public static bool Finite(double f)
        {
            return !double.IsNaN(f) && !double.IsInfinity(f);
        }

        void Error(string path, string message)
        {
            Errors.Add(path + ": " + message);
        }

        void Value(UAnimValue v, string path, bool requireFloat = false)
        {
            if(v == null)
            {
                Error(path, "missing value");
                return;
            }

            if(!Finite(v.constant))
            {
                Error(path, "non-finite value");
            }

            if(v.type == UAnimParameterType.Vector2 && (!Finite(v.vector.x) || !Finite(v.vector.y)))
            {
                Error(path, "non-finite Vector2 constant");
            }

            if(requireFloat && v.type != UAnimParameterType.Float)
            {
                Error(path, "float input required");
            }

            if(!string.IsNullOrEmpty(v.parameterId) && (!parameters.TryGetValue(v.parameterId, out var p) || p.type != v.type))
            {
                Error(path, "parameter ID/type contract mismatch: " + v.parameterId);
            }
        }

        void Graph(UAnimPoseGraph g, bool state, string path)
        {
            if(!g)
            {
                Error(path, "missing pose graph");
                return;
            }

            path += "/" + g.name;
            if(!stack.Add(g))
            {
                Error(path, "cyclic asset reference");
                return;
            }

            if(!string.IsNullOrEmpty(g.importError))
            {
                Error(path, g.importError);
            }

            if(g.schemaVersion != 1 || g.stateGraph != state)
            {
                Error(path, "schema or output kind mismatch");
            }

            var visiting = new HashSet<int>();
            var visited = new HashSet<int>();
            var ids = new HashSet<string>();
            for(int i = 0; i < g.nodes.Length; i++)
            {
                var n = g.nodes[i];
                var np = path + "/Node " + i;
                if(n == null)
                {
                    Error(np, "missing node");
                    continue;
                }

                if(string.IsNullOrEmpty(n.id) || !ids.Add(n.id))
                {
                    Error(np, "missing or duplicate ID");
                }

                if(!Finite(n.speed) || n.speed < 0 || !Finite(n.offset) || n.offset < 0 || n.offset > 1)
                {
                    Error(np, "invalid speed or normalized offset");
                }

                if(n.kind == UAnimPoseKind.Clip && !n.clip)
                {
                    Error(np, "missing clip");
                }

                if(n.kind == UAnimPoseKind.Blend1D)
                {
                    Value(n.value, np, true);
                    if(n.inputs.Length < 1 || n.thresholds.Length != n.inputs.Length)
                    {
                        Error(np, "Blend1D requires matching thresholds");
                    }

                    for(int j = 0; j < n.thresholds.Length; j++)
                    {
                        if(!Finite(n.thresholds[j]))
                        {
                            Error(np, "thresholds must be finite");
                        }

                        for(int k = 0; k < j; k++)
                        {
                            if(n.thresholds[j] == n.thresholds[k])
                            {
                                Error(np, "duplicate threshold");
                            }
                        }
                    }
                }

                if(n.kind == UAnimPoseKind.Blend2D)
                {
                    Value(n.value, np, n.value.type != UAnimParameterType.Vector2);
                    if(n.value.type != UAnimParameterType.Vector2)
                    {
                        Value(n.valueY, np, true);
                    }

                    if(n.inputs.Length < 1 || n.samples.Length != n.inputs.Length)
                    {
                        Error(np, "Mixer2D inputs and coordinates must match");
                    }

                    for(int j = 0; j < n.samples.Length; j++)
                    {
                        if(!Finite(n.samples[j].x) || !Finite(n.samples[j].y))
                        {
                            Error(np, "non-finite sample coordinate");
                        }

                        for(int k = 0; k < j; k++)
                        {
                            if(n.samples[j].Equals(n.samples[k]))
                            {
                                Error(np, "duplicate sample coordinate");
                            }
                        }
                    }
                }

                if(n.kind == UAnimPoseKind.Mixer)
                {
                    if(n.inputs.Length == 0 || n.inputs.Length != n.weights.Length)
                    {
                        Error(np, "mixer inputs and weights must match");
                    }

                    foreach(var w in n.weights)
                    {
                        Value(w, np, true);
                        if(w != null && w.constant < 0)
                        {
                            Error(np, "negative mixer weight");
                        }
                    }
                }

                if(n.kind == UAnimPoseKind.StateMachine)
                {
                    if(state)
                    {
                        Error(np, "nested state machines are not supported");
                    }
                    else
                    {
                        Machine(n.machine, np);
                    }
                }
            }

            Walk(g.output);
            var contributing = new HashSet<int>(visited);
            for(int i = 0; i < g.nodes.Length; i++)
            {
                Walk(i);
                if(!contributing.Contains(i))
                {
                    Warnings.Add(path + "/Node " + i + ": does not contribute to output");
                }
            }

            stack.Remove(g);
            void Walk(int i)
            {
                if(i < 0 || i >= g.nodes.Length || g.nodes[i] == null)
                {
                    Error(path, "missing pose input/output");
                    return;
                }

                if(visiting.Contains(i))
                {
                    Error(path, "pose cycle");
                    return;
                }

                if(!visited.Add(i))
                {
                    return;
                }

                visiting.Add(i);
                foreach(var input in g.nodes[i].inputs)
                {
                    Walk(input);
                }

                visiting.Remove(i);
            }
        }

        void Machine(UAnimMachine m, string path)
        {
            if(!m)
            {
                Error(path, "missing state machine");
                return;
            }

            path += "/" + m.name;
            if(!stack.Add(m))
            {
                Error(path, "cyclic asset reference");
                return;
            }

            if(!string.IsNullOrEmpty(m.importError))
            {
                Error(path, m.importError);
            }

            if(m.defaultState < 0 || m.defaultState >= m.states.Length)
            {
                Error(path, "exactly one default state required");
            }

            var ids = new HashSet<string>();
            for(int i = 0; i < m.states.Length; i++)
            {
                var s = m.states[i];
                if(s == null)
                {
                    Error(path, "missing state");
                    continue;
                }

                if(string.IsNullOrEmpty(s.id) || !ids.Add(s.id))
                {
                    Error(path, "missing or duplicate state ID");
                }

                if(!Finite(s.duration) || s.duration < 0 || !Finite(s.speed) || s.speed < 0)
                {
                    Error(path + "/" + s.name, "invalid duration/speed");
                }

                Graph(s.graph, true, path + "/" + s.name);
            }

            ids.Clear();
            foreach(var t in m.transitions)
            {
                if(t == null)
                {
                    Error(path, "missing transition");
                    continue;
                }

                if(string.IsNullOrEmpty(t.id) || !ids.Add(t.id))
                {
                    Error(path, "missing or duplicate transition ID");
                }

                if(t.from < 0 || t.to < 0 || t.from >= m.states.Length || t.to >= m.states.Length)
                {
                    Error(path, "invalid transition endpoint");
                    continue;
                }

                if(!Finite(t.duration) || t.duration < 0 || !Finite(t.exitTime) || t.exitTime < 0 || t.exitTime > 1)
                {
                    Error(path, "invalid transition duration/exit time");
                }

                if(t.hasExitTime && m.states[t.from].duration <= 0)
                {
                    Error(path, "exit time requires positive state duration");
                }

                if(!string.IsNullOrEmpty(t.evaluation))
                {
                    try
                    {
                        TransitionEvaluationRegistry.Resolve(t.evaluation);
                    }
                    catch(Exception e)
                    {
                        Error(path, e.Message);
                    }

                    if(t.conditions.Length != 0 || t.hasExitTime)
                    {
                        Error(path, "Code conditions cannot combine with field or Exit Time conditions");
                    }
                }

                foreach(var c in t.conditions)
                {
                    if(c == null)
                    {
                        Error(path, "missing condition");
                        continue;
                    }

                    Value(new UAnimValue { parameterId = c.parameterId, type = c.type }, path);
                    if(!Finite(c.value))
                    {
                        Error(path, "non-finite condition value");
                    }

                    if(string.IsNullOrEmpty(c.parameterId))
                    {
                        Error(path, "condition requires parameter ID");
                    }

                    if((c.type == UAnimParameterType.Bool || c.type == UAnimParameterType.Trigger) && c.comparison != UAnimComparison.Equal && c.comparison != UAnimComparison.NotEqual)
                    {
                        Error(path, "boolean conditions require Equal/NotEqual");
                    }
                }
            }

            var reached = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(m.defaultState);
            while(queue.Count > 0)
            {
                int i = queue.Dequeue();
                if(!reached.Add(i))
                {
                    continue;
                }

                foreach(var t in m.transitions)
                {
                    if(t != null && t.from == i)
                    {
                        queue.Enqueue(t.to);
                    }
                }
            }

            for(int i = 0; i < m.states.Length; i++)
            {
                if(!reached.Contains(i))
                {
                    Warnings.Add(path + "/State " + i + ": unreachable");
                }
            }

            stack.Remove(m);
        }
    }
}
