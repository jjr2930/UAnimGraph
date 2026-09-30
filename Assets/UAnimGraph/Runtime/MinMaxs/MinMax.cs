using System;
using UnityEngine;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public abstract class MinMax<T>
    {
        [SerializeField] T min;
        [SerializeField] T max;

        public MinMax(T min, T max)
        {
            this.min = min;
            this.max = max;
        }

        public T Min => min;
        public T Max => max;

        public void SetMinMax(T min, T max)
        {
            this.min = min;
            this.max = max;
        }

        public void SetMin(T min)
        {
            this.min = min;
        }

        public void SetMax(T max)
        {
            this.max = max;
        }

        public abstract T Clamp(T value);
    }
}
