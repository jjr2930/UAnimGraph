using System;
using UnityEngine;

namespace UAnimGraph
{
    // Value snapshot: retaining a context never changes its recorded State times.
    public readonly struct State
    {
        public readonly UAnimState Definition;
        public readonly double Time;
        internal State(UAnimState definition, double time)
        {
            Definition = definition;
            Time = time;
        }

        public string Id
        {
            get
            {
                return Definition?.id;
            }
        }

        public string Name
        {
            get
            {
                return Definition?.name;
            }
        }

        public double TotalNormalizedTime
        {
            get
            {
                return Definition != null && Definition.duration > 0 ? Time / Definition.duration : 0;
            }
        }

        public float NormalizedTime
        {
            get
            {
                if(Definition?.graph != null)
                {
                    foreach(var node in Definition.graph.nodes)
                    {
                        if(node.kind == UAnimPoseKind.Clip && node.loop)
                        {
                            return (float)(TotalNormalizedTime - Math.Floor(TotalNormalizedTime));
                        }
                    }
                }
                return Mathf.Clamp01((float)TotalNormalizedTime);
            }
        }
    }
}
