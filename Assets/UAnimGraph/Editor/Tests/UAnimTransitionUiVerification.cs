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
    public static class UAnimTransitionUiVerification
    {
        public static void Run()
        {
            var checks = new List<string>();
            void Check(bool ok, string name)
            {
                if(!ok)
                {
                    throw new InvalidOperationException(name);
                }

                checks.Add("PASS: " + name);
            }

            string folder = "Assets/TransitionUi-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            string path = folder + "/SampleGraph.uanimasset", cache = null;
            try
            {
                CreateUAnimAsset.CreateAtPath(path);
                var root = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                var layer = root.GetNodes().OfType<RootLayerNode>().Single();
                layer.GetNodeOptionByName("Name").TrySetValue("Layer1");
                UAnimEmbeddedAssets.CreateMachine(layer);
                root = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                string machinePath = UAnimEmbeddedAssets.CachePath(path, root.embeddedSources.Single(e => e.extension == "uamachine"));
                cache = Path.GetDirectoryName(machinePath).Replace('\\', '/');
                Check(UAnimEmbeddedAssets.DisplayPath(machinePath) == "SampleGraph/Layer1/StateMachine", "embedded StateMachine uses the root/Layer/StateMachine path");
                var window = ScriptableObject.CreateInstance<UAnimTransitionConditions>();
                try
                {
                    UAnimWindowPaths.Apply(window, UAnimEmbeddedAssets.DisplayPath(machinePath));
                    Check(window.titleContent.text == "StateMachine" && window.titleContent.tooltip == "SampleGraph/Layer1/StateMachine", "window tab uses only the name and tooltip preserves its full path");
                    UAnimWindowPaths.Apply(window, "SampleGraph/Layer1/StateMachine/Idle");
                    Check(window.titleContent.text == "Idle", "State Pose tab uses its State name");
                    var toolbar = new VisualElement();
                    toolbar.AddToClassList("ge-toolbar");
                    var pathLabel = new Label("cached-graph-id");
                    toolbar.Add(pathLabel);
                    UAnimWindowPaths.ApplyToolbarPath(toolbar, "cached-graph-id", "SampleGraph/Layer1/StateMachine/Idle");
                    Check(pathLabel.text == "SampleGraph/Layer1/StateMachine/Idle" && pathLabel.tooltip == pathLabel.text, "toolbar replaces the cache ID with the path from the root graph");
                    UAnimWindowPaths.ApplyToolbarPath(toolbar, "cached-graph-id", "Renamed/Layer1/StateMachine/Idle");
                    Check(pathLabel.text == "Renamed/Layer1/StateMachine/Idle", "toolbar path follows ancestor renaming");
                    var breadcrumb = new VisualElement();
                    var nativeLabel = new Label("0123456789abcdef0123456789abcdef");
                    breadcrumb.Add(nativeLabel);
                    UAnimWindowPaths.ApplyToolbarPath(breadcrumb, nativeLabel.text, "SampleGraph/Layer1/StateMachine/Idle");
                    Check(nativeLabel.text == "SampleGraph/Layer1/StateMachine/Idle", "cache breadcrumb does not require a version-specific toolbar CSS class");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(window);
                }

                var machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(machinePath);
                var a = machine.GetStates().OfType<PoseState>().Single();
                a.Title = "Idle";
                a.GetOptionByName("Name").TrySetValue("Idle");
                var b = new PoseState();
                machine.AddState(b);
                b.Title = "Move";
                b.GetOptionByName("Name").TrySetValue("Move");
                var pose = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>("Assets/UAnimGraph/Demo/Idle.uastate");
                a.GetOptionByName("Graph").TrySetValue(pose);
                b.GetOptionByName("Graph").TrySetValue(pose);
                var entry = machine.GetStates().OfType<UAnimEntryState>().Single().GetOutgoingTransitions().Single();
                var edge = machine.Connect(a, b);
                var rule = edge.GetRule(0);
                rule.RootCondition.Add(new DurationCondition { Value = .3f });
                rule.RootCondition.Add(new PriorityCondition { Value = 5 });
                rule.RootCondition.Add(new ParameterCondition { Value = "speed|Float|Greater|0" });
                Check(!UAnimTransitionConditions.CanChoose(entry) && UAnimTransitionConditions.CanChoose(edge), "Entry is excluded while ordinary State transitions support functions");
                string key = TransitionEvaluationRegistry.Key(typeof(LocomotionTransitions).GetMethod(nameof(LocomotionTransitions.Always)));
                bool rejected = false;
                try
                {
                    UAnimTransitionConditions.Choose(machine, entry, key);
                }
                catch(InvalidOperationException)
                {
                    rejected = true;
                }

                Check(rejected, "function assignment to Entry is rejected");
                UAnimTransitionConditions.Choose(machine, edge, key);
                machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(machinePath);
                edge = machine.GetStates().Where(s => s.Title == "Idle").Single().GetOutgoingTransitions().Single();
                rule = edge.GetRule(0);
                Check(UAnimTransitionConditions.Functions(rule.RootCondition).Single().Value == key, "function selection saves and reloads through native GTK conditions");
                Check(rule.RootCondition.Get().OfType<DurationCondition>().Single().Value == .3f && rule.RootCondition.Get().OfType<PriorityCondition>().Single().Value == 5, "duration and priority survive choosing a function");
                Check(!rule.RootCondition.Get().OfType<ParameterCondition>().Any(), "incompatible field predicates are replaced");
                var runtime = AssetDatabase.LoadAssetAtPath<UAnimMachine>(machinePath);
                Check(string.IsNullOrEmpty(runtime.importError) && runtime.transitions.Single().evaluation == key, "selected function compiles into the runtime transition");
                var compiled = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                Check(compiled.layers[0].graph.nodes[0].machine.transitions.Single().evaluation == key, "function choice is captured in the owning root subasset");
                string second = TransitionEvaluationRegistry.Key(typeof(LocomotionTransitions).GetMethod(nameof(LocomotionTransitions.AfterFirstCycle)));
                UAnimTransitionConditions.Choose(machine, edge, second);
                Undo.PerformUndo();
                Check(UAnimTransitionConditions.Functions(machine.GetStates().Single(s => s.Title == "Idle").GetOutgoingTransitions().Single().GetRule(0).RootCondition).Single().Value == key, "Undo restores the previous selected function");
                Undo.PerformRedo();
                Check(UAnimTransitionConditions.Functions(machine.GetStates().Single(s => s.Title == "Idle").GetOutgoingTransitions().Single().GetRule(0).RootCondition).Single().Value == second, "Redo restores the new selected function");
                var layerAgain = GraphDatabase.LoadGraph<UAnimRootGraph>(path).GetNodes().OfType<RootLayerNode>().Single();
                layerAgain.GetNodeOptionByName("Name").TrySetValue("Locomotion");
                Check(UAnimEmbeddedAssets.DisplayPath(machinePath) == "SampleGraph/Locomotion/StateMachine", "window path follows Layer renaming");
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/UAnimGraph-transition-ui-verification.txt", checks);
                Debug.Log("TRANSITION UI PASS: " + checks.Count);
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
            catch(Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
