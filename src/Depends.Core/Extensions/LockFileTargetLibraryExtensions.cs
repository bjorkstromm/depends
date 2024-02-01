using System;
using Depends.Core.Graph;
using NuGet.ProjectModel;

namespace Depends.Core.Extensions
{
    internal static class LockFileTargetLibraryExtensions
    {
        public static bool IsPackage(this LockFileTargetLibrary library)
        {
            return library.Type != null && library.Type.Equals("package", StringComparison.OrdinalIgnoreCase);
        }

        public static PackageReferenceNode ToNode(this LockFileTargetLibrary library)
        {
            if (!library.IsPackage())
            {
                throw new ArgumentException("Empty parameter", nameof(library));
            }

            return new PackageReferenceNode(library.Name, library.Version?.ToNormalizedString());
        }
    }
}
