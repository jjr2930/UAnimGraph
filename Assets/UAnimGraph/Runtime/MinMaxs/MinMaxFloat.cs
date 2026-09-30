using System;
using UnityEngine;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class MinMaxFloat : MinMax<float>
    {
        public MinMaxFloat(float min, float max) : base(min, max)
        {
        }

        public override float Clamp(float value)
        {
            return Mathf.Clamp(value, Min, Max);
        }
    }
}
