using System;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimRootChainRegression
    {
        [MenuItem("UAnimGraph/Verify Root Layer Chain")]
        public static void Run()
        {
            Directory.CreateDirectory("Logs");
            var root = UAnimRootExample.Build();
            var errors = UAnimValidation.Validate(root).Errors;
            Check(errors.Count == 0, "example validates: " + string.Join("; ", errors));
            Check(root.layers.Length == 2, "root compiles two ordered layers");
            Check(root.layers[0].name == "Base Locomotion" && root.layers[1].name == "Upper Aim", "wire order");
            Check(root.layers[0].weight == 1 && Mathf.Approximately(root.layers[1].weight, .45f), "node weights imported");
            Check(root.layers[1].mask && !root.layers[1].additive, "upper body mask and override imported");
            var actor = new GameObject("Root chain verification", typeof(Animator));
            var body = new GameObject("Body");
            body.transform.SetParent(actor.transform, false);
            body.transform.localPosition = Vector3.up;
            new GameObject("LeftLeg").transform.SetParent(body.transform, false);
            new GameObject("RightLeg").transform.SetParent(body.transform, false);
            var upper = new GameObject("UpperBody");
            upper.transform.SetParent(body.transform, false);
            var arm = new GameObject("Arm");
            arm.transform.SetParent(upper.transform, false);
            try
            {
                using(var instance = new UAnimInstance(root, actor.GetComponent<Animator>(), true))
                {
                    instance.Set("speed", UAnimParameterType.Float, .65);
                    for(int i = 0; i < 20; i++)
                    {
                        instance.Tick(.025f);
                    }

                    Check(instance.Machines.Count == 2, "both Layer machines execute");
                    Check(instance.Machines[0].CurrentState == 1, "speed starts Move state");
                    var blend = instance.Poses.First(p => p.Weights.Length == 2 && p.Activity > 0);
                    Check(Mathf.Abs(blend.Weights[0] - .7f) < .001f && Mathf.Abs(blend.Weights[1] - .3f) < .001f, "Walk 70 / Run 30");
                    Check(Mathf.Abs(Mathf.DeltaAngle(0, arm.transform.localEulerAngles.z)) > 15, "masked Aim reaches actual Animator");
                    instance.Set("speed", UAnimParameterType.Float, 0);
                    for(int i = 0; i < 20; i++)
                    {
                        instance.Tick(.025f);
                    }

                    Check(instance.Machines[0].CurrentState == 0, "speed zero returns Idle");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(UAnimRootExample.Folder + "/ChainRegression.uanimasset");
            try
            {
                var graph = GraphDatabase.CreateGraph<UAnimRootGraph>(path);
                graph.layerChain = true;
                graph.parameters = root.parameters;
                var first = UAnimRootExample.Layer(graph, "First", root.layers[0].graph.nodes[0].machine, 1, new Vector2(900, 100));
                var second = UAnimRootExample.Layer(graph, "Second", root.layers[1].graph.nodes[0].machine, .45f, new Vector2(0, 100));
                var output = new LayerOutputNode();
                graph.AddNode(output);
                graph.Connect(first.GetOutputPortByName("Pose"), second.GetInputPortByName("PreviousPose"));
                graph.Connect(second.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
                GraphDatabase.SaveGraph(graph);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var compiled = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                Check(compiled.layers[0].name == "First", "positions never reorder layers");
                string rootId = compiled.id, layerId = compiled.layers[0].id;
                compiled.parameters = new[]
                {
                    new UAnimParameter
                    {
                        id = "speed",
                        name = "Speed",
                        type = UAnimParameterType.Float,
                        defaultValue = .4
                    }
                };
                UAnimRootGraph.SaveSettings(compiled);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                compiled = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                Check(compiled.id == rootId && compiled.layers[0].id == layerId, "reimport preserves IDs");
                Check(compiled.parameters[0].defaultValue == .4 && compiled.layers[1].weight == .45f, "Inspector saves parameters without replacing node settings");
                graph = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                first = graph.GetNodes().OfType<RootLayerNode>().Single(n => n.Option<string>("Name") == "First");
                first.GetNodeOptionByName("Weight").TrySetValue(.5f);
                GraphDatabase.SaveGraph(graph);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                compiled = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                Check(!string.IsNullOrEmpty(compiled.importError) && UAnimValidation.Validate(compiled).Errors.Count > 0, "invalid first layer cannot run");
                first.GetNodeOptionByName("Weight").TrySetValue(1f);
                var orphan = UAnimRootExample.Layer(graph, "Disconnected", root.layers[0].graph.nodes[0].machine, 1, Vector2.zero);
                bool rejected = false;
                try
                {
                    UAnimRootChainCompiler.Ordered(graph);
                }
                catch(InvalidOperationException)
                {
                    rejected = true;
                }

                Check(rejected, "disconnected layer rejected");
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }

            UAnimSingleAssetRegression.Run();
            UAnimVerification.Run();
            File.WriteAllText("Logs/UAnimGraph-root-chain-verification.txt", "PASS: two Layer nodes -> Output; wire order; weights/mask; real Animator output; independent machine execution; Walk70/Run30; Idle/Move; settings/reload IDs; invalid first layer and disconnected layer rejection; legacy single-asset and existing regression suite.");
            Debug.Log("UAnimGraph Root Layer Chain PASS");
        }

        static void Check(bool pass, string message)
        {
            if(!pass)
            {
                throw new InvalidOperationException("Root chain: " + message);
            }
        }
    }
}
