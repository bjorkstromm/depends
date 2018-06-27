using System;
using System.Collections.Generic;
using System.Text;
using Depends.Core.Graph;
using NuGet.ProjectModel;

namespace Depends.Core.Extensions
{
    internal static class LockFileTargetLibraryExtensions
    {
        public static bool IsPackage(this LockFileTargetLibrary library)
        {
            return library.Type.Equals("package", StringComparison.OrdinalIgnoreCase);
        }

        public static PackageReferenceNode ToNode(this LockFileTargetLibrary library)
        {
            if (!library.IsPackage())
            {
                throw new ArgumentException(nameof(library));
            }

            return new PackageReferenceNode(library.Name, library.Version.ToNormalizedString());
        }
    }
}
