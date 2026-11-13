using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UAnimGraph.Authoring
{
    [InitializeOnLoad]
    public static class UAnimAnimationWorkflow
    {
        const string Folder = "Assets/UAnimGraph/Tests/Locomotion";
        const string ScenePath = Folder + "/YBotLocomotion.unity";
        const string Pending = "UAnimGraph.AnimationWorkflow.PendingPlay";
        const string Batch = "UAnimGraph.AnimationWorkflow.Batch";
        const string BatchResult = "UAnimGraph.AnimationWorkflow.BatchResult";
        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static double began;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static int firstFrame, stateMask, errors;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static Quaternion firstRotation;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static bool moved;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static bool waitingForPlay;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static double readyAt;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static Transform bone;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static UAnimGraphPlayer player;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly List<string> messages = new List<string>();
        static UAnimAnimationWorkflow()
        {
            EditorApplication.playModeStateChanged += ModeChanged;
            EditorApplication.update += Observe;
            if(SessionState.GetBool(Pending, false))
            {
                Application.logMessageReceived += Capture;
            }
        }

        public static void RunBackground()
        {
            SessionState.SetBool(Batch, true);
            Build();
            UAnimVerification.Run();
            UAnimDraftRegression.Run();
            UAnimSingleAssetRegression.Run();
            // Command-line execution happens before editor startup delay calls.
            // Let indexing and other initialization finish before measuring Play.
            readyAt = EditorApplication.timeSinceStartup + 2;
            waitingForPlay = true;
        }

        [MenuItem("UAnimGraph/Create Y Bot Animation and Verify")]
        public static void BuildAndVerify()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play mode before authoring animation assets.");
            }

            Build();
            UAnimVerification.Run();
            if(Application.isBatchMode)
            {
                return;
            }

            if(!File.Exists(ScenePath))
            {
                return;
            }

            if(SceneManager.GetActiveScene().isDirty)
            {
                Debug.LogWarning("Y Bot animation is ready. Save the current scene, then use UAnimGraph/Verify Y Bot in Play Mode.");
                return;
            }

            VerifyInPlayMode();
        }

        [MenuItem("UAnimGraph/Verify Y Bot in Play Mode")]
        public static void VerifyInPlayMode()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            {
                throw new InvalidOperationException("Save the current scene and stop Play mode first.");
            }

            EditorSceneManager.OpenScene(ScenePath);
            errors = 0;
            messages.Clear();
            Application.logMessageReceived -= Capture;
            Application.logMessageReceived += Capture;
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        static void ModeChanged(PlayModeStateChange state)
        {
            if(state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
            {
                player = Object.FindObjectsByType<UAnimGraphPlayer>().FirstOrDefault(p => p.asset && p.asset.name == "YBotLocomotion");
                began = EditorApplication.timeSinceStartup;
                firstFrame = Time.frameCount;
                stateMask = 0;
                moved = false;
                if(player && player.animator && player.animator.isHuman)
                {
                    bone = player.animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                }

                firstRotation = bone ? bone.localRotation : Quaternion.identity;
                Application.logMessageReceived -= Capture;
                Application.logMessageReceived += Capture;
            }

            if(state == PlayModeStateChange.ExitingPlayMode)
            {
                Application.logMessageReceived -= Capture;
            }

            if(state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Batch, false))
            {
                SessionState.SetBool(Batch, false);
                EditorApplication.Exit(SessionState.GetInt(BatchResult, 1));
            }
        }

        static void Capture(string message, string stack, LogType type)
        {
            if(type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errors++;
                messages.Add(message);
            }
        }

        static void Observe()
        {
            if(waitingForPlay)
            {
                if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < readyAt)
                {
                    return;
                }

                waitingForPlay = false;
                VerifyInPlayMode();
                return;
            }

            if(!EditorApplication.isPlaying || !SessionState.GetBool(Pending, false) || began <= 0)
            {
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - began;
            if(player && player.Instance != null)
            {
                player.SetFloat("speed", elapsed < .5 || elapsed >= 2 ? 0 : 2);
                foreach(var machine in player.Instance.Machines)
                {
                    stateMask |= 1 << machine.CurrentState;
                }

                if(bone && Quaternion.Angle(firstRotation, bone.localRotation) > .5f)
                {
                    moved = true;
                }
            }

            if(elapsed < 3.5)
            {
                return;
            }

            bool pass = player && player.Instance != null && player.Instance.Graph.IsValid() && stateMask == 3 && moved && errors == 0;
            string report = (pass ? "PASS" : "FAIL") + ": Y Bot Play mode; frames=" + (Time.frameCount - firstFrame) + ", states=" + stateMask + ", animatedBone=" + moved + ", errors=" + errors + "\n" + string.Join("\n", messages);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/UAnimGraph-YBot-play-verification.txt", report);
            SessionState.SetBool(Pending, false);
            began = 0;
            Application.logMessageReceived -= Capture;
            if(pass)
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }

            SessionState.SetInt(BatchResult, pass ? 0 : 1);
            EditorApplication.ExitPlaymode();
        }

        public static void Build()
        {
            if(!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/UAnimGraph/Tests", "Locomotion");
            }

            var idle = FindClip("Y Bot@Rifle Idle.fbx");
            var walk = FindClip("Y Bot@Walking.fbx");
            var run = FindClip("Y Bot@Running.fbx");
            string rootPath = Folder + "/YBotLocomotion.asset";
            var root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(rootPath);
            if(!root)
            {
                var candidate = AssetDatabase.LoadAssetAtPath<UAnimAsset>("Assets/UAnimGraph/Tests/UAnimAsset.asset");
                var candidateGraph = GraphDatabase.LoadGraph<UAnimLayerGraph>("Assets/UAnimGraph/Tests/UAnimAsset.uapose");
                bool empty = candidateGraph != null && candidateGraph.GetNodes().All(n => n is LayerOutputNode) && candidateGraph.GetNodes().Count() == 1;
                bool generated = candidateGraph != null && candidateGraph.GetNodes().OfType<StateMachineNode>().Any(n => AssetDatabase.GetAssetPath(n.Option<UAnimMachine>("Machine")) == Folder + "/Locomotion.uamachine");
                if(candidate && (empty || generated))
                {
                    root = candidate;
                }
                else
                {
                    root = ScriptableObject.CreateInstance<UAnimAsset>();
                    AssetDatabase.CreateAsset(root, rootPath);
                }
            }

            Undo.RecordObject(root, "Create Y Bot locomotion");
            root.name = "YBotLocomotion";
            root.parameters = new[]
            {
                new UAnimParameter
                {
                    id = "speed",
                    name = "Speed",
                    type = UAnimParameterType.Float
                }
            };
            var idlePose = Pose("Idle", idle);
            string movePath = Folder + "/Move.uastate";
            UAnimPoseGraph move;
            if(!File.Exists(movePath))
            {
                var g = GraphDatabase.CreateGraph<UAnimStateGraph>(movePath);
                var a = new ClipNode();
                var b = new ClipNode();
                var blend = new Blend1DNode();
                var p = new ParameterNode();
                var output = new StateOutputNode();
                g.AddNode(a);
                g.AddNode(b);
                g.AddNode(blend);
                g.AddNode(p);
                g.AddNode(output);
                a.Position = new Vector2(0, 0);
                b.Position = new Vector2(0, 250);
                p.Position = new Vector2(0, 500);
                blend.Position = new Vector2(350, 100);
                output.Position = new Vector2(700, 100);
                a.GetNodeOptionByName("Clip").TrySetValue(walk);
                b.GetNodeOptionByName("Clip").TrySetValue(run);
                p.GetNodeOptionByName("ParameterId").TrySetValue("speed");
                blend.Settings.inputs[0].threshold = .5f;
                blend.Settings.inputs[1].threshold = 2f;
                output.GetNodeOptionByName("Duration").TrySetValue(walk.length);
                g.Connect(a.GetOutputPortByName("Pose"), blend.GetInputPortByName("Pose0"));
                g.Connect(b.GetOutputPortByName("Pose"), blend.GetInputPortByName("Pose1"));
                g.Connect(p.GetOutputPortByName("Value"), blend.GetInputPortByName("Value"));
                g.Connect(blend.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
                GraphDatabase.SaveGraph(g);
            }

            AssetDatabase.ImportAsset(movePath);
            move = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(movePath);
            string machinePath = Folder + "/Locomotion.uamachine";
            if(!File.Exists(machinePath))
            {
                var machine = StateMachineDatabase.CreateStateMachine<UAnimStateMachineGraph>(machinePath);
                var a = new PoseState();
                var b = new PoseState();
                machine.AddState(a);
                machine.AddState(b);
                a.Title = "Idle";
                b.Title = "Move";
                b.Position = new Vector2(350, 0);
                a.GetOptionByName("Graph").TrySetValue(idlePose);
                a.GetOptionByName("Default").TrySetValue(true);
                b.GetOptionByName("Graph").TrySetValue(move);
                Connect(machine, a, b, "speed|Float|Greater|0.1");
                Connect(machine, b, a, "speed|Float|LessOrEqual|0.1");
                StateMachineDatabase.SaveStateMachine(machine);
            }

            AssetDatabase.ImportAsset(machinePath);
            var definition = AssetDatabase.LoadAssetAtPath<UAnimMachine>(machinePath);
            string layerPath = root.layers.Length == 1 && root.layers[0].graph ? AssetDatabase.GetAssetPath(root.layers[0].graph) : Folder + "/Base.uapose";
            var layer = File.Exists(layerPath) ? GraphDatabase.LoadGraph<UAnimLayerGraph>(layerPath) : GraphDatabase.CreateGraph<UAnimLayerGraph>(layerPath);
            if(!layer.GetNodes().Any(n => n is StateMachineNode))
            {
                if(layer.GetNodes().Any(n => n is not LayerOutputNode))
                {
                    throw new InvalidOperationException("Existing layer contains authored nodes; not overwriting them.");
                }

                var output = layer.GetNodes().OfType<LayerOutputNode>().SingleOrDefault();
                if(output == null)
                {
                    output = new LayerOutputNode();
                    layer.AddNode(output);
                }

                var node = new StateMachineNode();
                layer.AddNode(node);
                node.GetNodeOptionByName("Machine").TrySetValue(definition);
                output.Position = new Vector2(400, 0);
                layer.Connect(node.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
                GraphDatabase.SaveGraph(layer);
            }

            AssetDatabase.ImportAsset(layerPath);
            var baseLayer = root.layers.Length == 1 && root.layers[0] != null ? root.layers[0] : new UAnimLayer();
            baseLayer.graph = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(layerPath);
            root.layers = new[]
            {
                baseLayer
            };
            EditorUtility.SetDirty(root);
            if(!UAnimBuildValidation.Compile(root))
            {
                throw new Exception("Y Bot definition invalid");
            }

            AssetDatabase.SaveAssets();
            VerifyManual(root);
            string authoredRootPath = AssetDatabase.GetAssetPath(root);
            if(!File.Exists(ScenePath))
            {
                CreateScene(root);
            }

            root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(authoredRootPath);
            Selection.activeObject = root;
            Debug.Log("Y Bot animation authored: " + authoredRootPath + "; " + ScenePath);
        }

        static AnimationClip FindClip(string name)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/UAnimGraph/Tests/Animations/" + name).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            return clip ? clip : throw new InvalidOperationException("Missing animation: " + name);
        }

        static UAnimPoseGraph Pose(string name, AnimationClip clip)
        {
            string path = Folder + "/" + name + ".uastate";
            if(!File.Exists(path))
            {
                var graph = GraphDatabase.CreateGraph<UAnimStateGraph>(path);
                var c = new ClipNode();
                var o = new StateOutputNode();
                graph.AddNode(c);
                graph.AddNode(o);
                o.Position = new Vector2(350, 0);
                c.GetNodeOptionByName("Clip").TrySetValue(clip);
                o.GetNodeOptionByName("Duration").TrySetValue(clip.length);
                graph.Connect(c.GetOutputPortByName("Pose"), o.GetInputPortByName("Pose"));
                GraphDatabase.SaveGraph(graph);
            }

            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(path);
        }

        static void Connect(UAnimStateMachineGraph m, IState a, IState b, string condition)
        {
            var t = m.Connect(a, b);
            var rule = t.RuleCount > 0 ? t.GetRule(0) : t.AddRule();
            rule.RootCondition.Add(new ParameterCondition { Value = condition });
            rule.RootCondition.Add(new DurationCondition { Value = .2f });
        }

        static GameObject Model(Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UAnimGraph/Tests/Models/Y Bot.fbx");
            if(!prefab)
            {
                throw new InvalidOperationException("Missing Y Bot model");
            }

            var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            actor.name = "Y Bot";
            var animator = actor.GetComponentInChildren<Animator>();
            if(!animator || !animator.avatar || !animator.avatar.isValid)
            {
                throw new InvalidOperationException("Y Bot Avatar invalid");
            }

            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return actor;
        }

        static void VerifyManual(UAnimAsset root)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var actor = Model(scene);
                var animator = actor.GetComponentInChildren<Animator>();
                var leg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                using(var instance = new UAnimInstance(root, animator, true))
                {
                    instance.Tick(0);
                    var before = leg.localRotation;
                    int count = instance.Graph.GetPlayableCount();
                    instance.Set("speed", UAnimParameterType.Float, 2);
                    instance.Tick(.1f);
                    instance.Tick(.2f);
                    instance.Tick(.25f);
                    if(instance.Machines[0].CurrentState != 1 || Quaternion.Angle(before, leg.localRotation) < .1f)
                    {
                        throw new Exception("Y Bot move state did not animate skeleton");
                    }

                    instance.Set("speed", UAnimParameterType.Float, 0);
                    instance.Tick(.1f);
                    instance.Tick(.2f);
                    if(instance.Machines[0].CurrentState != 0 || instance.Graph.GetPlayableCount() != count)
                    {
                        throw new Exception("Y Bot return transition changed graph topology");
                    }
                }

                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/UAnimGraph-YBot-manual-verification.txt", "PASS: real Humanoid clips; Idle->Walk/Run->Idle; animated leg; stable playable count; graph disposed");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void CreateScene(UAnimAsset root)
        {
            string rootPath = AssetDatabase.GetAssetPath(root);
            var previous = SceneManager.GetActiveScene();
            if(!Application.isBatchMode && string.IsNullOrEmpty(previous.path))
            {
                Debug.LogWarning("Animation assets are ready. Save the current untitled scene before generating the Y Bot scene.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(rootPath);
                if(!root)
                {
                    throw new InvalidOperationException("Animation asset could not be reloaded after creating the scene");
                }

                var actor = Model(scene);
                var p = actor.AddComponent<UAnimGraphPlayer>();
                p.asset = root;
                p.animator = actor.GetComponentInChildren<Animator>();
                var camera = new GameObject("Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(camera, scene);
                camera.transform.position = new Vector3(0, 1.1f, -3.5f);
                camera.transform.LookAt(new Vector3(0, 1, 0));
                camera.GetComponent<Camera>().backgroundColor = new Color(.1f, .12f, .16f);
                camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                var light = new GameObject("Light", typeof(Light));
                SceneManager.MoveGameObjectToScene(light, scene);
                light.GetComponent<Light>().type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(40, 30, 0);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if(!Application.isBatchMode)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                if(previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
            }
        }
    }
}
