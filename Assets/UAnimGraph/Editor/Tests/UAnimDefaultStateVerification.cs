using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UAnimGraph.Authoring
{
    public static class UAnimDefaultStateVerification
    {
        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static string folder, path, cache, machinePath;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static UAnimStateMachineGraph machine;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static int frames;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly List<string> checks = new List<string>();
        static void Check(bool ok, string text)
        {
            if(!ok)
            {
                throw new InvalidOperationException(text);
            }

            checks.Add("PASS: " + text);
        }

        public static void RunBatch()
        {
            try
            {
                RunData();
                AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(machinePath));
                frames = 0;
                EditorApplication.update += VerifyUi;
            }
            catch(Exception e)
            {
                Finish(e);
            }
        }

        static void RunData()
        {
            checks.Clear();
            folder = "Assets/DefaultState-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            path = folder + "/Character.uanimasset";
            int windows = Resources.FindObjectsOfTypeAll<EditorWindow>().OfType<IGraphWindow>().Count();
            var asset = CreateUAnimAsset.CreateAtPath(path);
            var root = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
            cache = UAnimEmbeddedAssets.CacheFolder + "/" + AssetDatabase.AssetPathToGUID(path);
            machinePath = UAnimEmbeddedAssets.CachePath(path, root.embeddedSources.Single(s => s.extension == "uamachine"));
            machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(machinePath);
            var first = machine.GetStates().OfType<PoseState>().Single();
            Check(!string.IsNullOrEmpty(first.embeddedGraphId) && root.embeddedSources.Count(s => s.extension == "uastate") == 1, "initial State automatically owns a Pose graph");
            var pose = asset.layers[0].graph.nodes[0].machine.states[0].graph;
            Check(pose && AssetDatabase.GetAssetPath(pose) == path, "automatic Pose definition is a root subasset");
            string firstPath = UAnimEmbeddedAssets.CachePath(path, root.embeddedSources.Single(s => s.extension == "uastate"));
            var graph = GraphDatabase.LoadGraph<UAnimStateGraph>(firstPath);
            Check(graph.GetNodes().OfType<ClipNode>().Count() == 1 && graph.GetNodes().OfType<StateOutputNode>().Single().GetInputPortByName("Pose").IsConnected, "new Pose graph has Animation Player connected to State Output");
            Check(Resources.FindObjectsOfTypeAll<EditorWindow>().OfType<IGraphWindow>().Count() == windows, "automatic State graph creation opens no graph window");
            var second = new PoseState();
            machine.AddState(second);
            second.Title = "Second";
            second.GetOptionByName("Name").TrySetValue("Second");
            string secondId = second.ID.ToString();
            StateMachineDatabase.SaveStateMachine(machine);
            UAnimDefaultStatePose.Flush();
            machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(machinePath);
            second = machine.GetStates().OfType<PoseState>().Single(s => s.ID.ToString() == secondId);
            root = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
            Check(!string.IsNullOrEmpty(second.embeddedGraphId) && second.embeddedGraphId != first.embeddedGraphId && root.embeddedSources.Count(s => s.extension == "uastate") == 2, "a newly added State receives its own distinct internal Pose graph");
            string savedName = null;
            second.GetOptionByName("Name").TryGetValue(out savedName);
            Check(savedName == "Second", "automatic graph creation preserves the authored State name");
            string id = second.embeddedGraphId;
            UAnimDefaultStatePose.EnsureAll(machine);
            UAnimDefaultStatePose.Flush();
            root = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
            Check(root.embeddedSources.Count(s => s.extension == "uastate") == 2 && second.embeddedGraphId == id, "repeated initialization creates no duplicate graphs");
            var assigned = new PoseState();
            machine.AddState(assigned);
            assigned.Title = "External";
            var external = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>("Assets/UAnimGraph/Demo/Idle.uastate");
            assigned.GetOptionByName("Graph").TrySetValue(external);
            StateMachineDatabase.SaveStateMachine(machine);
            UAnimDefaultStatePose.Flush();
            UAnimPoseGraph actual = null;
            assigned.GetOptionByName("Graph").TryGetValue(out actual);
            Check(actual == external && string.IsNullOrEmpty(assigned.embeddedGraphId), "an explicitly assigned Pose graph is preserved");
            var removed = new PoseState();
            machine.AddState(removed);
            machine.RemoveState(removed);
            StateMachineDatabase.SaveStateMachine(machine);
            UAnimDefaultStatePose.Flush();
            Check(GraphDatabase.LoadGraph<UAnimRootGraph>(path).embeddedSources.Count(s => s.extension == "uastate") == 2, "a State deleted before initialization creates no orphan graph");
            AssetDatabase.ImportAsset(machinePath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            Check(StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(machinePath).GetStates().OfType<PoseState>().Single(s => s.ID.ToString() == secondId).embeddedGraphId == id, "State keeps its generated graph after save and reimport");
        }

        static void VerifyUi()
        {
            if(++frames < 20)
            {
                return;
            }

            EditorApplication.update -= VerifyUi;
            try
            {
                var window = Resources.FindObjectsOfTypeAll<EditorWindow>().First(w => w is IGraphWindow g && g.StateMachine?.ID == machine.ID);
                var bodies = window.rootVisualElement.Query<VisualElement>("uanim-pose-state-controls").ToList();
                Check(bodies.Count == 3, "all native Pose State views construct their controls");
                foreach(var body in bodies)
                {
                    var buttons = body.Query<Button>().ToList();
                    Check(buttons.Count == 1 && buttons[0].text == "Open State Pose Graph", "native State has only the Open State Pose Graph button");
                }

                Finish(null);
            }
            catch(Exception e)
            {
                Finish(e);
            }
        }

        static void Finish(Exception error)
        {
            EditorApplication.update -= VerifyUi;
            if(folder != null)
            {
                AssetDatabase.DeleteAsset(folder);
            }

            if(cache != null)
            {
                AssetDatabase.DeleteAsset(cache);
            }

            if(error != null)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
                return;
            }

            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/UAnimGraph-default-state-verification.txt", checks);
            Debug.Log("DEFAULT STATE POSE PASS: " + checks.Count);
            try
            {
                UAnimDefaultMachineVerification.Run();
                UAnimEmbeddedVerification.Run();
                UAnimTransitionUiVerification.Run();
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
