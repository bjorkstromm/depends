using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Buildalyzer;
using Depends.Core.Extensions;
using Depends.Core.Graph;
using Microsoft.Extensions.Logging;
using NuGet.ProjectModel;

namespace Depends.Core
{
    public class DependencyAnalyzer
    {
        private ILogger _logger;

        public DependencyAnalyzer(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger(typeof(DependencyAnalyzer)) ??
                      throw new ArgumentNullException(nameof(loggerFactory));
        }

        public DependencyGraph Analyze(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                throw new ArgumentException(nameof(projectPath));
            }

            if (!File.Exists(projectPath))
            {
                throw new ArgumentException("Project path does not exist.", nameof(projectPath));
            }

            var analyzerManager = new AnalyzerManager();
            var projectAnalyzer = analyzerManager.GetProject(projectPath);

            var projectInstance = projectAnalyzer.Compile();

            if (projectInstance == null)
            {
                // Todo: Something went wrong, log and return better exception.
                throw new InvalidOperationException("Unable to load project.");
            }

            if (!projectInstance.IsNetSdkProject())
            {
                // Todo: Support "legacy" projects in the future.
                throw new InvalidOperationException("Unable to load project.");
            }

            var projectAssetsFilePath = projectInstance.GetProjectAssetsFilePath();

            if (!File.Exists(projectAssetsFilePath))
            {
                // Todo: Make sure this exists in future
                throw new InvalidOperationException($"{projectAssetsFilePath} not found. Please run 'dotnet restore'");
            }

            var lockFile = new LockFileFormat().Read(projectAssetsFilePath);

            // Todo, support target selecting target framework.
            var targetFramework = projectInstance.GetTargetFramework();

            var libraries = lockFile.Targets.Single(x => x.TargetFramework == targetFramework)
                .Libraries.Where(x => x.IsPackage()).ToList();

            var projectNode = new ProjectReferenceNode(projectPath);
            var builder = new DependencyGraph.Builder(projectNode);

            var libraryNodes = new Dictionary<string, PackageReferenceNode>(StringComparer.OrdinalIgnoreCase);
            foreach (var library in libraries)
            {
                var libraryNode = library.ToNode();
                builder.WithNode(libraryNode);
                libraryNodes.Add(libraryNode.PackageId, libraryNode);

                if (library.FrameworkAssemblies.Count <= 0)
                {
                    continue;
                }

                var frameworkAssemblyNodes = library.FrameworkAssemblies
                    .Select(x => new AssemblyReferenceNode(x));
                builder.WithNodes(frameworkAssemblyNodes);
                builder.WithEdges(frameworkAssemblyNodes
                    .Select(x => new Edge(libraryNode, x)));
            }

            foreach (var library in libraries)
            {
                var libraryNode = library.ToNode();

                if (library.Dependencies.Count > 0)
                {
                    builder.WithEdges(library.Dependencies
                        .Select(x => new Edge(libraryNode, libraryNodes[x.Id], x.VersionRange.ToString())));
                }
            }

            builder.WithEdges(projectInstance.GetItems("PackageReference")
                .Select(x => new Edge(projectNode, libraryNodes[x.EvaluatedInclude])));

            var references = projectInstance.GetItems("Reference").Where(x => !x.HasMetadata("NuGetPackageId"))
                .Select(x => new AssemblyReferenceNode(x.EvaluatedInclude));

            builder.WithNodes(references);
            builder.WithEdges(references.Select(x => new Edge(projectNode, x)));

            return builder.Build();
        }
    }
}
