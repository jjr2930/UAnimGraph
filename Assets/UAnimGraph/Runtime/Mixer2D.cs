using System;
using UnityEngine;
using UnityEngine.Animations;
namespace UAnimGraph.Runtime
{
    [Serializable]
    public class Mixer2D : Element
    {
        [SerializeField] AnimationSequence sequence1;
        [SerializeField] AnimationSequence sequence2;
        public void BuildMixer2D()
        {
            AnimationMixerPlayable mixerPlayable = AnimationMixerPlayable.Create(component.GetComponent<Animator>().playableGraph, 2);

        }
    }
}
