using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimEmbeddedVerification
    {
        public static void Run()
        {
            var checks = new List<string>();
            void Check(bool ok, string message)
            {
                if(!ok)
                {
                    throw new InvalidOperationException(message);
                }

                checks.Add("PASS: " + message);
            }

            string folder = "Assets/EmbeddedVerification-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            string path = folder + "/Character.uanimasset", copy = folder + "/Copy.uanimasset";
            string cache = null, copyCache = null;
            try
            {
                CreateUAnimAsset.CreateAtPath(path);
                var root = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                var layer = root.GetNodes().OfType<RootLayerNode>().Single();
                UAnimEmbeddedAssets.CreateMachine(layer);
                root = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                layer = root.GetNodes().OfType<RootLayerNode>().Single();
                Check(root.embeddedSources.Count(e => e.extension == "uamachine") == 1 && !string.IsNullOrEmpty(layer.embeddedMachineId), "Layer creates an embedded StateMachine source");
                string machinePath = UAnimEmbeddedAssets.CachePath(path, root.embeddedSources.Single(e => e.extension == "uamachine"));
                cache = Path.GetDirectoryName(machinePath).Replace('\\', '/');
                var machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(machinePath);
                var state = machine.GetStates().OfType<PoseState>().Single();
                UAnimEmbeddedAssets.CreateStateGraph(state);
                root = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(machinePath);
                state = machine.GetStates().OfType<PoseState>().Single();
                Check(root.embeddedSources.Count == 2 && !string.IsNullOrEmpty(state.embeddedGraphId), "State creates an embedded Pose source");
                string posePath = UAnimEmbeddedAssets.CachePath(path, root.embeddedSources.Single(e => e.extension == "uastate"));
                var pose = GraphDatabase.LoadGraph<UAnimStateGraph>(posePath);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/UAnimGraph/Demo/Walk.anim");
                Check(clip, "fixture animation exists");
                pose.GetNodes().OfType<ClipNode>().Single().GetNodeOptionByName("Clip").TrySetValue(clip);
                GraphDatabase.SaveGraph(pose);
                AssetDatabase.ImportAsset(posePath, ImportAssetOptions.ForceSynchronousImport);
                UAnimEmbeddedAssets.Capture(posePath);
                AssetDatabase.ImportAsset(machinePath, ImportAssetOptions.ForceSynchronousImport);
                UAnimEmbeddedAssets.Capture(machinePath);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var asset = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                Check(UAnimValidation.Validate(asset).Errors.Count == 0, "embedded authored root validates: " + string.Join(";", UAnimValidation.Validate(asset).Errors));
                var runtime = asset.layers[0].graph.nodes[0].machine;
                Check(AssetDatabase.GetAssetPath(runtime) == path && AssetDatabase.GetAssetPath(runtime.states[0].graph) == path, "StateMachine and State Pose are actual root subassets");
                Check(runtime.states[0].graph.nodes[0].clip == clip, "embedded Pose preserves external AnimationClip reference");
                Check(Directory.GetFiles(folder).Count(f => !f.EndsWith(".meta")) == 1, "only one source asset exists beside the root");
                string machineId = runtime.id, poseId = runtime.states[0].graph.id, hash = UAnimBuildValidation.Hash(asset);
                var actor = new GameObject("Embedded animated actor", typeof(Animator));
                new GameObject("Body").transform.SetParent(actor.transform, false);
                try
                {
                    using(var instance = new UAnimInstance(asset, actor.GetComponent<Animator>(), true))
                    {
                        instance.Tick(.2f);
                        Check(instance.Machines.Count == 1 && instance.Graph.IsValid(), "embedded runtime evaluates a real Animator graph");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(actor);
                }

                AssetDatabase.DeleteAsset(cache);
                UAnimEmbeddedAssets.Restore(path);
                asset = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                runtime = asset.layers[0].graph.nodes[0].machine;
                Check(UAnimValidation.Validate(asset).Errors.Count == 0 && runtime.id == machineId && runtime.states[0].graph.id == poseId, "deleting editor cache and restoring from root preserves graph identities and validation");
                Check(UAnimBuildValidation.Hash(asset) == hash, "cache recreation does not change the definition fingerprint");
                AssetDatabase.CopyAsset(path, copy);
                AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport);
                UAnimEmbeddedAssets.Restore(copy);
                var other = AssetDatabase.LoadAssetAtPath<UAnimAsset>(copy);
                Check(other.id != asset.id && AssetDatabase.GetAssetPath(other.layers[0].graph.nodes[0].machine) == copy, "duplicated root owns independent runtime subassets");
                var copyRoot = GraphDatabase.LoadGraph<UAnimRootGraph>(copy);
                string copyPose = UAnimEmbeddedAssets.CachePath(copy, copyRoot.embeddedSources.Single(e => e.extension == "uastate"));
                copyCache = Path.GetDirectoryName(copyPose).Replace('\\', '/');
                var copyGraph = GraphDatabase.LoadGraph<UAnimStateGraph>(copyPose);
                copyGraph.GetNodes().OfType<ClipNode>().Single().GetNodeOptionByName("Speed").TrySetValue(2f);
                GraphDatabase.SaveGraph(copyGraph);
                AssetDatabase.ImportAsset(copyPose, ImportAssetOptions.ForceSynchronousImport);
                UAnimEmbeddedAssets.Capture(copyPose);
                Check(GraphDatabase.LoadGraph<UAnimStateGraph>(posePath).GetNodes().OfType<ClipNode>().Single().Option<float>("Speed") == 1, "editing a duplicated embedded graph does not change the original");
                string renamed = AssetDatabase.RenameAsset(path, "Renamed");
                Check(string.IsNullOrEmpty(renamed), "embedded root can be renamed");
                path = folder + "/Renamed.uanimasset";
                UAnimEmbeddedAssets.Restore(path);
                Check(AssetDatabase.LoadAssetAtPath<UAnimAsset>(path).layers[0].graph.nodes[0].machine.id == machineId, "renaming root preserves embedded machine identity");
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/UAnimGraph-embedded-verification.txt", checks);
                Debug.Log("UAnimGraph EMBEDDED PASS: " + checks.Count);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
                if(cache != null)
                {
                    AssetDatabase.DeleteAsset(cache);
                }

                if(copyCache != null)
                {
                    AssetDatabase.DeleteAsset(copyCache);
                }
            }
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                UAnimCompleteVerification.Run();
                EditorApplication.Exit(0);
            }
            catch(Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
