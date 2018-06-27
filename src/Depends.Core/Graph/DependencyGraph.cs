using System.Collections.Generic;
using System.Collections.Immutable;

namespace Depends.Core.Graph
{
    public sealed partial class DependencyGraph
    {
        public ImmutableHashSet<Node> Nodes { get; }

        public ImmutableHashSet<Edge> Edges { get; }

        private DependencyGraph(IEnumerable<Node> nodes, IEnumerable<Edge> edges)
        {
            Nodes = nodes.ToImmutableHashSet();
            Edges = edges.ToImmutableHashSet();
        }
    }
}
