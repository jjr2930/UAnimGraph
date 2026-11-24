using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    // Temporary definitions select the open graph without modifying imported assets.
    public sealed class UAnimGraphPreviewSource : IDisposable
    {
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        public UAnimAsset Asset
        {
            get;
            private set;
        }

        public UAnimGraphPreviewSource(string sourcePath, bool wholeGraph)
        {
            string rootPath = UAnimSharedBlackboard.ResolveRoot(sourcePath);
            var root = AssetDatabase.LoadAssetAtPath<UAnimAsset>(rootPath ?? "");
            if(wholeGraph || sourcePath == rootPath)
            {
                Asset = root;
                return;
            }

            var pose = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(sourcePath);
            var machine = AssetDatabase.LoadAssetAtPath<UAnimMachine>(sourcePath);
            if(!pose && !machine)
            {
                return;
            }

            Asset = Create<UAnimAsset>();
            Asset.name = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            Asset.parameters = root ? root.parameters : Array.Empty<UAnimParameter>();
            if(pose && pose.stateGraph)
            {
                machine = Create<UAnimMachine>();
                machine.id = Guid.NewGuid().ToString("N");
                machine.defaultState = 0;
                machine.states = new[]
                {
                    new UAnimState
                    {
                        name = Asset.name,
                        graph = pose,
                        duration = pose.stateDuration,
                        speed = pose.stateSpeed
                    }
                };
            }

            if(machine)
            {
                pose = Create<UAnimPoseGraph>();
                pose.id = Guid.NewGuid().ToString("N");
                pose.output = 0;
                pose.nodes = new[]
                {
                    new UAnimPoseNode
                    {
                        id = Guid.NewGuid().ToString("N"),
                        kind = UAnimPoseKind.StateMachine,
                        machine = machine
                    }
                };
            }

            Asset.layers = new[] { new UAnimLayer { graph = pose } };
            Asset.compiledDependencyHash = UAnimBuildValidation.Hash(Asset);
        }

        T Create<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>();
            value.hideFlags = HideFlags.HideAndDontSave;
            owned.Add(value);
            return value;
        }

        public void Dispose()
        {
            for(int i = owned.Count - 1; i >= 0; i--)
            {
                if(owned[i])
                {
                    UnityEngine.Object.DestroyImmediate(owned[i]);
                }
            }
            owned.Clear();
            Asset = null;
        }
    }
}
