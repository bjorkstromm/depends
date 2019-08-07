using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Buildalyzer;
using Microsoft.Build.Execution;
using NuGet.Frameworks;

namespace Depends.Core.Extensions
{
    internal static class AnalyzerResultExtensions
    {
        public static NuGetFramework GetTargetFramework(this AnalyzerResult result) =>
            NuGetFramework.Parse(result.TargetFramework, DefaultFrameworkNameProvider.Instance);

        public static bool IsNetSdkProject(this AnalyzerResult result) =>
            string.Equals(bool.TrueString, result.GetProperty("UsingMicrosoftNETSdk"),
                StringComparison.InvariantCultureIgnoreCase);

        public static string GetProjectAssetsFilePath(this AnalyzerResult result) =>
            result.GetProperty("ProjectAssetsFile");

        public static string GetRuntimeIdentifier(this AnalyzerResult result) =>
            result.GetProperty("RuntimeIdentifier");

        public static IEnumerable<ProjectItem> GetItems(this AnalyzerResult result, string name) =>
            result.Items.TryGetValue(name, out var items) ? items : Enumerable.Empty<ProjectItem>();
    }
}
