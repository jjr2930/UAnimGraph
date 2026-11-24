using System;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UAnimGraph.Authoring
{
    public static class UAnimDedicatedEditorVerification
    {
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static string folder, rootPath, machinePath, posePath, userSource;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static UAnimGraphEditor window;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static double started;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static int checks, nativeWindows, stage;
        static void Check(bool value, string description)
        {
            if(!value)
            {
                throw new InvalidOperationException(description);
            }

            checks++;
            Debug.Log("DEDICATED CHECK: " + description);
        }

        public static void RunBatch()
        {
            try
            {
                checks = stage = 0;
                VerifyData();
                nativeWindows = Resources.FindObjectsOfTypeAll<EditorWindow>().Count(w => w is IGraphWindow);
                window = UAnimGraphEditor.OpenPath(rootPath);
                started = EditorApplication.timeSinceStartup;
                EditorApplication.update += VerifyUi;
            }
            catch(Exception exception)
            {
                Finish(exception);
            }
        }

        static void VerifyData()
        {
            folder = "Assets/DedicatedEditorVerification-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
            rootPath = folder + "/Character.uanimasset";
            var root = GraphDatabase.CreateGraph<UAnimRootGraph>(rootPath);
            root.layerChain = true;
            var layer = new RootLayerNode();
            var output = new LayerOutputNode();
            root.AddNode(layer);
            root.AddNode(output);
            layer.GetNodeOptionByName("Name").TrySetValue("Base Layer");
            layer.Title = "Base Layer";
            output.Position = new Vector2(420, 0);
            root.Connect(layer.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
            GraphDatabase.SaveGraph(root);
            var document = new UAnimEditorDocument(rootPath);
            machinePath = document.ChildPath(layer);
            Check(File.Exists(machinePath), "Layer owns an embedded StateMachine without opening Toolkit UI");
            var machine = new UAnimEditorDocument(machinePath);
            var idle = machine.States.OfType<PoseState>().Single();
            machine.Edit("Rename Idle", () =>
            {
                idle.Title = "Idle";
                idle.GetOptionByName("Name").TrySetValue("Idle");
            });
            posePath = machine.ChildPath(idle);
            Check(File.Exists(posePath), "State owns an embedded Pose graph");
            var pose = new UAnimEditorDocument(posePath);
            var clip = pose.Nodes.OfType<ClipNode>().Single();
            var walk = AssetDatabase.LoadAllAssetsAtPath("Assets/UAnimGraph/Tests/Animations/Y Bot@Walking.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            pose.SetOption(clip.GetNodeOptionByName("Clip"), typeof(AnimationClip), walk, clip);
            var stateOutput = pose.Nodes.OfType<StateOutputNode>().Single();
            Check(Mathf.Abs(stateOutput.Option<float>("Duration") - walk.length) < .001f, "Clip assignment keeps original animation duration");
            Vector2 before = clip.Position;
            string clipId = clip.ID.ToString();
            pose.Move(clipId, new Vector2(150, 80));
            Check(pose.Nodes.Single(n => n.ID.ToString() == clipId).Position == new Vector2(150, 80), "Node movement edits persisted model position");
            Undo.PerformUndo();
            pose.Reload();
            Check(pose.Nodes.Single(n => n.ID.ToString() == clipId).Position == before, "Node movement supports Undo");
            pose.Save();
            clip = pose.Nodes.OfType<ClipNode>().Single();
            var mixer = (Blend1DNode)pose.AddNode(typeof(Blend1DNode), new Vector2(350, 50));
            Check(mixer.GetInputPorts().Count(p => p.DataType == typeof(UAnimPose)) == 2, "Mixer has two default Pose inputs");
            pose.Connect(clip.GetOutputPortByName("Pose"), mixer.GetInputPortByName("Pose0"));
            pose.Connect(clip.GetOutputPortByName("Pose"), mixer.GetInputPortByName("Pose1"));
            pose.Connect(mixer.GetOutputPortByName("Pose"), pose.Nodes.OfType<StateOutputNode>().Single().GetInputPortByName("Pose"));
            Check(pose.Nodes.OfType<StateOutputNode>().Single().GetInputPortByName("Pose").FirstConnectedPort.ID == mixer.GetOutputPortByName("Pose").ID, "Reconnecting a Pose input replaces its previous wire");
            UAnimMixerInputEditing.Add(mixer);
            Check(MixerAuthoring.Inputs(mixer).Count == 3, "Dedicated Mixer adds a third Pose port");
            string added = MixerAuthoring.Inputs(mixer)[2].port;
            UAnimMixerInputEditing.Remove(mixer, added);
            Check(MixerAuthoring.Inputs(mixer).Count == 2, "Dedicated Mixer removes optional Pose ports");
            Check(!UAnimMixerInputEditing.Remove(mixer, "Pose0"), "First two Mixer inputs remain protected");
            bool cycle = false;
            try
            {
                pose.Connect(mixer.GetOutputPortByName("Pose"), mixer.GetInputPortByName("Pose0"));
            }
            catch(InvalidOperationException)
            {
                cycle = true;
            }

            Check(cycle, "Cycle connection is rejected before changing the graph");
            var constant = (ConstantNode)pose.AddNode(typeof(ConstantNode), new Vector2(0, 300));
            bool mismatch = false;
            try
            {
                pose.Connect(constant.GetOutputPortByName("Value"), mixer.GetInputPortByName("Pose0"));
            }
            catch(InvalidOperationException)
            {
                mismatch = true;
            }

            Check(mismatch, "Mismatched port types are rejected");
            pose.Delete(constant.ID.ToString());
            Check(!pose.Nodes.OfType<ConstantNode>().Any(), "Node deletion persists");
            UAnimMixerInputEditing.Edit(mixer, "Natural clip timing", () => mixer.Settings.independentTime = true);
            string parameter = UAnimSharedBlackboard.Add(rootPath, UAnimParameterType.Float);
            UAnimSharedBlackboard.Update(rootPath, parameter, "speed", 0, Vector2.zero);
            Check(UAnimSharedBlackboard.Parameters(rootPath).Single().name == "speed", "Root blackboard shares stable parameter IDs");
            var run = machine.AddState(new Vector2(420, 240));
            machine.Edit("Rename Run", () =>
            {
                run.Title = "Run";
                run.GetOptionByName("Name").TrySetValue("Run");
            });
            var runPose = new UAnimEditorDocument(machine.ChildPath(run));
            var runClip = runPose.Nodes.OfType<ClipNode>().Single();
            runPose.SetOption(runClip.GetNodeOptionByName("Clip"), typeof(AnimationClip), walk, runClip);
            var edge = machine.ConnectStates(idle, run);
            string key = TransitionEvaluationRegistry.Key(typeof(LocomotionTransitions).GetMethod(nameof(LocomotionTransitions.CanMove)));
            UAnimTransitionConditions.Choose(machine.Machine, edge, key);
            Check(UAnimTransitionConditions.Functions(edge.GetRule(0).RootCondition).Single().Value == key, "Transition function selection is saved on the selected edge");
            UAnimEmbeddedAssets.Flush();
            var asset = AssetDatabase.LoadAssetAtPath<UAnimAsset>(rootPath);
            Check(asset && UAnimValidation.Validate(asset).Errors.Count == 0, "Dedicated editing compiles into the existing runtime format: " + string.Join("; ", UAnimValidation.Validate(asset).Errors));
            var actor = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UAnimGraph/Tests/Models/Y Bot.fbx"));
            try
            {
                using(var instance = new UAnimInstance(asset, actor.GetComponentInChildren<Animator>(), true))
                {
                    instance.Set("speed", UAnimParameterType.Float, 1);
                    for(int i = 0; i < 30; i++)
                    {
                        instance.Tick(.02f);
                    }

                    Check(instance.Machines[0].Definition.states[instance.Machines[0].CurrentState].name == "Run", "Edited transition executes Idle to Run at runtime");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }

            machine.Reload();
            edge = machine.Transitions.First(t => t.FromState.Title == "Idle");
            machine.Delete(edge.ID.ToString(), true);
            Check(!machine.Transitions.Any(t => t.FromState.Title == "Idle"), "Transition deletion preserves the other States");
            machine.Save();
            Check(File.ReadAllText(rootPath).Contains("Run"), "Embedded changes are archived into the single root asset");
        }

        static void VerifyUi()
        {
            try
            {
                if(EditorApplication.timeSinceStartup - started > 30)
                {
                    throw new InvalidOperationException("Dedicated editor UI did not finish attaching");
                }

                if(!window || window.rootVisualElement.Q("uanim-dedicated-canvas")?.panel == null)
                {
                    return;
                }

                if(stage == 0)
                {
                    Check(window.rootVisualElement.Query<VisualElement>().ToList().Any(v => v.name.StartsWith("uanim-card-")), "Custom root nodes are rendered without Toolkit views");
                    Check(window.rootVisualElement.Q("uanim-editor-navigation") != null, "Navigation tree is embedded");
                    window.Navigate(machinePath);
                    stage = 1;
                    return;
                }

                if(stage == 1)
                {
                    Check(window.Document.IsMachine, "The same dedicated window navigates into StateMachines");
                    Check(window.rootVisualElement.Query<Button>().ToList().Count(b => b.text == "Open State Pose Graph") == 2, "Idle and Run have identical dedicated State controls");
                    window.Navigate(posePath);
                    stage = 2;
                    return;
                }

                if(stage == 2)
                {
                    Check(!window.Document.IsMachine && window.Document.Graph is UAnimStateGraph, "The same window navigates into Pose graphs");
                    Check(Resources.FindObjectsOfTypeAll<EditorWindow>().Count(w => w is IGraphWindow) == nativeWindows, "Editing opens no native Toolkit graph window");
                    Check(window.rootVisualElement.Q("uanim-editor-side") != null, "Inspector and preview share the dedicated window");
                    const string userPath = "Assets/CacheRepairFixture/UserGraph.uanimasset";
                    if(File.Exists(userPath))
                    {
                        userSource = File.ReadAllText(userPath);
                        window.Navigate(userPath);
                        stage = 3;
                        return;
                    }
                }

                if(stage == 3)
                {
                    Check(File.ReadAllText("Assets/CacheRepairFixture/UserGraph.uanimasset") == userSource, "Opening the existing user sample does not rewrite its graph asset");
                    var layer = window.Document.Nodes.OfType<RootLayerNode>().First();
                    var machine = new UAnimEditorDocument(window.Document.ChildPath(layer));
                    var run = machine.States.First(s => s.Title == "Run");
                    using(var source = new UAnimGraphPreviewSource(machine.ChildPath(run), false))
                    {
                        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UAnimGraph/Tests/Models/Y Bot.fbx");
                        Directory.CreateDirectory("Logs");
                        UAnimWorkbench.CapturePreview(source.Asset, model, "Logs/DedicatedEditorPreview.png");
                        Check(File.Exists("Logs/DedicatedEditorPreview.png"), "Existing Run graph renders an animated model in the shared preview engine");
                    }
                }

                Finish(null);
            }
            catch(Exception exception)
            {
                Finish(exception);
            }
        }

        static void Finish(Exception exception)
        {
            EditorApplication.update -= VerifyUi;
            if(window)
            {
                window.Close();
            }

            if(exception != null)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }

            if(folder != null)
            {
                string guid = AssetDatabase.AssetPathToGUID(rootPath);
                AssetDatabase.DeleteAsset(folder);
                AssetDatabase.DeleteAsset(UAnimEmbeddedAssets.CacheFolder + "/" + guid);
            }

            Debug.Log("DEDICATED EDITOR PASS: " + checks);
            UAnimCompleteVerification.Run();
            EditorApplication.Exit(0);
        }
    }
}
