using System;
using UnityEngine;

namespace UAnimGraph
{
    [CreateAssetMenu(menuName = "UAnimGraph/Runtime/Pose Graph")]
    public sealed class UAnimPoseGraph : ScriptableObject
    {
        public int schemaVersion = 1;
        public string id;
        public bool stateGraph;
        public float stateDuration = 1;
        public float stateSpeed = 1;
        public int output = -1;
        public UAnimPoseNode[] nodes = Array.Empty<UAnimPoseNode>();
        [HideInInspector]
        public string importError;
    }
}
