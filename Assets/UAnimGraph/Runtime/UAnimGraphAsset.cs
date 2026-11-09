using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Assertions;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    [CreateAssetMenu(fileName = "New UAnimGraph Asset", menuName = "UAnimGraph/Asset", order = 0)]
    public class UAnimGraphAsset : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeReference]
        List<Element> elements = new List<Element>();
        List<Layer> layers = new List<Layer>();
        PlayableGraph playableGraph;
        AnimationMixerPlayable mixerPlayable;

        public void BuildPlayableGraph(Animator animator)
        {
            playableGraph = PlayableGraph.Create("UAnimGraphPlayableGraph");

            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            var playableOutput = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);

            playableOutput.SetSourcePlayable(mixerPlayable);
        }

        public void Play()
        {
            Assert.IsTrue(playableGraph.IsValid(), "PlayableGraph is not valid. Call BuildPlayableGraph() before Play().");

            playableGraph.Play();
        }

        public void EvaluateGraph(float deltaTime)
        {
            if (!playableGraph.IsValid())
                return;

            playableGraph.Evaluate(0f);
        }

        public void ResetElapsedTime()
        {
        }

        public void OnDestroy()
        {
            playableGraph.Destroy();
        }

        public void OnBeforeSerialize()
        {
            //throw new System.NotImplementedException();
        }

        public void OnAfterDeserialize()
        {
            foreach (Layer layer in layers)
            {
                layer.OnPostDeserialization(this);
            }
        }

        public void InsertLayer()
        {
            elements.Add(new Layer());
        }

        public void InsertStateMachine()
        {
            elements.Add(new StateMachine());
        }

        public void InsertState()
        {
            elements.Add(new State());
        }

        public void InsertTransition()
        {
            elements.Add(new Transition());
        }

        public T GetElementById<T>(string stateMachineId)
            where T : Element
        {
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i].Id == stateMachineId && elements[i] is T)
                {
                    return elements[i] as T;
                }
            }

            return null;
        }
    }
}
