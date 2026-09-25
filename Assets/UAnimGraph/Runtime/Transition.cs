using System;
using UnityEngine;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class Transition : Element
    {
        [SerializeField] State fromState;
        [SerializeField] State toState;
    }
}
