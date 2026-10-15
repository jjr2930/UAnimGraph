using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Assertions;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    [CreateAssetMenu(fileName = "New UAnimGraph Asset", menuName = "UAnimGraph/Asset", order = 0)]
    public class UAnimGraphAsset : ScriptableObject
    {
        [SerializeField] Layer TestLayer;

        float elapsedTime = 0f;

        Animator targetAnimator;
        PlayableGraph playableGraph;
        AnimationMixerPlayable mixerPlayable;

        public void BuildPlayableGraph(Animator animator)
        {
            targetAnimator = animator;

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

            elapsedTime += deltaTime;

            playableGraph.Evaluate(0f);
        }

        public void ResetElapsedTime()
        {
            elapsedTime = 0f;
        }

        public void OnDestroy()
        {
            playableGraph.Destroy();
        }
    }
}
