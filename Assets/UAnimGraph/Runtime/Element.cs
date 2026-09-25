using System;
using UnityEngine;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class Element
    {
        [SerializeField] protected string id;
        [SerializeField] protected string displayName;
        [SerializeField] protected RuntimeBlackboard blackboard;
        [SerializeField] protected GameObject owner;
        [SerializeField] protected UAnimGraphComponent component;
    }
}
