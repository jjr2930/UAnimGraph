using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace UAnimGraph.Authoring
{
    public static class UAnimDefaultLayerMachine
    {
        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly Dictionary<string, HashSet<string>> pending = new Dictionary<string, HashSet<string>>();
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static bool scheduled, creating;
        public static void Queue(RootLayerNode node)
        {
            if(creating || !string.IsNullOrEmpty(node.embeddedMachineId) || node.Option<UAnimMachine>("Machine") || node.Option<UAnimPoseGraph>("PoseGraph"))
            {
                return;
            }

            string path = GraphDatabase.GetGraphAssetPath(node.Graph);
            if(string.IsNullOrEmpty(path))
            {
                return;
            }

            if(!pending.TryGetValue(path, out var nodes))
            {
                pending[path] = nodes = new HashSet<string>();
            }

            nodes.Add(node.ID.ToString());
            if(scheduled)
            {
                return;
            }

            scheduled = true;
            EditorApplication.delayCall += Flush;
        }

        public static void EnsureAll(UAnimRootGraph root)
        {
            foreach(var node in root.GetNodes().OfType<RootLayerNode>().ToArray())
            {
                Ensure(node);
            }
        }

        public static void Ensure(RootLayerNode node)
        {
            if(!string.IsNullOrEmpty(node.embeddedMachineId) || node.Option<UAnimMachine>("Machine") || node.Option<UAnimPoseGraph>("PoseGraph"))
            {
                return;
            }

            creating = true;
            try
            {
                UAnimEmbeddedAssets.CreateMachine(node, false);
            }
            finally
            {
                creating = false;
            }
        }

        public static void Flush()
        {
            scheduled = false;
            var work = pending.ToArray();
            pending.Clear();
            foreach(var item in work)
            {
                if(!File.Exists(item.Key))
                {
                    continue;
                }

                var root = GraphDatabase.LoadGraph<UAnimRootGraph>(item.Key);
                if(root == null || !root.layerChain)
                {
                    continue;
                }

                foreach(string id in item.Value)
                {
                    var node = root.GetNodes().OfType<RootLayerNode>().FirstOrDefault(n => n.ID.ToString() == id);
                    if(node != null)
                    {
                        Ensure(node);
                    }
                }
            }
        }
    }
}
