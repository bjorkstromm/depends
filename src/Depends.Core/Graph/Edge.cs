using System;
using System.Globalization;

namespace Depends.Core.Graph
{
    public sealed class Edge : IEquatable<Edge>
    {
        public Node Start { get; }
        public Node End { get; }
        public string Label { get; }

        public Edge(Node start, Node end) : this(start, end, string.Empty)
        {
        }

        public Edge(Node start, Node end, string label)
        {
            Start = start ?? throw new ArgumentNullException(nameof(start));
            End = end ?? throw new ArgumentNullException(nameof(end));
            Label = label;
        }

        public bool Equals(Edge other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(Start, other.Start) && Equals(End, other.End) && string.Equals(Label, other.Label);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Edge edge && Equals(edge);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Start != null ? Start.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (End != null ? End.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Label != null ? Label.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} -{2}-> {1}", Start, End,
                string.IsNullOrEmpty(Label) ? string.Empty : $"[{Label}]");
        }
    }
}
