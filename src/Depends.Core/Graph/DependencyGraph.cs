using System.Collections.Generic;
using System.Collections.Immutable;

namespace Depends.Core.Graph
{
    public sealed partial class DependencyGraph
    {
        public Node Root { get; }

        public ImmutableHashSet<Node> Nodes { get; }

        public ImmutableHashSet<Edge> Edges { get; }

        private DependencyGraph(Node root, IEnumerable<Node> nodes, IEnumerable<Edge> edges)
        {
            Root = root;
            Nodes = nodes.ToImmutableHashSet();
            Edges = edges.ToImmutableHashSet();
        }
    }
}
