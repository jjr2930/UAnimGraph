using System;
using System.Collections.Generic;
using UnityEngine;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class Layer : Element
    {
        [SerializeField] List<StateMachine> stateMachines = new List<StateMachine>();
        [SerializeReference] List<Mixerbase> mixers = new List<Mixerbase>();
    }
}
