using System;
using UnityEngine;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class Transition : Element
    {
        [SerializeField] string fromStateId;
        [SerializeField] string toStateId;
    }
}
