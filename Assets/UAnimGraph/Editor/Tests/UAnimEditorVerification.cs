using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimEditorVerification
    {
        public static void OpenVerifiedReview()
        {
            UAnimVerification.Run();
            OpenReview();
        }

        public static void VerifyRenderedPreview()
        {
            var scene = EditorSceneManager.OpenScene("Assets/UAnimGraph/Demo/Demo.unity");
            var model = Object.FindObjectsByType<UAnimGraphPlayer>().First();
            var body = model.transform.Find("Body");
            var original = body.localPosition;
            Directory.CreateDirectory("Logs");
            UAnimWorkbench.CapturePreview(model.asset, model.gameObject, "Logs/UAnimGraph-preview.png");
            if(body.localPosition != original || model.Instance != null)
            {
                throw new System.Exception("Preview changed source scene object");
            }

            Debug.Log("UAnimGraph rendered preview PASS: clone rendered; original scene object untouched; graph disposed");
        }

        [MenuItem("UAnimGraph/Open Native State Verification")]
        public static void OpenNativeReview()
        {
            const string path = "Assets/UAnimGraph/Verification/NativeStates.uamachine";
            if(!File.Exists(path))
            {
                var graph = StateMachineDatabase.CreateStateMachine<UAnimStateMachineGraph>(path);
                var state = new PoseState();
                graph.AddState(state);
                state.Title = "Default Pose";
                state.GetOptionByName("Graph").TrySetValue(AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>("Assets/UAnimGraph/Demo/Idle.uastate"));
                state.GetOptionByName("Default").TrySetValue(true);
                StateMachineDatabase.SaveStateMachine(graph);
                AssetDatabase.ImportAsset(path);
            }

            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UAnimMachine>(path));
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>("Assets/UAnimGraph/Demo/Idle.uastate");
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        [MenuItem("UAnimGraph/Open Demo Preview")]
        public static void OpenReview()
        {
            UAnimDemoBuilder.Create();
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene("Assets/UAnimGraph/Demo/Demo.unity");
            var model = Object.FindObjectsByType<UAnimGraphPlayer>().First();
            UAnimWorkbench.Preview(model.asset, model.gameObject);
        }

        [MenuItem("UAnimGraph/Verify Authored Native States")]
        public static void VerifyNativeStates()
        {
            const string path = "Assets/UAnimGraph/Verification/NativeStates.uamachine";
            var machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(path);
            if(machine == null)
            {
                throw new System.Exception("Create NativeStates.uamachine in Verification and drag a State Pose Graph into it first.");
            }

            var states = machine.GetStates().OfType<ISubgraphState>().ToArray();
            if(states.Length == 0 || states.Any(s => s.GetSubgraphAsGraph()is not UAnimStateGraph))
            {
                throw new System.Exception("No native State Pose Graph subgraph state found");
            }

            StateMachineDatabase.SaveStateMachine(machine);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(path);
            if(machine.GetStates().OfType<ISubgraphState>().Count() != states.Length)
            {
                throw new System.Exception("Native state count changed after save/reimport");
            }

            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/UAnimGraph-native-state-verification.txt", "PASS: Native subgraph state links State Pose Graph, saves, reimports and reopens. Count=" + states.Length);
            Debug.Log("UAnimGraph native subgraph state save/reimport verification PASS");
        }
    }
}
