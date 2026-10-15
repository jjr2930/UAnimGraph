using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Assertions;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    /// <summary>
    /// TODO: Inherit from MixerBase
    /// in normalize space, 0~1, 0 is first sequence's threshold, 1 is last sequence's threshold
    /// </summary>
    [Serializable]
    public class Mixer1D : MixerBase<float>
    {
        [SerializeField] MinMaxFloat weightMinMax;
        public override void BuildPlayable(PlayableGraph playableGraph)
        {
            mixerPlayable = AnimationMixerPlayable.Create(playableGraph, inputs.Length);

            for (int i = 0; i < inputs.Length; i++)
            {
                var playableElement = inputs[i].PlayableElement;
                playableElement.BuildPlayable(playableGraph);
                AnimationClipPlayable clipPlayable
                    = playableElement.GetPlayable<AnimationClipPlayable>();
                playableGraph.Connect(clipPlayable, 0, mixerPlayable, i);
            }

            for (int i = 0; i < inputs.Length; i++)
            {
                mixerPlayable.SetInputWeight(i, 0f);
            }
        }

        public override void Evaluate(float time)
        {
            Assert.IsTrue(mixerPlayable.IsValid(), "MixerPlayable is not valid.");

            float clampedTime = weightMinMax.Clamp(time);

            AnimationClipPlayable left;
            AnimationClipPlayable right;


            for (int i = 0; i < inputs.Length; i++)
            {
                mixerPlayable.SetInputWeight(i, 0f);
            }

            for (int i = 0; i < inputs.Length - 1; i++)
            {
                if (inputs[i].Threshold <= clampedTime && clampedTime <= inputs[i + 1].Threshold)
                {
                    left = inputs[i].PlayableElement.GetPlayable<AnimationClipPlayable>();
                    right = inputs[i + 1].PlayableElement.GetPlayable<AnimationClipPlayable>();

                    float normalizedValue = Mathf.InverseLerp(inputs[i].Threshold, inputs[i + 1].Threshold, clampedTime);
                    mixerPlayable.SetInputWeight(i, 1f - normalizedValue);
                    mixerPlayable.SetInputWeight(i + 1, normalizedValue);
                    break;
                }
            }
        }
    }
}
