using System;
using UnityEngine;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public abstract class PlayableElement : Element
    {
        [SerializeField] protected float localTimeScale;

        protected IPlayable runtimePlayable;
        public T GetPlayable<T>() where T : struct, IPlayable
        {
            if (runtimePlayable is T typedPlayable)
            {
                return typedPlayable;
            }
            else
            {
                throw new InvalidOperationException($"The runtime playable is not of type {typeof(T).Name}.");
            }
        }
        public abstract void BuildPlayable(PlayableGraph playableGraph);
        public abstract void Evaluate(float time);
    }
}
