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
    [ScriptedImporter(3, UAnimRootGraph.Extension)]
    public sealed class UAnimRootImporter : ScriptedImporter
    {
        static void ImportChain(AssetImportContext ctx, UAnimRootGraph graph)
        {
            var root = ScriptableObject.CreateInstance<UAnimAsset>();
            root.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            root.id = AssetDatabase.AssetPathToGUID(ctx.assetPath);
            root.parameters = graph.parameters;
            root.schemaVersion = graph.schemaVersion;
            var layers = new List<UAnimLayer>();
            try
            {
                // Keep authored children available even while another Layer in
                // the root is unfinished and the accumulation chain is invalid.
                var embedded = new Dictionary<string, UAnimMachine>();
                foreach(var source in graph.embeddedSources.Where(e => e.extension == "uamachine"))
                {
                    embedded[source.id] = UAnimEmbeddedAssets.CompileMachine(ctx, graph, source.id);
                }

                foreach(var node in UAnimRootChainCompiler.Ordered(graph))
                {
                    var pose = node.Option<UAnimPoseGraph>("PoseGraph");
                    var machine = string.IsNullOrEmpty(node.embeddedMachineId) ? node.Option<UAnimMachine>("Machine") : embedded[node.embeddedMachineId];
                    if(machine)
                    {
                        pose = ScriptableObject.CreateInstance<UAnimPoseGraph>();
                        pose.id = graph.ID.ToString();
                        pose.name = node.Option<string>("Name");
                        pose.output = 0;
                        pose.nodes = new[]
                        {
                            new UAnimPoseNode
                            {
                                id = node.ID.ToString(),
                                kind = UAnimPoseKind.StateMachine,
                                machine = machine
                            }
                        };
                        pose.hideFlags = HideFlags.HideInHierarchy;
                        ctx.AddObjectToAsset("Layer-" + node.ID, pose);
                        UAnimLayerImporter.Depend(ctx, machine);
                    }
                    else
                    {
                        UAnimLayerImporter.Depend(ctx, pose);
                    }

                    var mask = node.Option<AvatarMask>("AvatarMask");
                    UAnimLayerImporter.Depend(ctx, mask);
                    layers.Add(new UAnimLayer { id = node.ID.ToString(), name = node.Option<string>("Name"), graph = pose, mask = mask, weight = node.Option<float>("Weight"), additive = node.Option<bool>("Additive"), enabled = node.Option<bool>("Enabled") });
                }
            }
            catch(Exception e)
            {
                root.importError = e.Message;
            }

            root.layers = layers.ToArray();
            ctx.AddObjectToAsset("Root", root);
            ctx.SetMainObject(root);
            root.compiledDependencyHash = UAnimBuildValidation.Hash(root, ctx.assetPath);
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var graph = GraphDatabase.LoadGraphForImporter<UAnimRootGraph>(ctx.assetPath);
            if(graph == null)
            {
                ctx.LogImportError("Cannot load UAnimAsset graph");
                return;
            }

            if(graph.layerChain)
            {
                ImportChain(ctx, graph);
                return;
            }

            var settings = graph;
            var pose = UAnimGraphCompiler.Compile(graph, false, o => UAnimLayerImporter.Depend(ctx, o));
            pose.name = "Base Layer";
            pose.hideFlags = HideFlags.HideInHierarchy;
            var root = ScriptableObject.CreateInstance<UAnimAsset>();
            root.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            root.id = AssetDatabase.AssetPathToGUID(ctx.assetPath);
            root.schemaVersion = settings.schemaVersion;
            root.parameters = settings.parameters;
            root.layers = new UAnimLayer[settings.layers.Length];
            for(int i = 0; i < root.layers.Length; i++)
            {
                var layer = settings.layers[i];
                root.layers[i] = new UAnimLayer
                {
                    id = layer.id,
                    name = layer.name,
                    enabled = layer.enabled,
                    weight = layer.weight,
                    additive = layer.additive,
                    mask = layer.mask,
                    graph = i == 0 ? pose : layer.graph
                };
                if(string.IsNullOrEmpty(root.layers[i].id))
                {
                    root.layers[i].id = Hash128.Compute(graph.ID + "/layer/" + i).ToString();
                }

                UAnimLayerImporter.Depend(ctx, root.layers[i].mask);
                if(i > 0)
                {
                    UAnimLayerImporter.Depend(ctx, root.layers[i].graph);
                }
            }

            ctx.AddObjectToAsset("BaseLayer", pose);
            ctx.AddObjectToAsset("Root", root);
            ctx.SetMainObject(root);
            root.compiledDependencyHash = UAnimBuildValidation.Hash(root, ctx.assetPath);
        }
    }
}
