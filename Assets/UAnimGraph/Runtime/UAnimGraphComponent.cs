using UnityEngine;

namespace UAnimGraph.Runtime
{
    public class UAnimGraphComponent : MonoBehaviour
    {
        [SerializeField] UAnimGraphAsset graphAsset;
        [SerializeField] Animator animator;
        [SerializeField] bool autoPlay = false;

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
            if (autoPlay)
            {
                graphAsset.Play();
            }
        }

        public void Update()
        {
            if (!autoPlay)
            {
                graphAsset.UpdateGraph(Time.deltaTime);
            }
        }
    }
}