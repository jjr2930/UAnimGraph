using System;
using System.Collections.Generic;
using UnityEngine;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class StateMachine : Element
    {
        [SerializeField] List<State> states = new List<State>();
        [SerializeField] List<Transition> transitions = new List<Transition>();
        [SerializeField] List<StateMachine> subStateMachines = new List<StateMachine>();
    }
}
