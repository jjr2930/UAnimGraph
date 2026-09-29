using UAnimGraph.Runtime;
using UnityEditor;

namespace UAnimGraph.Editor
{
    [CustomEditor(typeof(UAnimGraphComponent))]
    public class UAnimGraphComponentEditor : UnityEditor.Editor
    {
        // 캐시된 에셋 에디터
        UnityEditor.Editor cachedAssetEditor;

        public override void OnInspectorGUI()
        {
            // 1. 기본 Inspector GUI를 먼저 그립니다.
            base.OnInspectorGUI();

            // 2. graphComponent.GraphAsset 에디터를 그립니다.
            UAnimGraphComponent graphComponent = (UAnimGraphComponent)target;
            UAnimGraphAsset currentAsset = graphComponent.GraphAsset;

            if (currentAsset != null)
            {
                // 에셋이 바뀌지 않았으면 기존 에디터를 재사용합니다.
                CreateCachedEditor(currentAsset, null, ref cachedAssetEditor);

                if (cachedAssetEditor != null)
                {
                    cachedAssetEditor.DrawHeader();
                    // 에셋의 인스펙터를 컴포넌트 인스펙터 안에 렌더링
                    cachedAssetEditor.OnInspectorGUI();
                }
            }
        }

        void OnDisable()
        {
            if (cachedAssetEditor != null)
            {
                DestroyImmediate(cachedAssetEditor);
                cachedAssetEditor = null;
            }

        }
    }
}
