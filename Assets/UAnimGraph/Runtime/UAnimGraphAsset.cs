using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UAnimGraph.Runtime
{
    [CreateAssetMenu(fileName = "New UAnimGraph Asset", menuName = "UAnimGraph/Asset", order = 0)]
    public class UAnimGraphAsset : ScriptableObject
    {
        [SerializeField] Mixer1D mixer1D;
        [Range(0f, 2f)]
        [SerializeField] float testWeight = 0f;
        Animator targetAnimator;
        PlayableGraph playableGraph;
        AnimationMixerPlayable mixerPlayable;

        public void BuildPlayableGraph(Animator animator)
        {
            targetAnimator = animator;

            playableGraph = PlayableGraph.Create("UAnimGraphPlayableGraph");

            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            var playableOutput = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);

            mixerPlayable = mixer1D.BuildMixer1D(playableGraph);

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

            mixer1D.SetWeight(testWeight);
            //playableGraph.Evaluate(deltaTime);
            //mixer1D.Update(deltaTime);
        }

        public void OnDestroy()
        {
            playableGraph.Destroy();
        }
    }
}
