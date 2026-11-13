using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimInspectorFunctionVerification
    {
        public static void Run()
        {
            var checks = new List<string>();
            void Check(bool ok, string name)
            {
                if(!ok)
                {
                    throw new InvalidOperationException(name);
                }

                checks.Add("PASS: " + name);
            }

            UAnimEvaluationCatalog.Refresh();
            var entries = UAnimEvaluationCatalog.Entries.Where(e => e.method.DeclaringType == typeof(SampleTransitionConditions)).ToArray();
            Check(entries.Length == 2 && entries.All(e => e.Valid), "both sample functions are discoverable valid runtime library methods");
            var root = UAnimTransitionFunctionSample.Build();
            Check(UAnimValidation.Validate(root).Errors.Count == 0, "two-State sample validates: " + string.Join(";", UAnimValidation.Validate(root).Errors));
            var machine = root.layers[0].graph.nodes[0].machine;
            Check(machine.states.Length == 2 && machine.transitions.Length == 2, "sample contains ordinary State-to-State transitions in both directions");
            Check(machine.transitions.All(t => t.evaluation.Contains("SampleTransitionConditions")), "both sample transitions select the new example class");
            var graph = GraphDatabase.LoadGraph<UAnimRootGraph>(UAnimTransitionFunctionSample.RootPath);
            string cache = UAnimEmbeddedAssets.CachePath(UAnimTransitionFunctionSample.RootPath, graph.embeddedSources.Single(e => e.extension == "uamachine"));
            Check(UAnimTransitionInspector.Resolve(AssetDatabase.LoadAssetAtPath<UAnimMachine>(cache)) == cache, "default Inspector resolves an imported StateMachine to its editable source");
            Check(UAnimTransitionInspector.Resolve(machine) == cache, "root Machine subasset Inspector resolves to the same function controls");
            var source = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(cache);
            string name = Path.GetFileNameWithoutExtension(cache);
            var native = Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.name.Replace(" ", "") == name && o.GetType().Name == "GraphObjectImp");
            Check(native, "native GTK GraphObject is available");
            Check(UAnimTransitionInspector.Resolve(native) == cache, "screenshot's native GraphObject Inspector resolves to the same function controls");
            var actor = new GameObject("Sample function verification", typeof(Animator));
            new GameObject("Body").transform.SetParent(actor.transform, false);
            try
            {
                using(var instance = new UAnimInstance(root, actor.GetComponent<Animator>(), true))
                {
                    int seen = 1;
                    for(int i = 0; i < 300; i++)
                    {
                        instance.Tick(.02f);
                        seen |= 1 << instance.Machines[0].CurrentState;
                    }

                    Check(seen == 3, "sample functions execute and visit both States without root parameters");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }

            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/UAnimGraph-inspector-functions-verification.txt", checks);
            Debug.Log("INSPECTOR FUNCTIONS PASS: " + checks.Count);
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                UAnimTransitionUiVerification.Run();
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
