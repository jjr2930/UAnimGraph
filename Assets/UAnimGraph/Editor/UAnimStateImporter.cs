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
    [ScriptedImporter(3, "uastate")]
    public sealed class UAnimStateImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            UAnimLayerImporter.Import(ctx, GraphDatabase.LoadGraphForImporter<UAnimStateGraph>(ctx.assetPath), true);
        }
    }
}
