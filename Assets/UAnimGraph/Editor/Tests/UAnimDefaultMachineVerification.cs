using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimDefaultMachineVerification
    {
        public static void Run()
        {
            var checks = new List<string>();
            void Check(bool ok, string text)
            {
                if(!ok)
                {
                    throw new InvalidOperationException(text);
                }

                checks.Add("PASS: " + text);
            }

            string folder = "Assets/DefaultMachine-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            string path = folder + "/Character.uanimasset", cache = null;
            try
            {
                int windows = Resources.FindObjectsOfTypeAll<EditorWindow>().OfType<IGraphWindow>().Count();
                var asset = CreateUAnimAsset.CreateAtPath(path);
                var graph = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                var first = graph.GetNodes().OfType<RootLayerNode>().Single();
                cache = UAnimEmbeddedAssets.CacheFolder + "/" + AssetDatabase.AssetPathToGUID(path);
                Check(!string.IsNullOrEmpty(first.embeddedMachineId) && graph.embeddedSources.Count(e => e.extension == "uamachine") == 1, "root creation immediately creates and connects one default StateMachine");
                Check(asset.layers.Length == 1 && asset.layers[0].graph.nodes[0].machine, "default Layer runtime points to the new machine");
                var machine = asset.layers[0].graph.nodes[0].machine;
                Check(AssetDatabase.GetAssetPath(machine) == path, "default machine is a root subasset");
                string machinePath = UAnimEmbeddedAssets.CachePath(path, graph.embeddedSources.Single(e => e.extension == "uamachine"));
                var native = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(machinePath);
                var entry = native.GetStates().OfType<UAnimEntryState>().Single();
                Check(native.GetStates().OfType<PoseState>().Count() == 1 && entry.GetOutgoingTransitions().Count() == 1 && machine.defaultState == 0, "default machine contains Entry connected to its initial Pose State");
                Check(Resources.FindObjectsOfTypeAll<EditorWindow>().OfType<IGraphWindow>().Count() == windows, "automatic creation does not open or steal a graph window");
                var added = new RootLayerNode();
                graph.AddNode(added);
                added.GetNodeOptionByName("Name").TrySetValue("Upper Body");
                GraphDatabase.SaveGraph(graph);
                UAnimDefaultLayerMachine.Flush();
                graph = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                added = graph.GetNodes().OfType<RootLayerNode>().Single(n => n.Option<string>("Name") == "Upper Body");
                Check(!string.IsNullOrEmpty(added.embeddedMachineId) && added.embeddedMachineId != first.embeddedMachineId && graph.embeddedSources.Count(e => e.extension == "uamachine") == 2, "adding another Layer generates its own connected machine");
                string id = added.embeddedMachineId;
                UAnimDefaultLayerMachine.EnsureAll(graph);
                UAnimDefaultLayerMachine.Flush();
                Check(graph.embeddedSources.Count(e => e.extension == "uamachine") == 2 && added.embeddedMachineId == id, "repeated initialization does not duplicate or replace machines");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var reopened = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                Check(reopened.GetNodes().OfType<RootLayerNode>().Single(n => n.Option<string>("Name") == "Upper Body").embeddedMachineId == id, "saved Layer retains its generated machine after reimport");
                var external = AssetDatabase.LoadAssetAtPath<UAnimMachine>("Assets/UAnimGraph/Demo/Locomotion.uamachine");
                var manual = new RootLayerNode();
                reopened.AddNode(manual);
                manual.GetNodeOptionByName("Machine").TrySetValue(external);
                GraphDatabase.SaveGraph(reopened);
                UAnimDefaultLayerMachine.Flush();
                Check(string.IsNullOrEmpty(manual.embeddedMachineId) && manual.Option<UAnimMachine>("Machine") == external, "explicitly assigned machine is preserved before deferred initialization");
                var removed = new RootLayerNode();
                reopened.AddNode(removed);
                reopened.RemoveNode(removed);
                GraphDatabase.SaveGraph(reopened);
                UAnimDefaultLayerMachine.Flush();
                Check(reopened.embeddedSources.Count(e => e.extension == "uamachine") == 2, "a Layer deleted before initialization creates no orphan machine");
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/UAnimGraph-default-machine-verification.txt", checks);
                Debug.Log("DEFAULT MACHINE PASS: " + checks.Count);
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
                UAnimEmbeddedVerification.Run();
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
