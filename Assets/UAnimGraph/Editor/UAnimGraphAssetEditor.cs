using UAnimGraph.Runtime;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Editor
{
    [CustomEditor(typeof(UAnimGraphAsset))]
    public class UAnimGraphAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var CastedTarget = target as UAnimGraphAsset;

            if (GUILayout.Button("Add Layer"))
            {
                CastedTarget.InsertLayer();
                EditorUtility.SetDirty(target);
            }

            if (GUILayout.Button("Add State Machine"))
            {
                CastedTarget.InsertStateMachine();
                EditorUtility.SetDirty(target);
            }

            if (GUILayout.Button("Add State"))
            {
                CastedTarget.InsertState();
                EditorUtility.SetDirty(target);
            }

            if (GUILayout.Button("Add Transition"))
            {
                CastedTarget.InsertTransition();
                EditorUtility.SetDirty(target);
            }
        }
    }
}