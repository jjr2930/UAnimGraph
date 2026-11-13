using System;
using UnityEngine;

namespace UAnimGraph
{
    // Runtime view of one instance's shared parameters, not a global value store.
    public sealed class SharedBlackboard
    {
        readonly UAnimInstance instance;

        internal SharedBlackboard(UAnimInstance instance)
        {
            this.instance = instance;
        }

        public float GetFloat(string key)
        {
            return (float)instance.ContextValue(key, UAnimParameterType.Float);
        }

        public int GetInt(string key)
        {
            return (int)instance.ContextValue(key, UAnimParameterType.Int);
        }

        public bool GetBool(string key)
        {
            return instance.ContextValue(key, UAnimParameterType.Bool) != 0;
        }

        public bool GetTrigger(string key)
        {
            return instance.ContextValue(key, UAnimParameterType.Trigger) != 0;
        }

        public Vector2 GetVector2(string key)
        {
            return instance.ContextVector(key);
        }

        public void SetFloat(string key, float value)
        {
            instance.Set(key, UAnimParameterType.Float, value);
        }

        public void SetInt(string key, int value)
        {
            instance.Set(key, UAnimParameterType.Int, value);
        }

        public void SetBool(string key, bool value)
        {
            instance.Set(key, UAnimParameterType.Bool, value ? 1 : 0);
        }

        public void SetTrigger(string key)
        {
            instance.Set(key, UAnimParameterType.Trigger, 1);
        }

        public void ResetTrigger(string key)
        {
            instance.Set(key, UAnimParameterType.Trigger, 0);
        }

        public void SetVector2(string key, Vector2 value)
        {
            instance.SetVector2(key, value);
        }
    }
}
