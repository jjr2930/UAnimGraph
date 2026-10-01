using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    [CreateAssetMenu(fileName = "New UAnimGraph Asset", menuName = "UAnimGraph/Asset", order = 0)]
    public class UAnimGraphAsset : ScriptableObject
    {
        [SerializeField] Mixer2D mixer2D;

        [SerializeField] Vector2 testValue = Vector2.zero;
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

            playableOutput.SetSourcePlayable(mixerPlayable);
        }

        public void Play()
        {
            if (!playableGraph.IsValid())
                return;

            playableGraph.Play();
        }

        public void UpdateGraph(float deltaTime)
        {
            if (!playableGraph.IsValid())
                return;

            mixer2D.SetWeights(testValue);
            //playableGraph.Evaluate(deltaTime);
            //mixer1D.Update(deltaTime);
        }

        public void OnDestroy()
        {
            playableGraph.Destroy();
        }
    }
}
