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

        public class RuntimeMixer2DElement
        {
            public AnimationClipPlayable ClipPlayable { get; private set; }
            public Vector2 Threshold { get; private set; }
            public RuntimeMixer2DElement(PlayableGraph playableGraph, AnimationClip clipPlayable, Vector2 threshold)
            {
                ClipPlayable = AnimationClipPlayable.Create(playableGraph, clipPlayable);
                Threshold = threshold;
            }
        }

        [SerializeField] List<Mixer2DElement> inputs;
        [SerializeField] MinMaxFloat horizontalMinMax;
        [SerializeField] MinMaxFloat verticalMinMax;

        AnimationMixerPlayable horizontalMixer;
        AnimationMixerPlayable verticalMixer;
        AnimationMixerPlayable combinedMixer;

        List<RuntimeMixer2DElement> runtimeHorizontalInputs = null;
        List<RuntimeMixer2DElement> runtimeVerticalInputs = null;

        public AnimationMixerPlayable BuildMixer2D(PlayableGraph playableGraph)
        {
            runtimeHorizontalInputs = new List<RuntimeMixer2DElement>(inputs.Count);
            runtimeVerticalInputs = new List<RuntimeMixer2DElement>(inputs.Count);

            horizontalMixer = AnimationMixerPlayable.Create(playableGraph, inputs.Count);
            verticalMixer = AnimationMixerPlayable.Create(playableGraph, inputs.Count);
            combinedMixer = AnimationMixerPlayable.Create(playableGraph, 2);

            for (int i = 0; i < inputs.Count; i++)
            {
                RuntimeMixer2DElement horizontalRuntimeElement
                    = new RuntimeMixer2DElement(playableGraph, inputs[i].Sequence.GetClip(), inputs[i].Threshold);
                runtimeHorizontalInputs.Add(horizontalRuntimeElement);

                RuntimeMixer2DElement verticalRuntimeElement
                    = new RuntimeMixer2DElement(playableGraph, inputs[i].Sequence.GetClip(), inputs[i].Threshold);

                runtimeVerticalInputs.Add(verticalRuntimeElement);

                playableGraph.Connect(horizontalRuntimeElement.ClipPlayable, 0, horizontalMixer, i);
                playableGraph.Connect(verticalRuntimeElement.ClipPlayable, 0, verticalMixer, i);
            }

            playableGraph.Connect(horizontalMixer, 0, combinedMixer, 0);
            playableGraph.Connect(verticalMixer, 0, combinedMixer, 1);

            combinedMixer.SetInputWeight(0, 0.5f);
            combinedMixer.SetInputWeight(1, 0.5f);

            return combinedMixer;
        }

        public void SetWeights(Vector2 value)
        {
            //1. horizontal weight setting using value
            for (int i = 0; i < runtimeHorizontalInputs.Count - 1; i++)
            {
                if (runtimeHorizontalInputs[i].Threshold.x <= value.x && value.x <= runtimeHorizontalInputs[i + 1].Threshold.x)
                {
                    float weight = Mathf.InverseLerp(horizontalMinMax.Min, horizontalMinMax.Max, value.x);
                    horizontalMixer.SetInputWeight(i, weight);
                    horizontalMixer.SetInputWeight(i + 1, 1f - weight);

                    i++;
                }
                else
                {
                    horizontalMixer.SetInputWeight(i, 0f);
                    horizontalMixer.SetInputWeight(i + 1, 0f);
                }
            }

            //2. vertical weight setting using value
            for (int i = 0; i < runtimeVerticalInputs.Count - 1; i++)
            {
                if (runtimeVerticalInputs[i].Threshold.y <= value.y && value.y <= runtimeVerticalInputs[i + 1].Threshold.y)
                {
                    float weight = Mathf.InverseLerp(verticalMinMax.Min, verticalMinMax.Max, value.y);
                    verticalMixer.SetInputWeight(i, weight);
                    verticalMixer.SetInputWeight(i + 1, 1f - weight);

                    i++;
                }
                else
                {
                    verticalMixer.SetInputWeight(i, 0f);
                    verticalMixer.SetInputWeight(i + 1, 0f);
                }
            }
        }
    }
}
