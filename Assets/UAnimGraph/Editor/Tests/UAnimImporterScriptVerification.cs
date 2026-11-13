using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimImporterScriptVerification
    {
        public static void Run()
        {
            var checks = new List<string>();
            void Check(bool ok, string text)
            {
                if(!ok)
                {
                    throw new InvalidOperationException(text);
                }

                checks.Add("PASS: " + text);
            }

            foreach(var type in new[]
            {
                typeof(UAnimRootImporter),
                typeof(UAnimLayerImporter),
                typeof(UAnimStateImporter),
                typeof(UAnimMachineImporter)
            }

            )
            {
                var mono = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/UAnimGraph/Editor/" + type.Name + ".cs");
                Check(mono && mono.GetClass() == type, type.Name + " has a resolvable matching MonoScript");
            }

            var paths = new[]
            {
                UAnimTransitionFunctionSample.RootPath,
                "Assets/UAnimGraph/Tests/UAnimAsset.uapose",
                "Assets/UAnimGraph/Demo/Idle.uastate",
                "Assets/UAnimGraph/Demo/Locomotion.uamachine"
            };
            foreach(string path in paths)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path);
                Check(importer, "AssetImporter is loadable: " + path);
                var serialized = new SerializedObject(importer);
                var script = serialized.FindProperty("m_Script")?.objectReferenceValue as MonoScript;
                Check(script && script.GetClass() == importer.GetType(), "Inspector Script reference resolves: " + path);
            }

            var root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(UAnimTransitionFunctionSample.RootPath);
            Check(root && UAnimValidation.Validate(root).Errors.Count == 0, "existing embedded sample definition still validates");
            Check(AssetDatabase.LoadAllAssetsAtPath(UAnimTransitionFunctionSample.RootPath).All(o => o), "root import has no missing script objects");
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/UAnimGraph-importer-script-verification.txt", checks);
            Debug.Log("IMPORTER SCRIPT PASS: " + checks.Count);
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                UAnimEmbeddedVerification.Run();
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
