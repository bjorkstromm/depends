using System;
using System.Globalization;

namespace Depends.Core.Graph
{
    public sealed class PackageReferenceNode : Node
    {
        public string PackageId { get; }
        public string Version { get; }

        public PackageReferenceNode(string packageId, string version)
            : base(packageId)
        {
            PackageId = packageId ?? throw new ArgumentNullException(nameof(packageId));
            Version = version ?? throw new ArgumentNullException(nameof(version));
        }

        public override string Type { get; } = "Package";

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}", PackageId, Version);
        }
    }
}
