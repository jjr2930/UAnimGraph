using UnityEngine;

namespace UAnimGraph.Runtime
{
    public class UAnimGraphComponent : MonoBehaviour
    {
        [SerializeField] UAnimGraphAsset graphAsset;
        [SerializeField] Animator animator;
        [SerializeField] float selfTimeScale = 1f;
        public UAnimGraphAsset GraphAsset => graphAsset;

        public void Reset()
        {
            animator = GetComponent<Animator>();
        }

        public void Awake()
        {
            graphAsset.BuildPlayableGraph(animator);
        }

        public void Start()
        {
            graphAsset.Play();
        }

        public void Update()
        {
            graphAsset.EvaluateGraph(Time.deltaTime * selfTimeScale);
        }
    }
}