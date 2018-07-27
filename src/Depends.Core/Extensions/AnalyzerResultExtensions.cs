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
        public static NuGetFramework GetTargetFramework(this AnalyzerResult result)
        {
            var targetFramework = result.TargetFramework ?? result.ProjectInstance.GetItems("_TargetFramework").FirstOrDefault().EvaluatedInclude;

            return NuGetFramework.Parse(targetFramework, DefaultFrameworkNameProvider.Instance);
        }
    }
}
