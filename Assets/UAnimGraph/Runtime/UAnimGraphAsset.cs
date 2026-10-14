using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Assertions;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    [CreateAssetMenu(fileName = "New UAnimGraph Asset", menuName = "UAnimGraph/Asset", order = 0)]
    public class UAnimGraphAsset : ScriptableObject
    {
        [SerializeField] Mixer2D mixer2D;

        [SerializeField, Range(-1, 1)]
        float testX = 0f;

        [SerializeField, Range(-1, 1)]
        float testY = 0f;

        [SerializeField, Range(0f, 5f)]
        float testTime = 0f;
        [SerializeField]
        int testClipIndex = 0;

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

            mixerPlayable = mixer2D.BuildMixer2D(playableGraph);
            mixerPlayable.SetSpeed(0f);

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
            mixer2D.SetWeights(new Vector2(testX, testY));
            mixer2D.SetAllTime(elapsedTime);

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
