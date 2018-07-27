using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Build.Execution;
using NuGet.Frameworks;

namespace Depends.Core.Extensions
{
    internal static class ProjectInstanceExtensions
    {
        public static bool IsNetSdkProject(this ProjectInstance instance) => 
            string.Equals(bool.TrueString, instance.GetPropertyValue("UsingMicrosoftNETSdk"),
                StringComparison.InvariantCultureIgnoreCase);

        public static string GetProjectAssetsFilePath(this ProjectInstance instance) =>
            instance.GetPropertyValue("ProjectAssetsFile");

        public static NuGetFramework GetTargetFramework(this ProjectInstance instance)
        {
            var targetFramework = instance.GetItems("_TargetFramework").FirstOrDefault().EvaluatedInclude;

            return NuGetFramework.Parse(targetFramework, DefaultFrameworkNameProvider.Instance);
            // var targetFrameworkIdentifier = instance.GetPropertyValue("TargetFrameworkIdentifier");
            // var targetFrameworkVersion = instance.GetPropertyValue("TargetFrameworkVersion");

            // return NuGetFramework.Parse(
            //     $"{targetFrameworkIdentifier},Version={targetFrameworkVersion}",
            //     DefaultFrameworkNameProvider.Instance);
        }
    }
}
