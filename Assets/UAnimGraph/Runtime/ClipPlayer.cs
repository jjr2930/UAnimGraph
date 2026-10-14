using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    //[CreateAssetMenu(fileName = "New Animation Sequence", menuName = "UAnimGraph/Animation Sequence", order = 1)]
    [Serializable]
    public class ClipPlayer
    {
        //[SerializeField] float duration;
        //[SerializeField] float playbackSpeed;
        [SerializeField] AnimationClip animationClip;
        //[SerializeReference] AnimationNotifyBase[] notifies;
        //[SerializeField] AnimationCurve curve;
        //[SerializeField] float startTime;
        [SerializeField] bool loop;

        AnimationClipPlayable clipPlayable;

        public AnimationClip GetClip()
        {
            return animationClip;
        }

        public AnimationClipPlayable BuildClipPlayable(PlayableGraph playableGraph)
        {
            clipPlayable = AnimationClipPlayable.Create(playableGraph, animationClip);
            return clipPlayable;
        }

        public void SetTime(float time)
        {
            if (!clipPlayable.IsValid())
            {
                Debug.LogError("ClipPlayable is not valid.");
                return;
            }

            float finalTime = loop ? Mathf.Repeat(time, animationClip.length)
                                    : Mathf.Clamp(time, 0f, animationClip.length);

            clipPlayable.SetTime(finalTime);
        }
    }
}
