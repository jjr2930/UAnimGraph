using System;
using UnityEngine;

namespace UAnimGraph
{
    [CreateAssetMenu(menuName = "UAnimGraph/Runtime/State Machine")]
    public sealed class UAnimMachine : ScriptableObject
    {
        public string id;
        public int defaultState = -1;
        public UAnimState[] states = Array.Empty<UAnimState>();
        public UAnimTransition[] transitions = Array.Empty<UAnimTransition>();
        [HideInInspector]
        public string importError;
    }
}
