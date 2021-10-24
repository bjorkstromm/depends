using System;
using System.Collections.Generic;
using System.Linq;
using Buildalyzer;
using NuGet.Frameworks;

namespace Depends.Core.Extensions
{
    internal static class AnalyzerResultExtensions
    {
        public static NuGetFramework GetTargetFramework(this IAnalyzerResult result) =>
            NuGetFramework.Parse(result.TargetFramework, DefaultFrameworkNameProvider.Instance);

        public static bool IsNetSdkProject(this IAnalyzerResult result) =>
            string.Equals(bool.TrueString, result.GetProperty("UsingMicrosoftNETSdk"),
                StringComparison.InvariantCultureIgnoreCase);

        public static string GetProjectAssetsFilePath(this IAnalyzerResult result) =>
            result.GetProperty("ProjectAssetsFile");

        public static string GetRuntimeIdentifier(this IAnalyzerResult result) =>
            result.GetProperty("RuntimeIdentifier");

        public static IEnumerable<ProjectItem> GetItems(this IAnalyzerResult result, string name) =>
            result.Items.TryGetValue(name, out var items) ? (IEnumerable<ProjectItem>)items : Enumerable.Empty<ProjectItem>();
    }
}
