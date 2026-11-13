using System;
using UnityEngine;

namespace UAnimGraph
{
    public enum UAnimParameterType
    {
        Float,
        Int,
        Bool,
        Trigger,
        Vector2
    }

    public enum UAnimPoseKind
    {
        Clip,
        Blend1D,
        Mixer,
        StateMachine,
        Blend2D
    }

    public enum UAnimComparison
    {
        Equal,
        NotEqual,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual
    }

    [Serializable]
    public sealed class UAnimParameter
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "Parameter";
        public UAnimParameterType type;
        public double defaultValue;
        public Vector2 defaultVector2;
    }

    [Serializable]
    public sealed class UAnimLayer
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "Base Layer";
        public bool enabled = true;
        [Range(0, 1)]
        public float weight = 1;
        public bool additive;
        public AvatarMask mask;
        public UAnimPoseGraph graph;
    }

    [Serializable]
    public sealed class UAnimValue
    {
        public string parameterId;
        public UAnimParameterType type;
        public float constant;
        public Vector2 vector;
    }

    [Serializable]
    public sealed class UAnimPoseNode
    {
        public string id;
        public UAnimPoseKind kind;
        public AnimationClip clip;
        public float speed = 1;
        public bool loop = true;
        public float offset;
        public int[] inputs = Array.Empty<int>();
        public UAnimValue value = new UAnimValue();
        public float[] thresholds = Array.Empty<float>();
        public Vector2[] samples = Array.Empty<Vector2>();
        public string[] inputIds = Array.Empty<string>();
        public bool independentTime;
        public UAnimValue valueY;
        public UAnimValue[] weights = Array.Empty<UAnimValue>();
        public UAnimMachine machine;
    }

    [Serializable]
    public sealed class UAnimCondition
    {
        public string parameterId;
        public UAnimParameterType type;
        public UAnimComparison comparison;
        public double value;
    }

    [Serializable]
    public sealed class UAnimTransition
    {
        public string id = Guid.NewGuid().ToString("N");
        public int from, to, priority;
        public float duration = .2f;
        public bool hasExitTime;
        [Range(0, 1)]
        public float exitTime = 1;
        public bool allowSelf;
        public string evaluation;
        public UAnimCondition[] conditions = Array.Empty<UAnimCondition>();
    }

    [Serializable]
    public sealed class UAnimState
    {
        public string id = Guid.NewGuid().ToString("N");
        public string name = "State";
        public UAnimPoseGraph graph;
        public float duration = 1;
        public float speed = 1;
    }

    public sealed class UAnimAsset : ScriptableObject
    {
        public int schemaVersion = 1;
        public string id = Guid.NewGuid().ToString("N");
        public UAnimParameter[] parameters = Array.Empty<UAnimParameter>();
        public UAnimLayer[] layers =
        {
            new UAnimLayer()
        };
        [HideInInspector]
        public string compiledDependencyHash;
        [HideInInspector]
        public string importError;
    }
}
