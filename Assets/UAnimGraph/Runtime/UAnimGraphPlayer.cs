using System;
using UnityEngine;

namespace UAnimGraph
{
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class UAnimGraphPlayer : MonoBehaviour
    {
        public UAnimAsset asset;
        public Animator animator;
        public bool applyRootMotion;
        public UAnimInstance Instance
        {
            get;
            private set;
        }
        public string LastError
        {
            get;
            private set;
        }

        void Reset()
        {
            animator = GetComponent<Animator>();
        }

        void OnEnable()
        {
            Restart();
        }

        public void Restart()
        {
            Stop();
            if(!asset)
            {
                return;
            }

            if(!animator)
            {
                animator = GetComponent<Animator>();
            }

            try
            {
                Instance = new UAnimInstance(asset, animator, graphOwner: gameObject);
                animator.applyRootMotion = applyRootMotion;
                LastError = null;
            }
            catch(Exception e)
            {
                LastError = e.Message;
                Debug.LogError("UAnimGraph: " + e.Message, this);
            }
        }

        void Update()
        {
            Instance?.Tick(Time.deltaTime);
        }

        void OnDisable()
        {
            Stop();
        }

        void OnDestroy()
        {
            Stop();
        }

        public void Stop()
        {
            Instance?.Dispose();
            Instance = null;
        }

        public void SetFloat(string idOrName, float value)
        {
            Instance?.Set(idOrName, UAnimParameterType.Float, value);
        }

        public void SetVector2(string idOrName, Vector2 value)
        {
            Instance?.SetVector2(idOrName, value);
        }

        public void SetInt(string idOrName, int value)
        {
            Instance?.Set(idOrName, UAnimParameterType.Int, value);
        }

        public void SetBool(string idOrName, bool value)
        {
            Instance?.Set(idOrName, UAnimParameterType.Bool, value ? 1 : 0);
        }

        public void SetTrigger(string idOrName)
        {
            Instance?.Set(idOrName, UAnimParameterType.Trigger, 1);
        }

        public void ResetTrigger(string idOrName)
        {
            Instance?.Set(idOrName, UAnimParameterType.Trigger, 0);
        }
    }
}
