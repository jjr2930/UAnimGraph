using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace UAnimGraph.Editor
{
    [Graph(AssetExtension, GraphOptions.SupportsSubgraphs)]
    [Serializable]
    public class EditorGraphAsset : Graph
    {
        public const string AssetExtension = "uanim";

        [MenuItem("UAnimGraph/Create UAnimGraph Asset", false)]
        static void CreateAssetFile()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<EditorGraphAsset>();
        }
    }
}
