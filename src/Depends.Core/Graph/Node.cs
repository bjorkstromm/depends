using System;

namespace Depends.Core.Graph
{
    public abstract class Node : IEquatable<Node>
    {
        public string Id { get; }

        public abstract string Type { get; }

        protected Node(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(nameof(id));
            }

            Id = id;
        }

        public bool Equals(Node other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Node node && Equals(node);
        }

        public override int GetHashCode()
        {
            return (Id != null ? Id.GetHashCode() : 0);
        }

        public override string ToString()
        {
            return Id;
        }
    }
}
