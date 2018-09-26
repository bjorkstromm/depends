using System;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using Depends.Core;
using Depends.Core.Graph;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;
using Terminal.Gui;

namespace Depends
{
    internal class Program
    {
        public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        [Argument(0, Description = "The project file to analyze. If a project file is not specified, Depends searches the current working directory for a file that has a file extension that ends in proj and uses that file.")]
        // ReSharper disable once UnassignedGetOnlyAutoProperty
        public string Project { get; set; } = Directory.GetCurrentDirectory();

        [Option("-v|--verbosity <LEVEL>", Description = "Sets the verbosity level of the command. Allowed values are Trace, Debug, Information, Warning, Error, Critical, None")]
        // ReSharper disable once UnassignedGetOnlyAutoProperty
        public LogLevel Verbosity { get; }

        [Option("-f|--framework <FRAMEWORK>", Description = "Analyzes for a specific framework. The framework must be defined in the project file.")]
        public string Framework { get; }

        [Option("--package <PACKAGE>", Description = "Analyzes a specific package.")]
        public string Package { get; }

        [Option("--version <PACKAGEVERSION>", Description = "The version of the package to analyze.")]
        public string Version { get; }


        // ReSharper disable once UnusedMember.Local
        private ValidationResult OnValidate()
        {
            if (File.Exists(Project))
            {
                return ValidationResult.Success;
            }

            if (!Directory.Exists(Project))
            {
                return new ValidationResult("Project path does not exist.");
            }

            var csproj = Directory.GetFiles(Project, "*.csproj", SearchOption.TopDirectoryOnly).ToArray();

            if (!csproj.Any())
            {
                return string.IsNullOrEmpty(Package) || string.IsNullOrEmpty(Framework) ?
                    new ValidationResult("Unable to find any project files in working directory.") :
                    ValidationResult.Success;
            }

            if (csproj.Length > 1)
            {
                return new ValidationResult("More than one project file found in working directory.");
            }

            Project = csproj[0];
            return ValidationResult.Success;
        }

        // ReSharper disable once UnusedMember.Local
        private void OnExecute()
        {
            var loggerFactory = new LoggerFactory()
                .AddConsole(Verbosity);
            var analyzer = new DependencyAnalyzer(loggerFactory);

            var graph = string.IsNullOrEmpty(Package) ?
                analyzer.Analyze(Project, Framework) :
                analyzer.Analyze(Package, Version, Framework);

            Application.Init();

            var top = new CustomWindow();

            var left = new FrameView("Dependencies")
            {
                Width = Dim.Percent(50),
                Height = Dim.Fill(1)
            };
            var right = new View()
            {
                X = Pos.Right(left),
                Width = Dim.Fill(),
                Height = Dim.Fill(1)
            };
            var helpText = new Label("Use arrow keys and Tab to move around. Ctrl+D to toggle assembly visibility. Esc to quit.")
            {
                Y = Pos.AnchorEnd(1)
            };

            var runtimeDepends = new FrameView("Runtime depends")
            {
                Width = Dim.Fill(),
                Height = Dim.Percent(33f)
            };
            var packageDepends = new FrameView("Package depends")
            {
                Y = Pos.Bottom(runtimeDepends),
                Width = Dim.Fill(),
                Height = Dim.Percent(50f)
            };
            var reverseDepends = new FrameView("Reverse depends")
            {
                Y = Pos.Bottom(packageDepends),
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            var orderedDependencyList = graph.Nodes.OrderBy(x => x.Id).ToImmutableList();
            var dependenciesView = new ListView(orderedDependencyList)
            {
                CanFocus = true,
                AllowsMarking = false
            };
            left.Add(dependenciesView);
            var runtimeDependsView = new ListView(Array.Empty<Node>())
            {
                CanFocus = true,
                AllowsMarking = false
            };
            runtimeDepends.Add(runtimeDependsView);
            var packageDependsView = new ListView(Array.Empty<Node>())
            {
                CanFocus = true,
                AllowsMarking = false
            };
            packageDepends.Add(packageDependsView);
            var reverseDependsView = new ListView(Array.Empty<Node>())
            {
                CanFocus = true,
                AllowsMarking = false
            };
            reverseDepends.Add(reverseDependsView);

            right.Add(runtimeDepends, packageDepends, reverseDepends);
            top.Add(left, right, helpText);
            Application.Top.Add(top);

            top.Dependencies = orderedDependencyList;
            top.VisibleDependencies = orderedDependencyList;
            top.DependenciesView = dependenciesView;

            dependenciesView.SelectedItem = 0;
            UpdateLists();

            dependenciesView.SelectedChanged += UpdateLists;

            Application.Run();

            void UpdateLists()
            {
                var selectedNode = top.VisibleDependencies[dependenciesView.SelectedItem];

                runtimeDependsView.SetSource(graph.Edges.Where(x => x.Start.Equals(selectedNode) && x.End is AssemblyReferenceNode)
                    .Select(x => x.End).ToImmutableList());
                packageDependsView.SetSource(graph.Edges.Where(x => x.Start.Equals(selectedNode) && x.End is PackageReferenceNode)
                    .Select(x => $"{x.End}{(string.IsNullOrEmpty(x.Label) ? string.Empty : " (Wanted: " + x.Label + ")")}").ToImmutableList());
                reverseDependsView.SetSource(graph.Edges.Where(x => x.End.Equals(selectedNode))
                    .Select(x => $"{x.Start}{(string.IsNullOrEmpty(x.Label) ? string.Empty : " (Wanted: " + x.Label + ")")}").ToImmutableList());
            }
        }

        private class CustomWindow : Window
        {
            public CustomWindow() : base("Depends", 0) { }

            public ListView DependenciesView { get; set; }
            public ImmutableList<Node> Dependencies { get; set; }
            public ImmutableList<Node> VisibleDependencies { get; set; }

            private bool _assembliesVisible = true;

            public override bool ProcessKey(KeyEvent keyEvent)
            {
                if (keyEvent.Key == Key.Esc)
                {
                    Application.RequestStop();
                    return true;
                }
                if (keyEvent.Key == Key.ControlD)
                {
                    _assembliesVisible = !_assembliesVisible;

                    VisibleDependencies = _assembliesVisible ?
                        Dependencies :
                        Dependencies.Where(d => !(d is AssemblyReferenceNode)).ToImmutableList();

                    DependenciesView.SetSource(VisibleDependencies);

                    DependenciesView.SelectedItem = 0;
                    return true;
                }

                return base.ProcessKey(keyEvent);
            }
        }
    }
}
