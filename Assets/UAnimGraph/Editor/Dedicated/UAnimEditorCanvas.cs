using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ObjectField = UnityEditor.UIElements.ObjectField;

namespace UAnimGraph.Authoring
{
    public sealed class UAnimEditorCanvas : VisualElement
    {
        readonly UAnimGraphEditor owner;
        readonly VisualElement layer;
        readonly IMGUIContainer wires;
        readonly Dictionary<string, VisualElement> cards = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, VisualElement> ports = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, IPort> portModels = new Dictionary<string, IPort>();
        readonly Dictionary<string, Vector2> positions = new Dictionary<string, Vector2>();
        readonly List<Edge> edges = new List<Edge>();
        Vector2 pan = new Vector2(60, 70), pointer, dragStart, originalPosition;
        float zoom = 1;
        string moving, transitionFrom;
        IPort pendingPort;
        bool panning;
        int panPointerId = -1;
        Vector2 panStartPointer, panStartPosition;
        bool viewportTouched;
        bool initialFramePending = true;
        public string Selected
        {
            get;
            private set;
        }
        public bool TransitionSelected
        {
            get;
            private set;
        }

        sealed class Edge
        {
            public string id;
            public IPort output, input;
            public Vector2 from, to, a, b;
        }

        public UAnimEditorCanvas(UAnimGraphEditor owner)
        {
            this.owner = owner;
            name = "uanim-dedicated-canvas";
            focusable = true;
            style.flexGrow = 1;
            style.overflow = Overflow.Hidden;
            style.backgroundColor = new Color(.105f, .115f, .13f);
            wires = new IMGUIContainer(DrawWires)
            {
                pickingMode = PickingMode.Ignore
            };
            wires.StretchToParentSize();
            Add(wires);
            layer = new VisualElement
            {
                name = "uanim-dedicated-nodes",
                pickingMode = PickingMode.Ignore
            };
            layer.style.position = Position.Absolute;
            layer.style.width = Length.Percent(100);
            layer.style.height = Length.Percent(100);
            layer.style.transformOrigin = new TransformOrigin(0, 0, 0);
            Add(layer);
            RegisterCallback<WheelEvent>(e =>
            {
                Vector2 at = e.localMousePosition;
                Vector2 before = ToGraph(at);
                zoom = Mathf.Clamp(zoom * Mathf.Exp(-e.delta.y * .05f), .25f, 1.75f);
                pan = at - before * zoom;
                viewportTouched = true;
                ApplyTransform();
                e.StopPropagation();
            });
            // Capture Alt gestures before fields, ports and IMGUI children can
            // consume them. Pointer capture keeps the drag alive outside nodes.
            RegisterCallback<PointerDownEvent>(OnPanDown, TrickleDown.TrickleDown);
            RegisterCallback<PointerMoveEvent>(OnPanMove, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(OnPanUp, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(OnPanUp);
            RegisterCallback<PointerCancelEvent>(e =>
            {
                if(ReferenceEquals(e.target, this))
                {
                    EndPan();
                }
            });
            RegisterCallback<MouseDownEvent>(e =>
            {
                if(!panning && (e.button == 2 || (e.button == 0 && e.altKey)))
                {
                    BeginPan(PointerId.mousePointerId, this.WorldToLocal(e.mousePosition));
                }
                if(panning)
                {
                    e.StopImmediatePropagation();
                }
            }, TrickleDown.TrickleDown);
            RegisterCallback<MouseMoveEvent>(e =>
            {
                // The click that activates an EditorWindow can arrive without
                // PointerDown. Recover from the first held-button drag as well.
                if(!panning && e.altKey && (e.pressedButtons & 1) != 0)
                {
                    Vector2 at = this.WorldToLocal(e.mousePosition);
                    BeginPan(PointerId.mousePointerId, at - e.mouseDelta);
                }
                if(panning)
                {
                    pan = panStartPosition + (Vector2)this.WorldToLocal(e.mousePosition) - panStartPointer;
                    ApplyTransform();
                    e.StopImmediatePropagation();
                }
            }, TrickleDown.TrickleDown);
            RegisterCallback<PointerCaptureOutEvent>(e =>
            {
                // Capture-out bubbles from descendants. A text field losing
                // capture to the canvas is the start of our drag, not its end.
                if(ReferenceEquals(e.target, this) && e.pointerId == panPointerId)
                {
                    panning = false;
                    panPointerId = -1;
                }
            });
            RegisterCallback<MouseDownEvent>(OnDown);
            RegisterCallback<MouseMoveEvent>(OnMove);
            RegisterCallback<MouseUpEvent>(OnUp);
            RegisterCallback<KeyDownEvent>(OnKey);
            RegisterCallback<GeometryChangedEvent>(_ =>
            {
                FrameOnOpen();
                wires.MarkDirtyRepaint();
            });
            schedule.Execute(UpdateActivity).Every(80);
        }

        public Vector2 ToGraph(Vector2 point)
        {
            return (point - pan) / zoom;
        }

        public void Select(string id, bool transition = false)
        {
            Selected = id;
            TransitionSelected = transition;
            UpdateActivity();
            wires.MarkDirtyRepaint();
            owner.RefreshInspector();
        }

        public void Rebuild()
        {
            layer.Clear();
            cards.Clear();
            ports.Clear();
            portModels.Clear();
            positions.Clear();
            pendingPort = null;
            moving = null;
            var doc = owner.Document;
            if(doc == null)
            {
                return;
            }

            if(doc.IsMachine)
            {
                foreach(var state in doc.States)
                {
                    BuildState(state);
                }
            }
            else
            {
                foreach(var node in doc.Nodes)
                {
                    BuildNode(node);
                }
            }

            ApplyTransform();
            UpdateActivity();
        }

        VisualElement Card(string id, string title, Vector2 position, bool state)
        {
            if(!Finite(position))
            {
                position = new Vector2((cards.Count % 3) * 360, (cards.Count / 3) * 220);
            }
            var card = new VisualElement
            {
                name = "uanim-card-" + id
            };
            card.style.position = Position.Absolute;
            card.style.left = position.x;
            card.style.top = position.y;
            card.style.width = state ? 260 : 285;
            card.style.backgroundColor = new Color(.205f, .22f, .245f);
            card.style.borderTopWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = card.style.borderRightWidth = 2;
            card.style.borderTopLeftRadius = card.style.borderTopRightRadius = card.style.borderBottomLeftRadius = card.style.borderBottomRightRadius = 9;
            card.style.paddingBottom = 8;
            var header = new Label(title)
            {
                name = "uanim-card-title"
            };
            header.style.height = 36;
            header.style.fontSize = 15;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.style.backgroundColor = new Color(.22f, .36f, .43f);
            card.Add(header);
            card.RegisterCallback<MouseDownEvent>(e =>
            {
                if(e.button != 0 || e.altKey || panning || IsField(e.target as VisualElement, card))
                {
                    return;
                }

                Focus();
                if(!string.IsNullOrEmpty(transitionFrom))
                {
                    string from = transitionFrom;
                    transitionFrom = null;
                    owner.Run(() => owner.Document.ConnectStates(owner.Document.States.First(s => s.ID.ToString() == from), owner.Document.States.First(s => s.ID.ToString() == id)));
                    e.StopPropagation();
                    return;
                }

                Select(id);
                if(e.clickCount == 2)
                {
                    owner.OpenChild(id);
                    e.StopPropagation();
                    return;
                }

                moving = id;
                dragStart = this.WorldToLocal(e.mousePosition);
                originalPosition = positions[id];
                this.CaptureMouse();
                e.StopPropagation();
            });
            card.RegisterCallback<ContextClickEvent>(e =>
            {
                Select(id);
                var menu = new GenericMenu();
                if(state)
                {
                    menu.AddItem(new GUIContent("Create Transition"), false, () => transitionFrom = id);
                }

                menu.AddItem(new GUIContent("Open Contents"), false, () => owner.OpenChild(id));
                menu.AddItem(new GUIContent("Delete"), false, () => owner.Run(() => owner.Document.Delete(id)));
                menu.ShowAsContext();
                e.StopPropagation();
            });
            cards[id] = card;
            positions[id] = position;
            layer.Add(card);
            return card;
        }

        static bool IsField(VisualElement target, VisualElement card)
        {
            for(var current = target; current != null && current != card; current = current.parent)
            {
                if(current is Button || current is TextField || current is FloatField || current is Vector2Field || current is ObjectField || current.name == "uanim-port")
                {
                    return true;
                }
            }

            return false;
        }

        void BuildState(IState state)
        {
            string id = state.ID.ToString();
            var card = Card(id, state is UAnimEntryState ? "Entry" : state.Title, state.Position, true);
            if(state is not UAnimEntryState)
            {
                var name = new TextField("Name")
                {
                    value = state.Title,
                    isDelayed = true
                };
                name.style.marginLeft = name.style.marginRight = 8;
                name.RegisterValueChangedCallback(e => owner.Run(() => owner.Document.Edit("Rename State", () =>
                {
                    state.Title = e.newValue;
                    state.GetOptionByName("Name")?.TrySetValue(e.newValue);
                })));
                card.Add(name);
                card.Add(new Button(() => owner.OpenChild(id)) { text = "Open State Pose Graph" });
            }

            var transition = new Button(() =>
            {
                transitionFrom = id;
                Select(id);
            })
            {
                text = "Transition →"
            };
            card.Add(transition);
        }

        void BuildNode(INode node)
        {
            string id = node.ID.ToString();
            string title = string.IsNullOrEmpty(node.Title) ? UAnimEditorDocument.Friendly(node.GetType()) : node.Title;
            var card = Card(id, title, node.Position, false);
            if(node is RootLayerNode layerNode)
            {
                var name = new TextField("Name")
                {
                    value = layerNode.Option<string>("Name"),
                    isDelayed = true
                };
                name.RegisterValueChangedCallback(e => owner.Run(() => owner.Document.Edit("Rename Layer", () =>
                {
                    layerNode.GetNodeOptionByName("Name").TrySetValue(e.newValue);
                    layerNode.Title = e.newValue;
                })));
                card.Add(name);
                card.Add(new Button(() => owner.OpenChild(id)) { text = "Open StateMachine" });
            }

            if(node is ClipNode clip)
            {
                var field = new ObjectField
                {
                    objectType = typeof(AnimationClip),
                    allowSceneObjects = false,
                    value = clip.Option<AnimationClip>("Clip")
                };
                field.RegisterValueChangedCallback(e => owner.Run(() => owner.Document.SetOption(clip.GetNodeOptionByName("Clip"), typeof(AnimationClip), e.newValue, clip)));
                card.Add(field);
            }

            var inputs = node.GetInputPorts().ToArray();
            var outputs = node.GetOutputPorts().ToArray();
            for(int i = 0; i < Math.Max(inputs.Length, outputs.Length); i++)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.height = 29;
                row.style.alignItems = Align.Center;
                if(i < inputs.Length)
                {
                    var input = inputs[i];
                    row.Add(Port(input));
                    row.Add(new Label(input.DisplayName ?? input.Name));
                    if(node is PoseNode pose && node is IMixerAuthoring)
                    {
                        var entries = MixerAuthoring.Inputs(pose);
                        int index = entries.FindIndex(entry => entry.port == input.Name);
                        if(index >= 0)
                        {
                            var entry = entries[index];
                            if(node is Blend2DNode)
                            {
                                var field = new Vector2Field
                                {
                                    value = entry.position
                                };
                                field.style.width = 135;
                                field.RegisterValueChangedCallback(e => owner.Run(() => UAnimMixerInputEditing.SetPosition((Blend2DNode)pose, entry.port, e.newValue)));
                                row.Add(field);
                            }
                            else
                            {
                                var field = new FloatField
                                {
                                    value = entry.threshold,
                                    isDelayed = true
                                };
                                field.style.width = 60;
                                field.RegisterValueChangedCallback(e => owner.Run(() => UAnimMixerInputEditing.SetThreshold((Blend1DNode)pose, entry.port, e.newValue)));
                                row.Add(field);
                            }

                            var remove = new Button(() => owner.Run(() => UAnimMixerInputEditing.Remove(pose, entry.port)))
                            {
                                text = "×"
                            };
                            remove.SetEnabled(index >= 2);
                            row.Add(remove);
                        }
                    }
                }

                var spacer = new VisualElement();
                spacer.style.flexGrow = 1;
                row.Add(spacer);
                if(i < outputs.Length)
                {
                    row.Add(new Label(outputs[i].DisplayName ?? outputs[i].Name));
                    row.Add(Port(outputs[i]));
                }

                card.Add(row);
            }

            if(node is PoseNode mixer && node is IMixerAuthoring)
            {
                card.style.width = node is Blend2DNode ? 410 : 320;
                card.Add(new Button(() => owner.Run(() => UAnimMixerInputEditing.Add(mixer))) { text = "Add Input Pose" });
            }
        }

        VisualElement Port(IPort port)
        {
            var dot = new VisualElement
            {
                name = "uanim-port",
                tooltip = port.Name + " : " + port.DataType.Name
            };
            dot.style.width = dot.style.height = 14;
            dot.style.marginLeft = dot.style.marginRight = 5;
            dot.style.flexShrink = 0;
            dot.style.borderTopLeftRadius = dot.style.borderTopRightRadius = dot.style.borderBottomLeftRadius = dot.style.borderBottomRightRadius = 7;
            dot.style.backgroundColor = port.DataType == typeof(UAnimPose) ? new Color(.25f, .85f, .8f) : new Color(.65f, .8f, 1);
            ports[port.ID.ToString()] = dot;
            portModels[port.ID.ToString()] = port;
            dot.RegisterCallback<MouseDownEvent>(e =>
            {
                if(e.button != 0 || e.altKey || panning)
                {
                    return;
                }

                if(pendingPort != null && pendingPort.Direction != port.Direction)
                {
                    ConnectPort(port);
                }
                else
                {
                    pendingPort = port;
                    pointer = this.WorldToLocal(e.mousePosition);
                }

                e.StopPropagation();
            });
            dot.RegisterCallback<MouseUpEvent>(e =>
            {
                if(pendingPort != null && pendingPort.ID != port.ID && pendingPort.Direction != port.Direction)
                {
                    ConnectPort(port);
                }

                e.StopPropagation();
            });
            dot.RegisterCallback<ContextClickEvent>(e =>
            {
                var connected = new List<IPort>();
                port.GetConnectedPorts(connected);
                var menu = new GenericMenu();
                foreach(var other in connected)
                {
                    var captured = other;
                    menu.AddItem(new GUIContent("Disconnect " + other.Name), false, () => owner.Run(() => owner.Document.Edit("Disconnect port", () => owner.Document.Graph.Disconnect(port, captured))));
                }

                menu.ShowAsContext();
                e.StopPropagation();
            });
            return dot;
        }

        void ConnectPort(IPort port)
        {
            var first = pendingPort;
            pendingPort = null;
            owner.Run(() => owner.Document.Connect(first.Direction == PortDirection.Output ? first : port, first.Direction == PortDirection.Input ? first : port));
        }

        Vector2 PortPoint(IPort port)
        {
            return ports.TryGetValue(port.ID.ToString(), out var element) ? this.WorldToLocal(element.worldBound.center) : Vector2.zero;
        }

        void ApplyTransform()
        {
            // A pre-layout size is NaN. Never allow it to poison every later
            // drag/zoom calculation, including editors restored after reload.
            if(!UAnimValidation.Finite(zoom) || zoom <= 0)
            {
                zoom = 1;
            }
            if(!Finite(pan))
            {
                pan = new Vector2(60, 70);
            }
            layer.style.left = pan.x;
            layer.style.top = pan.y;
            layer.style.scale = new Scale(new Vector3(zoom, zoom, 1));
            wires.MarkDirtyRepaint();
        }

        void DrawWires()
        {
            if(Event.current.type != EventType.Repaint)
            {
                return;
            }

            Color previousColor = Handles.color;
            Handles.BeginGUI();
            Handles.color = new Color(1, 1, 1, .045f);
            float spacing = 32 * zoom;
            for(float x = pan.x % spacing; x < resolvedStyle.width; x += spacing)
            {
                Handles.DrawLine(new Vector3(x, 0), new Vector3(x, resolvedStyle.height));
            }

            for(float y = pan.y % spacing; y < resolvedStyle.height; y += spacing)
            {
                Handles.DrawLine(new Vector3(0, y), new Vector3(resolvedStyle.width, y));
            }

            // Grid opacity must not tint the graph connections.
            Handles.color = Color.white;
            edges.Clear();
            var doc = owner.Document;
            if(doc != null && doc.IsMachine)
            {
                foreach(var transition in doc.Transitions)
                {
                    if(!cards.TryGetValue(transition.FromState.ID.ToString(), out var from) || !cards.TryGetValue(transition.ToState.ID.ToString(), out var to))
                    {
                        continue;
                    }

                    Vector2 a = this.WorldToLocal(from.worldBound.center), b = this.WorldToLocal(to.worldBound.center);
                    Vector2 direction = (b - a).normalized;
                    if(transition.FromState.ID == transition.ToState.ID)
                    {
                        a += new Vector2(30, -from.worldBound.height * .5f);
                        b += new Vector2(-30, -to.worldBound.height * .5f);
                    }
                    else
                    {
                        Vector2 normal = new Vector2(-direction.y, direction.x) * 14;
                        a += direction * Mathf.Min(from.worldBound.width, from.worldBound.height) * .45f + normal;
                        b -= direction * Mathf.Min(to.worldBound.width, to.worldBound.height) * .45f - normal;
                    }

                    var edge = new Edge
                    {
                        id = transition.ID.ToString(),
                        from = a,
                        to = b
                    };
                    bool self = transition.FromState.ID == transition.ToState.ID;
                    edge.a = self ? a + Vector2.up * 90 : Vector2.Lerp(a, b, .33f);
                    edge.b = self ? b + Vector2.up * 90 : Vector2.Lerp(a, b, .66f);
                    edges.Add(edge);
                    Color color = TransitionSelected && Selected == edge.id ? new Color(.25f, .8f, 1) : new Color(.85f, .92f, 1f, 1f);
                    if(owner.LiveInstance?.Machines.Any(m => m.ActiveTransitionId == edge.id && m.ActivityWeight > 0) == true)
                    {
                        color = new Color(.3f, 1, .5f);
                    }

                    Handles.color = Color.white;
                    Handles.DrawBezier(a, b, edge.a, edge.b, color, null, 4);
                    Vector2 mid = .125f * a + .375f * edge.a + .375f * edge.b + .125f * b;
                    Vector2 tangent = self ? Vector2.left : (b - a).normalized;
                    Handles.color = color;
                    Handles.DrawAAConvexPolygon(mid + tangent * 10, mid - tangent * 8 + new Vector2(-tangent.y, tangent.x) * 7, mid - tangent * 8 - new Vector2(-tangent.y, tangent.x) * 7);
                }
            }
            else if(doc != null)
            {
                foreach(var node in doc.Nodes)
                {
                    foreach(var output in node.GetOutputPorts())
                    {
                        var connected = new List<IPort>();
                        output.GetConnectedPorts(connected);
                        foreach(var input in connected)
                        {
                            Vector2 from = PortPoint(output), to = PortPoint(input);
                            var edge = new Edge
                            {
                                output = output,
                                input = input,
                                from = from,
                                to = to,
                                a = from + Vector2.right * 65,
                                b = to + Vector2.left * 65
                            };
                            edges.Add(edge);
                            Handles.DrawBezier(from, to, edge.a, edge.b, new Color(.1f, 1f, .85f, 1f), null, 4);
                        }
                    }
                }
            }

            if(pendingPort != null)
            {
                Handles.color = Color.white;
                Vector2 point = PortPoint(pendingPort);
                Handles.DrawBezier(point, pointer, point + Vector2.right * 50, pointer - Vector2.right * 50, Color.yellow, null, 4);
            }

            Handles.color = previousColor;
            Handles.EndGUI();
        }

        void OnPanDown(PointerDownEvent e)
        {
            if(e.button != 2 && !(e.button == 0 && e.altKey))
            {
                return;
            }
            BeginPan(e.pointerId, this.WorldToLocal(e.position));
            e.StopImmediatePropagation();
        }

        void BeginPan(int pointerId, Vector2 position)
        {
            if(panning || !Finite(position))
            {
                return;
            }
            Focus();
            moving = null;
            pendingPort = null;
            transitionFrom = null;
            panning = true;
            viewportTouched = true;
            initialFramePending = false;
            panPointerId = pointerId;
            panStartPointer = position;
            panStartPosition = pan;
            this.CapturePointer(pointerId);
        }

        void OnPanMove(PointerMoveEvent e)
        {
            if(!panning || e.pointerId != panPointerId)
            {
                return;
            }
            pan = panStartPosition + (Vector2)this.WorldToLocal(e.position) - panStartPointer;
            ApplyTransform();
            e.StopImmediatePropagation();
        }

        void OnPanUp(PointerUpEvent e)
        {
            if(!panning || e.pointerId != panPointerId)
            {
                return;
            }
            EndPan();
            e.StopImmediatePropagation();
        }

        void EndPan()
        {
            int pointerId = panPointerId;
            panning = false;
            panPointerId = -1;
            if(pointerId >= 0 && this.HasPointerCapture(pointerId))
            {
                this.ReleasePointer(pointerId);
            }
        }

        void OnDown(MouseDownEvent e)
        {
            Focus();
            pointer = e.localMousePosition;
            if(panning)
            {
                e.StopPropagation();
                return;
            }

            if(e.target != this && e.target != layer)
            {
                return;
            }

            var edge = edges.LastOrDefault(t => HandleUtility.DistancePointBezier(pointer, t.from, t.to, t.a, t.b) < 10);
            if(e.button == 1)
            {
                if(edge != null && edge.output != null)
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Disconnect"), false, () => owner.Run(() => owner.Document.Edit("Disconnect wire", () => owner.Document.Graph.Disconnect(edge.output, edge.input))));
                    menu.ShowAsContext();
                }
                else
                {
                    owner.ShowCreateMenu(ToGraph(pointer));
                }
            }
            else if(edge?.id != null)
            {
                Select(edge.id, true);
            }
            else
            {
                pendingPort = null;
                transitionFrom = null;
                Select(null);
            }
        }

        void OnMove(MouseMoveEvent e)
        {
            pointer = e.localMousePosition;
            if(panning)
            {
                return;
            }
            else if(moving != null && cards.TryGetValue(moving, out var card))
            {
                Vector2 position = originalPosition + (pointer - dragStart) / zoom;
                card.style.left = position.x;
                card.style.top = position.y;
                positions[moving] = position;
            }

            wires.MarkDirtyRepaint();
        }

        void OnUp(MouseUpEvent e)
        {
            if(panning)
            {
                EndPan();
                e.StopPropagation();
                return;
            }
            if(moving != null)
            {
                string id = moving;
                Vector2 position = positions[id];
                moving = null;
                if((position - originalPosition).sqrMagnitude > .01f)
                {
                    owner.Run(() => owner.Document.Move(id, position));
                }
            }

            if(this.HasMouseCapture())
            {
                this.ReleaseMouse();
            }
        }

        void OnKey(KeyDownEvent e)
        {
            if(IsField(e.target as VisualElement, this))
            {
                return;
            }

            if(e.keyCode == KeyCode.Escape)
            {
                EndPan();
                pendingPort = null;
                transitionFrom = null;
            }

            if((e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace) && !string.IsNullOrEmpty(Selected))
            {
                owner.Run(() => owner.Document.Delete(Selected, TransitionSelected));
                Select(null);
                e.StopPropagation();
            }

            if(e.keyCode == KeyCode.F)
            {
                FrameAll();
            }
        }

        public void BeginNavigation()
        {
            EndPan();
            viewportTouched = false;
            initialFramePending = true;
        }

        public void FrameOnOpen()
        {
            if(initialFramePending && !viewportTouched && !panning && TryFrameAll())
            {
                initialFramePending = false;
            }
        }

        public void FrameAll()
        {
            if(TryFrameAll())
            {
                viewportTouched = true;
                initialFramePending = false;
            }
        }

        static bool Finite(Vector2 value)
        {
            return UAnimValidation.Finite(value.x) && UAnimValidation.Finite(value.y);
        }

        bool TryFrameAll()
        {
            float width = layout.width;
            float height = layout.height;
            if(cards.Count == 0 || !UAnimValidation.Finite(width) || !UAnimValidation.Finite(height) || width <= 70 || height <= 70)
            {
                return false;
            }

            Rect bounds = new Rect(positions.First().Value, new Vector2(300, 160));
            foreach(var position in positions.Values)
            {
                bounds.xMin = Mathf.Min(bounds.xMin, position.x);
                bounds.yMin = Mathf.Min(bounds.yMin, position.y);
                bounds.xMax = Mathf.Max(bounds.xMax, position.x + 420);
                bounds.yMax = Mathf.Max(bounds.yMax, position.y + 240);
            }

            float fittedZoom = Mathf.Clamp(Mathf.Min((width - 70) / bounds.width, (height - 70) / bounds.height), .25f, 1);
            Vector2 fittedPan = new Vector2(width, height) * .5f - bounds.center * fittedZoom;
            if(!UAnimValidation.Finite(fittedZoom) || !Finite(fittedPan))
            {
                return false;
            }
            zoom = fittedZoom;
            pan = fittedPan;
            ApplyTransform();
            return true;
        }

        void UpdateActivity()
        {
            FrameOnOpen();
            var live = owner.LiveInstance;
            foreach(var pair in cards)
            {
                bool active = live != null && (live.Poses.Any(p => p.Id == pair.Key && p.Activity > .001f) || live.Machines.Any(m => (m.ActivityWeight > 0 && m.Definition.states[m.CurrentState].id == pair.Key) || (m.ActivityWeight > 0 && m.DestinationState >= 0 && m.Definition.states[m.DestinationState].id == pair.Key)));
                Color border = active ? new Color(.3f, 1, .5f) : pair.Key == Selected ? new Color(.25f, .8f, 1) : new Color(.35f, .39f, .44f);
                pair.Value.style.borderLeftColor = pair.Value.style.borderRightColor = pair.Value.style.borderTopColor = pair.Value.style.borderBottomColor = border;
            }

            wires.MarkDirtyRepaint();
        }
    }
}
