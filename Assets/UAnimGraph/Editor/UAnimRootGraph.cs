using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    [Serializable, Graph(Extension, GraphOptions.SupportsSubgraphs)]
    public sealed class UAnimRootGraph : UAnimLayerGraph
    {
        public const string Extension = "uanimasset";
        [SerializeField]
        internal string rootId = Guid.NewGuid().ToString("N");
        [SerializeField]
        internal int schemaVersion = 1;
        [SerializeField]
        internal bool layerChain;
        [SerializeField]
        internal UAnimParameter[] parameters = Array.Empty<UAnimParameter>();
        [SerializeField]
        internal UAnimLayer[] layers =
        {
            new UAnimLayer()
        };
        [SerializeField]
        internal List<UAnimEmbeddedSource> embeddedSources = new List<UAnimEmbeddedSource>();
        public static bool IsEmbedded(UAnimAsset root)
        {
            return Path.GetExtension(AssetDatabase.GetAssetPath(root)) == "." + Extension;
        }

        public static void SaveSettings(UAnimAsset root)
        {
            string path = AssetDatabase.GetAssetPath(root);
            var graph = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
            if(graph == null)
            {
                throw new InvalidOperationException("Cannot load UAnimAsset graph: " + path);
            }

            graph.rootId = root.id;
            graph.schemaVersion = root.schemaVersion;
            graph.parameters = root.parameters;
            if(!graph.layerChain)
            {
                graph.layers = new UAnimLayer[root.layers.Length];
                for(int i = 0; i < root.layers.Length; i++)
                {
                    var layer = root.layers[i];
                    graph.layers[i] = new UAnimLayer
                    {
                        id = layer.id,
                        name = layer.name,
                        enabled = layer.enabled,
                        weight = layer.weight,
                        additive = layer.additive,
                        mask = layer.mask,
                        graph = i == 0 ? null : layer.graph
                    };
                }
            }

            // This Toolkit build exposes no public dirty notification for custom
            // graph fields. A balanced public model edit requests serialization
            // without retaining an extra variable or node in the authored graph.
            var transaction = graph.CreateVariable("UAnimAsset settings save", 0, VariableKind.Local);
            graph.RemoveVariable(transaction, true);
            GraphDatabase.SaveGraph(graph);
        }
    }
}
