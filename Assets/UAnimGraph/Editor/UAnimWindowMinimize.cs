#if UNITY_EDITOR_WIN
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    [InitializeOnLoad]
    public static class UAnimWindowMinimize
    {
        const int StyleIndex = -16;
        const int MinimizeBox = 0x00020000;
        const uint MinimizeCommand = 0xF020;
        const uint FrameChanged = 0x0020;
        const uint NoMoveSizeActivate = 0x0010 | 0x0002 | 0x0001 | 0x0004;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static double nextUpdate;
        static UAnimWindowMinimize()
        {
            EditorApplication.update += Update;
        }

        static void Update()
        {
            if(Application.isBatchMode || EditorApplication.timeSinceStartup < nextUpdate)
            {
                return;
            }

            nextUpdate = EditorApplication.timeSinceStartup + 1;
            VisitGraphWindows(false);
        }

        [MenuItem("UAnimGraph/Restore Minimized Graph Windows")]
        public static void RestoreWindows()
        {
            VisitGraphWindows(true);
        }

        static void VisitGraphWindows(bool restore)
        {
            var titles = new HashSet<string>();
            foreach(var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if(window is not IGraphWindow graph)
                {
                    continue;
                }

                if(graph.Graph is UAnimLayerGraph || graph.Graph is UAnimStateGraph || graph.StateMachine is UAnimStateMachineGraph)
                {
                    titles.Add(window.titleContent.text);
                }
            }

            if(titles.Count == 0)
            {
                return;
            }

            uint process = GetCurrentProcessId();
            EnumWindows((handle, _) =>
            {
                GetWindowThreadProcessId(handle, out uint ownerProcess);
                if(ownerProcess != process)
                {
                    return true;
                }

                var title = new StringBuilder(1024);
                GetWindowText(handle, title, title.Capacity);
                if(!titles.Contains(title.ToString().TrimEnd('*')))
                {
                    return true;
                }

                EnableMinimize(handle);
                if(restore && IsIconic(handle))
                {
                    ShowWindow(handle, 9);
                }

                return true;
            }, IntPtr.Zero);
        }

        public static void EnableMinimize(IntPtr handle)
        {
            int style = GetWindowLong(handle, StyleIndex);
            if((style & MinimizeBox) == 0)
            {
                SetWindowLong(handle, StyleIndex, style | MinimizeBox);
                SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, FrameChanged | NoMoveSizeActivate);
            }

            IntPtr menu = GetSystemMenu(handle, false);
            if(menu != IntPtr.Zero)
            {
                EnableMenuItem(menu, MinimizeCommand, 0);
            }
        }

        delegate bool EnumWindowCallback(IntPtr handle, IntPtr parameter);
        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
        [DllImport("kernel32.dll")]
        static extern uint GetCurrentProcessId();
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr handle, out uint process);
        [DllImport("user32.dll")]
        static extern IntPtr GetWindow(IntPtr handle, uint command);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr handle, StringBuilder text, int maximum);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        static extern int GetWindowLong(IntPtr handle, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        static extern int SetWindowLong(IntPtr handle, int index, int value);
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")]
        static extern IntPtr GetSystemMenu(IntPtr handle, bool revert);
        [DllImport("user32.dll")]
        static extern uint EnableMenuItem(IntPtr menu, uint item, uint flags);
        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr handle);
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr handle, int command);
    }
}
#endif
