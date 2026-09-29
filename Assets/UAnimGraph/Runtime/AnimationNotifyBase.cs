using System;
using UnityEngine;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class AnimationNotifyBase
    {
        [SerializeField] string notifyName;
        [SerializeField] float absoluteTime;
        [SerializeField] float normalizedTime;

        public string NotifyName
        {
            get => notifyName;
            set => notifyName = value;
        }

        public virtual void OnNotifyBegin()
        {
            // This method is called when the notify begins.
        }
    }
}
