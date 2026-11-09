using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace UAnimGraph.Editor
{
    [Serializable]
    [Graph("uanimsubgraph", GraphOptions.SupportsSubgraphs)]
    [Subgraph(typeof(EditorGraphAsset))]
    public class EditorLayerSubgraph : Graph
    {
        [MenuItem("Assets/Create/UAnimGraph/Create Layer sub graph Asset", false)]
        static void CreateAssetFile()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<EditorLayerSubgraph>("New Layer Subgraph");
        }
    }
}