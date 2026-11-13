using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Callbacks;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    [Serializable]
    public sealed class UAnimEmbeddedSource
    {
        public string id, name, extension, source;
    }

    // The .uanimasset is the canonical source. Native Toolkit windows edit
    // disposable files because its public API cannot open a StateMachine
    // stored inside a Graph. All runtime references point to real subassets.
    [InitializeOnLoad]
    public static class UAnimEmbeddedAssets
    {
        public const string CacheFolder = "Assets/Editor/UAnimGraphCache";
        // Editor-owned state persists across Play Mode; editor callbacks and runners manage its lifetime.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly HashSet<string> pending = new HashSet<string>();
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static bool scheduled, restoring;
        static UAnimEmbeddedAssets()
        {
            EditorApplication.delayCall += RestoreAll;
            EditorApplication.quitting += Flush;
            EditorApplication.playModeStateChanged += PrepareForPlay;
        }

        static void PrepareForPlay(PlayModeStateChange state)
        {
            if(state == PlayModeStateChange.ExitingEditMode)
            {
                Flush();
                RestoreAll();
            }
        }

        public static bool IsCache(string path)
        {
            return !string.IsNullOrEmpty(path) && path.Replace('\\', '/').StartsWith(CacheFolder + "/", StringComparison.Ordinal);
        }

        public static string CachePath(string rootPath, UAnimEmbeddedSource entry)
        {
            return CacheFolder + "/" + AssetDatabase.AssetPathToGUID(rootPath) + "/" + entry.id + "." + entry.extension;
        }

        public static string DisplayPath(string path)
        {
            if(!IsCache(path))
            {
                return Path.GetFileNameWithoutExtension(path);
            }

            string owner = RootPath(path);
            if(string.IsNullOrEmpty(owner))
            {
                return Path.GetFileNameWithoutExtension(path);
            }

            var root = GraphDatabase.LoadGraph<UAnimRootGraph>(owner);
            if(root == null)
            {
                return Path.GetFileNameWithoutExtension(path);
            }

            string id = Path.GetFileNameWithoutExtension(path), prefix = Path.GetFileNameWithoutExtension(owner);
            var layer = root.GetNodes().OfType<RootLayerNode>().FirstOrDefault(n => n.embeddedMachineId == id);
            if(layer != null)
            {
                return prefix + "/" + layer.Option<string>("Name") + "/StateMachine";
            }

            foreach(var machineSource in root.embeddedSources.Where(e => e.extension == "uamachine"))
            {
                var machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(CachePath(owner, machineSource));
                var state = machine?.GetStates().OfType<PoseState>().FirstOrDefault(s => s.embeddedGraphId == id);
                if(state == null)
                {
                    continue;
                }

                layer = root.GetNodes().OfType<RootLayerNode>().FirstOrDefault(n => n.embeddedMachineId == machineSource.id);
                string stateName = null;
                state.GetOptionByName("Name")?.TryGetValue(out stateName);
                return prefix + "/" + (layer?.Option<string>("Name") ?? machineSource.name) + "/StateMachine/" + (string.IsNullOrWhiteSpace(stateName) ? state.Title : stateName);
            }

            return prefix + "/" + (root.embeddedSources.FirstOrDefault(e => e.id == id)?.name ?? id);
        }

        static string RootPath(string cache)
        {
            if(!IsCache(cache))
            {
                return null;
            }

            var relative = cache.Replace('\\', '/').Substring(CacheFolder.Length + 1);
            int slash = relative.IndexOf('/');
            return slash < 0 ? null : AssetDatabase.GUIDToAssetPath(relative.Substring(0, slash));
        }

        static UAnimRootGraph Owner(string cache)
        {
            return GraphDatabase.LoadGraph<UAnimRootGraph>(RootPath(cache));
        }

        static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void DirtySave(UAnimRootGraph root)
        {
            var token = root.CreateVariable("Save embedded authoring", 0, VariableKind.Local);
            root.RemoveVariable(token, true);
            GraphDatabase.SaveGraph(root);
        }

        static UAnimEmbeddedSource Add(UAnimRootGraph root, string name, string extension)
        {
            var entry = new UAnimEmbeddedSource
            {
                id = Guid.NewGuid().ToString("N"),
                name = name,
                extension = extension,
                source = ""
            };
            root.embeddedSources.Add(entry);
            string path = CachePath(GraphDatabase.GetGraphAssetPath(root), entry);
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            return entry;
        }

        public static void Capture(string cache)
        {
            string rootPath = RootPath(cache);
            if(string.IsNullOrEmpty(rootPath) || !File.Exists(cache))
            {
                return;
            }

            var root = GraphDatabase.LoadGraph<UAnimRootGraph>(rootPath);
            if(root == null)
            {
                return;
            }

            string id = Path.GetFileNameWithoutExtension(cache);
            var entry = root.embeddedSources.FirstOrDefault(e => e.id == id);
            if(entry == null)
            {
                return;
            }

            string text = File.ReadAllText(cache);
            if(entry.source == text)
            {
                return;
            }

            entry.source = text;
            DirtySave(root);
        }

        public static void Queue(string path)
        {
            if(restoring)
            {
                return;
            }

            if(IsCache(path) || path.EndsWith(".uanimasset", StringComparison.Ordinal))
            {
                pending.Add(path);
                if(IsCache(path))
                {
                    string owner = RootPath(path);
                    if(!string.IsNullOrEmpty(owner))
                    {
                        pending.Add(owner);
                    }
                }
            }
            else
            {
                return;
            }

            if(scheduled)
            {
                return;
            }

            scheduled = true;
            EditorApplication.delayCall += Flush;
        }

        public static void Flush()
        {
            scheduled = false;
            var paths = pending.ToArray();
            pending.Clear();
            foreach(string path in paths.Where(IsCache))
            {
                Capture(path);
            }

            foreach(string path in paths.Where(p => !IsCache(p) && File.Exists(p)))
            {
                Restore(path);
            }
        }

        public static void RestoreAll()
        {
            foreach(string path in AssetDatabase.GetAllAssetPaths().Where(p => p.EndsWith(".uanimasset", StringComparison.Ordinal)).ToArray())
            {
                Restore(path);
            }
        }

        public static void Restore(string rootPath)
        {
            var root = GraphDatabase.LoadGraph<UAnimRootGraph>(rootPath);
            if(root == null || root.embeddedSources.Count == 0)
            {
                return;
            }

            if(restoring)
            {
                return;
            }

            var runtime = AssetDatabase.LoadAssetAtPath<UAnimAsset>(rootPath);
            bool repair = (runtime && (runtime.importError ?? "").Contains("Restoring embedded StateMachine editor cache")) ||
                HasMissingEmbeddedPoses(rootPath, root, runtime);
            bool changed = false;
            restoring = true;
            try
            {
                // Pose graphs must import first, so machines can resolve their
                // local State references on a fresh checkout or cache rebuild.
                foreach(var entry in root.embeddedSources.OrderBy(e => e.extension == "uamachine" ? 1 : 0))
                {
                    string path = CachePath(rootPath, entry);
                    if(string.IsNullOrEmpty(entry.source))
                    {
                        continue;
                    }

                    bool missing = !File.Exists(path);
                    if(missing)
                    {
                        Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
                        File.WriteAllText(path, entry.source);
                    }

                    // Existing files may still have no usable imported artifact.
                    // Preserve live edits and rebuild children before their parent.
                    bool unavailable = !AssetDatabase.LoadMainAssetAtPath(path);
                    if(missing || repair || unavailable)
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                        changed = true;
                    }
                }

                if(changed)
                {
                    AssetDatabase.ImportAsset(rootPath, ImportAssetOptions.ForceSynchronousImport);
                }
            }
            finally
            {
                restoring = false;
            }
        }

        public static void CreateMachine(RootLayerNode node, bool open = true)
        {
            node.defaultMachineInitialized = true;
            if(!string.IsNullOrEmpty(node.embeddedMachineId) || node.Option<UAnimMachine>("Machine") || node.Option<UAnimPoseGraph>("PoseGraph"))
            {
                if(open)
                {
                    node.OpenContents();
                }

                return;
            }

            var root = (UAnimRootGraph)node.Graph;
            string rootPath = GraphDatabase.GetGraphAssetPath(root);
            var entry = Add(root, node.Option<string>("Name"), "uamachine");
            string path = CachePath(rootPath, entry);
            var machine = StateMachineDatabase.CreateStateMachine<UAnimStateMachineGraph>(path);
            machine.requireEvaluation = true;
            var start = new UAnimEntryState();
            var state = new PoseState();
            machine.AddState(start);
            machine.AddState(state);
            start.Title = "Entry";
            state.Title = "Pose State";
            state.Position = new Vector2(300, 0);
            machine.Connect(start, state);
            StateMachineDatabase.SaveStateMachine(machine);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            entry.source = File.ReadAllText(path);
            root.UndoBeginRecordGraph("Create embedded State Machine", node);
            node.embeddedMachineId = entry.id;
            node.GetNodeOptionByName("Machine").TrySetValue<UAnimMachine>(null);
            node.GetNodeOptionByName("PoseGraph").TrySetValue<UAnimPoseGraph>(null);
            root.UndoEndRecordGraph();
            DirtySave(root);
            AssetDatabase.ImportAsset(rootPath, ImportAssetOptions.ForceSynchronousImport);
            UAnimDefaultStatePose.EnsureAll(machine);
            if(open)
            {
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UAnimMachine>(path));
            }
        }

        public static void CreateStateGraph(PoseState state, bool open = true)
        {
            state.defaultPoseInitialized = true;
            if(!string.IsNullOrEmpty(state.embeddedGraphId))
            {
                if(open)
                {
                    OpenState(state);
                }

                return;
            }

            UAnimPoseGraph existing = null;
            state.GetOptionByName("Graph")?.TryGetValue(out existing);
            if(existing)
            {
                if(open)
                {
                    AssetDatabase.OpenAsset(existing);
                }

                return;
            }

            string machinePath = StateMachineDatabase.GetStateMachineAssetPath(state.StateMachine);
            var root = Owner(machinePath);
            if(root == null)
            {
                throw new InvalidOperationException("Embedded State has no owning UAnimAsset.");
            }

            string rootPath = GraphDatabase.GetGraphAssetPath(root);
            var entry = Add(root, state.Title, "uastate");
            string path = CachePath(rootPath, entry);
            var graph = GraphDatabase.CreateGraph<UAnimStateGraph>(path);
            var output = new StateOutputNode();
            var clip = new ClipNode();
            graph.AddNode(output);
            graph.AddNode(clip);
            output.Position = new Vector2(400, 0);
            graph.Connect(clip.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            entry.source = File.ReadAllText(path);
            state.StateMachine.UndoBeginRecordStateMachine("Create embedded State Pose");
            state.embeddedGraphId = entry.id;
            state.GetOptionByName("Graph").TrySetValue<UAnimPoseGraph>(null);
            state.StateMachine.UndoEndRecordStateMachine();
            StateMachineDatabase.SaveStateMachine(state.StateMachine);
            AssetDatabase.ImportAsset(machinePath, ImportAssetOptions.ForceSynchronousImport);
            var machineEntry = root.embeddedSources.First(e => e.id == Path.GetFileNameWithoutExtension(machinePath));
            machineEntry.source = File.ReadAllText(machinePath);
            DirtySave(root);
            AssetDatabase.ImportAsset(rootPath, ImportAssetOptions.ForceSynchronousImport);
            if(open)
            {
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(path));
            }
        }

        static bool HasMissingEmbeddedPoses(string rootPath, UAnimRootGraph root, UAnimAsset runtime)
        {
            var missing = new HashSet<string>();
            if(runtime)
            {
                foreach(var layer in runtime.layers)
                {
                    if(layer?.graph == null)
                    {
                        continue;
                    }
                    foreach(var node in layer.graph.nodes)
                    {
                        if(node.machine)
                        {
                            foreach(var state in node.machine.states.Where(state => !state.graph))
                            {
                                missing.Add(state.id);
                            }
                        }
                    }
                }
            }
            foreach(var source in root.embeddedSources.Where(source => source.extension == "uamachine"))
            {
                string path = CachePath(rootPath, source);
                if(!File.Exists(path))
                {
                    continue;
                }
                var compiled = AssetDatabase.LoadAssetAtPath<UAnimMachine>(path);
                if(compiled)
                {
                    foreach(var state in compiled.states.Where(state => !state.graph))
                    {
                        missing.Add(state.id);
                    }
                }
                if(missing.Count == 0)
                {
                    continue;
                }
                var machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(path);
                if(machine != null && machine.GetStates().OfType<PoseState>().Any(state =>
                    !string.IsNullOrEmpty(state.embeddedGraphId) && missing.Contains(state.ID.ToString()) &&
                    root.embeddedSources.Any(pose => pose.id == state.embeddedGraphId && !string.IsNullOrEmpty(pose.source))))
                {
                    return true;
                }
            }
            return false;
        }

        public static string CachedPosePath(string machinePath, string id)
        {
            if(!IsCache(machinePath) || string.IsNullOrEmpty(id))
            {
                return null;
            }
            // Cache siblings can be resolved before the owning root has imported.
            return Path.GetDirectoryName(machinePath).Replace('\\', '/') + "/" + id + ".uastate";
        }

        public static UAnimPoseGraph CachedPose(string machinePath, string id)
        {
            string path = CachedPosePath(machinePath, id);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(path);
        }

        public static UAnimMachine CompileMachine(AssetImportContext ctx, UAnimRootGraph root, string id)
        {
            var entry = root.embeddedSources.FirstOrDefault(e => e.id == id);
            if(entry == null)
            {
                throw new InvalidOperationException("Missing embedded StateMachine: " + id);
            }

            string path = CachePath(ctx.assetPath, entry);
            ctx.DependsOnSourceAsset(path);
            ctx.DependsOnArtifact(path);
            var cached = AssetDatabase.LoadAssetAtPath<UAnimMachine>(path);
            if(!cached)
            {
                throw new InvalidOperationException("Restoring embedded StateMachine editor cache. Reimport the UAnimAsset if needed.");
            }

            var result = UnityEngine.Object.Instantiate(cached);
            result.name = entry.name;
            result.hideFlags = HideFlags.None;
            var cloned = new Dictionary<UAnimPoseGraph, UAnimPoseGraph>();
            foreach(var state in result.states)
            {
                if(!state.graph)
                {
                    continue;
                }

                if(!cloned.TryGetValue(state.graph, out var pose))
                {
                    if(!IsCache(AssetDatabase.GetAssetPath(state.graph)))
                    {
                        UAnimLayerImporter.Depend(ctx, state.graph);
                        continue;
                    }

                    pose = UnityEngine.Object.Instantiate(state.graph);
                    pose.name = state.name;
                    pose.hideFlags = HideFlags.None;
                    cloned.Add(state.graph, pose);
                    ctx.AddObjectToAsset("Pose-" + id + "-" + pose.id, pose);
                    UAnimLayerImporter.Depend(ctx, state.graph);
                    foreach(var node in pose.nodes)
                    {
                        UAnimLayerImporter.Depend(ctx, node.clip);
                    }
                }

                state.graph = pose;
            }

            ctx.AddObjectToAsset("Machine-" + id, result);
            return result;
        }

        public static UAnimMachine RuntimeMachine(RootLayerNode node)
        {
            string path = GraphDatabase.GetGraphAssetPath(node.Graph);
            var root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
            var result = root?.layers.FirstOrDefault(l => l.id == node.ID.ToString())?.graph?.nodes.FirstOrDefault(n => n.kind == UAnimPoseKind.StateMachine)?.machine;
            if(result)
            {
                return result;
            }

            var source = ((UAnimRootGraph)node.Graph).embeddedSources.FirstOrDefault(e => e.id == node.embeddedMachineId);
            if(source == null)
            {
                return null;
            }

            var cached = AssetDatabase.LoadAssetAtPath<UAnimMachine>(CachePath(path, source));
            return cached ? AssetDatabase.LoadAllAssetsAtPath(path).OfType<UAnimMachine>().FirstOrDefault(m => m.id == cached.id) : null;
        }

        public static void OpenMachine(RootLayerNode node)
        {
            string rootPath = GraphDatabase.GetGraphAssetPath(node.Graph);
            Restore(rootPath);
            var root = (UAnimRootGraph)node.Graph;
            var entry = root.embeddedSources.FirstOrDefault(e => e.id == node.embeddedMachineId);
            if(entry != null)
            {
                AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<UAnimMachine>(CachePath(rootPath, entry)));
            }
        }

        public static void OpenState(PoseState state)
        {
            string machinePath = StateMachineDatabase.GetStateMachineAssetPath(state.StateMachine);
            var pose = CachedPose(machinePath, state.embeddedGraphId);
            if(pose)
            {
                AssetDatabase.OpenAsset(pose);
            }
        }

        public static string[] PackExternal(string rootPath)
        {
            var root = GraphDatabase.LoadGraph<UAnimRootGraph>(rootPath);
            if(root == null || !root.layerChain)
            {
                throw new InvalidOperationException("Select a UAnimAsset Layer Chain.");
            }

            var originalPaths = new HashSet<string>();
            var poseEntries = new Dictionary<string, UAnimEmbeddedSource>();
            foreach(var layer in root.GetNodes().OfType<RootLayerNode>())
            {
                if(!string.IsNullOrEmpty(layer.embeddedMachineId))
                {
                    continue;
                }

                var external = layer.Option<UAnimMachine>("Machine");
                if(!external)
                {
                    continue;
                }

                string original = AssetDatabase.GetAssetPath(external);
                if(!original.EndsWith(".uamachine", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("StateMachine has no editable source: " + original);
                }

                var entry = Add(root, layer.Option<string>("Name"), "uamachine");
                string cache = CachePath(rootPath, entry);
                File.WriteAllText(cache, File.ReadAllText(original));
                AssetDatabase.ImportAsset(cache, ImportAssetOptions.ForceSynchronousImport);
                var machine = StateMachineDatabase.LoadStateMachine<UAnimStateMachineGraph>(cache);
                foreach(var state in machine.GetStates().OfType<PoseState>())
                {
                    UAnimPoseGraph pose = null;
                    state.GetOptionByName("Graph")?.TryGetValue(out pose);
                    if(!pose)
                    {
                        continue;
                    }

                    string source = AssetDatabase.GetAssetPath(pose);
                    if(!source.EndsWith(".uastate", StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("State Pose has no editable source: " + source);
                    }

                    if(!poseEntries.TryGetValue(source, out var poseEntry))
                    {
                        poseEntry = Add(root, state.Title, "uastate");
                        poseEntry.source = File.ReadAllText(source);
                        poseEntries.Add(source, poseEntry);
                        string poseCache = CachePath(rootPath, poseEntry);
                        File.WriteAllText(poseCache, poseEntry.source);
                        AssetDatabase.ImportAsset(poseCache, ImportAssetOptions.ForceSynchronousImport);
                        originalPaths.Add(source);
                    }

                    machine.UndoBeginRecordStateMachine("Embed State Pose");
                    state.embeddedGraphId = poseEntry.id;
                    state.GetOptionByName("Graph").TrySetValue<UAnimPoseGraph>(null);
                    machine.UndoEndRecordStateMachine();
                }

                StateMachineDatabase.SaveStateMachine(machine);
                AssetDatabase.ImportAsset(cache, ImportAssetOptions.ForceSynchronousImport);
                entry.source = File.ReadAllText(cache);
                originalPaths.Add(original);
                root.UndoBeginRecordGraph("Embed Layer StateMachine", layer);
                layer.embeddedMachineId = entry.id;
                layer.GetNodeOptionByName("Machine").TrySetValue<UAnimMachine>(null);
                root.UndoEndRecordGraph();
            }

            DirtySave(root);
            AssetDatabase.ImportAsset(rootPath, ImportAssetOptions.ForceSynchronousImport);
            return originalPaths.ToArray();
        }

        [MenuItem("Assets/UAnimGraph/Embed Referenced State Graphs", false, 2000)]
        static void PackSelected()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            var paths = PackExternal(path);
            Debug.Log("UAnimGraph: embedded " + paths.Length + " source graphs. Original files were retained for other references.");
        }

        [MenuItem("Assets/UAnimGraph/Embed Referenced State Graphs", true)]
        static bool CanPackSelected()
        {
            return Selection.activeObject is UAnimAsset && AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".uanimasset", StringComparison.Ordinal);
        }

        [OnOpenAsset(-100)]
        static bool OpenSubasset(EntityId instanceId, int line)
        {
            var asset = EditorUtility.EntityIdToObject(instanceId);
            if(asset is not UAnimMachine && asset is not UAnimPoseGraph)
            {
                return false;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            if(!path.EndsWith(".uanimasset", StringComparison.Ordinal))
            {
                return false;
            }

            Restore(path);
            var root = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
            if(root == null)
            {
                return false;
            }

            string id = asset is UAnimMachine m ? m.id : ((UAnimPoseGraph)asset).id;
            foreach(var entry in root.embeddedSources)
            {
                string cache = CachePath(path, entry);
                var runtime = AssetDatabase.LoadMainAssetAtPath(cache);
                if(runtime is UAnimMachine cm && cm.id == id || runtime is UAnimPoseGraph cp && cp.id == id)
                {
                    AssetDatabase.OpenAsset(runtime);
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class UAnimEmbeddedPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach(string path in imported)
            {
                UAnimEmbeddedAssets.Queue(path);
            }

            foreach(string path in moved)
            {
                UAnimEmbeddedAssets.Queue(path);
            }
        }
    }
}
