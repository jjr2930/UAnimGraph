using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    [InitializeOnLoad]
    public static class UAnimSharedBlackboard
    {
        // Source graphs own the data; this set only tracks sources to save after Undo.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly HashSet<string> editedRoots = new HashSet<string>();
        static UAnimSharedBlackboard()
        {
            Undo.undoRedoPerformed += SaveUndo;
        }

        public static string SourcePath(EditorWindow window)
        {
            if(window is not IGraphWindow graph)
            {
                return null;
            }

            if(graph.StateMachine is UAnimStateMachineGraph machine)
            {
                return StateMachineDatabase.GetStateMachineAssetPath(machine);
            }

            if(graph.Graph is UAnimLayerGraph || graph.Graph is UAnimStateGraph)
            {
                return GraphDatabase.GetGraphAssetPath(graph.Graph);
            }

            return null;
        }

        public static string ResolveRoot(string sourcePath)
        {
            if(string.IsNullOrEmpty(sourcePath))
            {
                return null;
            }

            sourcePath = sourcePath.Replace('\\', '/');
            if(Path.IsPathRooted(sourcePath))
            {
                sourcePath = FileUtil.GetProjectRelativePath(sourcePath);
            }

            if(sourcePath.EndsWith(".uanimasset", StringComparison.Ordinal))
            {
                return sourcePath;
            }

            if(UAnimEmbeddedAssets.IsCache(sourcePath))
            {
                string relative = sourcePath.Substring(UAnimEmbeddedAssets.CacheFolder.Length + 1);
                int slash = relative.IndexOf('/');
                return slash < 0 ? null : AssetDatabase.GUIDToAssetPath(relative.Substring(0, slash));
            }

            // Legacy external graphs can have several owners. Never guess an owner.
            string found = null;
            foreach(string guid in AssetDatabase.FindAssets("t:UAnimAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                if(!path.EndsWith(".uanimasset", StringComparison.Ordinal) || !root)
                {
                    continue;
                }

                if(!root.layers.Any(layer => layer != null && Contains(layer.graph, sourcePath, new HashSet<UnityEngine.Object>())))
                {
                    continue;
                }

                if(found != null && found != path)
                {
                    return null;
                }

                found = path;
            }

            return found;
        }

        static bool Contains(UnityEngine.Object value, string path, HashSet<UnityEngine.Object> visited)
        {
            if(!value || !visited.Add(value))
            {
                return false;
            }

            if(AssetDatabase.GetAssetPath(value) == path)
            {
                return true;
            }

            if(value is UAnimPoseGraph pose)
            {
                return pose.nodes.Any(node => node != null && Contains(node.machine, path, visited));
            }

            if(value is UAnimMachine machine)
            {
                return machine.states.Any(state => state != null && Contains(state.graph, path, visited));
            }

            return false;
        }

        public static UAnimParameter[] Parameters(string rootPath)
        {
            return GraphDatabase.LoadGraph<UAnimRootGraph>(rootPath)?.parameters ?? Array.Empty<UAnimParameter>();
        }

        public static string Add(string rootPath, UAnimParameterType type)
        {
            var parameters = Parameters(rootPath);
            string name = "Parameter";
            int suffix = 1;
            while(parameters.Any(parameter => parameter.name == name))
            {
                name = "Parameter " + suffix++;
            }

            var added = new UAnimParameter
            {
                name = name,
                type = type
            };
            Edit(rootPath, "Add shared parameter", values => values.Concat(new[] { added }).ToArray());
            return added.id;
        }

        public static void Update(string rootPath, string id, string name, double value, Vector2 vector)
        {
            var parameters = Parameters(rootPath);
            if(string.IsNullOrWhiteSpace(name) || parameters.Any(parameter => parameter.id != id && parameter.name == name))
            {
                throw new ArgumentException("Parameter names must be nonempty and unique.");
            }

            if(!UAnimValidation.Finite(value) || !UAnimValidation.Finite(vector.x) || !UAnimValidation.Finite(vector.y))
            {
                throw new ArgumentException("Parameter defaults must be finite.");
            }

            Edit(rootPath, "Edit shared parameter", values =>
            {
                var parameter = values.First(item => item.id == id);
                parameter.name = name;
                parameter.defaultValue = value;
                parameter.defaultVector2 = vector;
                return values;
            });
        }

        public static void Remove(string rootPath, string id)
        {
            Edit(rootPath, "Remove shared parameter", values => values.Where(value => value.id != id).ToArray());
        }

        static void Edit(string path, string label, Func<UAnimParameter[], UAnimParameter[]> change)
        {
            var graph = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
            if(graph == null)
            {
                throw new InvalidOperationException("Cannot find the owning UAnimGraph.");
            }

            var values = graph.parameters.Select(parameter => new UAnimParameter { id = parameter.id, name = parameter.name, type = parameter.type, defaultValue = parameter.defaultValue, defaultVector2 = parameter.defaultVector2 }).ToArray();
            values = change(values);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            graph.UndoBeginRecordGraph(label);
            graph.parameters = values;
            var token = graph.CreateVariable("Save shared parameters", 0, VariableKind.Local);
            graph.RemoveVariable(token, true);
            graph.UndoEndRecordGraph();
            Undo.CollapseUndoOperations(group);
            editedRoots.Add(path);
            Save(path);
        }

        static void Save(string path)
        {
            if(!File.Exists(path))
            {
                return;
            }

            var graph = GraphDatabase.LoadGraph<UAnimRootGraph>(path);
            if(graph != null)
            {
                GraphDatabase.SaveGraph(graph);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        static void SaveUndo()
        {
            foreach(string path in editedRoots.ToArray())
            {
                Save(path);
            }
        }

        public static ParameterNode CreateNode(Graph graph, string rootPath, string id)
        {
            if(ResolveRoot(GraphDatabase.GetGraphAssetPath(graph)) != rootPath)
            {
                throw new InvalidOperationException("The graph does not have this shared blackboard owner.");
            }

            var parameter = Parameters(rootPath).First(item => item.id == id);
            var node = new ParameterNode();
            graph.UndoBeginRecordGraph("Create shared Parameter node");
            graph.AddNode(node);
            node.GetNodeOptionByName("ParameterId").TrySetValue(parameter.id);
            node.GetNodeOptionByName("Type").TrySetValue(parameter.type);
            node.Position = new Vector2(40, 80 + graph.GetNodes().OfType<ParameterNode>().Count() * 80);
            node.DefineNode();
            graph.UndoEndRecordGraph();
            string path = GraphDatabase.GetGraphAssetPath(graph);
            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            UAnimEmbeddedAssets.Capture(path);
            return node;
        }
    }
}
