using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimPlanningGapVerification
    {
        [MenuItem("UAnimGraph/Verify Planned Mixer Editing")]
        public static void Run()
        {
            var passed = new List<string>();
            void Check(bool condition, string message)
            {
                if(!condition)
                {
                    throw new InvalidOperationException(message);
                }

                passed.Add("PASS: " + message);
            }

            var root = ScriptableObject.CreateInstance<UAnimAsset>();
            var pose = ScriptableObject.CreateInstance<UAnimPoseGraph>();
            var layer = ScriptableObject.CreateInstance<UAnimPoseGraph>();
            var machine = ScriptableObject.CreateInstance<UAnimMachine>();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/PlannedMixerVerification.uastate");
            try
            {
                root.parameters = new[]
                {
                    new UAnimParameter
                    {
                        id = "stable-speed",
                        name = "Speed",
                        type = UAnimParameterType.Float
                    },
                    new UAnimParameter
                    {
                        id = "run",
                        name = "Run Speed",
                        type = UAnimParameterType.Float
                    },
                    new UAnimParameter
                    {
                        id = "bool",
                        name = "Speed Enabled",
                        type = UAnimParameterType.Bool
                    },
                    new UAnimParameter
                    {
                        id = "move",
                        name = "이동 방향",
                        type = UAnimParameterType.Vector2
                    }
                };
                Check(UAnimParameterPicker.Search(root, UAnimParameterType.Float, "  sPeEd ").Length == 2, "case-insensitive name search trims whitespace and filters type");
                Check(UAnimParameterPicker.Search(root, UAnimParameterType.Float, "stable-speed").Length == 0, "search uses display names rather than stable IDs");
                Check(UAnimParameterPicker.Search(root, UAnimParameterType.Vector2, "방향").Single().id == "move", "Korean parameter names are searchable");
                Check(UAnimParameterPicker.Search(null, UAnimParameterType.Float, "").Length == 0, "missing root produces an empty selection");
                root.parameters[0].name = "Velocity";
                Check(UAnimParameterPicker.Search(root, UAnimParameterType.Float, "Velocity").Single().id == "stable-speed", "rename preserves stored binding identity");
                machine.states = new[]
                {
                    new UAnimState
                    {
                        name = "Idle",
                        graph = pose,
                        duration = 2
                    },
                    new UAnimState
                    {
                        name = "Move",
                        graph = pose,
                        duration = 3
                    }
                };
                layer.nodes = new[]
                {
                    new UAnimPoseNode
                    {
                        machine = machine
                    },
                    new UAnimPoseNode
                    {
                        machine = machine
                    }
                };
                root.layers = new[]
                {
                    new UAnimLayer
                    {
                        name = "Base",
                        graph = layer
                    }
                };
                var targets = UAnimStateSettingsNavigation.Find(root, pose);
                Check(targets.Length == 2 && targets.All(t => t.Machine == machine), "shared Pose resolves both owning States and deduplicates repeated machine nodes");
                Check(targets.Single(t => t.State.name == "Move").State.duration == 3, "duration navigation uses the State setting rather than the clip length");
                Check(UAnimStateSettingsNavigation.Find(null, pose).Length == 0, "duration navigation handles unassigned root");
                var graph = GraphDatabase.CreateGraph<UAnimStateGraph>(path);
                var mix = new Blend2DNode();
                graph.AddNode(mix);
                graph.AddNode(new StateOutputNode());
                var issues = UAnimAuthoringValidation.GetIssues(graph, true).Where(i => ReferenceEquals(i.element, mix)).ToArray();
                Check(issues.Length > 0 && issues.All(i => UAnimAuthoringValidation.IsIncomplete(graph, i.message)), "unconnected Mixer inputs are counted as warnings");
                mix.Settings.inputs[1].position = Vector2.zero;
                Check(UAnimAuthoringValidation.GetIssues(graph, true).Any(i => i.message.Contains("Duplicate") && !UAnimAuthoringValidation.IsIncomplete(graph, i.message)), "duplicate sample positions are counted as errors");
                mix.Settings.inputs[1].position = Vector2.up;
                graph.UndoBeginRecordGraph("Bind selected parameter", mix);
                mix.Settings.binding = MixerBinding.Vector2Parameter;
                mix.Settings.parameter = "move";
                mix.DefineNode();
                graph.UndoEndRecordGraph();
                GraphDatabase.SaveGraph(graph);
                AssetDatabase.ImportAsset(path);
                var reopened = GraphDatabase.LoadGraph<UAnimStateGraph>(path).GetNodes().OfType<Blend2DNode>().Single();
                Check(reopened.Settings.parameter == "move" && reopened.Settings.binding == MixerBinding.Vector2Parameter, "searched parameter ID persists through save and reopen");
                Undo.PerformUndo();
                Undo.PerformRedo();
                GraphDatabase.SaveGraph(graph);
                Check(graph.GetNodes().OfType<Blend2DNode>().Single().Settings.parameter == "move", "parameter binding survives Undo and Redo");
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(pose);
                UnityEngine.Object.DestroyImmediate(layer);
                UnityEngine.Object.DestroyImmediate(machine);
            }

            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/UAnimGraph-planned-mixer-verification.txt", passed);
            Debug.Log("UAnimGraph PLANNED MIXER PASS: " + passed.Count);
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                UAnimVerification.Run();
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
