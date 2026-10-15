using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class ClipPlayer : PlayableElement
    {
        //[SerializeField] float duration;
        //[SerializeField] float playbackSpeed;
        [SerializeField] AnimationClip animationClip;
        //[SerializeReference] AnimationNotifyBase[] notifies;
        //[SerializeField] AnimationCurve curve;
        //[SerializeField] float startTime;
        [SerializeField] bool loop;

        AnimationClipPlayable clipPlayable;

        ~ClipPlayer()
        {
            clipPlayable.Destroy();
        }

        public AnimationClip GetClip()
        {
            return animationClip;
        }

        public override void BuildPlayable(PlayableGraph playableGraph)
        {
            clipPlayable = AnimationClipPlayable.Create(playableGraph, animationClip);
        }

        public override void Evaluate(float time)
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
