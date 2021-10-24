using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using Buildalyzer;
using Depends.Core.Extensions;
using Depends.Core.Graph;
using Microsoft.Extensions.Logging;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.Threading.Tasks;
using NuGet.Resolver;
using NuGet.Packaging;
using Microsoft.Build.Construction;

namespace Depends.Core
{
    public class DependencyAnalyzer
    {
        static DependencyAnalyzer()
        {
            _ = typeof(NuGet.Common.LogLevel);
        }

        private ILogger _logger;
        private ILoggerFactory _loggerFactory;

        public DependencyAnalyzer(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = _loggerFactory.CreateLogger(typeof(DependencyAnalyzer));
        }

        public DependencyGraph Analyze(string packageId, string version, string framework)
        {
            var package = new PackageIdentity(packageId, NuGetVersion.Parse(version));
            var settings = Settings.LoadDefaultSettings(root: null, configFileName: null, machineWideSettings: null);
            var sourceRepositoryProvider = new SourceRepositoryProvider(new PackageSourceProvider(settings), Repository.Provider.GetCoreV3());
            var nuGetFramework = NuGetFramework.ParseFolder(framework);
            var nugetLogger = _logger.AsNuGetLogger();

            using (var cacheContext = new SourceCacheContext())
            {
                var repositories = sourceRepositoryProvider.GetRepositories();
                var resolvedPackages = new ConcurrentDictionary<PackageIdentity, SourcePackageDependencyInfo>(PackageIdentityComparer.Default);
                ResolvePackage(package, nuGetFramework, cacheContext, nugetLogger, repositories, resolvedPackages).Wait();

                var availablePackages = new HashSet<SourcePackageDependencyInfo>(resolvedPackages.Values);

                var resolverContext = new PackageResolverContext(
                    DependencyBehavior.Lowest,
                    new[] { packageId },
                    Enumerable.Empty<string>(),
                    Enumerable.Empty<PackageReference>(),
                    Enumerable.Empty<PackageIdentity>(),
                    availablePackages,
                    sourceRepositoryProvider.GetRepositories().Select(s => s.PackageSource),
                    nugetLogger);

                var resolver = new PackageResolver();
                var prunedPackages = resolver.Resolve(resolverContext, CancellationToken.None)
                    .Select(x => resolvedPackages[x]);

                var rootNode = new PackageReferenceNode(package.Id, package.Version.ToString());
                var packageNodes = new Dictionary<string, PackageReferenceNode>(StringComparer.OrdinalIgnoreCase);
                var builder = new DependencyGraph.Builder(rootNode);

                foreach (var target in prunedPackages)
                {
                    var downloadResource = target.Source.GetResource<DownloadResource>();
                    var downloadResult = downloadResource.GetDownloadResourceResultAsync(new PackageIdentity(target.Id, target.Version),
                        new PackageDownloadContext(cacheContext),
                        SettingsUtility.GetGlobalPackagesFolder(settings),
                        nugetLogger, CancellationToken.None).Result;

                    var libItems = downloadResult.PackageReader.GetLibItems();
                    var reducer = new FrameworkReducer();
                    var nearest = reducer.GetNearest(nuGetFramework, libItems.Select(x => x.TargetFramework));

                    var assemblyReferences = libItems
                        .Where(x => x.TargetFramework.Equals(nearest))
                        .SelectMany(x => x.Items)
                        .Where(x => Path.GetExtension(x).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                        .Select(x => new AssemblyReferenceNode(Path.GetFileName(x)));

                    var frameworkItems = downloadResult.PackageReader.GetFrameworkItems();
                    nearest = reducer.GetNearest(nuGetFramework, frameworkItems.Select(x => x.TargetFramework));

                    assemblyReferences = assemblyReferences.Concat(frameworkItems
                        .Where(x => x.TargetFramework.Equals(nearest))
                        .SelectMany(x => x.Items)
                        .Select(x => new AssemblyReferenceNode(x)));

                    var packageReferenceNode = new PackageReferenceNode(target.Id, target.Version.ToString());
                    builder.WithNode(packageReferenceNode);
                    builder.WithNodes(assemblyReferences);
                    builder.WithEdges(assemblyReferences.Select(x => new Edge(packageReferenceNode, x)));
                    packageNodes.Add(target.Id, packageReferenceNode);
                }

                foreach (var target in prunedPackages)
                {
                    var packageReferenceNode = packageNodes[target.Id];
                    builder.WithEdges(target.Dependencies.Select(x =>
                        new Edge(packageReferenceNode, packageNodes[x.Id], x.VersionRange.ToString())));
                }

                return builder.Build();
            }
        }

        private async Task ResolvePackage(PackageIdentity package,
            NuGetFramework framework,
            SourceCacheContext cacheContext,
            NuGet.Common.ILogger logger,
            IEnumerable<SourceRepository> repositories,
            ConcurrentDictionary<PackageIdentity, SourcePackageDependencyInfo> availablePackages)
        {
            if (availablePackages.ContainsKey(package))
            {
                return;
            }

            // TODO
            // Avoid getting info for e.g. netstandard1.x if our framework is highet (e.g. netstandard2.0)
            //if (framework.IsPackageBased &&
            //    package.Id.Equals("netstandard.library", StringComparison.OrdinalIgnoreCase) &&
            //    NuGetFrameworkUtility.IsCompatibleWithFallbackCheck(framework,
            //        NuGetFramework.Parse($"netstandard{package.Version.Major}.{package.Version.Minor}")))
            //{
            //    return;
            //}

            foreach (var sourceRepository in repositories)
            {
                var dependencyInfoResource = await sourceRepository.GetResourceAsync<DependencyInfoResource>();
                var dependencyInfo = await dependencyInfoResource.ResolvePackage(
                    package, framework, cacheContext, logger, CancellationToken.None);

                if (dependencyInfo == null)
                {
                    continue;
                }

                if (availablePackages.TryAdd(new PackageIdentity(dependencyInfo.Id, dependencyInfo.Version), dependencyInfo))
                {
                    await Task.WhenAll(dependencyInfo.Dependencies.Select(dependency =>
                    {
                        return ResolvePackage(new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion),
                            framework, cacheContext, logger, repositories, availablePackages);
                    }));
                }
            }
        }

        public DependencyGraph AnalyzeSolution(string solution, string framework = null)
        {
            var analyzerManager = new AnalyzerManager(solution, new AnalyzerManagerOptions
            {
                LoggerFactory = _loggerFactory
            });

            var solutionNode = new SolutionReferenceNode(solution);
            var builder = new DependencyGraph.Builder(solutionNode);
            foreach (var project in analyzerManager.Projects.Where(p => p.Value.ProjectInSolution.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat))
            {
                builder = CreateBuilder(project.Value, project.Key, builder, framework);
            }

            return builder.Build();
        }

        public DependencyGraph Analyze(string projectPath, string framework = null)
        {
            var analyzerManager = new AnalyzerManager( new AnalyzerManagerOptions
            {
                LoggerFactory = _loggerFactory
            });

            if (string.IsNullOrWhiteSpace(projectPath))
            {
                throw new ArgumentException(nameof(projectPath));
            }

            if (!File.Exists(projectPath))
            {
                throw new ArgumentException("Project path does not exist.", nameof(projectPath));
            }

            var projectAnalyzer = analyzerManager.GetProject(projectPath);
            return CreateBuilder(projectAnalyzer, projectPath, null, framework).Build();
        }

        private DependencyGraph.Builder CreateBuilder(IProjectAnalyzer  projectAnalyzer, string projectPath, DependencyGraph.Builder builder = null, string framework = null)
        {
            var analyzeResults = string.IsNullOrEmpty(framework) ?
                projectAnalyzer.Build() : projectAnalyzer.Build(framework);

            var analyzerResult = string.IsNullOrEmpty(framework) ?
                analyzeResults.FirstOrDefault() : analyzeResults[framework];

            if (analyzerResult == null)
            {
                // Todo: Something went wrong, log and return better exception.
                throw new InvalidOperationException("Unable to load project.");
            }
            var projectNode = new ProjectReferenceNode(projectPath);
            if (builder == null)
            {
                builder = new DependencyGraph.Builder(projectNode);
            }
            else
            {
                builder.WithNode(projectNode);
                builder.WithEdge(new Edge(builder.Root, projectNode));
            }

            var projectAssetsFilePath = analyzerResult.GetProjectAssetsFilePath();

            if (!File.Exists(projectAssetsFilePath))
            {
                if (analyzerResult.IsNetSdkProject())
                {
                    // a new project doesn't have an asset file
                    throw new InvalidOperationException($"{projectAssetsFilePath} not found. Please run 'dotnet restore'");
                }

                // Old csproj

                var oldStylePackageReferences = analyzerResult.GetItems("Reference").Where(x => x.ItemSpec.Contains("Version"));
                foreach (var reference in oldStylePackageReferences)
                {
                    var split = reference.ItemSpec.Split(',');
                    var version = split.Single(s => s.Contains("Version"))?.Split('=')[1];
                    var name = reference.ItemSpec.Split(',')[0];
                    var node = new PackageReferenceNode(name, version);
                    builder.WithNode(node);
                    builder.WithEdge(new Edge(projectNode, node, version));
                }
            }
            else
            {
                // New csproj

                var lockFile = new LockFileFormat().Read(projectAssetsFilePath);

                var targetFramework = analyzerResult.GetTargetFramework();
                var runtimeIdentifier = analyzerResult.GetRuntimeIdentifier();

                var libraries = lockFile.Targets.Single(
                        x => x.TargetFramework == targetFramework && x.RuntimeIdentifier == runtimeIdentifier)
                    .Libraries.Where(x => x.IsPackage()).ToList();

                var libraryNodes = new Dictionary<string, PackageReferenceNode>(StringComparer.OrdinalIgnoreCase);
                foreach (var library in libraries)
                {
                    var libraryNode = library.ToNode();
                    builder.WithNode(libraryNode);
                    libraryNodes.Add(libraryNode.PackageId, libraryNode);

                    if (library.FrameworkAssemblies.Count > 0)
                    {
                        var assemblyNodes = library.FrameworkAssemblies
                            .Select(x => new AssemblyReferenceNode($"{x}.dll"));
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

                // Ignore unversioned references like implicit SDK packages
                builder.WithEdges(analyzerResult.GetItems("PackageReference")
                    .Where(x => x.Metadata.ContainsKey("Version"))
                    .Select(x => new Edge(projectNode, libraryNodes[x.ItemSpec], x.Metadata["Version"])));
            }

            var references = analyzerResult.References.Select(x => new AssemblyReferenceNode(Path.GetFileName(x)));

            builder.WithNodes(references);
            builder.WithEdges(references.Select(x => new Edge(projectNode, x)));

            return builder;
        }
    }
}
