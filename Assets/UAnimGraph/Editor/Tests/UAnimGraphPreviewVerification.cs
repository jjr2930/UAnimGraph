using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimGraphPreviewVerification
    {
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static int checks;
        static void Check(bool value, string message)
        {
            if(!value)
            {
                throw new InvalidOperationException(message);
            }
            checks++;
        }

        public static void RunBatch()
        {
            try
            {
                string rootPath = File.Exists("Assets/CacheRepairFixture/UserGraph.uanimasset") ? "Assets/CacheRepairFixture/UserGraph.uanimasset" : "Assets/UAnimGraph/Tests/Graphs/UAnimAsset.uanimasset";
                var root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(rootPath);
                Check(root, "User test graph exists");
                UAnimEmbeddedAssets.Restore(rootPath);
                root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(rootPath);
                var machine = root.layers.SelectMany(l => l.graph.nodes).First(n => n.machine).machine;
                string cache = UAnimEmbeddedAssets.CacheFolder + "/" + AssetDatabase.AssetPathToGUID(rootPath);
                machine = Directory.GetFiles(cache, "*.uamachine").Select(AssetDatabase.LoadAssetAtPath<UAnimMachine>).First(m => m && m.id == machine.id);
                var run = machine.states.First(s => s.name == "Run");
                string posePath = AssetDatabase.GetAssetPath(run.graph);
                string original = File.ReadAllText(rootPath);
                using(var source = new UAnimGraphPreviewSource(posePath, false))
                {
                    Check(source.Asset && source.Asset != root, "State preview uses temporary root");
                    Check(UAnimValidation.Validate(source.Asset).Errors.Count == 0, "State preview validates");
                    Check(source.Asset.parameters == root.parameters, "State preview inherits shared parameter definitions");
                    var isolatedMachine = source.Asset.layers[0].graph.nodes[0].machine;
                    Check(isolatedMachine.states.Length == 1 && isolatedMachine.states[0].graph == run.graph, "Only the currently open state is previewed");
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UAnimGraph/Tests/Models/Y Bot.fbx");
                    if(!model)
                    {
                        model = AssetDatabase.FindAssets("Y Bot t:GameObject").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).First(m => m);
                    }
                    Directory.CreateDirectory("Logs");
                    UAnimWorkbench.CapturePreview(source.Asset, model, "Logs/GraphEmbeddedPreview.png");
                    Check(File.Exists("Logs/GraphEmbeddedPreview.png"), "Preview renders visible animated model");
                    var view = ScriptableObject.CreateInstance<UAnimWorkbench>();
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var type = typeof(UAnimWorkbench);
                    try
                    {
                        view.BindRoot(source.Asset);
                        type.GetField("targetObject", flags).SetValue(view, model);
                        type.GetMethod("StartPreview", flags).Invoke(view, null);
                        var instance = (UAnimInstance)type.GetField("instance", flags).GetValue(view);
                        Check(instance != null && instance.Graph.IsValid(), "Embedded preview starts");
                        var clone = (GameObject)type.GetField("clone", flags).GetValue(view);
                        var animator = clone.GetComponentInChildren<Animator>();
                        Check(animator.avatar && animator.avatar.isHuman, "Preview model has humanoid avatar");
                        instance.SeekPreview(0);
                        var leg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                        Quaternion before = leg.localRotation;
                        instance.SeekPreview(.35f);
                        Check(Quaternion.Angle(before, leg.localRotation) > 1, "Seeking visibly changes animated bone pose");
                        Check(Math.Abs(instance.Machines[0].Elapsed - .35) < .001, "Time seeking evaluates current state");
                        type.GetField("playing", flags).SetValue(view, false);
                        double time = instance.Machines[0].Elapsed;
                        type.GetMethod("UpdatePreview", flags).Invoke(view, null);
                        Check(instance.Machines[0].Elapsed == time, "Paused preview does not advance");
                        int repaints = 0;
                        view.repaintRequested = () => repaints++;
                        type.GetMethod("UpdatePreview", flags).Invoke(view, null);
                        Check(repaints == 1, "Embedded panel receives repaint callback");
                        view.BindRoot(root);
                        Check(!instance.Graph.IsValid(), "Changing graph disposes previous playable graph");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(view);
                    }
                }
                using(var source = new UAnimGraphPreviewSource(posePath, true))
                {
                    Check(source.Asset == root, "Whole graph mode uses owning UAnimGraph");
                }
                using(var source = new UAnimGraphPreviewSource(AssetDatabase.GetAssetPath(machine), false))
                {
                    Check(source.Asset.layers[0].graph.nodes[0].machine == machine, "StateMachine preview retains its transitions");
                    Check(UAnimValidation.Validate(source.Asset).Errors.Count == 0, "Machine preview validates");
                }
                Check(File.ReadAllText(rootPath) == original, "Preview does not modify saved root");
                UAnimCompleteVerification.Run();
                Debug.Log("GRAPH PREVIEW PASS: " + checks);
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
