using System;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimSingleAssetRegression
    {
        public static void Run()
        {
            string folder = "Assets/UAnimGraph/Verification/SingleAsset-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets/UAnimGraph/Verification", Path.GetFileName(folder));
            string path = folder + "/Animation." + UAnimRootGraph.Extension;
            GameObject actor = null;
            try
            {
                var root = CreateUAnimAsset.CreateAtPath(path, false);
                Check(root, "root is the main asset");
                Check(AssetDatabase.IsMainAsset(root), "UAnimAsset must be main asset");
                Check(Directory.GetFiles(folder).Count(f => !f.EndsWith(".meta")) == 1, "creation must produce exactly one source asset");
                Check(root.layers.Length == 1 && root.layers[0].graph, "default layer must exist");
                Check(AssetDatabase.GetAssetPath(root.layers[0].graph) == path && EditorUtility.IsPersistent(root.layers[0].graph), "default graph must be persisted inside the same asset");
                Check((root.layers[0].graph.hideFlags & HideFlags.HideInHierarchy) != 0, "compiled graph must be hidden from Project hierarchy");
                Check(AssetDatabase.LoadAllAssetsAtPath(path).Count(o => (o.hideFlags & HideFlags.HideInHierarchy) == 0) == 1, "Project must expose exactly one asset object");
                string id = root.id, layerId = root.layers[0].id;
                var second = CreateUAnimAsset.CreateAtPath(AssetDatabase.GenerateUniqueAssetPath(path), false);
                Check(second.id != id, "separate creations need distinct root IDs");
                Check(Directory.GetFiles(folder).Count(f => !f.EndsWith(".meta")) == 2, "two creations must produce two assets");
                var clip = new AnimationClip();
                clip.SetCurve("Body", typeof(Transform), "localPosition.y", AnimationCurve.Linear(0, 0, 1, 1));
                AssetDatabase.CreateAsset(clip, folder + "/Motion.anim");
                var graph = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                var output = graph.GetNodes().OfType<LayerOutputNode>().Single();
                var node = new ClipNode();
                graph.AddNode(node);
                node.GetNodeOptionByName("Clip").TrySetValue(clip);
                graph.Connect(node.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
                GraphDatabase.SaveGraph(graph);
                root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                root.parameters = new[]
                {
                    new UAnimParameter
                    {
                        id = "speed",
                        name = "Speed",
                        type = UAnimParameterType.Float,
                        defaultValue = .5
                    }
                };
                UAnimRootGraph.SaveSettings(root);
                var fresh = GraphDatabase.LoadGraphForImporter<UAnimRootGraph>(path);
                Check(fresh.parameters.Length == 1 && fresh.parameters[0].id == "speed" && fresh.parameters[0].defaultValue == .5, "Inspector settings must persist in source");
                Check(!fresh.GetVariables().Any(), "saving settings must not leave additional graph variables");
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                Check(root.id == id && root.layers[0].id == layerId, "IDs must survive reimport");
                Check(root.parameters.Length == 1 && root.parameters[0].id == "speed" && root.parameters[0].defaultValue == .5, "runtime settings must survive reimport");
                Check(UAnimValidation.Validate(root).Errors.Count == 0, "embedded graph must compile");
                actor = new GameObject("Single-asset regression", typeof(Animator));
                new GameObject("Body").transform.SetParent(actor.transform, false);
                using(var instance = new UAnimInstance(root, actor.GetComponent<Animator>(), true))
                {
                    instance.Tick(.25f);
                    Check(actor.transform.Find("Body").localPosition.y > .1f, "embedded graph must animate");
                }

                string renameError = AssetDatabase.RenameAsset(path, "Renamed");
                Check(string.IsNullOrEmpty(renameError), "rename must succeed");
                path = folder + "/Renamed." + UAnimRootGraph.Extension;
                root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                Check(root && root.id == id && root.layers[0].graph.nodes[0].clip == clip, "rename must preserve graph and clip references");
                File.WriteAllText("Logs/UAnimGraph-single-asset-regression.txt", "PASS: one creation = one visible/source asset; embedded default graph; unique IDs; settings save/reload; reimport; animation output; rename");
                Debug.Log("UAnimGraph single-asset regression PASS");
            }
            finally
            {
                if(actor)
                {
                    UnityEngine.Object.DestroyImmediate(actor);
                }

                AssetDatabase.DeleteAsset(folder);
            }
        }

        static void Check(bool pass, string message)
        {
            if(!pass)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
