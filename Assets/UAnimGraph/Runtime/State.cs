using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class State : PlayableElement
    {
        [SerializeReference]
        List<PlayableElement> playableElements;

        string rootElementId;

        public override void BuildPlayable(PlayableGraph playableGraph)
        {
            foreach (PlayableElement element in playableElements)
            {
                element.BuildPlayable(playableGraph);
            }

            runtimePlayable = GetRootElement().GetPlayable<AnimationMixerPlayable>();
        }

        public override void Evaluate(float time)
        {
            foreach (PlayableElement element in playableElements)
            {
                element.Evaluate(time);
            }
        }

        PlayableElement GetRootElement()
        {
            foreach (PlayableElement element in playableElements)
            {
                if (element.Id == rootElementId)
                {
                    return element;
                }
            }
            throw new InvalidOperationException($"Root element with ID {rootElementId} not found.");
        }


    }
}
