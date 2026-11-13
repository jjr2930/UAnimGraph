using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace UAnimGraph.Authoring
{
    public static class UAnimDefaultStatePose
    {
        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly Dictionary<string, HashSet<string>> pending = new Dictionary<string, HashSet<string>>();
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static bool scheduled, creating;
        public static void Queue(PoseState state)
        {
            if(creating || state.StateMachine == null)
            {
                return;
            }

            string path = StateMachineDatabase.GetStateMachineAssetPath(state.StateMachine);
            if(string.IsNullOrEmpty(path))
            {
                return;
            }

            if(!pending.TryGetValue(path, out var states))
            {
                pending[path] = states = new HashSet<string>();
            }

            states.Add(state.ID.ToString());
            if(scheduled)
            {
                return;
            }

            scheduled = true;
            EditorApplication.delayCall += Flush;
        }

        public static void EnsureAll(StateMachine machine)
        {
            foreach(var state in machine.GetStates().OfType<PoseState>().ToArray())
            {
                Ensure(state);
            }
        }

        public static void Ensure(PoseState state)
        {
            if(state.StateMachine == null || !string.IsNullOrEmpty(state.embeddedGraphId))
            {
                return;
            }

            UAnimPoseGraph graph = null;
            state.GetOptionByName("Graph")?.TryGetValue(out graph);
            if(graph)
            {
                return;
            }

            creating = true;
            try
            {
                UAnimAuthoringControls.CreateStateGraph(state, false);
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

                var machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(item.Key);
                if(machine == null)
                {
                    continue;
                }

                foreach(string id in item.Value)
                {
                    var state = machine.GetStates().OfType<PoseState>().FirstOrDefault(s => s.ID.ToString() == id);
                    if(state != null)
                    {
                        Ensure(state);
                    }
                }
            }
        }
    }
}
