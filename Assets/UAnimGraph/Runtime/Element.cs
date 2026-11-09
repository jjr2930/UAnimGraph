using System;
using UnityEngine;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class Element
    {
        [SerializeField] private string id = Guid.NewGuid().ToString();
        [SerializeField] protected string displayName;
        [SerializeField] protected RuntimeBlackboard blackboard;
        [SerializeField] protected GameObject owner;
        [SerializeField] protected UAnimGraphComponent component;
        public string Id { get => id; }

        public virtual void OnPostDeserialization(UAnimGraphAsset asset) { }
    }
}
