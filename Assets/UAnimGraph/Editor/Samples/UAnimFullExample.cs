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
    public static class UAnimFullExample
    {
        public const string Folder = "Assets/UAnimGraph/FullExample";
        public const string RootPath = Folder + "/DirectionalCharacter.uanimasset";
        [MenuItem("UAnimGraph/Create Complete Example")]
        public static void Open()
        {
            var root = Build();
            Selection.activeObject = root;
            UAnimEditorWorkspace.Open(root);
        }

        public static UAnimAsset Build()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UAnimAsset>(RootPath);
            if(existing)
            {
                RepairNames();
                // Resolve external definitions after a Toolkit graph save/reimport.
                AssetDatabase.ImportAsset(RootPath, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<UAnimAsset>(RootPath);
                foreach(var dependency in AssetDatabase.GetDependencies(RootPath, true))
                {
                    if(dependency.EndsWith(".uamachine"))
                    {
                        AssetDatabase.LoadAssetAtPath<UAnimMachine>(dependency);
                    }
                    else if(dependency.EndsWith(".uastate"))
                    {
                        AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(dependency);
                    }
                }

                return loaded;
            }

            var previous = UAnimRootExample.Build();
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            string posePath = Folder + "/DirectionalMove.uastate";
            var pose = GraphDatabase.CreateGraph<UAnimStateGraph>(posePath);
            var mixer = new Blend2DNode();
            pose.AddNode(mixer);
            mixer.Position = new Vector2(420, 100);
            var settings = mixer.Settings;
            settings.binding = MixerBinding.Vector2Parameter;
            settings.parameter = "direction";
            settings.inputs.Clear();
            var locations = new[]
            {
                Vector2.zero,
                Vector2.up,
                Vector2.down,
                Vector2.left,
                Vector2.right
            };
            var names = new[]
            {
                "Idle",
                "Forward",
                "Back",
                "Left",
                "Right"
            };
            for(int i = 0; i < locations.Length; i++)
            {
                settings.inputs.Add(new MixerPoseInput("Pose" + i, i, locations[i]));
                var node = new ClipNode();
                pose.AddNode(node);
                node.Title = names[i];
                node.Position = new Vector2(0, i * 160);
                AnimationClip clip;
                if(i == 0)
                {
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/UAnimGraph/Demo/Idle.anim");
                }
                else
                {
                    clip = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/UAnimGraph/Demo/Walk.anim"));
                    clip.name = names[i];
                    float rotation = i == 2 ? 180 : i == 3 ? -90 : i == 4 ? 90 : 0;
                    clip.SetCurve("Body", typeof(Transform), "localEulerAnglesRaw.y", AnimationCurve.Constant(0, 1, rotation));
                    AssetDatabase.CreateAsset(clip, Folder + "/" + names[i] + ".anim");
                }

                node.GetNodeOptionByName("Clip").TrySetValue(clip);
            }

            mixer.DefineNode();
            var clips = pose.GetNodes().OfType<ClipNode>().ToArray();
            for(int i = 0; i < clips.Length; i++)
            {
                pose.Connect(clips[i].GetOutputPortByName("Pose"), mixer.GetInputPortByName("Pose" + i));
            }

            var output = new StateOutputNode();
            pose.AddNode(output);
            output.Position = new Vector2(850, 100);
            pose.Connect(mixer.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
            GraphDatabase.SaveGraph(pose);
            AssetDatabase.ImportAsset(posePath);
            string machinePath = Folder + "/Directional.uamachine";
            var machine = StateMachineDatabase.CreateStateMachine<UAnimStateMachineGraph>(machinePath);
            machine.requireEvaluation = true;
            var entry = new UAnimEntryState();
            var idle = new PoseState();
            var move = new PoseState();
            machine.AddState(entry);
            machine.AddState(idle);
            machine.AddState(move);
            entry.Title = "Entry";
            idle.Title = "Idle";
            move.Title = "Move";
            idle.GetOptionByName("Name").TrySetValue("Idle");
            move.GetOptionByName("Name").TrySetValue("Move");
            entry.Position = new Vector2(-300, 0);
            move.Position = new Vector2(400, 0);
            idle.GetOptionByName("Graph").TrySetValue(AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(UAnimRootExample.Folder + "/Idle.uastate"));
            move.GetOptionByName("Graph").TrySetValue(AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(posePath));
            machine.Connect(entry, idle);
            Connect(machine, idle, move, nameof(LocomotionTransitions.CanMove));
            Connect(machine, move, idle, nameof(LocomotionTransitions.CanStop));
            StateMachineDatabase.SaveStateMachine(machine);
            AssetDatabase.ImportAsset(machinePath);
            var root = GraphDatabase.CreateGraph<UAnimRootGraph>(RootPath);
            root.layerChain = true;
            root.parameters = new[]
            {
                new UAnimParameter
                {
                    id = "speed",
                    name = "Speed",
                    defaultValue = .65
                },
                new UAnimParameter
                {
                    id = "direction",
                    name = "MoveInput",
                    type = UAnimParameterType.Vector2,
                    defaultVector2 = Vector2.up
                }
            };
            var first = UAnimRootExample.Layer(root, "Directional Locomotion", AssetDatabase.LoadAssetAtPath<UAnimMachine>(machinePath), 1, new Vector2(0, 100));
            var upper = UAnimRootExample.Layer(root, "Upper Aim", previous.layers[1].graph.nodes[0].machine, .45f, new Vector2(500, 100));
            upper.GetNodeOptionByName("AvatarMask").TrySetValue(previous.layers[1].mask);
            var result = new LayerOutputNode();
            root.AddNode(result);
            result.Position = new Vector2(1000, 100);
            root.Connect(first.GetOutputPortByName("Pose"), upper.GetInputPortByName("PreviousPose"));
            root.Connect(upper.GetOutputPortByName("Pose"), result.GetInputPortByName("Pose"));
            GraphDatabase.SaveGraph(root);
            AssetDatabase.ImportAsset(RootPath);
            var asset = AssetDatabase.LoadAssetAtPath<UAnimAsset>(RootPath);
            if(!UAnimBuildValidation.Compile(asset))
            {
                throw new InvalidOperationException("Complete example invalid");
            }

            UAnimEvaluationLinker.Generate();
            return asset;
        }

        static void RepairNames()
        {
            string path = Folder + "/Directional.uamachine";
            var graph = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(path);
            if(graph == null)
            {
                return;
            }

            bool changed = !graph.requireEvaluation;
            graph.requireEvaluation = true;
            foreach(var state in graph.GetStates().OfType<PoseState>())
            {
                string name = null;
                state.GetOptionByName("Name")?.TryGetValue(out name);
                if(!string.IsNullOrEmpty(name))
                {
                    continue;
                }

                UAnimPoseGraph pose = null;
                state.GetOptionByName("Graph")?.TryGetValue(out pose);
                if(!pose)
                {
                    continue;
                }

                name = pose.name == "DirectionalMove" ? "Move" : pose.name;
                state.GetOptionByName("Name").TrySetValue(name);
                changed = true;
            }

            if(changed)
            {
                StateMachineDatabase.SaveStateMachine(graph);
                AssetDatabase.ImportAsset(path);
            }
        }

        public const string ScenePath = Folder + "/CompleteExample.unity";
        [MenuItem("UAnimGraph/Open Complete Example Scene")]
        public static void OpenScene()
        {
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            CreateScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void CreateScene()
        {
            if(File.Exists(ScenePath))
            {
                return;
            }

            var root = Build();
            AssetDatabase.CopyAsset(UAnimRootExample.ScenePath, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, Application.isBatchMode ? OpenSceneMode.Single : OpenSceneMode.Additive);
            try
            {
                var first = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<UAnimGraphPlayer>()).Single();
                first.asset = root;
                first.transform.position = Vector3.left * .8f;
                var driver = first.gameObject.AddComponent<UAnimDemoDriver>();
                driver.player = first;
                var second = UnityEngine.Object.Instantiate(first.gameObject);
                second.name = "Second Directional Character";
                second.transform.position = Vector3.right * .8f;
                second.GetComponent<UAnimDemoDriver>().phase = .8f;
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if(!Application.isBatchMode)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        internal static void Connect(UAnimStateMachineGraph machine, IState from, IState to, string method)
        {
            var t = machine.Connect(from, to);
            var rule = t.RuleCount == 0 ? t.AddRule() : t.GetRule(0);
            rule.RootCondition.Add(new EvaluationFunctionCondition { Value = TransitionEvaluationRegistry.Key(typeof(LocomotionTransitions).GetMethod(method)) });
            rule.RootCondition.Add(new DurationCondition { Value = .2f });
            rule.RootCondition.Add(new PriorityCondition { Value = 0 });
        }
    }
}
