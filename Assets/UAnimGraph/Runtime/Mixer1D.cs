using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Assertions;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    /// <summary>
    /// in normalize space, 0~1, 0 is first sequence's threshold, 1 is last sequence's threshold
    /// </summary>
    [Serializable]
    public class Mixer1D : Element
    {
        [Serializable]
        public class Mixer1DElement
        {
            [SerializeField] AnimationSequence sequence;
            [SerializeField] float threshold;

            public AnimationSequence Sequence => sequence;
            public float Threshold => threshold;
        }

        [SerializeField] Mixer1DElement[] inputs;
        [SerializeField] float min;
        [SerializeField] float max;
        [SerializeField] float weight;

        AnimationClipPlayable[] clipPlayables;

        AnimationMixerPlayable mixerPlayable;
        List<int> leftRightIndexes;

        public AnimationMixerPlayable BuildMixer1D(PlayableGraph graph)
        {
            mixerPlayable = AnimationMixerPlayable.Create(graph, inputs.Length);

            clipPlayables = new AnimationClipPlayable[inputs.Length];
            // Create two AnimationClipPlayable playables, then connect them to the mixer.
            for (int i = 0; i < inputs.Length; i++)
            {
                clipPlayables[i] = inputs[i].Sequence.GetClipPlayable(graph);
                graph.Connect(clipPlayables[i], 0, mixerPlayable, i);
            }

            return mixerPlayable;
        }


        public void SetWeight(float value)
        {
            Assert.IsTrue(mixerPlayable.IsValid(), "MixerPlayable is not valid.");

            value = Mathf.Clamp(value, min, max);

            AnimationClipPlayable left;
            AnimationClipPlayable right;

            leftRightIndexes.Clear();
            for (int i = 0; i < inputs.Length - 1; i++)
            {
                if (inputs[i].Threshold <= value && value <= inputs[i + 1].Threshold)
                {
                    left = clipPlayables[i];
                    right = clipPlayables[i + 1];
                    float normalizedValue = Mathf.InverseLerp(inputs[i].Threshold, inputs[i + 1].Threshold, value);
                    mixerPlayable.SetInputWeight(i, 1f - normalizedValue);
                    mixerPlayable.SetInputWeight(i + 1, normalizedValue);

                    leftRightIndexes = new List<int> { i, i + 1 };
                    break;
                }
            }

            for (int i = 0; i < inputs.Length; i++)
            {
                if (!leftRightIndexes.Contains(i))
                {
                    mixerPlayable.SetInputWeight(i, 0f);
                }
            }
        }
        public void Update(float deltaTime)
        {
            if (!mixerPlayable.IsValid())
                return;

            for (int i = 0; i < clipPlayables.Length; i++)
            {
                clipPlayables[i].SetTime(clipPlayables[i].GetTime() + deltaTime);
            }
        }
    }
}
