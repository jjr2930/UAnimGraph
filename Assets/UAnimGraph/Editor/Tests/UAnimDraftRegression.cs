using System;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimDraftRegression
    {
        public static void Run()
        {
            const string folder = "Assets/UAnimGraph/Verification";
            if(!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/UAnimGraph", "Verification");
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/DraftRegression.uapose");
            int errors = 0;
            Application.LogCallback capture = (message, stack, type) =>
            {
                if(type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    errors++;
                }
            };
            Application.logMessageReceived += capture;
            UAnimAsset root = null;
            try
            {
                var graph = GraphDatabase.CreateGraph<UAnimLayerGraph>(path);
                var output = new LayerOutputNode();
                graph.AddNode(output);
                GraphDatabase.SaveGraph(graph);
                AssetDatabase.ImportAsset(path);
                var draft = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(path);
                if(!draft || string.IsNullOrEmpty(draft.importError))
                {
                    throw new Exception("Draft must retain its validation diagnostic");
                }

                root = ScriptableObject.CreateInstance<UAnimAsset>();
                root.layers = new[]
                {
                    new UAnimLayer
                    {
                        graph = draft
                    }
                };
                if(UAnimValidation.Validate(root).Errors.Count == 0)
                {
                    throw new Exception("Incomplete drafts must still be rejected for execution");
                }

                var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/UAnimGraph/Tests/Animations/Y Bot@Walking.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
                var node = new ClipNode();
                graph.AddNode(node);
                node.GetNodeOptionByName("Clip").TrySetValue(clip);
                graph.Connect(node.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
                GraphDatabase.SaveGraph(graph);
                AssetDatabase.ImportAsset(path);
                root.layers[0].graph = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(path);
                if(UAnimValidation.Validate(root).Errors.Count != 0)
                {
                    throw new Exception("Completing the draft must clear its diagnostic");
                }

                if(errors != 0)
                {
                    throw new Exception("Draft creation/completion emitted " + errors + " Console errors");
                }

                VerifyReloadFingerprint();
                if(errors != 0)
                {
                    throw new Exception("Draft/reload regression emitted " + errors + " Console errors");
                }

                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/UAnimGraph-draft-regression.txt", "PASS: empty graph -> connected real clip; invalid draft rejected; definition fingerprint stable after asset reload; Console errors=0");
                Debug.Log("UAnimGraph draft regression PASS: Console errors=0; runtime validation retained");
            }
            finally
            {
                Application.logMessageReceived -= capture;
                if(root)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                AssetDatabase.DeleteAsset(path);
            }
        }

        static void VerifyReloadFingerprint()
        {
            var root = AssetDatabase.LoadAssetAtPath<UAnimAsset>("Assets/UAnimGraph/Tests/UAnimAsset.asset");
            if(!root)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(root.layers[0].graph);
            string before = UAnimBuildValidation.Hash(root);
            Resources.UnloadAsset(root.layers[0].graph);
            root.layers[0].graph = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(path);
            if(before != UAnimBuildValidation.Hash(root))
            {
                throw new Exception("Definition fingerprint changed merely because its graph was reloaded");
            }
        }

    }
}
