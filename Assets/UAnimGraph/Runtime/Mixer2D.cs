using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
namespace UAnimGraph.Runtime
{
    [Serializable]
    public class Mixer2D : Element
    {
        [Serializable]
        public class Mixer2DElement
        {
            [SerializeField] AnimationSequence sequence;
            [SerializeField] Vector2 threshold;
            public AnimationSequence Sequence => sequence;
            public Vector2 Threshold => threshold;
        }

        [SerializeField] List<Mixer2DElement> inputs;
        [SerializeField] MinMaxFloat horizontalMinMax;
        [SerializeField] MinMaxFloat verticalMinMax;

        AnimationMixerPlayable horizontalMixer;
        AnimationMixerPlayable verticalMixer;
        AnimationMixerPlayable combinedMixer;
        public AnimationMixerPlayable BuildMixer2D(PlayableGraph playableGraph)
        {
            horizontalMixer = AnimationMixerPlayable.Create(playableGraph, inputs.Count);
            verticalMixer = AnimationMixerPlayable.Create(playableGraph, inputs.Count);
            combinedMixer = AnimationMixerPlayable.Create(playableGraph, 2);

            for (int i = 0; i < inputs.Count; i++)
            {
                AnimationClipPlayable clipPlayable = inputs[i].Sequence.GetClipPlayable(playableGraph);
                horizontalMixer.ConnectInput(i, clipPlayable, 0);
                verticalMixer.ConnectInput(i, clipPlayable, 0);
            }

            combinedMixer.ConnectInput(0, horizontalMixer, 0);
            combinedMixer.ConnectInput(1, verticalMixer, 0);

            return combinedMixer;
        }

        public void SetWeights(Vector2 value)
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                float horizontalWeight = Mathf.Clamp01(1f - Mathf.Abs(value.x - inputs[i].Threshold.x));
                float verticalWeight = Mathf.Clamp01(1f - Mathf.Abs(value.y - inputs[i].Threshold.y));
                horizontalMixer.SetInputWeight(i, horizontalWeight);
                verticalMixer.SetInputWeight(i, verticalWeight);
            }
        }
    }
}
