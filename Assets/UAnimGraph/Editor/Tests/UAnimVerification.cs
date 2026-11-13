using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;

namespace UAnimGraph.Authoring
{
    public static class UAnimVerification
    {
        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly List<string> passed = new List<string>();
        static void Check(bool ok, string name)
        {
            if(!ok)
            {
                throw new Exception("FAILED: " + name);
            }

            passed.Add(name);
        }

        static UAnimPoseGraph Clip(AnimationClip clip, bool state = true)
        {
            var g = ScriptableObject.CreateInstance<UAnimPoseGraph>();
            g.name = "TestPose";
            g.stateGraph = state;
            g.output = 0;
            g.nodes = new[]
            {
                new UAnimPoseNode
                {
                    id = Guid.NewGuid().ToString("N"),
                    kind = UAnimPoseKind.Clip,
                    clip = clip
                }
            };
            return g;
        }

        static UAnimAsset Root(UAnimMachine machine, UAnimParameter parameter, int layers = 1)
        {
            var g = ScriptableObject.CreateInstance<UAnimPoseGraph>();
            g.output = 0;
            g.name = "TestLayer";
            g.nodes = new[]
            {
                new UAnimPoseNode
                {
                    id = "machine",
                    kind = UAnimPoseKind.StateMachine,
                    machine = machine
                }
            };
            var a = ScriptableObject.CreateInstance<UAnimAsset>();
            a.name = "TestRoot";
            a.parameters = new[]
            {
                parameter
            };
            a.layers = new UAnimLayer[layers];
            for(int i = 0; i < layers; i++)
            {
                a.layers[i] = new UAnimLayer
                {
                    graph = g
                };
            }

            return a;
        }

        [MenuItem("UAnimGraph/Run Verification")]
        public static void Run()
        {
            passed.Clear();
            var clip = new AnimationClip();
            clip.SetCurve("Body", typeof(Transform), "localPosition.y", AnimationCurve.Linear(0, 0, 1, 1));
            var pose = Clip(clip);
            var parameter = new UAnimParameter
            {
                id = "go",
                name = "Go",
                type = UAnimParameterType.Trigger
            };
            var machine = ScriptableObject.CreateInstance<UAnimMachine>();
            machine.name = "TestMachine";
            machine.defaultState = 0;
            machine.states = new[]
            {
                new UAnimState
                {
                    name = "Idle",
                    graph = pose
                },
                new UAnimState
                {
                    name = "Move",
                    graph = pose
                }
            };
            machine.transitions = new[]
            {
                new UAnimTransition
                {
                    id = "b",
                    from = 0,
                    to = 1,
                    duration = 0,
                    conditions = new[]
                    {
                        new UAnimCondition
                        {
                            parameterId = "go",
                            type = UAnimParameterType.Trigger,
                            value = 1
                        }
                    }
                }
            };
            var asset = Root(machine, parameter, 2);
            var go = new GameObject("Verification", typeof(Animator));
            new GameObject("Body").transform.SetParent(go.transform, false);
            var second = new GameObject("Second Verification", typeof(Animator));
            new GameObject("Body").transform.SetParent(second.transform, false);
            try
            {
                Check(UAnimValidation.Validate(asset).Errors.Count == 0, "valid definition");
                using(var a = new UAnimInstance(asset, go.GetComponent<Animator>(), true))
                {
                    using(var b = new UAnimInstance(asset, second.GetComponent<Animator>(), true))
                    {
                        a.Set("Go", UAnimParameterType.Trigger, 1);
                        a.Tick(.016f);
                        b.Tick(.016f);
                        Check(a.Machines.All(m => m.CurrentState == 1), "trigger snapshot shared by every layer");
                        Check(a.Get("go") == 0, "used trigger consumed at end of tick");
                        Check(b.Machines.All(m => m.CurrentState == 0), "two instances have independent state and parameters");
                        bool rejected = false;
                        try
                        {
                            using(var duplicate = new UAnimInstance(asset, go.GetComponent<Animator>(), true))
                            {
                            }
                        }
                        catch(InvalidOperationException)
                        {
                            rejected = true;
                        }

                        Check(rejected, "one output owner per Animator enforced");
                        Check(a.Machines.All(m => m.Elapsed == 0), "zero second transition resets destination time");
                        a.Set("go", UAnimParameterType.Trigger, 1);
                        a.Tick(.1f);
                        Check(a.Get("go") == 0, "unused trigger expires at end of tick");
                        a.SetLayer(0, false, 1);
                        a.SetLayer(0, true, 1);
                        Check(a.Machines[0].CurrentState == 0, "reactivating layer resets default state");
                    }
                }

                machine.transitions[0].duration = .2f;
                using(var a = new UAnimInstance(asset, go.GetComponent<Animator>(), true))
                {
                    a.Set("go", UAnimParameterType.Trigger, 1);
                    a.Tick(.05f);
                    Check(a.Machines[0].DestinationState == 1 && Mathf.Approximately(a.Machines[0].TransitionProgress, 0), "crossfade starts at the recorded transition time");
                    a.Tick(.05f);
                    Check(a.Machines[0].DestinationState == 1 && Mathf.Approximately(a.Machines[0].TransitionProgress, .25f), "linear crossfade progress");
                    a.Tick(.15f);
                    Check(a.Machines[0].CurrentState == 1 && a.Machines[0].DestinationState == -1, "crossfade completion");
                }

                parameter.type = UAnimParameterType.Float;
                machine.transitions[0].conditions = Array.Empty<UAnimCondition>();
                machine.transitions[0].duration = 0;
                machine.transitions[0].hasExitTime = true;
                machine.transitions[0].exitTime = .5f;
                using(var a = new UAnimInstance(asset, go.GetComponent<Animator>(), true))
                {
                    a.Tick(.8f);
                    Check(a.Machines[0].CurrentState == 1, "exit time crossing survives skipped frame");
                }

                parameter.type = UAnimParameterType.Int;
                using(var a = new UAnimInstance(asset, go.GetComponent<Animator>(), true))
                {
                    a.Set("go", UAnimParameterType.Int, int.MaxValue);
                    Check(a.Get("go") == int.MaxValue, "int parameters retain full 32-bit precision");
                }

                parameter.type = UAnimParameterType.Float;
                machine.states = machine.states.Concat(new[] { new UAnimState { graph = pose, name = "HighPriority" } }).ToArray();
                machine.transitions = new[]
                {
                    new UAnimTransition
                    {
                        id = "z",
                        from = 0,
                        to = 1,
                        priority = 1,
                        duration = 0
                    },
                    new UAnimTransition
                    {
                        id = "a",
                        from = 0,
                        to = 2,
                        priority = 2,
                        duration = 0
                    }
                };
                using(var a = new UAnimInstance(asset, go.GetComponent<Animator>(), true))
                {
                    a.Tick(.01f);
                    Check(a.Machines[0].CurrentState == 2, "higher priority wins");
                }

                machine.transitions[0].priority = 2;
                using(var a = new UAnimInstance(asset, go.GetComponent<Animator>(), true))
                {
                    a.Tick(.01f);
                    Check(a.Machines[0].CurrentState == 2, "equal priority uses stable transition ID");
                }

                machine.transitions = new[]
                {
                    new UAnimTransition
                    {
                        from = 0,
                        to = 1,
                        duration = 0
                    },
                    new UAnimTransition
                    {
                        from = 1,
                        to = 2,
                        duration = 0
                    }
                };
                using(var a = new UAnimInstance(asset, go.GetComponent<Animator>(), true))
                {
                    a.Tick(.01f);
                    Check(a.Machines[0].CurrentState == 1, "at most one new transition per tick");
                }

                machine.transitions = new[]
                {
                    new UAnimTransition
                    {
                        from = 0,
                        to = 0,
                        duration = 0,
                        allowSelf = false
                    }
                };
                using(var a = new UAnimInstance(asset, go.GetComponent<Animator>(), true))
                {
                    a.Tick(.2f);
                    Check(a.Machines[0].Elapsed > .19, "self transition requires opt in");
                }

                machine.transitions[0].allowSelf = true;
                using(var a = new UAnimInstance(asset, go.GetComponent<Animator>(), true))
                {
                    a.Tick(.2f);
                    Check(a.Machines[0].Elapsed == 0, "explicit self transition reenters at zero");
                }

                machine.transitions[0].duration = -1;
                Check(UAnimValidation.Validate(asset).Errors.Count > 0, "negative transition rejected");
                machine.transitions[0].duration = 0;
                pose.nodes[0].clip = null;
                Check(UAnimValidation.Validate(asset).Errors.Count > 0, "missing clip rejected");
                pose.nodes[0].clip = clip;
                parameter.defaultValue = float.NaN;
                Check(UAnimValidation.Validate(asset).Errors.Count > 0, "NaN rejected");
                parameter.defaultValue = 0;
                machine.transitions[0].conditions = new[]
                {
                    new UAnimCondition
                    {
                        parameterId = "missing",
                        type = UAnimParameterType.Float
                    }
                };
                Check(UAnimValidation.Validate(asset).Errors.Count > 0, "parameter contract mismatch rejected");
                machine.transitions[0].conditions = Array.Empty<UAnimCondition>();
                var blend = ScriptableObject.CreateInstance<UAnimPoseGraph>();
                blend.stateGraph = false;
                blend.output = 2;
                blend.nodes = new[]
                {
                    pose.nodes[0],
                    new UAnimPoseNode
                    {
                        id = "other",
                        kind = UAnimPoseKind.Clip,
                        clip = clip
                    },
                    new UAnimPoseNode
                    {
                        id = "blend",
                        kind = UAnimPoseKind.Blend1D,
                        inputs = new[]
                        {
                            0,
                            1
                        },
                        thresholds = new[]
                        {
                            0f,
                            1f
                        },
                        value = new UAnimValue
                        {
                            parameterId = "go"
                        }
                    }
                };
                var ba = ScriptableObject.CreateInstance<UAnimAsset>();
                ba.parameters = new[]
                {
                    parameter
                };
                ba.layers = new[]
                {
                    new UAnimLayer
                    {
                        graph = blend
                    }
                };
                using(var a = new UAnimInstance(ba, go.GetComponent<Animator>(), true))
                {
                    a.Set("go", UAnimParameterType.Float, .25f);
                    a.Tick(0);
                    var root = a.Graph.GetRootPlayable(0); // layer mixer is the sole output root
                    Check(Mathf.Approximately(root.GetInput(0).GetInputWeight(0), .75f) && Mathf.Approximately(root.GetInput(0).GetInputWeight(1), .25f), "Blend1D interpolation");
                    for(int i = 0; i < 100; i++)
                    {
                        a.Tick(.016f);
                    }

                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for(int i = 0; i < 1000; i++)
                    {
                        a.Tick(.016f);
                    }

                    long delta = GC.GetAllocatedBytesForCurrentThread() - before;
                    Check(delta == 0, "steady tick allocates zero managed bytes (1000 ticks)");
                    a.Dispose();
                    Check(!a.Graph.IsValid(), "playable graph disposal is idempotent");
                }

                blend.nodes[2].thresholds = new[]
                {
                    1f,
                    1f
                };
                Check(UAnimValidation.Validate(ba).Errors.Count > 0, "duplicate thresholds rejected");
                blend.nodes[2].thresholds = new[]
                {
                    0f,
                    1f
                };
                blend.nodes[2].inputs[0] = 2;
                Check(UAnimValidation.Validate(ba).Errors.Any(e => e.Contains("cycle")), "pose cycle rejected");
                VerifyToolkit(clip);
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/UAnimGraph-verification.txt", passed.Select(p => "PASS: " + p));
                Debug.Log("UAnimGraph verification PASS: " + passed.Count + " checks");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        static void VerifyToolkit(AnimationClip clip)
        {
            const string folder = "Assets/UAnimGraph/Verification";
            if(!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/UAnimGraph", "Verification");
            }

            string clipPath = folder + "/Verification.anim";
            if(!AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath))
            {
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            else
            {
                clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/RoundTrip.uapose");
            var graph = GraphDatabase.CreateGraph<UAnimLayerGraph>(path);
            var c = new ClipNode();
            var o = new LayerOutputNode();
            graph.AddNode(c);
            graph.AddNode(o);
            c.GetNodeOptionByName("Clip").TrySetValue(clip);
            graph.Connect(c.GetOutputPortByName("Pose"), o.GetInputPortByName("Pose"));
            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(path);
            var loaded = GraphDatabase.LoadGraph<UAnimLayerGraph>(path);
            Check(loaded.GetNodes().OfType<ClipNode>().Count() == 1, "Graph Toolkit save/reopen preserves nodes");
            var runtime = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(path);
            Check(runtime && string.IsNullOrEmpty(runtime.importError) && runtime.nodes.Length == 1 && runtime.nodes[0].clip == clip, "Graph Toolkit importer produces playable runtime definition");
            var actor = new GameObject("Overlay verification", typeof(Animator));
            var animatedBody = new GameObject("Body");
            animatedBody.transform.SetParent(actor.transform, false);
            try
            {
                var root = ScriptableObject.CreateInstance<UAnimAsset>();
                root.layers = new[]
                {
                    new UAnimLayer
                    {
                        graph = runtime
                    }
                };
                using(var instance = new UAnimInstance(root, actor.GetComponent<Animator>(), true))
                {
                    using(var view = new UAnimGraphDebugView())
                    {
                        instance.Tick(.1f);
                        view.Apply(instance);
                        Check(instance.Poses[0].Activity == 1, "selected target supplies graph node activity overlay");
                        Check(animatedBody.transform.localPosition.y > .05f, "Playable output actually animates Animator target transforms");
                        view.Dispose();
                        Check(view.ContextCount == 0, "changing debug target clears graph overlays");
                    }
                }

                UnityEngine.Object.DestroyImmediate(root);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }

            string badPath = AssetDatabase.GenerateUniqueAssetPath(folder + "/Bad.uapose");
            var bad = GraphDatabase.CreateGraph<UAnimLayerGraph>(badPath);
            bad.AddNode(new LayerOutputNode());
            var invalid = UAnimGraphCompiler.Compile(bad, false, _ =>
            {
            });
            Check(!string.IsNullOrEmpty(invalid.importError), "compiler rejects unconnected output");
            var issues = UAnimAuthoringValidation.GetIssues(bad, false);
            Check(issues.Any(i => i.element is LayerOutputNode), "authoring errors carry exact node context for navigation");
            AssetDatabase.DeleteAsset(badPath);
            // Keep only valid persistent fixtures so build-wide validation remains usable.
            AssetDatabase.SaveAssets();
        }

        public static void RunAndBuild()
        {
            Run();
            UAnimDemoBuilder.BuildSmokePlayer();
        }
    }
}
