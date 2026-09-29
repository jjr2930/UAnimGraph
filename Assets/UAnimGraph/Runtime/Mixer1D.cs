using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class Mixer1D : Element
    {
        [SerializeField] AnimationSequence sequence1;
        [SerializeField] AnimationSequence sequence2;
        [Range(0f, 1f)]
        [SerializeField] float alpha;
        AnimationMixerPlayable mixerPlayable;
        public AnimationMixerPlayable BuildMixer1D(PlayableGraph graph)
        {
            mixerPlayable = AnimationMixerPlayable.Create(graph, 2);

            // Create two AnimationClipPlayable playables, then connect them to the mixer.
            var clipPlayable0 = AnimationClipPlayable.Create(graph, sequence1.GetClip());
            var clipPlayable1 = AnimationClipPlayable.Create(graph, sequence2.GetClip());

            graph.Connect(clipPlayable0, 0, mixerPlayable, 0);
            graph.Connect(clipPlayable1, 0, mixerPlayable, 1);

            return mixerPlayable;
        }

        public void Update(float deltaTime)
        {
            if (!mixerPlayable.IsValid())
                return;

            mixerPlayable.SetInputWeight(0, 1f - alpha);
            mixerPlayable.SetInputWeight(1, alpha);
        }
    }
}
