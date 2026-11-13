using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    [ScriptedImporter(3, "uamachine")]
    public sealed class UAnimMachineImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var graph = StateMachineDatabase.LoadStateMachineForImporter<UAnimStateMachineGraph>(ctx.assetPath);
            if(graph == null)
            {
                ctx.LogImportError("Cannot load state machine");
                return;
            }

            var result = ScriptableObject.CreateInstance<UAnimMachine>();
            result.id = graph.ID.ToString();
            result.name = System.IO.Path.GetFileNameWithoutExtension(ctx.assetPath);
            var allStates = graph.GetStates().ToArray();
            var states = allStates.Where(s => s is not UAnimEntryState).ToArray();
            var data = new List<UAnimState>();
            var transitions = new List<UAnimTransition>();
            int defaults = 0;
            try
            {
                foreach(var s in states)
                {
                    UAnimPoseGraph pose = null;
                    if(s is ISubgraphState sub)
                    {
                        var g = sub.GetSubgraphAsGraph();
                        if(g != null)
                        {
                            var sourcePath = GraphDatabase.GetGraphAssetPath(g);
                            if(sourcePath == ctx.assetPath)
                            {
                                pose = UAnimGraphCompiler.Compile(g, true, o => UAnimLayerImporter.Depend(ctx, o));
                                pose.name = s.Title;
                                ctx.AddObjectToAsset("State-" + s.ID, pose);
                            }
                            else
                            {
                                pose = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(sourcePath);
                            }
                        }
                    }
                    else if(s is PoseState embedded && !string.IsNullOrEmpty(embedded.embeddedGraphId))
                    {
                        string posePath = UAnimEmbeddedAssets.CachedPosePath(ctx.assetPath, embedded.embeddedGraphId);
                        if(!string.IsNullOrEmpty(posePath))
                        {
                            // Register even if loading currently returns null. The
                            // machine must retry when the Pose artifact is ready.
                            ctx.DependsOnSourceAsset(posePath);
                            ctx.DependsOnArtifact(posePath);
                        }
                        pose = UAnimEmbeddedAssets.CachedPose(ctx.assetPath, embedded.embeddedGraphId);
                    }
                    else
                    {
                        s.GetOptionByName("Graph")?.TryGetValue(out pose);
                    }

                    UAnimLayerImporter.Depend(ctx, pose);
                    string displayName = null;
                    s.GetOptionByName("Name")?.TryGetValue(out displayName);
                    data.Add(new UAnimState { id = s.ID.ToString(), name = string.IsNullOrWhiteSpace(displayName) ? s.Title : displayName, graph = pose, duration = pose ? pose.stateDuration : 1, speed = pose ? pose.stateSpeed : 1 });
                    bool isDefault = false;
                    s.GetOptionByName("Default")?.TryGetValue(out isDefault);
                    if(isDefault)
                    {
                        result.defaultState = data.Count - 1;
                        defaults++;
                    }
                }

                for(int i = 0; i < states.Length; i++)
                {
                    foreach(var t in states[i].GetOutgoingTransitions())
                    {
                        if(t is DefaultStateTransition)
                        {
                            result.defaultState = i;
                            defaults++;
                            continue;
                        }

                        var rules = t.GetRules().Where(r => r.Enabled).ToArray();
                        if(rules.Length == 0)
                        {
                            continue;
                        }

                        if(rules.Length != 1)
                        {
                            throw new Exception("State machines require one AND rule per transition; use separate transitions for OR");
                        }

                        var def = new UAnimTransition
                        {
                            id = t.ID.ToString(),
                            from = i,
                            to = Array.IndexOf(states, t.ToState)
                        };
                        var conditions = new List<UAnimCondition>();
                        Parse(rules[0].RootCondition, def, conditions);
                        if(!string.IsNullOrEmpty(def.evaluation) && (conditions.Count > 0 || def.hasExitTime))
                        {
                            throw new Exception("Code evaluation cannot combine with field or Exit Time conditions");
                        }

                        if(!string.IsNullOrEmpty(def.evaluation))
                        {
                            TransitionEvaluationRegistry.Resolve(def.evaluation);
                            var selected = UAnimEvaluationCatalog.Entries.FirstOrDefault(e => e.key == def.evaluation);
                            if(selected == null || !selected.Valid)
                            {
                                throw new Exception(selected?.error ?? "Evaluation function is not available in runtime assemblies");
                            }

                            def.allowSelf = true;
                        }

                        if(graph.requireEvaluation && string.IsNullOrEmpty(def.evaluation))
                        {
                            throw new Exception("Choose an evaluation function for each transition");
                        }

                        def.conditions = conditions.ToArray();
                        transitions.Add(def);
                    }
                }

                foreach(var entry in allStates.OfType<UAnimEntryState>())
                {
                    var edges = entry.GetOutgoingTransitions().ToArray();
                    if(entry.GetIncomingTransitions().Any() || edges.Length != 1)
                    {
                        throw new Exception("Entry must have no incoming and exactly one outgoing transition");
                    }

                    result.defaultState = Array.IndexOf(states, edges[0].ToState);
                    defaults++;
                }

                if(defaults != 1 || result.defaultState < 0)
                {
                    throw new Exception("Exactly one Default State self transition is required");
                }
            }
            catch(Exception e)
            {
                result.importError = e.Message;
            }

            result.states = data.ToArray();
            result.transitions = transitions.ToArray();
            ctx.AddObjectToAsset("Runtime", result);
            ctx.SetMainObject(result);
        }

        static void Parse(ICondition c, UAnimTransition t, List<UAnimCondition> conditions)
        {
            switch(c)
            {
                case TransitionFunctionPickerCondition:
                    break; // Editor selector; no runtime predicate.
                case IGroupCondition g:
                    if(g.Operation != GroupConditionOperation.And)
                    {
                        throw new Exception("Only AND condition groups supported; author OR using separate transitions");
                    }

                    foreach(var child in g.Get())
                    {
                        Parse(child, t, conditions);
                    }

                    break;
                case EvaluationFunctionCondition e:
                    if(!string.IsNullOrEmpty(t.evaluation))
                    {
                        throw new Exception("Choose exactly one evaluation function per transition");
                    }

                    t.evaluation = e.Value;
                    if(string.IsNullOrEmpty(e.Value))
                    {
                        throw new Exception("Choose an evaluation function");
                    }

                    break;
                case DurationCondition d:
                    t.duration = d.Value;
                    break;
                case PriorityCondition p:
                    t.priority = p.Value;
                    break;
                case ExitCondition e:
                    t.hasExitTime = true;
                    t.exitTime = e.Value;
                    break;
                case AllowSelfCondition a:
                    t.allowSelf = a.Value;
                    break;
                case ParameterCondition p:
                    var parts = (p.Value ?? "").Split('|');
                    if(parts.Length != 4)
                    {
                        throw new Exception("Parameter condition: expected ID|Type|Comparison|Value");
                    }

                    conditions.Add(new UAnimCondition { parameterId = parts[0].Trim(), type = Enum.Parse<UAnimParameterType>(parts[1], true), comparison = Enum.Parse<UAnimComparison>(parts[2], true), value = double.Parse(parts[3], CultureInfo.InvariantCulture) });
                    break;
                default:
                    throw new Exception("Unsupported or missing condition. Use UAnimGraph conditions");
            }
        }
    }
}
