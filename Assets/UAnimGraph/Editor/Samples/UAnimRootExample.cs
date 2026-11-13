using System;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UAnimGraph.Authoring
{
    public static class UAnimRootExample
    {
        public const string Folder = "Assets/UAnimGraph/RootExample";
        public const string RootPath = Folder + "/CharacterLocomotion.uanimasset";
        public const string ScenePath = Folder + "/RootLayerExample.unity";
        [MenuItem("UAnimGraph/Create Root Layer Example")]
        public static void Create()
        {
            var root = Build();
            Selection.activeObject = root;
            EditorGUIUtility.PingObject(root);
            if(!Application.isBatchMode)
            {
                AssetDatabase.OpenAsset(root);
            }
        }

        [MenuItem("UAnimGraph/Open Root Layer Example Scene")]
        static void OpenScene()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if(string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && !EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
            {
                return;
            }

            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static UAnimAsset Build()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UAnimAsset>(RootPath);
            if(existing)
            {
                if(!File.Exists(ScenePath))
                {
                    CreateScene(existing);
                }

                return existing;
            }

            UAnimDemoBuilder.Create();
            if(!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/UAnimGraph", "RootExample");
            }

            var idle = Pose("Idle", new[] { Clip("Idle") });
            var move = Pose("Move", new[] { Clip("Walk"), Clip("Run") });
            var aim = Pose("Aim", new[] { Clip("Aim") });
            var locomotion = Machine("Locomotion", idle, move);
            var aimMachine = Machine("UpperAim", aim, null);
            var graph = GraphDatabase.CreateGraph<UAnimRootGraph>(RootPath);
            graph.layerChain = true;
            graph.parameters = new[]
            {
                new UAnimParameter
                {
                    id = "speed",
                    name = "Speed",
                    type = UAnimParameterType.Float,
                    defaultValue = .65
                }
            };
            var first = Layer(graph, "Base Locomotion", locomotion, 1, new Vector2(0, 100));
            var upper = Layer(graph, "Upper Aim", aimMachine, .45f, new Vector2(500, 100));
            upper.GetNodeOptionByName("AvatarMask").TrySetValue(AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/UAnimGraph/Demo/UpperBody.mask"));
            var output = new LayerOutputNode();
            graph.AddNode(output);
            output.Position = new Vector2(1000, 100);
            graph.Connect(first.GetOutputPortByName("Pose"), upper.GetInputPortByName("PreviousPose"));
            graph.Connect(upper.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(RootPath, ImportAssetOptions.ForceUpdate);
            var root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(RootPath);
            if(!UAnimBuildValidation.Compile(root))
            {
                throw new InvalidOperationException("Root Layer example did not compile.");
            }

            CreateScene(root);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<UAnimAsset>(RootPath);
        }

        internal static RootLayerNode Layer(UAnimRootGraph graph, string name, UAnimMachine machine, float weight, Vector2 position)
        {
            var node = new RootLayerNode();
            graph.AddNode(node);
            node.Title = name;
            node.Position = position;
            node.GetNodeOptionByName("Name").TrySetValue(name);
            node.GetNodeOptionByName("Machine").TrySetValue(machine);
            node.GetNodeOptionByName("Weight").TrySetValue(weight);
            return node;
        }

        static AnimationClip Clip(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/UAnimGraph/Demo/" + name + ".anim");
        }

        static UAnimPoseGraph Pose(string name, AnimationClip[] clips)
        {
            string path = Folder + "/" + name + ".uastate";
            var graph = GraphDatabase.CreateGraph<UAnimStateGraph>(path);
            var output = new StateOutputNode();
            graph.AddNode(output);
            output.Position = new Vector2(700, 100);
            if(clips.Length == 1)
            {
                var node = new ClipNode();
                graph.AddNode(node);
                node.GetNodeOptionByName("Clip").TrySetValue(clips[0]);
                graph.Connect(node.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
            }
            else
            {
                var blend = new Blend1DNode();
                graph.AddNode(blend);
                blend.Position = new Vector2(350, 100);
                blend.Settings.inputs[0].threshold = .5f;
                blend.Settings.inputs[1].threshold = 1f;
                for(int i = 0; i < 2; i++)
                {
                    var clip = new ClipNode();
                    graph.AddNode(clip);
                    clip.Position = new Vector2(0, i * 220);
                    clip.GetNodeOptionByName("Clip").TrySetValue(clips[i]);
                    graph.Connect(clip.GetOutputPortByName("Pose"), blend.GetInputPortByName("Pose" + i));
                }

                var value = new ParameterNode();
                graph.AddNode(value);
                value.Position = new Vector2(0, 440);
                value.GetNodeOptionByName("ParameterId").TrySetValue("speed");
                graph.Connect(value.GetOutputPortByName("Value"), blend.GetInputPortByName("Value"));
                graph.Connect(blend.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
            }

            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(path);
        }

        static UAnimMachine Machine(string name, UAnimPoseGraph first, UAnimPoseGraph second)
        {
            string path = Folder + "/" + name + ".uamachine";
            var graph = StateMachineDatabase.CreateStateMachine<UAnimStateMachineGraph>(path);
            var a = new PoseState();
            graph.AddState(a);
            a.Title = first.name;
            a.GetOptionByName("Name").TrySetValue(first.name);
            a.GetOptionByName("Graph").TrySetValue(first);
            a.GetOptionByName("Default").TrySetValue(true);
            if(second)
            {
                var b = new PoseState();
                graph.AddState(b);
                b.Title = second.name;
                b.GetOptionByName("Name").TrySetValue(second.name);
                b.Position = new Vector2(400, 0);
                b.GetOptionByName("Graph").TrySetValue(second);
                var forward = graph.Connect(a, b);
                var r = forward.GetRule(0);
                r.RootCondition.Add(new ParameterCondition { Value = "speed|Float|Greater|0.1" });
                r.RootCondition.Add(new DurationCondition { Value = .2f });
                var back = graph.Connect(b, a);
                r = back.GetRule(0);
                r.RootCondition.Add(new ParameterCondition { Value = "speed|Float|LessOrEqual|0.1" });
                r.RootCondition.Add(new DurationCondition { Value = .2f });
            }

            StateMachineDatabase.SaveStateMachine(graph);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<UAnimMachine>(path);
        }

        static void CreateScene(UAnimAsset root)
        {
            // Work on a separate scene; never replace the user's current scene.
            if(!Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
            {
                Debug.LogWarning("Root graph created. Save the current scene and use Open Root Layer Example Scene to create the example scene.");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                var character = new GameObject("Root Layer Character", typeof(Animator));
                SceneManager.MoveGameObjectToScene(character, scene);
                var body = new GameObject("Body");
                body.transform.SetParent(character.transform, false);
                body.transform.localPosition = Vector3.up;
                Shape("Torso", body.transform, new Vector3(0, .2f, 0), new Vector3(.6f, .8f, .35f));
                var upper = new GameObject("UpperBody");
                upper.transform.SetParent(body.transform, false);
                upper.transform.localPosition = new Vector3(0, .65f, 0);
                Shape("Head", upper.transform, new Vector3(0, .2f, 0), Vector3.one * .35f);
                Shape("Arm", upper.transform, new Vector3(.55f, -.2f, 0), new Vector3(.6f, .18f, .18f));
                Shape("LeftLeg", body.transform, new Vector3(-.18f, -.6f, 0), new Vector3(.22f, .7f, .22f));
                Shape("RightLeg", body.transform, new Vector3(.18f, -.6f, 0), new Vector3(.22f, .7f, .22f));
                var player = character.AddComponent<UAnimGraphPlayer>();
                player.asset = root;
                player.animator = character.GetComponent<Animator>();
                var camera = new GameObject("Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(camera, scene);
                camera.transform.position = new Vector3(0, 1.5f, -4);
                camera.transform.LookAt(new Vector3(0, 1, 0));
                var light = new GameObject("Light", typeof(Light));
                SceneManager.MoveGameObjectToScene(light, scene);
                light.GetComponent<Light>().type = LightType.Directional;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                if(Application.isBatchMode)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
                else
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
    }
}
