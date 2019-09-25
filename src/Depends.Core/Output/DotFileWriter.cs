using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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

            var ranks = new Dictionary<Node, int>();
            writer.WriteLine($"digraph \"{GraphName}\" {{");
            writer.WriteLine("rankdir=LR;");
            Write(graph.Root, graph.Edges, writer, ranks);

            //var nodesByRank = ranks.GroupBy(x => x.Value).ToDictionary(g => g.Key, g => g.Select(pp => pp.Key).ToList());

            //foreach (var nodes in nodesByRank.OrderBy(x => x.Key))
            //{
            //    if (nodes.Value.Count < 2)
            //    {
            //        continue;
            //    }

            //    writer.WriteLine("{");
            //    writer.WriteLine("rank=same;");
            //    writer.WriteLine(string.Join("->", nodes.Value.Select(x => $"\"{x.Id}\"")) + "[style=invis];");
            //    writer.WriteLine("rankdir=TB;");
            //    writer.WriteLine("}");
            //}

            foreach (var node in graph.Nodes.OrderBy(x => x.Type))
            {
                if (node.Type == "Project")
                {
                    writer.WriteLine($"\"{node.Id}\" [label=\"{node}\" style=filled fillcolor=white];");
                }
                else if (node.Type == "Package")
                {
                    writer.WriteLine($"\"{node.Id}\" [label=\"{node}\" style=filled fillcolor=blue shape=box];");
                }
                else if (node.Type == "Assembly")
                {
                    writer.WriteLine($"\"{node.Id}\" [label=\"{node}\" style=filled fillcolor=grey];");
                }
                else if (node.Type == "Solution")
                {
                    writer.WriteLine($"\"{node.Id}\" [label=\"{node}\" style=filled fillcolor=red];");
                }
            }

            writer.WriteLine("}");
        }

        private static void Write(Node root, ImmutableHashSet<Edge> edges, TextWriter writer, IDictionary<Node, int> ranks, ISet<Edge> visited = null, int depth = 0)
        {
            if (visited == null)
            {
                visited = new HashSet<Edge>();
            }

            if (ranks.TryGetValue(root, out var currentRank))
            {
                ranks[root] = Math.Min(depth, currentRank);
            }
            else
            {
                ranks[root] = depth;
            }

            foreach (var edge in edges.Where(x => x.Start.Equals(root)).OrderBy(x => x.End.Type))
            {
                if (!visited.Add(edge))
                {
                    continue;
                }

                writer.WriteLine(edge.End is PackageReferenceNode
                    ? $"\"{edge.Start.Id}\" -> \"{edge.End.Id}\" [label=\"{edge.Label}\" color=\"blue\"];"
                    : $"\"{edge.Start.Id}\" -> \"{edge.End.Id}\"");

                Write(edge.End, edges.Remove(edge), writer, ranks, visited, depth + 1);
            }
        }
    }
}
