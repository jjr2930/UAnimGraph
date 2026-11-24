using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UAnimGraph.Authoring
{
    public static class UAnimCanvasPanVerification
    {
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static UAnimGraphEditor window;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static double deadline;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static int startupFrames;

        public static void RunBatch()
        {
            window = UAnimGraphEditor.OpenPath("Assets/CacheRepairFixture/UserGraph.uanimasset");
            deadline = EditorApplication.timeSinceStartup + 20;
            startupFrames = 0;
            EditorApplication.update += Verify;
        }

        static void Verify()
        {
            try
            {
                if(++startupFrames < 15)
                {
                    return;
                }
                var canvas = window.rootVisualElement.Q<UAnimEditorCanvas>();
                if(canvas?.panel == null || canvas.layout.width < 100)
                {
                    if(EditorApplication.timeSinceStartup > deadline)
                    {
                        throw new Exception("Canvas did not attach");
                    }
                    return;
                }
                EditorApplication.update -= Verify;
                var field = canvas.Query<TextField>().ToList().First();
                string beforeSource = File.ReadAllText(window.Document.Path);
                Vector2 before = canvas.ToGraph(Vector2.zero);
                if(!UAnimValidation.Finite(before.x) || !UAnimValidation.Finite(before.y))
                {
                    throw new Exception("Initial canvas transform is not finite: " + before);
                }
                Vector2 nativePoint = canvas.LocalToWorld(new Vector2(120, 100));
                Vector2 nativeDelta = new Vector2(80, 40);
                window.SendEvent(new Event { type = EventType.MouseDown, button = 0, modifiers = EventModifiers.Alt, mousePosition = nativePoint });
                window.SendEvent(new Event { type = EventType.MouseDrag, button = 0, modifiers = EventModifiers.Alt, mousePosition = nativePoint + nativeDelta, delta = nativeDelta });
                Vector2 nativeMoved = canvas.ToGraph(nativeDelta);
                if(!UAnimValidation.Finite(nativeMoved.x) || !UAnimValidation.Finite(nativeMoved.y) || (nativeMoved - before).sqrMagnitude > .001f)
                {
                    throw new Exception("First native EditorWindow Alt drag did not pan the canvas");
                }
                window.SendEvent(new Event { type = EventType.MouseUp, button = 0, modifiers = EventModifiers.Alt, mousePosition = nativePoint + nativeDelta });
                before = canvas.ToGraph(Vector2.zero);
                Vector2 point = field.worldBound.center;
                Vector2 delta = new Vector2(90, 55);
                field.Focus();
                using(var down = MouseDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, modifiers = EventModifiers.Alt, mousePosition = point }))
                {
                    field.SendEvent(down);
                }
                using(var lostByChild = PointerCaptureOutEvent.GetPooled(field, canvas, PointerId.mousePointerId))
                {
                    field.SendEvent(lostByChild);
                }
                using(var move = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, button = 0, modifiers = EventModifiers.Alt, mousePosition = point + delta }))
                {
                    canvas.SendEvent(move);
                }
                Vector2 moved = canvas.ToGraph(delta);
                if(!UAnimValidation.Finite(moved.x) || !UAnimValidation.Finite(moved.y) || (moved - before).sqrMagnitude > .001f)
                {
                    throw new Exception("Alt drag starting over the Name field did not pan the canvas");
                }
                using(var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, modifiers = EventModifiers.Alt, mousePosition = point + delta }))
                {
                    canvas.SendEvent(up);
                }
                Vector2 stopped = canvas.ToGraph(Vector2.zero);
                using(var move = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = point + delta * 2 }))
                {
                    canvas.SendEvent(move);
                }
                if((canvas.ToGraph(Vector2.zero) - stopped).sqrMagnitude > .001f)
                {
                    throw new Exception("Canvas continued panning after releasing the mouse: " + stopped + " -> " + canvas.ToGraph(Vector2.zero));
                }
                canvas.FrameOnOpen();
                if((canvas.ToGraph(Vector2.zero) - stopped).sqrMagnitude > .001f)
                {
                    throw new Exception("Deferred initial framing overwrote the first drag");
                }
                if(File.ReadAllText(window.Document.Path) != beforeSource)
                {
                    throw new Exception("Panning must not edit graph positions or save graph data");
                }
                Debug.Log("CANVAS PAN PASS: finite startup transform; first native EditorWindow Alt drag moves; focused-field drag moves; release stops; deferred framing preserves user pan; graph asset unchanged");
                window.Close();
                EditorApplication.Exit(0);
            }
            catch(Exception exception)
            {
                EditorApplication.update -= Verify;
                Debug.LogException(exception);
                if(window)
                {
                    window.Close();
                }
                EditorApplication.Exit(1);
            }
        }
    }
}
