using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    [TransitionLibrary]
    public static class UAnimTransitionContextVerification
    {
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static TransitionContext captured;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static bool allow, mutate, snapshotPreserved, triggerObserved;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static int evaluations, checks;

        [TransitionEvaluation]
        public static bool Capture(in TransitionContext context)
        {
            captured = context;
            evaluations++;
            triggerObserved = context.SharedBlackboard.GetTrigger("Fire");
            if(mutate)
            {
                float before = context.SharedBlackboard.GetFloat("Speed");
                context.SharedBlackboard.SetFloat("Speed", 99);
                snapshotPreserved = context.SharedBlackboard.GetFloat("Speed") == before;
            }
            return allow;
        }

        static void Check(bool ok, string message)
        {
            if(!ok)
            {
                throw new InvalidOperationException(message);
            }
            checks++;
            Debug.Log("TRANSITION CONTEXT CHECK: " + message);
        }

        public static void Run()
        {
            checks = evaluations = 0;
            allow = false;
            mutate = true;
            var clip = new AnimationClip();
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 2, 1));
            var pose = ScriptableObject.CreateInstance<UAnimPoseGraph>();
            pose.id = "pose";
            pose.stateGraph = true;
            pose.output = 0;
            pose.nodes = new[] { new UAnimPoseNode { id = "clip", clip = clip, kind = UAnimPoseKind.Clip, loop = true } };
            var machine = ScriptableObject.CreateInstance<UAnimMachine>();
            machine.id = "machine";
            machine.defaultState = 0;
            machine.states = new[]
            {
                new UAnimState { name = "Idle", graph = pose, duration = 2 },
                new UAnimState { name = "Run", graph = pose, duration = 2 }
            };
            machine.transitions = new[]
            {
                new UAnimTransition
                {
                    id = "edge", from = 0, to = 1, duration = .4f,
                    evaluation = TransitionEvaluationRegistry.Key(typeof(UAnimTransitionContextVerification).GetMethod(nameof(Capture)))
                }
            };
            var rootPose = ScriptableObject.CreateInstance<UAnimPoseGraph>();
            rootPose.id = "root-pose";
            rootPose.output = 0;
            rootPose.nodes = new[] { new UAnimPoseNode { id = "machine", kind = UAnimPoseKind.StateMachine, machine = machine } };
            var root = ScriptableObject.CreateInstance<UAnimAsset>();
            root.layers = new[] { new UAnimLayer { graph = rootPose } };
            root.parameters = new[]
            {
                new UAnimParameter { id = "speed", name = "Speed", defaultValue = 3 },
                new UAnimParameter { id = "count", name = "Count", type = UAnimParameterType.Int, defaultValue = 2 },
                new UAnimParameter { id = "ready", name = "Ready", type = UAnimParameterType.Bool, defaultValue = 1 },
                new UAnimParameter { id = "fire", name = "Fire", type = UAnimParameterType.Trigger },
                new UAnimParameter { id = "move", name = "Move", type = UAnimParameterType.Vector2, defaultVector2 = Vector2.up }
            };
            var actor = new GameObject("Output Animator", typeof(Animator));
            var host = new GameObject("Graph Runner");
            var other = new GameObject("Other Graph", typeof(Animator));
            try
            {
                var expected = new[] { "GraphOwner", "GraphInstance", "SharedBlackboard", "From", "TransitionStartNormalizedTimeAtFromState", "TransitionStartTimeAtFromState", "To", "TransitionDuration" };
                var fields = typeof(TransitionContext).GetFields(BindingFlags.Public | BindingFlags.Instance);
                Check(fields.Length == 8 && fields.All(field => field.IsInitOnly) && expected.All(name => fields.Any(field => field.Name == name)), "exactly eight requested readonly context fields");
                Check(typeof(TransitionContext).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length == 0 &&
                    typeof(TransitionContext).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length == 0,
                    "context exposes the new field contract without legacy accessor APIs");
                using(var instance = new UAnimInstance(root, actor.GetComponent<Animator>(), true, host))
                {
                    Check(instance.SharedBlackboard.GetFloat("Speed") == 3, "blackboard exposes default values before the first tick");
                    instance.SharedBlackboard.SetTrigger("Fire");
                    instance.Tick(2.25f);
                    Check(captured.GraphOwner == host && captured.GraphInstance == instance, "context uses the executing GameObject and graph instance");
                    Check(ReferenceEquals(captured.SharedBlackboard, instance.SharedBlackboard), "context references the instance shared blackboard");
                    Check(captured.From.Definition == machine.states[0] && captured.To.Definition == machine.states[1], "From and To identify the actual states");
                    Check(captured.From.Name == "Idle" && captured.To.Name == "Run", "state names are accessible");
                    Check(Mathf.Approximately(captured.TransitionStartNormalizedTimeAtFromState, .125f) && Mathf.Approximately(captured.TransitionStartTimeAtFromState, 2.25f), "looping normalized time stays in 0..1 while state seconds accumulate");
                    Check(Math.Abs(captured.From.TotalNormalizedTime - 1.125) < .00001, "cumulative timing is read through the From state");
                    Check(snapshotPreserved && triggerObserved && !instance.SharedBlackboard.GetTrigger("Fire"), "shared reads use the tick snapshot and triggers still expire");
                    Check(instance.SharedBlackboard.GetInt("Count") == 2 && instance.SharedBlackboard.GetBool("Ready") && instance.SharedBlackboard.GetVector2("Move") == Vector2.up, "blackboard typed parameter access works");
                    allow = true;
                    mutate = false;
                    instance.Tick(.25f);
                    var active = instance.Machines[0].ActiveTransitionContext.Value;
                    Check(active.TransitionDuration == .4f && active.TransitionStartTimeAtFromState == 2.5f && active.TransitionStartNormalizedTimeAtFromState == .25f, "accepted transition records its start time and duration");
                    Check(instance.Machines[0].TransitionProgress == 0 && active.To.Time == 0, "transition and destination playback start together at zero");
                    int calls = evaluations;
                    instance.Tick(.2f);
                    Check(Mathf.Approximately(instance.Machines[0].TransitionProgress, .5f) && evaluations == calls, "duration controls blending without reevaluating conditions");
                    Check(instance.Machines[0].ActiveTransitionContext.Value.TransitionStartTimeAtFromState == 2.5f, "recorded start time stays fixed during the blend");
                    instance.Tick(.199f);
                    Check(instance.Machines[0].CurrentState == 0, "transition does not finish before its duration");
                    instance.Tick(.002f);
                    Check(instance.Machines[0].CurrentState == 1 && !instance.Machines[0].ActiveTransitionContext.HasValue, "transition finishes after the full configured duration");
                    Check(active.From.Time == 2.5 && active.To.Name == "Run", "retained context preserves state time snapshots");
                }
                allow = false;
                using(var first = new UAnimInstance(root, actor.GetComponent<Animator>(), true))
                using(var second = new UAnimInstance(root, other.GetComponent<Animator>(), true))
                {
                    first.SharedBlackboard.SetFloat("Speed", 7);
                    Check(second.SharedBlackboard.GetFloat("Speed") == 3, "different graph instances never share runtime values");
                    for(int i = 0; i < 100; i++)
                    {
                        first.Tick(.01f);
                    }
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    for(int i = 0; i < 1000; i++)
                    {
                        first.Tick(.01f);
                    }
                    Check(GC.GetAllocatedBytesForCurrentThread() == before, "context evaluation produces no steady-state managed allocations");
                }
                pose.nodes[0].loop = false;
                using(var instance = new UAnimInstance(root, actor.GetComponent<Animator>(), true))
                {
                    instance.Tick(4);
                    Check(captured.TransitionStartNormalizedTimeAtFromState == 1, "non-looping normalized time clamps at one");
                }
                allow = true;
                machine.transitions[0].duration = 0;
                using(var instance = new UAnimInstance(root, actor.GetComponent<Animator>(), true))
                {
                    instance.Tick(.1f);
                    Check(instance.Machines[0].CurrentState == 1 && captured.TransitionDuration == 0, "zero-duration transitions complete immediately");
                }
                machine.transitions[0].to = 0;
                machine.transitions[0].allowSelf = true;
                machine.transitions[0].duration = .4f;
                using(var instance = new UAnimInstance(root, actor.GetComponent<Animator>(), true))
                {
                    instance.Tick(.1f);
                    Check(captured.From.Definition == captured.To.Definition && captured.From.Time > captured.To.Time, "self-transition preserves separate source and destination timing");
                }
                Debug.Log("TRANSITION CONTEXT PASS: " + checks);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(host);
                UnityEngine.Object.DestroyImmediate(other);
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(rootPose);
                UnityEngine.Object.DestroyImmediate(machine);
                UnityEngine.Object.DestroyImmediate(pose);
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                UAnimCompleteVerification.Run();
                UAnimTransitionUiVerification.Run();
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
