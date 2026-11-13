using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    [ScriptedImporter(3, "uapose")]
    public sealed class UAnimLayerImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            Import(ctx, GraphDatabase.LoadGraphForImporter<UAnimLayerGraph>(ctx.assetPath), false);
        }

        internal static void Import(AssetImportContext ctx, Graph graph, bool state)
        {
            if(graph == null)
            {
                ctx.LogImportError("Cannot load authoring graph");
                return;
            }

            var runtime = UAnimGraphCompiler.Compile(graph, state, o => Depend(ctx, o));
            runtime.name = System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath);
            // Incomplete authoring is expected while creating/connecting nodes.
            // Keep the diagnostic in the imported definition; the graph inspector,
            // explicit validation, player and build validator report it in context.
            ctx.AddObjectToAsset("Runtime", runtime);
            ctx.SetMainObject(runtime);
        }

        internal static void Depend(AssetImportContext ctx, UnityEngine.Object o)
        {
            if(!o)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(o);
            if(!string.IsNullOrEmpty(path))
            {
                ctx.DependsOnSourceAsset(path);
                ctx.DependsOnArtifact(path);
            }
        }
    }
}
