using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UAnimGraph
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class TransitionLibraryAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class TransitionEvaluationAttribute : Attribute
    {
    }

    public delegate bool TransitionEvaluator(in TransitionContext context);
    public readonly struct TransitionContext
    {
        public readonly GameObject GraphOwner;
        public readonly UAnimInstance GraphInstance;
        public readonly SharedBlackboard SharedBlackboard;
        public readonly State From;
        public readonly float TransitionStartNormalizedTimeAtFromState;
        public readonly float TransitionStartTimeAtFromState;
        public readonly State To;
        public readonly float TransitionDuration;

        internal TransitionContext(UAnimInstance instance, State from, State to, float duration)
        {
            GraphOwner = instance.GraphOwner;
            GraphInstance = instance;
            SharedBlackboard = instance.SharedBlackboard;
            From = from;
            To = to;
            TransitionStartNormalizedTimeAtFromState = from.NormalizedTime;
            TransitionStartTimeAtFromState = (float)from.Time;
            TransitionDuration = duration;
        }

    }

    // Resolution occurs when an instance is constructed, never in the tick loop.
    // The build linker file preserves annotated methods, including user assemblies.
    public static class TransitionEvaluationRegistry
    {
        // Explicitly reset by Clear at SubsystemRegistration below.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly Dictionary<string, TransitionEvaluator> cache = new Dictionary<string, TransitionEvaluator>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            cache.Clear();
        }

        public static string Key(MethodInfo method)
        {
            return method.DeclaringType.Assembly.GetName().Name + "|" + method.DeclaringType.FullName + "|" + method.Name;
        }

        public static bool IsValid(MethodInfo method, out string error)
        {
            error = null;
            var type = method?.DeclaringType;
            if (type == null || !type.IsVisible || !type.IsAbstract || !type.IsSealed || type.ContainsGenericParameters || !type.IsDefined(typeof(TransitionLibraryAttribute), false))
            {
                error = "A public non-generic static TransitionLibrary is required.";
            }
            else if (!method.IsPublic || !method.IsStatic || method.IsGenericMethod || !method.IsDefined(typeof(TransitionEvaluationAttribute), false) || method.ReturnType != typeof(bool))
            {
                error = "A public static bool TransitionEvaluation method is required.";
            }
            else
            {
                var p = method.GetParameters();
                if (p.Length != 1 || p[0].ParameterType != typeof(TransitionContext).MakeByRefType() || !p[0].IsIn)
                {
                    error = "Expected bool Method(in TransitionContext context).";
                }
                else if (type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly).Length > 0)
                {
                    int count = 0;
                    foreach (var sibling in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    {
                        if (sibling.Name == method.Name)
                        {
                            count++;
                        }
                    }

                    if (count != 1)
                    {
                        error = "Evaluation method overloads are not supported.";
                    }
                }
            }

            return error == null;
        }

        public static TransitionEvaluator Resolve(string key)
        {
            if (cache.TryGetValue(key ?? "", out var evaluator))
            {
                return evaluator;
            }

            var parts = (key ?? "").Split('|');
            if (parts.Length != 3)
            {
                throw new InvalidOperationException("Choose a transition evaluation function.");
            }

            Type type = null;
            foreach (var assembly in UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies())
            {
                if (assembly.GetName().Name == parts[0])
                {
                    type = assembly.GetType(parts[1], false);
                    break;
                }
            }

            MethodInfo found = null;
            if (type != null)
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    if (method.Name == parts[2])
                    {
                        if (found != null)
                        {
                            throw new InvalidOperationException("Ambiguous transition evaluation: " + key);
                        }

                        found = method;
                    }
                }
            }

            if (!IsValid(found, out var error))
            {
                throw new InvalidOperationException(key + ": " + error);
            }

            evaluator = (TransitionEvaluator)Delegate.CreateDelegate(typeof(TransitionEvaluator), found);
            cache.Add(key, evaluator);
            return evaluator;
        }
    }

    [TransitionLibrary]
    public static class LocomotionTransitions
    {
        [TransitionEvaluation]
        public static bool CanMove(in TransitionContext ctx)
        {
            return ctx.SharedBlackboard.GetFloat("speed") > .1f;
        }

        [TransitionEvaluation]
        public static bool CanStop(in TransitionContext ctx)
        {
            return ctx.SharedBlackboard.GetFloat("speed") <= .1f;
        }

        [TransitionEvaluation]
        public static bool Always(in TransitionContext ctx)
        {
            return true;
        }

        [TransitionEvaluation]
        public static bool AfterFirstCycle(in TransitionContext ctx)
        {
            return ctx.From.TotalNormalizedTime >= 1;
        }
    }
}



