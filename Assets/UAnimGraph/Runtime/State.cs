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
        [SerializeField] List<string> elementIds;
        [SerializeField] string TypeName = typeof(State).FullName;

        List<Mixer1D> mixer1ds;

        List<ClipPlayer> clipPlayers;

        string rootElementId;

        public override void BuildPlayable(PlayableGraph playableGraph)
        {
            foreach (PlayableElement element in mixer1ds)
            {
                element.BuildPlayable(playableGraph);
            }

            runtimePlayable = GetRootElement().GetPlayable<AnimationMixerPlayable>();
        }

        public override void Evaluate(float time)
        {
            foreach (PlayableElement element in mixer1ds)
            {
                element.Evaluate(time);
            }
        }

        PlayableElement GetRootElement()
        {
            foreach (PlayableElement element in mixer1ds)
            {
                if (element.Id == rootElementId)
                {
                    return element;
                }
            }
            throw new InvalidOperationException($"Root element with ID {rootElementId} not found.");
        }

        public void AddMixer1D()
        {
            Mixer1D mixer1D = new Mixer1D();
            mixer1ds.Add(mixer1D);
        }

        public void AddClipPlayer()
        {
            ClipPlayer clipPlayer = new ClipPlayer();
            clipPlayers.Add(clipPlayer);
        }

        public override void OnPostDeserialization(UAnimGraphAsset asset)
        {
            base.OnPostDeserialization(asset);
        }
    }
}
