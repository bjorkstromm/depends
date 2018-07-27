using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Buildalyzer;
using Buildalyzer.Environment;
using Depends.Core.Extensions;
using Depends.Core.Graph;
using Microsoft.Extensions.Logging;
using NuGet.ProjectModel;

namespace Depends.Core
{
    public class DependencyAnalyzer
    {
        private ILogger _logger;
        private ILoggerFactory _loggerFactory;

        public DependencyAnalyzer(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = _loggerFactory.CreateLogger(typeof(DependencyAnalyzer));
        }

        public DependencyGraph Analyze(string projectPath, string framework = null)
        {
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                throw new ArgumentException(nameof(projectPath));
            }

            if (!File.Exists(projectPath))
            {
                throw new ArgumentException("Project path does not exist.", nameof(projectPath));
            }

            var analyzerManager = new AnalyzerManager(new AnalyzerManagerOptions
            {
                LoggerFactory = _loggerFactory
            });
            var projectAnalyzer = analyzerManager.GetProject(projectPath);

            // var options = new EnvironmentOptions();
            // options.TargetsToBuild.Add("GenerateBuildDependencyFile");
            // options.TargetsToBuild.Add("Build");

            var analyzeResult = string.IsNullOrEmpty(framework) ?
                projectAnalyzer.Build() : projectAnalyzer.Build(framework);

            var projectInstance = analyzeResult.ProjectInstance;

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

            var targetFramework = analyzeResult.GetTargetFramework();

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

                if (library.FrameworkAssemblies.Count > 0)
                {
                    var assemblyNodes = library.FrameworkAssemblies
                        .Select(x => new AssemblyReferenceNode(x));
                    builder.WithNodes(assemblyNodes);
                    builder.WithEdges(assemblyNodes
                        .Select(x => new Edge(libraryNode, x)));
                }

                if (library.RuntimeAssemblies.Count > 0)
                {
                    var assemblyNodes = library.RuntimeAssemblies
                        .Select(x => new AssemblyReferenceNode(Path.GetFileName(x.Path)))
                        .Where(x => x.Id != "_._");

                    if (assemblyNodes.Any())
                    {
                        builder.WithNodes(assemblyNodes);
                        builder.WithEdges(assemblyNodes
                            .Select(x => new Edge(libraryNode, x)));
                    }
                }

                //if (library.CompileTimeAssemblies.Count > 0)
                //{
                //    var assemblyNodes = library.CompileTimeAssemblies
                //        .Select(x => new AssemblyReferenceNode(Path.GetFileName(x.Path)));
                //    builder.WithNodes(assemblyNodes);
                //    builder.WithEdges(assemblyNodes
                //        .Select(x => new Edge(libraryNode, x)));
                //}
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
                .Select(x => new Edge(projectNode, libraryNodes[x.EvaluatedInclude], x.GetMetadataValue("Version"))));

            var references = projectInstance.GetItems("Reference")
                .Select(x => new AssemblyReferenceNode(Path.GetFileName(x.EvaluatedInclude)));

            builder.WithNodes(references);
            builder.WithEdges(references.Select(x => new Edge(projectNode, x)));

            return builder.Build();
        }
    }
}
