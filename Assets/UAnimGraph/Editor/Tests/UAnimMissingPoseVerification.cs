using System;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimMissingPoseVerification
    {
        public static void Run()
        {
            string folder = "Assets/MissingPoseVerification-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            string path = folder + "/Character.uanimasset";
            string cache = null;
            int checks = 0;
            void Check(bool ok, string message)
            {
                if(!ok)
                {
                    throw new InvalidOperationException(message);
                }
                checks++;
                Debug.Log("MISSING POSE CHECK: " + message);
            }
            try
            {
                CreateUAnimAsset.CreateAtPath(path);
                var root = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                string machinePath = UAnimEmbeddedAssets.CachePath(path, root.embeddedSources.Single(source => source.extension == "uamachine"));
                cache = Path.GetDirectoryName(machinePath).Replace('\\', '/');
                var machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(machinePath);
                var run = new PoseState();
                machine.AddState(run);
                run.Title = "Run";
                run.GetOptionByName("Name").TrySetValue("Run");
                UAnimDefaultStatePose.Ensure(run);
                root = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/UAnimGraph/Demo/Walk.anim");
                foreach(var source in root.embeddedSources.Where(source => source.extension == "uastate").ToArray())
                {
                    string posePath = UAnimEmbeddedAssets.CachePath(path, source);
                    var pose = GraphDatabase.LoadGraph<UAnimStateGraph>(posePath);
                    pose.GetNodes().OfType<ClipNode>().Single().GetNodeOptionByName("Clip").TrySetValue(clip);
                    GraphDatabase.SaveGraph(pose);
                    AssetDatabase.ImportAsset(posePath, ImportAssetOptions.ForceSynchronousImport);
                    UAnimEmbeddedAssets.Capture(posePath);
                }
                AssetDatabase.ImportAsset(machinePath, ImportAssetOptions.ForceSynchronousImport);
                UAnimEmbeddedAssets.Capture(machinePath);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var asset = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                Check(UAnimValidation.Validate(asset).Errors.Count == 0, "two-state fixture validates");
                string hash = asset.compiledDependencyHash;
                var runtime = asset.layers[0].graph.nodes[0].machine;
                runtime.states.Single(state => state.name == "Run").graph = null;
                var cached = AssetDatabase.LoadAssetAtPath<UAnimMachine>(machinePath);
                cached.states.Single(state => state.name == "Run").graph = null;
                UAnimEmbeddedAssets.Restore(path);
                asset = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                Check(UAnimValidation.Validate(asset).Errors.Count == 0, "existing files repair both cached and root missing Run pose");
                Check(asset.compiledDependencyHash == hash, "recovery preserves authored definition fingerprint");
                runtime = asset.layers[0].graph.nodes[0].machine;
                runtime.states.Single(state => state.name == "Run").graph = null;
                UAnimEmbeddedAssets.Restore(path);
                asset = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                Check(UAnimValidation.Validate(asset).Errors.Count == 0, "root-only stale missing pose is also repaired");
                string runPath = UAnimEmbeddedAssets.CachedPosePath(machinePath, run.embeddedGraphId);
                Check(runPath == cache + "/" + run.embeddedGraphId + ".uastate", "pose path resolves directly from cache siblings");
                string text = File.ReadAllText(runPath);
                string meta = File.ReadAllText(runPath + ".meta");
                AssetDatabase.DeleteAsset(runPath);
                AssetDatabase.ImportAsset(machinePath, ImportAssetOptions.ForceSynchronousImport);
                Check(!AssetDatabase.LoadAssetAtPath<UAnimMachine>(machinePath).states.Single(state => state.name == "Run").graph, "machine imported before its pose reproduces the reported missing reference");
                File.WriteAllText(runPath, text);
                File.WriteAllText(runPath + ".meta", meta);
                AssetDatabase.ImportAsset(runPath, ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                UAnimEmbeddedAssets.Flush();
                Check(AssetDatabase.LoadAssetAtPath<UAnimMachine>(machinePath).states.Single(state => state.name == "Run").graph, "processing the pose import queue automatically rebuilds the waiting machine");
                Check(UAnimValidation.Validate(AssetDatabase.LoadAssetAtPath<UAnimAsset>(path)).Errors.Count == 0, "dependency propagation automatically repairs the root asset");
                AssetDatabase.DeleteAsset(cache);
                UAnimEmbeddedAssets.Restore(path);
                asset = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                Check(UAnimValidation.Validate(asset).Errors.Count == 0 && asset.layers[0].graph.nodes[0].machine.states.Length == 2, "fresh cache rebuild preserves both state pose graphs");
                var actor = new GameObject("Recovered player", typeof(Animator));
                try
                {
                    using(var instance = new UAnimInstance(asset, actor.GetComponent<Animator>(), true))
                    {
                        instance.Tick(.1f);
                        Check(instance.Graph.IsValid(), "recovered graph starts and evaluates successfully");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(actor);
                }
                Debug.Log("MISSING POSE PASS: " + checks);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
                if(cache != null)
                {
                    AssetDatabase.DeleteAsset(cache);
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
            catch(Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
