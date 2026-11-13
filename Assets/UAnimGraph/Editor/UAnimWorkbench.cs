using System;
using System.IO;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace UAnimGraph.Authoring
{
    public sealed class CreateUAnimAsset : AssetCreationEndAction
    {
        [MenuItem("Assets/Create/UAnimGraph/UAnimAsset", false, 0)]
        static void Create()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(default(EntityId), CreateInstance<CreateUAnimAsset>(), "UAnimAsset." + UAnimRootGraph.Extension, null, null);
        }

        public override void Action(EntityId instanceId, string pathName, string resourceFile)
        {
            ProjectWindowUtil.ShowCreatedAsset(CreateAtPath(pathName));
        }

        public static UAnimAsset CreateAtPath(string path, bool layerChain = true)
        {
            var graph = GraphDatabase.CreateGraph<UAnimRootGraph>(path);
            graph.layerChain = layerChain;
            var output = new LayerOutputNode();
            graph.AddNode(output);
            output.Position = new Vector2(450, 100);
            if(layerChain)
            {
                var layer = new RootLayerNode();
                graph.AddNode(layer);
                layer.Position = new Vector2(0, 100);
                layer.GetNodeOptionByName("Name").TrySetValue("Base Layer");
                graph.Connect(layer.GetOutputPortByName("Pose"), output.GetInputPortByName("Pose"));
            }

            GraphDatabase.SaveGraph(graph);
            if(layerChain)
            {
                UAnimDefaultLayerMachine.EnsureAll(graph);
            }

            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
        }
    }

    public sealed class UAnimSaveProcessor : AssetModificationProcessor
    {
        static string[] OnWillSaveAssets(string[] paths)
        {
            foreach(string path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<UAnimAsset>(path);
                if(asset)
                {
                    UAnimBuildValidation.Compile(asset, false);
                }
            }

            return paths;
        }
    }

    public static class UAnimBuildValidation
    {
        [Serializable]
        sealed class Fingerprint
        {
            public int schemaVersion;
            public string id;
            public UAnimParameter[] parameters;
            public LayerFingerprint[] layers;
        }

        [Serializable]
        sealed class LayerFingerprint
        {
            public string id, name, graph, mask;
            public bool enabled, additive;
            public float weight;
        }

        static string Reference(UnityEngine.Object asset)
        {
            if(!asset)
            {
                return "";
            }

            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId) ? guid + ":" + localId.ToString(System.Globalization.CultureInfo.InvariantCulture) : "transient:" + asset.GetType().FullName + ":" + asset.name;
        }

        public static string Hash(UAnimAsset asset, string sourcePath = null)
        {
            // JsonUtility serializes Unity object references as session-local
            // instance IDs. Persist GUID/local-ID references instead, so a reload
            // or asset import does not falsely report a changed definition.
            string path = sourcePath ?? AssetDatabase.GetAssetPath(asset);
            bool embedded = Path.GetExtension(path) == "." + UAnimRootGraph.Extension;
            var fingerprint = new Fingerprint
            {
                schemaVersion = asset.schemaVersion,
                id = asset.id,
                parameters = asset.parameters,
                layers = asset.layers.Select((l, i) => new LayerFingerprint { id = l.id, name = l.name, enabled = l.enabled, weight = l.weight, additive = l.additive, graph = embedded ? "embedded-layer:" + l.id : Reference(l.graph), mask = Reference(l.mask) }).ToArray()
            };
            string content = JsonUtility.ToJson(fingerprint);
            if(embedded && File.Exists(path))
            {
                content += Hash128.Compute(File.ReadAllText(path)).ToString();
            }

            var dependencies = new System.Collections.Generic.HashSet<string>();
            var visited = new System.Collections.Generic.HashSet<UnityEngine.Object>();
            if(!embedded && !string.IsNullOrEmpty(path))
            {
                foreach(var dependency in AssetDatabase.GetDependencies(path))
                {
                    dependencies.Add(dependency);
                }
            }

            for(int i = 0; i < asset.layers.Length; i++)
            {
                var layer = asset.layers[i];
                Add(layer.mask);
                if(!embedded || i > 0)
                {
                    Add(layer.graph);
                }

                if(embedded && layer.graph)
                {
                    foreach(var node in layer.graph.nodes)
                    {
                        Add(node.clip);
                        Add(node.machine);
                    }
                }
            }

            foreach(string dependency in dependencies.OrderBy(p => AssetDatabase.AssetPathToGUID(p), StringComparer.Ordinal))
            {
                if(dependency != path)
                {
                    content += AssetDatabase.AssetPathToGUID(dependency) + AssetDatabase.GetAssetDependencyHash(dependency).ToString();
                }
            }

            return Hash128.Compute(content).ToString();
            void Add(UnityEngine.Object value)
            {
                if(!value || !visited.Add(value))
                {
                    return;
                }

                string dependency = AssetDatabase.GetAssetPath(value);
                if(!string.IsNullOrEmpty(dependency) && !UAnimEmbeddedAssets.IsCache(dependency))
                {
                    dependencies.Add(dependency);
                }

                if(value is UAnimMachine machine)
                {
                    foreach(var state in machine.states)
                    {
                        Add(state.graph);
                    }
                }

                if(value is UAnimPoseGraph pose)
                {
                    foreach(var node in pose.nodes)
                    {
                        Add(node.clip);
                        Add(node.machine);
                    }
                }
            }
        }

        public static bool Compile(UAnimAsset asset, bool log = true)
        {
            var v = UAnimValidation.Validate(asset);
            if(log)
            {
                foreach(var e in v.Errors)
                {
                    Debug.LogError(e, asset);
                }

                foreach(var w in v.Warnings)
                {
                    Debug.LogWarning(w, asset);
                }
            }

            if(v.Errors.Count != 0)
            {
                asset.compiledDependencyHash = "";
                if(!UAnimRootGraph.IsEmbedded(asset))
                {
                    EditorUtility.SetDirty(asset);
                }

                return false;
            }

            asset.compiledDependencyHash = Hash(asset);
            if(!UAnimRootGraph.IsEmbedded(asset))
            {
                EditorUtility.SetDirty(asset);
            }

            return true;
        }
    }

    public sealed class UAnimBuildProcessor : IPreprocessBuildWithReport
    {
        public int callbackOrder
        {
            get
            {
                return 0;
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            UAnimEmbeddedAssets.RestoreAll();
            bool valid = true;
            foreach(var guid in AssetDatabase.FindAssets("t:UAnimAsset"))
            {
                valid &= UAnimBuildValidation.Compile(AssetDatabase.LoadAssetAtPath<UAnimAsset>(AssetDatabase.GUIDToAssetPath(guid)));
            }

            AssetDatabase.SaveAssets();
            if(!valid)
            {
                throw new BuildFailedException("UAnimGraph: invalid definitions. Resolve the Console errors before building.");
            }
        }
    }

    [CustomEditor(typeof(UAnimAsset))]
    public sealed class UAnimAssetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var a = (UAnimAsset)target;
            var rootAssetPath = AssetDatabase.GetAssetPath(a);
            var rootGraph = UAnimRootGraph.IsEmbedded(a) ? GraphDatabase.LoadGraph<UAnimRootGraph>(rootAssetPath) : null;
            bool chain = rootGraph != null && rootGraph.layerChain;
            if(chain && GUILayout.Button("Open Root Graph / Layer Chain"))
            {
                AssetDatabase.OpenAsset(a);
            }

            if(GUILayout.Button("Authoring Workspace"))
            {
                UAnimEditorWorkspace.Open(a);
            }

            EditorGUI.BeginChangeCheck();
            if(chain)
            {
                serializedObject.Update();
                DrawPropertiesExcluding(serializedObject, "m_Script", "layers", "compiledDependencyHash", "importError");
                serializedObject.ApplyModifiedProperties();
            }
            else
            {
                DrawDefaultInspector();
            }

            if(EditorGUI.EndChangeCheck() && UAnimRootGraph.IsEmbedded(a))
            {
                UAnimRootGraph.SaveSettings(a);
                return;
            }

            EditorGUILayout.HelpBox("Parameters are shared by stable ID. Copy each ID into Parameter nodes and transition conditions. Layer 0: weight 1, Override, no mask.", MessageType.Info);
            if(!chain && GUILayout.Button("Create missing Layer Graphs"))
            {
                Undo.RecordObject(a, "Create Layer Graphs");
                for(int i = 0; i < a.layers.Length; i++)
                {
                    if(a.layers[i] != null && !a.layers[i].graph)
                    {
                        string path = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.ChangeExtension(AssetDatabase.GetAssetPath(a), null) + "-Layer" + i + ".uapose");
                        var graph = GraphDatabase.CreateGraph<UAnimLayerGraph>(path);
                        graph.AddNode(new LayerOutputNode());
                        GraphDatabase.SaveGraph(graph);
                        AssetDatabase.ImportAsset(path);
                        a.layers[i].graph = AssetDatabase.LoadAssetAtPath<UAnimPoseGraph>(path);
                    }
                }

                if(UAnimRootGraph.IsEmbedded(a))
                {
                    UAnimRootGraph.SaveSettings(a);
                }
                else
                {
                    EditorUtility.SetDirty(a);
                    AssetDatabase.SaveAssets();
                }
            }

            if(chain)
            {
                EditorGUILayout.HelpBox("Edit Layer name, Machine, AvatarMask, Additive and Weight on root Layer nodes. The Pose wire order controls accumulation. The first Layer has no input and must use Weight 1 / Override / no mask.", MessageType.Info);
            }

            foreach(var l in a.layers)
            {
                if(l != null && l.graph && GUILayout.Button("Open / " + a.name + " / " + l.name))
                {
                    var machine = l.graph.nodes.FirstOrDefault(n => n.kind == UAnimPoseKind.StateMachine)?.machine;
                    AssetDatabase.OpenAsset(chain && machine ? (UnityEngine.Object)machine : l.graph);
                }
            }

            if(GUILayout.Button("Validate / Compile"))
            {
                if(UAnimBuildValidation.Compile(a))
                {
                    Debug.Log("UAnimGraph: valid — " + a.name, a);
                }

                AssetDatabase.SaveAssets();
            }

            if(GUILayout.Button("Preview / Debug"))
            {
                UAnimWorkbench.Open(a);
            }

            var v = UAnimValidation.Validate(a);
            foreach(var e in v.Errors)
            {
                EditorGUILayout.HelpBox(e, MessageType.Error);
            }

            foreach(var w in v.Warnings)
            {
                EditorGUILayout.HelpBox(w, MessageType.Warning);
            }
        }
    }

    public sealed class UAnimWorkbench : EditorWindow
    {
        public static UAnimInstance ActiveInstance
        {
            get
            {
                return Resources.FindObjectsOfTypeAll<UAnimWorkbench>().Select(w => w.debugTarget ? w.debugTarget.Instance : w.instance).FirstOrDefault(i => i != null);
            }
        }

        public static UAnimInstance PreviewInstance
        {
            get
            {
                return Resources.FindObjectsOfTypeAll<UAnimWorkbench>().Select(w => w.instance).FirstOrDefault(i => i != null);
            }
        }

        UAnimAsset asset;
        GameObject targetObject, clone;
        float previewTime, previewLength = 10;
        bool loopPreview = true;
        UAnimGraphPlayer debugTarget;
        UAnimInstance instance;
        UAnimGraphDebugView debugView;
        UAnimInstance visualizedInstance;
        PreviewRenderUtility preview;
        double lastTime;
        bool playing;
        string error;
        Vector2 scroll;
        [MenuItem("Window/UAnimGraph/Preview and Debug")]
        static void ShowWindow()
        {
            GetWindow<UAnimWorkbench>("UAnimGraph");
        }

        public static void Open(UAnimAsset a)
        {
            var w = GetWindow<UAnimWorkbench>("UAnimGraph");
            w.Stop();
            w.asset = a;
        }

        public static void Preview(UAnimAsset a, GameObject model)
        {
            Open(a);
            var w = GetWindow<UAnimWorkbench>("UAnimGraph");
            w.targetObject = model;
            w.StartPreview();
        }

        void OnEnable()
        {
            EditorApplication.update += UpdatePreview;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        void OnDisable()
        {
            EditorApplication.update -= UpdatePreview;
            AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorApplication.playModeStateChanged -= ModeChanged;
            Stop();
        }

        void ModeChanged(PlayModeStateChange change)
        {
            Stop();
        }

        void UpdatePreview()
        {
            double now = EditorApplication.timeSinceStartup;
            if(instance != null && playing)
            {
                float dt = (float)Math.Min(.1, now - lastTime);
                instance.Tick(dt);
                previewTime += dt;
                if(previewTime >= previewLength)
                {
                    if(loopPreview)
                    {
                        instance.SeekPreview(0);
                        previewTime = 0;
                    }
                    else
                    {
                        playing = false;
                    }
                }
            }

            var live = debugTarget ? debugTarget.Instance : instance;
            if(live != visualizedInstance)
            {
                debugView?.Dispose();
                debugView = live != null ? new UAnimGraphDebugView() : null;
                visualizedInstance = live;
            }

            if(live != null && live.Graph.IsValid())
            {
                debugView?.Apply(live);
            }

            lastTime = now;
            if(instance != null || debugTarget)
            {
                Repaint();
            }
        }

        void Stop()
        {
            instance?.Dispose();
            instance = null;
            debugView?.Dispose();
            debugView = null;
            visualizedInstance = null;
            if(preview != null)
            {
                preview.Cleanup();
                preview = null;
            }

            if(clone)
            {
                DestroyImmediate(clone);
            }

            clone = null;
            playing = false;
        }

        void StartPreview()
        {
            Stop();
            error = null;
            previewTime = 0;
            if(!asset || !targetObject)
            {
                error = "Select an asset and an Animator model.";
                return;
            }

            try
            {
                preview = new PreviewRenderUtility();
                var staging = new GameObject("Preview staging")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                staging.SetActive(false);
                preview.AddSingleGO(staging);
                clone = Instantiate(targetObject, staging.transform, false);
                clone.hideFlags = HideFlags.HideAndDontSave;
                foreach(var b in clone.GetComponentsInChildren<Behaviour>(true))
                {
                    if(b is not Animator)
                    {
                        b.enabled = false;
                    }
                }

                clone.transform.SetParent(null, false);
                preview.AddSingleGO(clone);
                DestroyImmediate(staging);
                var animator = clone.GetComponentInChildren<Animator>();
                animator.applyRootMotion = false;
                animator.runtimeAnimatorController = null;
                instance = new UAnimInstance(asset, animator, true);
                playing = true;
                lastTime = EditorApplication.timeSinceStartup;
            }
            catch(Exception e)
            {
                error = e.Message;
                Stop();
            }
        }

        void SetupCamera()
        {
            var bounds = new Bounds(clone.transform.position, Vector3.one);
            foreach(var renderer in clone.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(renderer.bounds);
            }

            preview.camera.fieldOfView = 30;
            preview.camera.orthographic = false;
            float distance = Mathf.Max(2, bounds.extents.magnitude / Mathf.Sin(15 * Mathf.Deg2Rad) * 1.15f);
            preview.camera.transform.position = bounds.center + new Vector3(0, .1f, -1).normalized * distance;
            preview.camera.backgroundColor = new Color(.12f, .14f, .18f);
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.transform.LookAt(bounds.center);
            preview.camera.nearClipPlane = .01f;
            preview.camera.farClipPlane = 1000;
            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40, 40, 0);
            preview.lights[1].intensity = .8f;
        }

        public static void CapturePreview(UAnimAsset asset, GameObject model, string path)
        {
            var window = CreateInstance<UAnimWorkbench>();
            try
            {
                window.asset = asset;
                window.targetObject = model;
                window.StartPreview();
                if(window.instance == null)
                {
                    throw new InvalidOperationException(window.error ?? "Preview did not start");
                }

                window.instance.Tick(.25f);
                window.SetupCamera();
                window.preview.BeginStaticPreview(new Rect(0, 0, 640, 480));
                window.preview.Render(true);
                var texture = window.preview.EndStaticPreview();
                try
                {
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    var pixels = texture.GetPixels32();
                    var first = pixels[0];
                    int varied = 0;
                    foreach(var pixel in pixels)
                    {
                        if(Math.Abs(pixel.r - first.r) + Math.Abs(pixel.g - first.g) + Math.Abs(pixel.b - first.b) > 20)
                        {
                            varied++;
                        }
                    }

                    if(varied < 100)
                    {
                        throw new InvalidOperationException("Preview rendered no visible model. active=" + window.clone.activeInHierarchy + ", renderers=" + window.clone.GetComponentsInChildren<Renderer>().Length + ", scene=" + window.clone.scene.name);
                    }
                }
                finally
                {
                    DestroyImmediate(texture);
                }
            }
            finally
            {
                window.Stop();
                DestroyImmediate(window);
            }
        }

        public void BindRoot(UAnimAsset root)
        {
            if(asset != root)
            {
                Stop();
                asset = root;
            }
        }

        public void DrawEmbedded()
        {
            OnGUI();
        }

        void OnGUI()
        {
            EditorGUI.BeginChangeCheck();
            asset = (UAnimAsset)EditorGUILayout.ObjectField("Root Asset", asset, typeof(UAnimAsset), false);
            if(EditorGUI.EndChangeCheck())
            {
                Stop();
            }

            targetObject = (GameObject)EditorGUILayout.ObjectField("Preview Model", targetObject, typeof(GameObject), true);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Start Preview"))
                {
                    StartPreview();
                }

                if(GUILayout.Button(playing ? "Pause" : "Resume"))
                {
                    playing = !playing;
                }

                if(GUILayout.Button("Stop"))
                {
                    Stop();
                }
            }

            if(instance != null)
            {
                loopPreview = EditorGUILayout.Toggle("Loop Preview", loopPreview);
                previewLength = Mathf.Clamp(EditorGUILayout.FloatField("Preview Length", previewLength), .1f, 60);
                float next = EditorGUILayout.Slider("Time", previewTime, 0, previewLength);
                if(!Mathf.Approximately(next, previewTime))
                {
                    playing = false;
                    instance.SeekPreview(next);
                    previewTime = next;
                }
            }

            if(!string.IsNullOrEmpty(error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }

            if(preview != null && clone)
            {
                Rect r = GUILayoutUtility.GetRect(100, 240, GUILayout.ExpandWidth(true));
                if(Event.current.type == EventType.Repaint)
                {
                    SetupCamera();
                    preview.BeginPreview(r, GUIStyle.none);
                    preview.Render(true);
                    GUI.DrawTexture(r, preview.EndPreview(), ScaleMode.StretchToFill, false);
                }
            }

            debugTarget = (UAnimGraphPlayer)EditorGUILayout.ObjectField("Live Debug Target", debugTarget, typeof(UAnimGraphPlayer), true);
            var live = debugTarget ? debugTarget.Instance : instance;
            if(live == null)
            {
                return;
            }

            if(!string.IsNullOrEmpty(live.LastParameterError))
            {
                EditorGUILayout.HelpBox(live.LastParameterError, MessageType.Warning);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Root / " + live.Asset.name, EditorStyles.boldLabel);
            foreach(var layer in live.Asset.layers)
            {
                if(layer.graph && GUILayout.Button("Open Layer / " + layer.name))
                {
                    AssetDatabase.OpenAsset(layer.graph);
                }
            }

            if(live.DefinitionVersion != UAnimBuildValidation.Hash(live.Asset))
            {
                EditorGUILayout.HelpBox("Definition changed. Restart the player or preview.", MessageType.Warning);
            }

            foreach(var p in live.Asset.parameters)
            {
                double value = live.Get(p.id), next = value;
                switch(p.type)
                {
                    case UAnimParameterType.Vector2:
                        live.SetVector2(p.id, EditorGUILayout.Vector2Field(p.name, live.GetVector2(p.id)));
                        break;
                    case UAnimParameterType.Float:
                        next = EditorGUILayout.FloatField(p.name, (float)value);
                        break;
                    case UAnimParameterType.Int:
                        next = EditorGUILayout.IntField(p.name, (int)value);
                        break;
                    case UAnimParameterType.Bool:
                        next = EditorGUILayout.Toggle(p.name, value != 0) ? 1 : 0;
                        break;
                    case UAnimParameterType.Trigger:
                        if(GUILayout.Button("Trigger / " + p.name))
                        {
                            next = 1;
                        }

                        break;
                }

                if(next != value)
                {
                    live.Set(p.id, p.type, next);
                }
            }

            foreach(var m in live.Machines)
            {
                EditorGUILayout.LabelField(m.Definition.name + " / " + m.Definition.states[m.CurrentState].name);
                if(GUILayout.Button("Open State Machine"))
                {
                    AssetDatabase.OpenAsset(m.Definition);
                }

                if(GUILayout.Button("Open Active State Graph"))
                {
                    AssetDatabase.OpenAsset(m.Definition.states[m.CurrentState].graph);
                }

                if(!string.IsNullOrEmpty(m.LastEvaluationError))
                {
                    EditorGUILayout.HelpBox(m.LastEvaluationError, MessageType.Error);
                }

                EditorGUILayout.LabelField("Last evaluation", m.LastEvaluationResult.ToString());
                EditorGUILayout.LabelField("Time / Normalized", m.Elapsed.ToString("F2") + " / " + m.NormalizedTime.ToString("F2"));
                if(m.DestinationState >= 0)
                {
                    EditorGUILayout.LabelField("Transition → " + m.Definition.states[m.DestinationState].name, m.TransitionProgress.ToString("P0"));
                }
            }

            foreach(var p in live.Poses)
            {
                if(p.Weights != null && p.Weights.Length > 0)
                {
                    EditorGUILayout.LabelField("Blend / Node " + p.Id);
                    for(int i = 0; i < p.Weights.Length; i++)
                    {
                        EditorGUILayout.LabelField("Input " + i, p.Weights[i].ToString("P1"));
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
