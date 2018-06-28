using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Depends.Core.Graph;

namespace Depends.Core.Output
{
    public sealed class DotFileWriter
    {
        public string GraphName { get; set; } = "depends";

        public void Write(DependencyGraph graph, TextWriter writer)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.WriteLine($"digraph \"{GraphName}\" {{");
            Write(graph.Root, graph.Edges, writer);
            writer.WriteLine("}");
        }

        private static void Write(Node root, ImmutableHashSet<Edge> edges, TextWriter writer, ISet<Edge> visited = null)
        {
            if (visited == null)
            {
                visited = new HashSet<Edge>();
            }

            foreach (var edge in edges.Where(x => x.Start.Equals(root)))
            {
                if (!visited.Add(edge))
                {
                    continue;
                }
                writer.WriteLine($"\"{edge.Start.Id}\" -> \"{edge.End.Id}\" [label=\"{edge.Label}\"]");
                Write(edge.End, edges.Remove(edge), writer, visited);
            }
        }
    }
}
