using System;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace UAnimGraph.Authoring
{
    [Overlay(defaultDisplay = false)]
    public sealed class UAnimGraphPreviewOverlay : Overlay
    {
        UAnimWorkbench workbench;
        UAnimGraphPreviewSource source;
        IMGUIContainer content;
        string boundPath, dependencyHash;
        [SerializeField]
        bool wholeGraph;
        [SerializeField]
        GameObject previewModel;

        public override VisualElement CreatePanelContent()
        {
            content = new IMGUIContainer(Draw);
            content.style.width = 360;
            content.style.maxHeight = 700;
            content.style.minHeight = 150;
            content.RegisterCallback<DetachFromPanelEvent>(_ => Release());
            content.schedule.Execute(Synchronize).Every(300);
            return content;
        }

        void Synchronize()
        {
            if(!displayed || collapsed || !containerWindow)
            {
                Release();
                return;
            }
            string path = UAnimSharedBlackboard.SourcePath(containerWindow);
            if(string.IsNullOrEmpty(path))
            {
                Release();
                return;
            }
            string hash = AssetDatabase.GetAssetDependencyHash(path).ToString();
            string rootPath = UAnimSharedBlackboard.ResolveRoot(path);
            if(!string.IsNullOrEmpty(rootPath))
            {
                hash += AssetDatabase.GetAssetDependencyHash(rootPath).ToString();
            }
            if(source != null && path == boundPath && hash == dependencyHash)
            {
                return;
            }
            if(!workbench)
            {
                workbench = ScriptableObject.CreateInstance<UAnimWorkbench>();
                workbench.hideFlags = HideFlags.HideAndDontSave;
                workbench.PreviewModel = previewModel;
                workbench.repaintRequested = () => content?.MarkDirtyRepaint();
            }
            workbench.BindRoot(null);
            source?.Dispose();
            source = new UAnimGraphPreviewSource(path, wholeGraph);
            workbench.BindRoot(source.Asset);
            boundPath = path;
            dependencyHash = hash;
            content?.MarkDirtyRepaint();
        }

        void Draw()
        {
            EditorGUI.BeginChangeCheck();
            wholeGraph = EditorGUILayout.Toggle("Preview Whole UAnimGraph", wholeGraph);
            if(EditorGUI.EndChangeCheck())
            {
                dependencyHash = null;
            }
            Synchronize();
            if(!workbench || source?.Asset == null)
            {
                EditorGUILayout.HelpBox("Save this graph and assign its animations before previewing.", MessageType.Info);
                return;
            }
            workbench.DrawEmbedded(true);
            if(workbench)
            {
                previewModel = workbench.PreviewModel;
            }
        }

        void Release()
        {
            if(workbench)
            {
                previewModel = workbench.PreviewModel;
                UnityEngine.Object.DestroyImmediate(workbench);
            }
            workbench = null;
            source?.Dispose();
            source = null;
            boundPath = null;
            dependencyHash = null;
        }

        public override void OnWillBeDestroyed()
        {
            Release();
            base.OnWillBeDestroyed();
        }

        public static UAnimGraphPreviewOverlay Show(EditorWindow window)
        {
            if(!window || window is not IGraphWindow || window.overlayCanvas == null)
            {
                return null;
            }
            var overlay = window.overlayCanvas.overlays.OfType<UAnimGraphPreviewOverlay>().FirstOrDefault();
            if(overlay == null)
            {
                overlay = new UAnimGraphPreviewOverlay { displayName = "Animation Preview" };
                window.overlayCanvas.Add(overlay);
            }
            window.overlayCanvas.overlaysEnabled = true;
            overlay.displayed = false;
            overlay.displayed = true;
            overlay.Undock();
            overlay.floatingPosition = new Vector2(Mathf.Max(12, window.position.width - 390), 70);
            overlay.collapsed = false;
            return overlay;
        }
    }

    public class UAnimGraphPreviewButton : EditorToolbarButton, IAccessContainerWindow
    {
        public EditorWindow containerWindow
        {
            get;
            set;
        }
        public UAnimGraphPreviewButton()
        {
            text = "Preview";
            tooltip = "Preview the current graph on a separate model copy.";
            clicked += () => UAnimGraphPreviewOverlay.Show(containerWindow);
        }
    }

    [GraphToolbarElement("UAnimGraph/RootPreview", typeof(UAnimRootGraph))]
    public sealed class UAnimRootPreviewButton : UAnimGraphPreviewButton
    {
    }

    [GraphToolbarElement("UAnimGraph/StatePreview", typeof(UAnimStateGraph))]
    public sealed class UAnimStatePreviewButton : UAnimGraphPreviewButton
    {
    }

    [GraphToolbarElement("UAnimGraph/MachinePreview", typeof(UAnimStateMachineGraph))]
    public sealed class UAnimMachinePreviewButton : UAnimGraphPreviewButton
    {
    }
}
