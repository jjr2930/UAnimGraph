using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using Unity.GraphToolkit.Editor.GraphVisualization;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    // Owned by the explicitly selected workbench target. Never attaches implicitly to
    // every player sharing an asset; closing/changing targets removes all overlays.
    public sealed class UAnimGraphDebugView : IDisposable
    {
        readonly Dictionary<string, Context> contexts = new Dictionary<string, Context>();
        readonly Dictionary<string, float> activity = new Dictionary<string, float>();
        public int ContextCount
        {
            get
            {
                return contexts.Count;
            }
        }

        static bool TryHash(string text, out Hash128 hash)
        {
            hash = default;
            if(string.IsNullOrEmpty(text) || text.Length != 32)
            {
                return false;
            }

            for(int i = 0; i < text.Length; i++)
            {
                if(!Uri.IsHexDigit(text[i]))
                {
                    return false;
                }
            }

            hash = Hash128.Parse(text);
            return hash.isValid;
        }

        Context For(string id)
        {
            if(!TryHash(id, out var hash))
            {
                return null;
            }

            if(!contexts.TryGetValue(id, out var context))
            {
                context = Registry.CreateVisualizationContext(hash);
                contexts.Add(id, context);
            }

            return context;
        }

        public void Apply(UAnimInstance instance)
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>().OfType<IGraphWindow>().ToArray();
            activity.Clear();
            foreach(var pose in instance.Poses)
            {
                Accumulate(pose);
            }

            foreach(var machine in instance.Machines)
            {
                Accumulate(machine);
                var window = windows.FirstOrDefault(w => w.StateMachine?.ID.ToString() == machine.Definition.id);
                if(window == null)
                {
                    continue;
                }

                var context = For(machine.Definition.id);
                if(context == null)
                {
                    continue;
                }

                for(int i = 0; i < machine.Definition.states.Length; i++)
                {
                    if(!TryHash(machine.Definition.states[i].id, out var stateId))
                    {
                        continue;
                    }

                    if(!window.StateMachine.GetStates().Any(s => s.ID == stateId))
                    {
                        continue;
                    }

                    var state = context.GetStateReference(stateId);
                    float weight = machine.ActivityWeight <= 0 ? 0 : i == machine.CurrentState ? 1 - machine.TransitionProgress : i == machine.DestinationState ? machine.TransitionProgress : 0;
                    state.FillAmount = weight * 100;
                    if(weight > 0)
                    {
                        state.FillAmount = weight * 100;
                        context.Motion.Play(state);
                    }
                    else
                    {
                        context.Motion.Stop(state);
                    }
                }

                foreach(var source in window.StateMachine.GetStates())
                {
                    foreach(var t in source.GetOutgoingTransitions())
                    {
                        var transition = context.GetTransitionReference(t.ID);
                        if(machine.ActivityWeight > 0 && t.ID.ToString() == machine.ActiveTransitionId)
                        {
                            transition.LineColor = new Color(.2f, 1f, .55f);
                            transition.FillColor = new Color(.2f, 1f, .55f);
                            transition.Opacity = 1;
                            context.Motion.Play(transition);
                        }
                        else
                        {
                            transition.ClearCustomization();
                        }
                    }
                }
            }

            foreach(var window in windows)
            {
                var graph = window.Graph;
                if(graph == null || (graph is not UAnimLayerGraph && graph is not UAnimStateGraph))
                {
                    continue;
                }

                var context = For(graph.ID.ToString());
                foreach(var source in graph.GetNodes())
                {
                    activity.TryGetValue(graph.ID + "/" + source.ID, out float weight);
                    var node = context.GetNodeReference(source.ID);
                    node.FillAmount = weight * 100;
                    if(weight > 0)
                    {
                        context.Motion.Play(node);
                    }
                    else
                    {
                        context.Motion.Stop(node);
                    }
                }
            }

            void Accumulate(UAnimInstance.Pose pose)
            {
                string key = pose.GraphId + "/" + pose.Id;
                activity.TryGetValue(key, out float previous);
                float weight = Mathf.Clamp01(previous + pose.Activity);
                activity[key] = weight;
            }
        }

        public void Dispose()
        {
            foreach(var c in contexts.Values)
            {
                // Unity 7000 alpha can detach a view before its context is disposed.
                // There is no view to clear in that case; never keep a dead context.
                try
                {
                    c.Dispose();
                }
                catch(ArgumentException)
                {
                }
            }

            contexts.Clear();
        }
    }
}
