using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    public abstract class MixerBase<WeightThresholdType> : Element
    {
        public class MixerElement
        {
            [SerializeField] AnimationClip animationClip;
            [SerializeField] WeightThresholdType threshold;
            [SerializeField] float localTimeScale;

            public AnimationClip AnimationClip => animationClip;
            public WeightThresholdType Threshold => threshold;

            public MixerElement(AnimationClip animationClip, WeightThresholdType threshold)
            {
                this.animationClip = animationClip;
                this.threshold = threshold;
            }
        }

        [Serializable]
        public class RuntimeMixerElement
        {
            [SerializeField] AnimationClipPlayable clipPlayable;
            [SerializeField] WeightThresholdType threshold;
            [SerializeField] float localTimeScale;

            public AnimationClipPlayable ClipPlayable => clipPlayable;
            public WeightThresholdType Threshold => threshold;
            public float LocalTimeScale => localTimeScale;

            public RuntimeMixerElement(AnimationClipPlayable clipPlayable, WeightThresholdType threshold)
            {
                this.clipPlayable = clipPlayable;
                this.threshold = threshold;
                this.localTimeScale = 1.0f;
            }
        }
        [SerializeField] protected float localTimeScale = 1.0f;
        [SerializeField] protected RuntimeMixerElement[] runtimeInputs;

        public abstract AnimationMixerPlayable BuildMixer(PlayableGraph playableGraph);
        public abstract void SetWeight(WeightThresholdType weight);

        public void SetTime(int portNumber, float time)
        {
            if (runtimeInputs == null || portNumber < 0 || portNumber >= runtimeInputs.Length)
                throw new ArgumentOutOfRangeException(nameof(portNumber), "Invalid port number.");

            AnimationClipPlayable clipPlayable = runtimeInputs[portNumber].ClipPlayable;
            float clipLocalTimeScale = runtimeInputs[portNumber].LocalTimeScale;
            if (!clipPlayable.IsValid())
                throw new InvalidOperationException($"ClipPlayable at port {portNumber} is not valid.");

            clipPlayable.SetTime(time * clipLocalTimeScale * localTimeScale);
        }

        public void SetAllTime(float time)
        {
            if (runtimeInputs == null)
                throw new InvalidOperationException("Runtime inputs are not initialized.");

            for (int i = 0; i < runtimeInputs.Length; i++)
            {
                SetTime(i, time);
            }
        }
    }
}
