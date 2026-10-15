using System;
using UnityEngine;
using UnityEngine.Animations;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public abstract class MixerBase<WeightThresholdType> : PlayableElement
    {
        public class MixerElement
        {
            [SerializeField] PlayableElement playableElement;
            [SerializeField] WeightThresholdType threshold;
            [SerializeField] float localTimeScale;

            public PlayableElement PlayableElement => playableElement;
            public WeightThresholdType Threshold => threshold;
            public float LocalTimeScale => localTimeScale;
            public MixerElement(PlayableElement playableElement, WeightThresholdType threshold)
            {
                this.playableElement = playableElement;
                this.threshold = threshold;
                this.localTimeScale = 1.0f;
            }
        }

        [SerializeField] protected MixerElement[] inputs;

        protected AnimationMixerPlayable mixerPlayable;

        public void SetTime(int portNumber, float time)
        {
            if (inputs == null || portNumber < 0 || portNumber >= inputs.Length)
                throw new ArgumentOutOfRangeException(nameof(portNumber), "Invalid port number.");

            inputs[portNumber].PlayableElement.Evaluate(time * localTimeScale);
        }

        public void SetAllTime(float time)
        {
            if (inputs == null)
                throw new InvalidOperationException("Inputs are not initialized.");

            for (int i = 0; i < inputs.Length; i++)
            {
                SetTime(i, time);
            }
        }
    }
}
