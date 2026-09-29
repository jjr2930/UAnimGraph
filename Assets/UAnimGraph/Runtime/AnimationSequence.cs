using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    [CreateAssetMenu(fileName = "New Animation Sequence", menuName = "UAnimGraph/Animation Sequence", order = 1)]
    [Serializable]
    public class AnimationSequence : ScriptableObject
    {
        [SerializeField] float duration;
        [SerializeField] float playbackSpeed;
        [SerializeField] AnimationClip animationClip;
        [SerializeReference] AnimationNotifyBase[] notifies;
        [SerializeField] AnimationCurve curve;
        [SerializeField] float startTime;
        [SerializeField] bool loop;

        AnimationClipPlayable clipPlayable;

        public AnimationClip GetClip()
        {
            return animationClip;
        }

        public AnimationClipPlayable GetClipPlayable(PlayableGraph playableGraph)
        {
            clipPlayable = AnimationClipPlayable.Create(playableGraph, animationClip);
            return clipPlayable;
        }
    }
}
