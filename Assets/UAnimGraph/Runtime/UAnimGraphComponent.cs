using UnityEngine;

namespace UAnimGraph.Runtime
{
    public class UAnimGraphComponent : MonoBehaviour
    {
        [SerializeField] UAnimGraphAsset graphAsset;
        [SerializeField] Animator animator;

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
            graphAsset.UpdateGraph(Time.deltaTime);
        }
    }
}