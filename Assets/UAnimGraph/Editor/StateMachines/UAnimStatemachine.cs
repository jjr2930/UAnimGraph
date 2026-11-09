using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace UAnimGraph.Editor
{
    [Serializable]
    [StateMachine(AssetExtension)]
    public class UAnimStatemachine : StateMachine
    {
        const string AssetExtension = "uanimstatemachine";

        [MenuItem("Assets/Create/UAnimGraph/UAnimStatemachine")]
        static void CreateAssetFile()
        {
            StateMachineDatabase.PromptInProjectBrowserToCreateNewAsset<UAnimStatemachine>("New UAnimStatemachine");
        }
    }
}