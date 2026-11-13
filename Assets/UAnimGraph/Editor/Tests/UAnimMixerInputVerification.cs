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
    public static class UAnimMixerInputVerification
    {
        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly List<string> passed = new List<string>();
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static string path;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static UAnimStateGraph graph;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static Blend1DNode mix;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static int frames, stage;
        static void Check(bool ok, string text)
        {
            if(!ok)
            {
                throw new InvalidOperationException(text);
            }

            passed.Add("PASS: " + text);
        }

        public static void RunBatch()
        {
            try
            {
                RunData();
                AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(path));
                frames = stage = 0;
                EditorApplication.update += VerifyUi;
            }
            catch(Exception e)
            {
                Finish(e);
            }
        }

        static void RunData()
        {
            passed.Clear();
            path = AssetDatabase.GenerateUniqueAssetPath("Assets/MixerInputsVerification.uastate");
            graph = GraphDatabase.CreateGraph<UAnimStateGraph>(path);
            mix = new Blend1DNode();
            var clip = new ClipNode();
            var output = new StateOutputNode();
            graph.AddNode(mix);
            Check(mix.Settings.inputs != null && mix.Settings.inputs.Count == 2 && mix.GetInputPortByName("Pose0") != null && mix.GetInputPortByName("Pose1") != null, "two default inputs exist immediately at node creation");
            graph.AddNode(clip);
            graph.AddNode(output);
            clip.GetNodeOptionByName("Clip").TrySetValue(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/UAnimGraph/Demo/Walk.anim"));
            mix.Settings.inputs[0].threshold = .1f;
            mix.Settings.inputs[1].threshold = 2f;
            graph.Connect(clip.GetOutputPortByName("Pose"), mix.GetInputPortByName("Pose0"));
            graph.Connect(clip.GetOutputPortByName("Pose"), mix.GetInputPortByName("Pose1"));
            graph.Connect(mix.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
            GraphDatabase.SaveGraph(graph);
            Check(MixerAuthoring.Inputs(mix).Count == 2, "new Mixer1D starts with two Pose inputs");
            Check(!UAnimMixerInputEditing.Remove(mix, "Pose0") && !UAnimMixerInputEditing.Remove(mix, "Pose1"), "first two inputs reject deletion through the editing API");
            UAnimMixerInputEditing.Add(mix);
            string added = mix.Settings.inputs[2].port;
            Check(mix.Settings.inputs.Count == 3 && mix.Settings.inputs[0].threshold == .1f && mix.Settings.inputs[1].threshold == 2f, "adding an input preserves configured thresholds");
            Check(mix.GetInputPortByName("Pose0").IsConnected && mix.GetInputPortByName("Pose1").IsConnected, "adding a port preserves both existing Pose wires");
            UAnimMixerInputEditing.SetThreshold(mix, added, .65f);
            Check(mix.Settings.inputs[2].threshold == .65f, "per-Pose threshold edits target the stable port ID");
            graph.Connect(clip.GetOutputPortByName("Pose"), mix.GetInputPortByName(added));
            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(path);
            Check(string.IsNullOrEmpty(AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(path).importError), "three connected Pose inputs compile successfully");
            Check(UAnimMixerInputEditing.Remove(mix, added) && mix.GetInputPortByName(added) == null, "third input and its port can be removed");
            Check(mix.GetInputPortByName("Pose0").IsConnected && mix.GetInputPortByName("Pose1").IsConnected, "deleting the third port keeps the first two wires");
            Undo.PerformUndo();
            mix = graph.GetNodes().OfType<Blend1DNode>().Single();
            Check(mix.Settings.inputs.Count == 3 && mix.GetInputPortByName(added).IsConnected, "Undo restores the removed port, threshold and wire");
            Undo.PerformRedo();
            mix = graph.GetNodes().OfType<Blend1DNode>().Single();
            Check(mix.Settings.inputs.Count == 2, "Redo removes only the third input");
            GraphDatabase.SaveGraph(graph);
            mix = GraphDatabase.LoadGraph<UAnimStateGraph>(path).GetNodes().OfType<Blend1DNode>().Single();
            Check(mix.Settings.inputs[0].port == "Pose0" && mix.Settings.inputs[1].port == "Pose1", "port identities survive save and reopen");
        }

        static void VerifyUi()
        {
            if(++frames < 20)
            {
                return;
            }

            frames = 0;
            try
            {
                var window = Resources.FindObjectsOfTypeAll<EditorWindow>().First(w => w is IGraphWindow g && g.Graph?.ID == graph.ID);
                var root = window.rootVisualElement;
                Blend1DNodeView.DecoratePorts(mix, root);
                if(stage == 0)
                {
                    Check(root.Q<Button>("uanim-add-input-pose") != null, "native Mixer node contains Add Input Pose button");
                    Check(root.Query<FloatField>("threshold").ToList().Count == 2, "each native Pose port has an inline threshold field");
                    Check(root.Query<Button>("remove").ToList().All(b => !b.enabledSelf), "both initial X buttons are disabled in the native port UI");
                    Check(!root.Query<Label>().ToList().Any(l => l.text == "Threshold7" || l.text == "Count"), "legacy Count and Threshold0..7 list is hidden from the node");
                    UAnimMixerInputEditing.Add(mix);
                    stage = 1;
                    return;
                }

                Check(root.Query<FloatField>("threshold").ToList().Count == 3, "adding an input creates a third native port threshold row");
                var buttons = root.Query<Button>("remove").ToList();
                Check(buttons.Count == 3 && !buttons[0].enabledSelf && !buttons[1].enabledSelf && buttons[2].enabledSelf, "third X is enabled while the first two stay disabled");
                foreach(var field in root.Query<FloatField>("threshold").ToList())
                {
                    Check(field.parent.parent.ClassListContains("ge-port"), "threshold and X are attached to a real GTK port row");
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
            if(path != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            if(error != null)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
                return;
            }

            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/UAnimGraph-mixer-input-verification.txt", passed);
            Debug.Log("MIXER INPUTS PASS: " + passed.Count);
            try
            {
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
