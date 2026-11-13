using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEditor.Build.Reporting;

namespace UAnimGraph.Authoring
{
    public static class UAnimCompleteVerification
    {
        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly List<string> passed = new List<string>();
        static void Check(bool value, string message)
        {
            if(!value)
            {
                throw new InvalidOperationException(message);
            }

            passed.Add("PASS: " + message);
        }

        [MenuItem("UAnimGraph/Verify Complete Feature Set")]
        public static void Run()
        {
            passed.Clear();
            UAnimEvaluationCatalog.Refresh();
            var method = typeof(LocomotionTransitions).GetMethod(nameof(LocomotionTransitions.CanMove));
            string key = TransitionEvaluationRegistry.Key(method);
            Check(TransitionEvaluationRegistry.IsValid(method, out _), "static library evaluation contract");
            Check(UAnimEvaluationCatalog.Entries.Any(e => e.key == key && e.Valid), "registered runtime functions discovered");
            Check(!UAnimEvaluationCatalog.Display(key).Contains("TransitionEvaluation"), "selection label contains class and method only");
            Check(!TransitionEvaluationRegistry.IsValid(typeof(InvalidLibrary).GetMethod("Bad"), out _), "unmarked class rejected");
            float[] weights = new float[3];
            UAnimBlendWeights.One(.65f, new[] { 0f, .5f, 1f }, weights);
            Check(Mathf.Abs(weights[1] - .7f) < .001f && Mathf.Abs(weights[2] - .3f) < .001f, "1D example weights");
            UAnimBlendWeights.One(.25f, new[] { 1f, 0f, .5f }, weights);
            Check(weights[1] == .5f && weights[2] == .5f, "1D unsorted samples preserve port order");
            UAnimBlendWeights.One(-20, new[] { 1f, 0f, .5f }, weights);
            Check(weights[1] == 1, "1D clamps outside range");
            var points = new[]
            {
                Vector2.zero,
                Vector2.up,
                Vector2.down,
                Vector2.left,
                Vector2.right
            };
            weights = new float[5];
            foreach(var v in new[]
            {
                Vector2.zero,
                Vector2.up,
                new Vector2(.3f, .6f),
                new Vector2(-10, 6),
                new Vector2(float.MaxValue, float.MaxValue)
            }

            )
            {
                UAnimBlendWeights.Two(v, points, weights);
                Check(weights.All(UAnimValidation.Finite) && Mathf.Abs(weights.Sum() - 1) < .001f, "2D finite normalized weights at " + v);
            }

            UAnimBlendWeights.Two(Vector2.up, points, weights);
            Check(weights[1] == 1, "2D exact sample selects only that input");
            var root = UAnimFullExample.Build();
            Check(UAnimValidation.Validate(root).Errors.Count == 0, "complete authored example validates: " + string.Join("; ", UAnimValidation.Validate(root).Errors));
            Check(root.layers[0].graph.nodes[0].machine.states.Any(s => s.name == "Move"), "State display names persist after reopening");
            var player = new GameObject("Complete verification", typeof(Animator));
            new GameObject("Body").transform.SetParent(player.transform, false);
            try
            {
                using(var instance = new UAnimInstance(root, player.GetComponent<Animator>(), true))
                {
                    instance.Set("Speed", UAnimParameterType.Float, 0);
                    instance.Tick(.25f);
                    instance.Tick(.25f);
                    Check(instance.Machines[0].CurrentState == 0, "code CanStop returns Idle");
                    instance.Set("Speed", UAnimParameterType.Float, 1);
                    instance.SetVector2("MoveInput", Vector2.up);
                    for(int i = 0; i < 25; i++)
                    {
                        instance.Tick(.02f);
                    }

                    Check(instance.Machines[0].CurrentState == 1, "code CanMove starts Move");
                    var mix = instance.Poses.First(p => p.PoseDefinition.kind == UAnimPoseKind.Blend2D && p.Activity > 0);
                    Check(mix.Weights[1] == 1, "runtime Mixer2D follows Vector2 parameter");
                    instance.SetVector2("MoveInput", new Vector2(float.NaN, 0));
                    Check(instance.GetVector2("MoveInput") == Vector2.up, "nonfinite Vector2 preserves last valid input");
                    instance.Set("Speed", UAnimParameterType.Float, float.NaN);
                    Check(instance.Get("Speed") == 1, "nonfinite float preserves last valid input");
                    mix.HasPreviewOverride = true;
                    mix.PreviewInput = Vector2.left;
                    instance.Tick(0);
                    Check(mix.Weights[3] == 1, "preview input overrides Mixer without changing parameter");
                    Check(instance.GetVector2("MoveInput") == Vector2.up, "preview override does not mutate parameters");
                    mix.HasPreviewOverride = false;
                    for(int i = 0; i < 100; i++)
                    {
                        instance.Tick(.01f);
                    }

                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for(int i = 0; i < 1000; i++)
                    {
                        instance.Tick(.01f);
                    }

                    Check(GC.GetAllocatedBytesForCurrentThread() == before, "2D and function evaluation allocate zero bytes in steady tick");
                    instance.SeekPreview(.5f);
                    Check(instance.Graph.IsValid(), "manual preview seek resets and replays safely");
                }

                VerifyTriggersAndSelf(player.GetComponent<Animator>(), root.layers[0].graph.nodes[0].machine.states[0].graph);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }

            VerifyAuthoring();
            VerifyFunctionAuthoring();
            string copy = AssetDatabase.GenerateUniqueAssetPath(UAnimFullExample.Folder + "/CopiedRoot.uanimasset");
            try
            {
                AssetDatabase.CopyAsset(UAnimFullExample.RootPath, copy);
                AssetDatabase.ImportAsset(copy);
                Check(AssetDatabase.LoadAssetAtPath<UAnimAsset>(copy).id != root.id, "duplicated root receives a distinct asset identity");
            }
            finally
            {
                AssetDatabase.DeleteAsset(copy);
            }

            UAnimEvaluationLinker.Generate();
            Check(File.ReadAllText("Assets/UAnimGraph/Generated/link.xml").Contains("LocomotionTransitions"), "player linker preserves annotated libraries");
            UAnimRootChainRegression.Run();
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/UAnimGraph-complete-verification.txt", passed);
            Debug.Log("UAnimGraph COMPLETE PASS: " + passed.Count);
        }

        static void VerifyTriggersAndSelf(Animator animator, UAnimPoseGraph pose)
        {
            var m = ScriptableObject.CreateInstance<UAnimMachine>();
            m.name = "CodeRules";
            m.id = "code";
            m.defaultState = 0;
            m.states = new[]
            {
                new UAnimState
                {
                    graph = pose,
                    duration = 1
                },
                new UAnimState
                {
                    graph = pose,
                    duration = 1
                }
            };
            string always = TransitionEvaluationRegistry.Key(typeof(LocomotionTransitions).GetMethod(nameof(LocomotionTransitions.Always)));
            m.transitions = new[]
            {
                new UAnimTransition
                {
                    from = 0,
                    to = 0,
                    allowSelf = true,
                    evaluation = always,
                    duration = .1f
                }
            };
            var g = ScriptableObject.CreateInstance<UAnimPoseGraph>();
            g.id = "test";
            g.output = 0;
            g.nodes = new[]
            {
                new UAnimPoseNode
                {
                    id = "state",
                    kind = UAnimPoseKind.StateMachine,
                    machine = m
                }
            };
            var a = ScriptableObject.CreateInstance<UAnimAsset>();
            a.layers = new[]
            {
                new UAnimLayer
                {
                    graph = g
                }
            };
            a.parameters = new[]
            {
                new UAnimParameter
                {
                    id = "attack",
                    name = "Attack",
                    type = UAnimParameterType.Trigger
                }
            };
            try
            {
                using(var instance = new UAnimInstance(a, animator, true))
                {
                    instance.Set("Attack", UAnimParameterType.Trigger, 1);
                    instance.Tick(.025f);
                    Check(instance.Get("Attack") == 0, "unused trigger expires at end of shared evaluation tick");
                    Check(instance.Machines[0].DestinationState == 0, "explicit code self transition starts another playback bank");
                    instance.Tick(.025f);
                    Check(Mathf.Approximately(instance.Machines[0].TransitionProgress, .25f), "transition continues from its recorded start without reevaluating during blend");
                }

                m.transitions[0].conditions = new[]
                {
                    new UAnimCondition
                    {
                        parameterId = "attack",
                        type = UAnimParameterType.Trigger,
                        value = 1
                    }
                };
                Check(UAnimValidation.Validate(a).Errors.Any(e => e.Contains("combine")), "code plus field condition is rejected");
                m.transitions[0].conditions = Array.Empty<UAnimCondition>();
                m.transitions[0].evaluation = TransitionEvaluationRegistry.Key(typeof(LocomotionTransitions).GetMethod(nameof(LocomotionTransitions.AfterFirstCycle)));
                m.transitions[0].to = 1;
                m.transitions[0].duration = 0;
                using(var instance = new UAnimInstance(a, animator, true))
                {
                    instance.Tick(.5f);
                    Check(instance.Machines[0].CurrentState == 0, "code Exit Time waits for threshold");
                    instance.Tick(.6f);
                    Check(instance.Machines[0].CurrentState == 1, "code Exit Time uses cumulative normalized time");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(a);
                UnityEngine.Object.DestroyImmediate(g);
                UnityEngine.Object.DestroyImmediate(m);
            }
        }

        static void VerifyAuthoring()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(UAnimFullExample.Folder + "/InspectorRoundTrip.uastate");
            try
            {
                var graph = GraphDatabase.CreateGraph<UAnimStateGraph>(path);
                var mix = new Blend2DNode();
                var clip = new ClipNode();
                var output = new StateOutputNode();
                graph.AddNode(mix);
                graph.AddNode(clip);
                graph.AddNode(output);
                clip.GetNodeOptionByName("Clip").TrySetValue(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/UAnimGraph/Demo/Walk.anim"));
                mix.Settings.inputs.RemoveAt(1);
                mix.DefineNode();
                graph.Connect(clip.GetOutputPortByName("Pose"), mix.GetInputPortByName("Pose0"));
                graph.Connect(mix.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
                GraphDatabase.SaveGraph(graph);
                AssetDatabase.ImportAsset(path);
                var runtime = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(path);
                Check(string.IsNullOrEmpty(runtime.importError), "single Pose Mixer2D compiles");
                string id = mix.ID.ToString();
                graph.UndoBeginRecordGraph("Inspector add sample", mix);
                UAnimMixerInspector.AddInput(mix);
                mix.DefineNode();
                graph.UndoEndRecordGraph();
                GraphDatabase.SaveGraph(graph);
                Check(mix.GetInputPortByName("Pose0").IsConnected, "adding input preserves existing wire");
                var added = mix.Settings.inputs[1];
                graph.Connect(clip.GetOutputPortByName("Pose"), mix.GetInputPortByName(added.port));
                GraphDatabase.SaveGraph(graph);
                AssetDatabase.ImportAsset(path);
                var loaded = GraphDatabase.LoadGraph<UAnimStateGraph>(path).GetNodes().OfType<Blend2DNode>().Single();
                Check(loaded.ID.ToString() == id && loaded.Settings.inputs[1].port == added.port, "sample IDs persist through save/reopen");
                Check(loaded.GetInputPortByName(added.port).IsConnected, "new stable-ID port wire persists");
                loaded.Settings.inputs[1].position = Vector2.zero;
                Check(UAnimAuthoringValidation.GetIssues(graph, true).Any(i => i.message.Contains("Duplicate")), "duplicate 2D sample highlighted");
                Undo.PerformUndo();
                Undo.PerformRedo();
                Check(graph.GetNodes().OfType<Blend2DNode>().Count() == 1, "Mixer edit Undo/Redo preserves graph node");
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        static void VerifyFunctionAuthoring()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath(UAnimFullExample.Folder + "/FunctionAuthoring.uamachine");
            try
            {
                var graph = StateMachineDatabase.CreateStateMachine<UAnimStateMachineGraph>(path);
                graph.requireEvaluation = true;
                var entry = new UAnimEntryState();
                var a = new PoseState();
                var b = new PoseState();
                graph.AddState(entry);
                graph.AddState(a);
                graph.AddState(b);
                graph.Connect(entry, a);
                var pose = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(UAnimRootExample.Folder + "/Idle.uastate");
                a.GetOptionByName("Graph").TrySetValue(pose);
                b.GetOptionByName("Graph").TrySetValue(pose);
                var t = graph.Connect(a, b);
                var rule = t.RuleCount == 0 ? t.AddRule() : t.GetRule(0);
                StateMachineDatabase.SaveStateMachine(graph);
                AssetDatabase.ImportAsset(path);
                Check(AssetDatabase.LoadAssetAtPath<UAnimMachine>(path).importError.Contains("Choose"), "new machine rejects a transition without a function");
                rule.RootCondition.Add(new EvaluationFunctionCondition { Value = TransitionEvaluationRegistry.Key(typeof(LocomotionTransitions).GetMethod(nameof(LocomotionTransitions.Always))) });
                StateMachineDatabase.SaveStateMachine(graph);
                AssetDatabase.ImportAsset(path);
                Check(string.IsNullOrEmpty(AssetDatabase.LoadAssetAtPath<UAnimMachine>(path).importError), "Entry plus selected function saves and compiles");
                var field = new ParameterCondition
                {
                    Value = "speed|Float|Greater|0"
                };
                rule.RootCondition.Add(field);
                StateMachineDatabase.SaveStateMachine(graph);
                AssetDatabase.ImportAsset(path);
                Check(AssetDatabase.LoadAssetAtPath<UAnimMachine>(path).importError.Contains("combine"), "native importer rejects automatic field and function combination");
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        public static void BuildPlayer()
        {
            Run();
            UAnimFullExample.CreateScene();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { UAnimFullExample.ScenePath }, locationPathName = "Build/CompleteFeatureSmoke/UAnimGraph.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if(report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("Complete feature player build failed");
            }

            File.WriteAllText("Logs/UAnimGraph-complete-player-build.txt", "PASS: Windows player build with preserved transition libraries.");
        }

        public static void OpenReview()
        {
            Run();
            var root = UAnimFullExample.Build();
            UAnimEditorWorkspace.Open(root);
            var graph = GraphDatabase.LoadGraph<UAnimStateGraph>(UAnimFullExample.Folder + "/DirectionalMove.uastate");
            UAnimMixerInspector.Open(graph.GetNodes().OfType<Blend2DNode>().Single());
            UAnimEvaluationPicker.Open(_ =>
            {
            });
        }

        public static class InvalidLibrary
        {
            public static bool Bad(in TransitionContext ctx)
            {
                return true;
            }
        }
    }
}
