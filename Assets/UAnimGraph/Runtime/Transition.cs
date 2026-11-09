using System;
using UnityEngine;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class Transition : Element
    {
        [SerializeField] string fromStateId;
        [SerializeField] string toStateId;
        [SerializeField] string TypeName = typeof(Transition).FullName;

        State fromState;
        State toState;

        public override void OnPostDeserialization(UAnimGraphAsset asset)
        {
            base.OnPostDeserialization(asset);

            fromState = asset.GetElementById<State>(fromStateId);
            toState = asset.GetElementById<State>(toStateId);
        }
    }
}
