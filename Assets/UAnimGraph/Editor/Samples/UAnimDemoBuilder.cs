using System;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UAnimGraph.Authoring
{
    public static class UAnimDemoBuilder
    {
        const string Folder = "Assets/UAnimGraph/Demo";
        [MenuItem("UAnimGraph/Create Demo Scene")]
        public static void Create()
        {
            if(!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/UAnimGraph", "Demo");
            }

            var existing = AssetDatabase.LoadAssetAtPath<UAnimAsset>(Folder + "/Demo.asset");
            if(existing)
            {
                RepairDemoRules();
                if(!File.Exists(Folder + "/Demo.unity"))
                {
                    CreateScene(existing);
                }

                return;
            }

            var idle = MakeClip("Idle", .025f, 0, 0);
            var walk = MakeClip("Walk", .08f, 25, 0);
            var run = MakeClip("Run", .15f, 50, 0);
            var aim = MakeClip("Aim", 0, 0, -65);
            var idlePose = MakePose("Idle", idle, true);
            string movePath = Folder + "/Locomotion.uastate";
            var moveGraph = GraphDatabase.CreateGraph<UAnimStateGraph>(movePath);
            var walkNode = new ClipNode();
            var runNode = new ClipNode();
            var blend = new Blend1DNode();
            var p = new ParameterNode();
            var stateOut = new StateOutputNode();
            moveGraph.AddNode(walkNode);
            moveGraph.AddNode(runNode);
            moveGraph.AddNode(blend);
            moveGraph.AddNode(p);
            moveGraph.AddNode(stateOut);
            walkNode.Position = new Vector2(0, 0);
            runNode.Position = new Vector2(0, 220);
            p.Position = new Vector2(0, 440);
            blend.Position = new Vector2(350, 100);
            stateOut.Position = new Vector2(700, 100);
            walkNode.GetNodeOptionByName("Clip").TrySetValue(walk);
            runNode.GetNodeOptionByName("Clip").TrySetValue(run);
            p.GetNodeOptionByName("ParameterId").TrySetValue("speed");
            blend.Settings.inputs[0].threshold = .1f;
            blend.Settings.inputs[1].threshold = 2f;
            moveGraph.Connect(walkNode.GetOutputPortByName("Pose"), blend.GetInputPortByName("Pose0"));
            moveGraph.Connect(runNode.GetOutputPortByName("Pose"), blend.GetInputPortByName("Pose1"));
            moveGraph.Connect(p.GetOutputPortByName("Value"), blend.GetInputPortByName("Value"));
            moveGraph.Connect(blend.GetOutputPortByName("Pose"), stateOut.GetInputPortByName("Pose"));
            GraphDatabase.SaveGraph(moveGraph);
            AssetDatabase.ImportAsset(movePath);
            var movePose = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(movePath);
            string machinePath = Folder + "/Locomotion.uamachine";
            var machine = StateMachineDatabase.CreateStateMachine<UAnimStateMachineGraph>(machinePath);
            var idleState = new PoseState();
            var moveState = new PoseState();
            machine.AddState(idleState);
            machine.AddState(moveState);
            idleState.Title = "Idle";
            moveState.Title = "Locomotion";
            idleState.GetOptionByName("Name").TrySetValue("Idle");
            moveState.GetOptionByName("Name").TrySetValue("Locomotion");
            idleState.Position = new Vector2(0, 0);
            moveState.Position = new Vector2(350, 0);
            idleState.GetOptionByName("Graph").TrySetValue(idlePose);
            idleState.GetOptionByName("Default").TrySetValue(true);
            moveState.GetOptionByName("Graph").TrySetValue(movePose);
            var toMove = machine.Connect(idleState, moveState);
            var r = toMove.RuleCount > 0 ? toMove.GetRule(0) : toMove.AddRule();
            r.RootCondition.Add(new ParameterCondition { Value = "speed|Float|Greater|0.1" });
            r.RootCondition.Add(new DurationCondition { Value = .2f });
            var toIdle = machine.Connect(moveState, idleState);
            r = toIdle.RuleCount > 0 ? toIdle.GetRule(0) : toIdle.AddRule();
            r.RootCondition.Add(new ParameterCondition { Value = "speed|Float|LessOrEqual|0.1" });
            r.RootCondition.Add(new DurationCondition { Value = .2f });
            StateMachineDatabase.SaveStateMachine(machine);
            AssetDatabase.ImportAsset(machinePath);
            var runtimeMachine = AssetDatabase.LoadAssetAtPath<UAnimMachine>(machinePath);
            string layerPath = Folder + "/Base.uapose";
            var layer = GraphDatabase.CreateGraph<UAnimLayerGraph>(layerPath);
            var sm = new StateMachineNode();
            var layerOut = new LayerOutputNode();
            layer.AddNode(sm);
            layer.AddNode(layerOut);
            layerOut.Position = new Vector2(350, 0);
            sm.GetNodeOptionByName("Machine").TrySetValue(runtimeMachine);
            layer.Connect(sm.GetOutputPortByName("Pose"), layerOut.GetInputPortByName("Pose"));
            GraphDatabase.SaveGraph(layer);
            AssetDatabase.ImportAsset(layerPath);
            var upper = MakePose("Aim", aim, false);
            var mask = new AvatarMask();
            mask.name = "Upper Body";
            mask.transformCount = 4;
            string[] paths =
            {
                "",
                "Body",
                "Body/UpperBody",
                "Body/UpperBody/Arm"
            };
            for(int i = 0; i < paths.Length; i++)
            {
                mask.SetTransformPath(i, paths[i]);
                mask.SetTransformActive(i, i >= 2);
            }

            AssetDatabase.CreateAsset(mask, Folder + "/UpperBody.mask");
            var asset = ScriptableObject.CreateInstance<UAnimAsset>();
            asset.parameters = new[]
            {
                new UAnimParameter
                {
                    id = "speed",
                    name = "Speed",
                    type = UAnimParameterType.Float
                }
            };
            asset.layers = new[]
            {
                new UAnimLayer
                {
                    graph = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(layerPath)
                },
                new UAnimLayer
                {
                    name = "Upper Body Aim",
                    graph = upper,
                    mask = mask,
                    weight = 1
                }
            };
            AssetDatabase.CreateAsset(asset, Folder + "/Demo.asset");
            UAnimBuildValidation.Compile(asset);
            AssetDatabase.SaveAssets();
            CreateScene(asset);
            Selection.activeObject = asset;
            Debug.Log("UAnimGraph demo created: " + Folder + "/Demo.unity");
        }

        static void RepairDemoRules()
        {
            string path = Folder + "/Locomotion.uamachine";
            var machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(path);
            bool changed = false;
            foreach(var state in machine.GetStates())
            {
                foreach(var t in state.GetOutgoingTransitions())
                {
                    if(t.RuleCount < 2)
                    {
                        continue;
                    }

                    foreach(var rule in t.GetRules().ToArray())
                    {
                        if(rule.RootCondition != null && !rule.RootCondition.Get().Any())
                        {
                            t.RemoveRule(rule);
                            changed = true;
                        }
                    }
                }
            }

            if(changed)
            {
                StateMachineDatabase.SaveStateMachine(machine);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(Folder + "/Base.uapose", ImportAssetOptions.ForceUpdate);
            }
        }

        static void CreateScene(UAnimAsset asset)
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                for(int i = 0; i < 2; i++)
                {
                    var character = new GameObject("Character " + (i + 1));
                    SceneManager.MoveGameObjectToScene(character, scene);
                    character.transform.position = new Vector3(i * 2 - 1, 0, 0);
                    var animator = character.AddComponent<Animator>();
                    var body = new GameObject("Body");
                    body.transform.SetParent(character.transform, false);
                    body.transform.localPosition = Vector3.up;
                    Shape("Torso", body.transform, new Vector3(0, .2f, 0), new Vector3(.6f, .8f, .35f));
                    var upperBody = new GameObject("UpperBody");
                    upperBody.transform.SetParent(body.transform, false);
                    upperBody.transform.localPosition = new Vector3(0, .65f, 0);
                    Shape("Head", upperBody.transform, new Vector3(0, .2f, 0), Vector3.one * .35f);
                    Shape("Arm", upperBody.transform, new Vector3(.55f, -.2f, 0), new Vector3(.6f, .18f, .18f));
                    Shape("LeftLeg", body.transform, new Vector3(-.18f, -.6f, 0), new Vector3(.22f, .7f, .22f));
                    Shape("RightLeg", body.transform, new Vector3(.18f, -.6f, 0), new Vector3(.22f, .7f, .22f));
                    var player = character.AddComponent<UAnimGraphPlayer>();
                    player.asset = asset;
                    player.animator = animator;
                    var driver = character.AddComponent<UAnimDemoDriver>();
                    driver.player = player;
                    driver.phase = i;
                }

                var cameraObject = new GameObject("Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                cameraObject.transform.position = new Vector3(0, 2, -6);
                cameraObject.transform.LookAt(new Vector3(0, 1, 0));
                cameraObject.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
                cameraObject.GetComponent<Camera>().backgroundColor = new Color(.07f, .09f, .14f);
                var light = new GameObject("Light", typeof(Light));
                SceneManager.MoveGameObjectToScene(light, scene);
                light.GetComponent<Light>().type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(40, 30, 0);
                EditorSceneManager.SaveScene(scene, Folder + "/Demo.unity");
            }
            finally
            {
                if(!Application.isBatchMode)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        static void Shape(string name, Transform parent, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        static AnimationClip MakeClip(string name, float bob, float leg, float arm)
        {
            var clip = new AnimationClip
            {
                name = name,
                frameRate = 30
            };
            clip.SetCurve("Body", typeof(Transform), "localPosition.y", new AnimationCurve(new Keyframe(0, 1), new Keyframe(.5f, 1 + bob), new Keyframe(1, 1)));
            clip.SetCurve("Body/LeftLeg", typeof(Transform), "localEulerAnglesRaw.x", new AnimationCurve(new Keyframe(0, -leg), new Keyframe(.5f, leg), new Keyframe(1, -leg)));
            clip.SetCurve("Body/RightLeg", typeof(Transform), "localEulerAnglesRaw.x", new AnimationCurve(new Keyframe(0, leg), new Keyframe(.5f, -leg), new Keyframe(1, leg)));
            clip.SetCurve("Body/UpperBody/Arm", typeof(Transform), "localEulerAnglesRaw.z", AnimationCurve.Constant(0, 1, arm));
            AssetDatabase.CreateAsset(clip, Folder + "/" + name + ".anim");
            return clip;
        }

        static UAnimPoseGraph MakePose(string name, AnimationClip clip, bool state)
        {
            string path = Folder + "/" + name + (state ? ".uastate" : ".uapose");
            Graph graph = state ? GraphDatabase.CreateGraph<UAnimStateGraph>(path) : GraphDatabase.CreateGraph<UAnimLayerGraph>(path);
            var c = new ClipNode();
            PoseNode o = state ? new StateOutputNode() : new LayerOutputNode();
            graph.AddNode(c);
            graph.AddNode(o);
            o.Position = new Vector2(350, 0);
            c.GetNodeOptionByName("Clip").TrySetValue(clip);
            graph.Connect(c.GetOutputPortByName("Pose"), o.GetInputPortByName("Pose"));
            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(path);
        }

        public static void BuildSmokePlayer()
        {
            Create();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { Folder + "/Demo.unity" }, locationPathName = "Build/UAnimGraphDemo/UAnimGraphDemo.exe", target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
            if(report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception("UAnimGraph player build failed: " + report.summary.result);
            }

            Debug.Log("UAnimGraph player build succeeded");
        }
    }
}
