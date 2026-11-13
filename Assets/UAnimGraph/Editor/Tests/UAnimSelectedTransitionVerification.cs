using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimSelectedTransitionVerification
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

            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/SelectedTransitionVerification.uamachine");
            object owner = new object ();
            try
            {
                var machine = StateMachineDatabase.CreateStateMachine<UAnimStateMachineGraph>(path);
                var entry = new UAnimEntryState();
                var a = new PoseState();
                var b = new PoseState();
                machine.AddState(entry);
                machine.AddState(a);
                machine.AddState(b);
                a.Title = "Idle";
                b.Title = "Move";
                var pose = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>("Assets/UAnimGraph/Demo/Idle.uastate");
                a.GetOptionByName("Graph").TrySetValue(pose);
                b.GetOptionByName("Graph").TrySetValue(pose);
                var initial = machine.Connect(entry, a);
                var edge = machine.Connect(a, b);
                StateMachineDatabase.SaveStateMachine(machine);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var before = AssetDatabase.LoadAssetAtPath<UAnimMachine>(path);
                string edgeId = before.transitions.Single().id;
                UAnimTransitionSelection.Clear();
                Check(UAnimTransitionInspector.SelectedFor(before) == null, "selecting the StateMachine alone shows no transition function UI");
                Check(UAnimTransitionSelection.EnsurePickers(machine), "empty State transition gets a native selection row");
                Check(!UAnimTransitionSelection.EnsurePickers(machine), "selector initialization is idempotent");
                Check(!initial.GetRules().SelectMany(r => r.RootCondition.Get()).OfType<TransitionFunctionPickerCondition>().Any(), "Entry connection gets no function selector");
                StateMachineDatabase.SaveStateMachine(machine);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var after = AssetDatabase.LoadAssetAtPath<UAnimMachine>(path);
                Check(string.IsNullOrEmpty(after.importError) && after.transitions.Single().id == edgeId && after.transitions.Single().conditions.Length == 0, "editor selector preserves the transition's runtime behavior and ID");
                UAnimTransitionSelection.Attach(owner, machine, edge);
                Check(UAnimTransitionInspector.SelectedFor(after)?.ID == edge.ID, "native transition-row attachment exposes only that selected edge");
                UAnimTransitionSelection.Clear();
                Check(UAnimTransitionInspector.SelectedFor(after) == null, "Project or StateMachine selection clears transition UI");
                UAnimTransitionSelection.Attach(owner, machine, edge);
                UAnimTransitionSelection.Detach(owner);
                Check(UAnimTransitionInspector.SelectedFor(after) == null, "leaving the transition inspector hides its function UI");
                UAnimTransitionSelection.Attach(owner, machine, initial);
                Check(UAnimTransitionInspector.SelectedFor(after) == null, "Entry cannot become an editable transition selection");
                string key = TransitionEvaluationRegistry.Key(typeof(SampleTransitionConditions).GetMethod(nameof(SampleTransitionConditions.AlwaysTrue)));
                UAnimTransitionConditions.Choose(machine, edge, key);
                Check(!edge.GetRules().SelectMany(r => r.RootCondition.Get()).OfType<TransitionFunctionPickerCondition>().Any() && UAnimTransitionConditions.Functions(edge.GetRule(0).RootCondition).Single().Value == key, "choosing a function replaces the placeholder with a native Evaluation condition");
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/UAnimGraph-selected-transition-verification.txt", checks);
                Debug.Log("SELECTED TRANSITION PASS: " + checks.Count);
            }
            finally
            {
                UAnimTransitionSelection.Detach(owner);
                UAnimTransitionSelection.Clear();
                AssetDatabase.DeleteAsset(path);
            }
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                UAnimTransitionUiVerification.Run();
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
