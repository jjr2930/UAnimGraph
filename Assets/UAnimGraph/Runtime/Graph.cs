using System;
using System.Collections.Generic;
using UnityEngine;

namespace UAnimGraph.Runtime
{
    [Serializable]
    public class Graph<T>
    {
        [Serializable]
        public class GraphNode
        {
            [SerializeField] T data;
            [SerializeField] List<GraphNode> children;

            public int ChildCount => children.Count;

            public GraphNode()
            {
                data = default(T);
                children = new List<GraphNode>();
            }
            public GraphNode(T data)
            {
                this.data = data;
                this.children = new List<GraphNode>();
            }

            public GraphNode(T data, List<GraphNode> children)
            {
                this.data = data;
                this.children = children;
            }
            public GraphNode GetChild(int index)
            {
                if (index < 0 || index >= children.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");
                }

                return children[index];
            }

            public void AddChild(GraphNode child)
            {
                children.Add(child);
            }

            public void RemoveChild(GraphNode child)
            {
                children.Remove(child);
            }

            public T Data
            {
                get => data;
            }
        }

        [SerializeField] List<GraphNode> nodes;
        [SerializeField] GraphNode rootNode;
    }
}
