using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class Layer : PlayableElement
    {
        [SerializeField] string StateMachineId;
        [SerializeField] string TypeName = typeof(Layer).FullName;
        StateMachine stateMachine;

        public override void OnPostDeserialization(UAnimGraphAsset asset)
        {
            base.OnPostDeserialization(asset);

            stateMachine = asset.GetElementById<StateMachine>(StateMachineId);
        }

        public override void BuildPlayable(PlayableGraph playableGraph)
        {
            runtimePlayable = AnimationLayerMixerPlayable.Create(playableGraph);
            stateMachine.BuildPlayable(playableGraph);

            // Connect the state machine's playable to the layer mixer
            AnimationLayerMixerPlayable layerMixer = (AnimationLayerMixerPlayable)runtimePlayable;
            layerMixer.ConnectInput(0, stateMachine.GetPlayable<AnimationMixerPlayable>(), 0);
        }

        public override void Evaluate(float time)
        {
            stateMachine.Evaluate(time);
        }
    }
}
