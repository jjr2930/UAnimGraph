using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class StateMachine : PlayableElement
    {
        [SerializeField] List<string> stateIds = new List<string>();
        [SerializeField] List<string> transitionIds = new List<string>();
        [SerializeField] string TypeName = typeof(StateMachine).FullName;
        List<State> states;
        List<Transition> transitions;

        public override void BuildPlayable(PlayableGraph playableGraph)
        {
            //runtimePlayable = state.GetPlayable<AnimationMixerPlayable>();
        }

        public override void Evaluate(float time)
        {
            //state.Evaluate(time);
        }

        public override void OnPostDeserialization(UAnimGraphAsset asset)
        {
            base.OnPostDeserialization(asset);

            foreach (string stateId in stateIds)
            {
                State state = asset.GetElementById<State>(stateId);
                if (state != null)
                {
                    states.Add(state);
                    state.OnPostDeserialization(asset);
                }
            }

            foreach (string transitionId in transitionIds)
            {
                Transition transition = asset.GetElementById<Transition>(transitionId);
                if (transition != null)
                {
                    transitions.Add(transition);
                    transition.OnPostDeserialization(asset);
                }
            }
        }
    }
}
