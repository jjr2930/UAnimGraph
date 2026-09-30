using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class Mixer1D : Element
    {
        [Serializable]
        public class Mixer1DElement
        {
            [SerializeField] AnimationSequence sequence;
            [SerializeField] float peekPoint;

            public float Evaluate(float alpha)
            {

            }
        }

        [SerializeField] AnimationSequence[] sequences;
        [Range(0f, 1f)]
        [SerializeField] float alpha;
        [SerializeField] AnimationCurve curve;

        AnimationMixerPlayable mixerPlayable;
        public AnimationMixerPlayable BuildMixer1D(PlayableGraph graph)
        {
            mixerPlayable = AnimationMixerPlayable.Create(graph, sequences.Length);

            // Create two AnimationClipPlayable playables, then connect them to the mixer.
            for (int i = 0; i < sequences.Length; i++)
            {
                var clipPlayable = AnimationClipPlayable.Create(graph, sequences[i].GetClip());
                graph.Connect(clipPlayable, 0, mixerPlayable, i);
            }

            return mixerPlayable;
        }

        public void Update(float deltaTime)
        {
            if (!mixerPlayable.IsValid())
                return;
        }
    }
}
