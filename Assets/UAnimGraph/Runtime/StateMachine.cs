using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class StateMachine : PlayableElement
    {
        //[SerializeField] List<State> states;
        //[SerializeField] List<Transition> transitions;
        [SerializeField] State state;
        public override void BuildPlayable(PlayableGraph playableGraph)
        {
            runtimePlayable = state.GetPlayable<AnimationMixerPlayable>();
        }

        public override void Evaluate(float time)
        {
            state.Evaluate(time);
        }
    }
}
