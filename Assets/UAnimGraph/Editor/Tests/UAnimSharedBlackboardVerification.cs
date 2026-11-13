using System;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public static class UAnimSharedBlackboardVerification
    {
        public static void Run()
        {
            string folder = "Assets/SharedBlackboardVerification-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            string path = folder + "/Character.uanimasset";
            string otherPath = folder + "/Other.uanimasset";
            string cache = null;
            string otherCache = null;
            int checks = 0;
            void Check(bool ok, string message)
            {
                if(!ok)
                {
                    throw new InvalidOperationException(message);
                }

                checks++;
                Debug.Log("SHARED BLACKBOARD CHECK: " + message);
            }

            try
            {
                CreateUAnimAsset.CreateAtPath(path);
                CreateUAnimAsset.CreateAtPath(otherPath);
                var root = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
                string machinePath = UAnimEmbeddedAssets.CachePath(path, root.embeddedSources.Single(source => source.extension == "uamachine"));
                string posePath = UAnimEmbeddedAssets.CachePath(path, root.embeddedSources.Single(source => source.extension == "uastate"));
                cache = Path.GetDirectoryName(machinePath).Replace('\\', '/');
                var other = GraphDatabase.LoadGraph<UAnimRootGraph>(otherPath);
                otherCache = Path.GetDirectoryName(UAnimEmbeddedAssets.CachePath(otherPath, other.embeddedSources.First())).Replace('\\', '/');
                Check(UAnimSharedBlackboard.ResolveRoot(path) == path && UAnimSharedBlackboard.ResolveRoot(machinePath) == path && UAnimSharedBlackboard.ResolveRoot(posePath) == path, "root, machine and state pose resolve the same owner");
                string speed = UAnimSharedBlackboard.Add(path, UAnimParameterType.Float);
                Check(UAnimSharedBlackboard.Parameters(path).Single().id == speed, "add writes the canonical root parameter");
                UAnimSharedBlackboard.Update(path, speed, "Speed", 0.75, Vector2.zero);
                Check(UAnimSharedBlackboard.Parameters(UAnimSharedBlackboard.ResolveRoot(machinePath)).Single().name == "Speed", "machine reads renamed shared parameter");
                Check(UAnimSharedBlackboard.Parameters(UAnimSharedBlackboard.ResolveRoot(posePath)).Single().defaultValue == 0.75, "state pose reads shared default");
                Check(AssetDatabase.LoadAssetAtPath<UAnimAsset>(path).parameters.Single().id == speed, "runtime import uses the same stable parameter ID");
                Check(UAnimSharedBlackboard.Parameters(otherPath).Length == 0, "unrelated roots do not share definitions");
                UAnimSharedBlackboard.Update(path, speed, "RunSpeed", 1.5, Vector2.zero);
                Undo.PerformUndo();
                Check(UAnimSharedBlackboard.Parameters(path).Single().name == "Speed", "Undo restores shared name and value");
                Undo.PerformRedo();
                Check(UAnimSharedBlackboard.Parameters(path).Single().name == "RunSpeed", "Redo restores shared edit");
                var pose = GraphDatabase.LoadGraph<UAnimStateGraph>(posePath);
                var parameterNode = UAnimSharedBlackboard.CreateNode(pose, path, speed);
                Check(parameterNode.Option<string>("ParameterId") == speed && parameterNode.Option<UAnimParameterType>("Type") == UAnimParameterType.Float, "Create Node binds shared stable ID and type");
                Check(UAnimSharedBlackboard.Parameters(path).Single().id == speed, "creating a node does not duplicate parameters");
                bool rejected = false;
                try
                {
                    UAnimSharedBlackboard.CreateNode(pose, otherPath, speed);
                }
                catch(InvalidOperationException)
                {
                    rejected = true;
                }

                Check(rejected, "node creation rejects a different root owner");
                foreach(UAnimParameterType type in Enum.GetValues(typeof(UAnimParameterType)))
                {
                    UAnimSharedBlackboard.Add(path, type);
                }

                Check(UAnimSharedBlackboard.Parameters(path).Length == 6 && UAnimSharedBlackboard.Parameters(path).Select(item => item.name).Distinct().Count() == 6, "all parameter types and unique default names supported");
                UAnimSharedBlackboard.Remove(path, speed);
                Check(!UAnimSharedBlackboard.Parameters(path).Any(item => item.id == speed), "deletion is shared");
                Undo.PerformUndo();
                Check(UAnimSharedBlackboard.Parameters(path).Any(item => item.id == speed), "Undo restores deleted parameter with original identity");
                AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(path));
                AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(machinePath));
                AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(posePath));
                var windows = Resources.FindObjectsOfTypeAll<EditorWindow>().Where(window => UAnimSharedBlackboard.ResolveRoot(UAnimSharedBlackboard.SourcePath(window)) == path).ToArray();
                Check(windows.Length >= 3, "native root, state machine and pose windows exist");
                foreach(var window in windows)
                {
                    var overlay = UAnimSharedBlackboardWindows.Ensure(window);
                    Check(overlay != null && overlay.displayed && overlay.CreatePanelContent() != null, "shared overlay attached to " + UAnimSharedBlackboard.SourcePath(window));
                    Check(window.overlayCanvas.overlays.OfType<UAnimSharedBlackboardOverlay>().Count() == 1, "exactly one shared panel per native graph window");
                }

                Debug.Log("SHARED BLACKBOARD PASS: " + checks);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
                if(cache != null)
                {
                    AssetDatabase.DeleteAsset(cache);
                }

                if(otherCache != null)
                {
                    AssetDatabase.DeleteAsset(otherCache);
                }
            }
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                UAnimCompleteVerification.Run();
                EditorApplication.Exit(0);
            }
            catch(Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
