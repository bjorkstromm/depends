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

        [Option("-v|--verbosity <LEVEL>", Description = "Sets the verbosity level of the command. Allowed values are Trace, Debug, Information, Warning, Error, Critical, None. Defaults to Information.")]
        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Local, used implicitly by McMaster.Extensions.CommandLineUtils 
        public LogLevel Verbosity { get; private set; } = LogLevel.Information;

        [Option("-f|--framework <FRAMEWORK>", Description = "Analyzes for a specific framework. The framework must be defined in the project file.")]
        public string Framework { get; }

        [Option("--package <PACKAGE>", Description = "Analyzes a specific package.")]
        public string Package { get; }

        [Option("--version <PACKAGEVERSION>", Description = "The version of the package to analyze.")]
        public string Version { get; }

        // Following method derived from dotnet-outdated, licensed under MIT
        // MIT License
        //
        // Copyright (c) 2018 Jerrie Pelser
        //
        // Permission is hereby granted, free of charge, to any person obtaining a copy
        // of this software and associated documentation files (the "Software"), to deal
        // in the Software without restriction, including without limitation the rights
        // to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        // copies of the Software, and to permit persons to whom the Software is
        // furnished to do so, subject to the following conditions:
        //
        // The above copyright notice and this permission notice shall be included in all
        // copies or substantial portions of the Software.
        //
        // THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        // IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        // FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        // AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        // LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        // OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        // SOFTWARE.
        //
        // https://github.com/jerriep/dotnet-outdated/blob/b2c9e99c530a64e246ac529bbdc42ddde19b1e1a/src/DotNetOutdated.Core/Services/ProjectDiscoveryService.cs
        // ReSharper disable once UnusedMember.Local
        private ValidationResult OnValidate()
        {
            if (!(File.Exists(Project) || Directory.Exists(Project)))
            {
                return new ValidationResult("Project path does not exist.");
            }

            var fileAttributes = File.GetAttributes(Project);

            // If a directory was passed in, search for a .sln or .proj file
            if (fileAttributes.HasFlag(FileAttributes.Directory))
            {
                // Search for solution(s)
                var solutionFiles = Directory.GetFiles(Project, "*.sln");
                if (solutionFiles.Length == 1)
                {
                    Project = Path.GetFullPath(solutionFiles[0]);
                    return ValidationResult.Success;
                }

                if (solutionFiles.Length > 1)
                {
                    return new ValidationResult($"More than one solution file found in working directory.");
                }

                // We did not find any solutions, so try and find individual projects
                var projectFiles = Directory.GetFiles(Project, "*.*proj").ToArray();

                if (projectFiles.Length == 1)
                {
                    Project = Path.GetFullPath(projectFiles[0]);
                    return ValidationResult.Success;
                }

                if (projectFiles.Length > 1)
                {
                    return new ValidationResult($"More than one project file found in working directory.");
                }

                // At this point the path contains no solutions or projects, so throw an exception
                return new ValidationResult($"Unable to find any solution or project files in working directory.");
            }

            Project = Path.GetFullPath(Project);
            return ValidationResult.Success;
        }

        // ReSharper disable once UnusedMember.Local
        private void OnExecute()
        {
            var loggerFactory = LoggerFactory.Create(builder => builder
                .SetMinimumLevel(Verbosity)
                .AddConsole());

            var graph = GetDependencyGraph(loggerFactory);

            Application.Init();
            Application.QuitKey = Key.Esc;
            Application.Top.Add(new AppWindow(graph));
            Application.Run();
        }

        private DependencyGraph GetDependencyGraph(ILoggerFactory loggerFactory)
        {
            var analyzer = new DependencyAnalyzer(loggerFactory);
            DependencyGraph graph;
            if (!string.IsNullOrEmpty(Package))
            {
                graph = analyzer.Analyze(Package, Version, Framework);
            }
            else if (Path.GetExtension(Project).Equals(".sln", StringComparison.OrdinalIgnoreCase))
            {
                graph = analyzer.AnalyzeSolution(Project, Framework);
            }
            else
            {
                graph = analyzer.Analyze(Project, Framework);
            }
            return graph;
        }

        private class AppWindow : Window
        {
            private readonly DependencyGraph _graph;
            private readonly ImmutableList<Node> _dependencies;
            private ImmutableList<Node> _visibleDependencies;
            private bool _assembliesVisible;
            private int _lastSelectedDependencyIndex;

            private readonly ListView _dependenciesView;
            private readonly ListView _runtimeDependsView;
            private readonly ListView _packageDependsView;
            private readonly ListView _reverseDependsView;

            class DependsListItemModel
            {
                public Node Node { get; }
                public string DisplayText { get; }

                public DependsListItemModel(Node node, string label)
                {
                    Node = node ?? throw new ArgumentNullException(nameof(node));
                    DisplayText = $"{node}{(string.IsNullOrEmpty(label) ? string.Empty : " (Wanted: " + label + ")")}";
                }

                public override string ToString()
                {
                    return DisplayText;
                }
            }

            public AppWindow(DependencyGraph graph) : base("Depends", 0)
            {
                _graph = graph ?? throw new ArgumentNullException(nameof(graph));
                _dependencies = _graph.Nodes.OrderBy(x => x.Id).ToImmutableList();
                _visibleDependencies = _dependencies;
                _assembliesVisible = true;
                _lastSelectedDependencyIndex = -1;

                ColorScheme = new ColorScheme
                {
                    Focus = Application.Driver.MakeAttribute(Color.Black, Color.White),
                    Normal = Application.Driver.MakeAttribute(Color.White, Color.Black),
                    HotFocus = Application.Driver.MakeAttribute(Color.Black, Color.White),
                    HotNormal = Application.Driver.MakeAttribute(Color.White, Color.Black)
                };

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
                    Height = Dim.Percent(33f)
                };
                var reverseDepends = new FrameView("Reverse depends")
                {
                    Y = Pos.Bottom(packageDepends),
                    Width = Dim.Fill(),
                    Height = Dim.Fill()
                };
                _dependenciesView = new ListView()
                {
                    CanFocus = true,
                    AllowsMarking = false,
                    Width = Dim.Fill(),
                    Height = Dim.Fill()
                };
                left.Add(_dependenciesView);
                _runtimeDependsView = new ListView()
                {
                    CanFocus = true,
                    AllowsMarking = false,
                    Width = Dim.Fill(),
                    Height = Dim.Fill()
                };
                runtimeDepends.Add(_runtimeDependsView);
                _packageDependsView = new ListView()
                {
                    CanFocus = true,
                    AllowsMarking = false,
                    Width = Dim.Fill(),
                    Height = Dim.Fill()
                };
                packageDepends.Add(_packageDependsView);
                _reverseDependsView = new ListView()
                {
                    CanFocus = true,
                    AllowsMarking = false,
                    Width = Dim.Fill(),
                    Height = Dim.Fill()
                };
                reverseDepends.Add(_reverseDependsView);

                right.Add(runtimeDepends, packageDepends, reverseDepends);
                Add(left, right, helpText);

                _runtimeDependsView.OpenSelectedItem += RuntimeDependsView_OpenSelectedItem;
                _packageDependsView.OpenSelectedItem += PackageDependsView_OpenSelectedItem;
                _reverseDependsView.OpenSelectedItem += ReverseDependsView_OpenSelectedItem;

                _dependenciesView.SelectedItemChanged += DependenciesView_SelectedItemChanged; ;
                _dependenciesView.SetSource(_visibleDependencies);
            }

            public override bool ProcessKey(KeyEvent keyEvent)
            {
                if (keyEvent.Key == (Key.D | Key.CtrlMask))
                {
                    _assembliesVisible = !_assembliesVisible;
                    _visibleDependencies = _assembliesVisible ?
                        _dependencies :
                        _dependencies.Where(d => !(d is AssemblyReferenceNode)).ToImmutableList();

                    _dependenciesView.SetSource(_visibleDependencies);
                    _lastSelectedDependencyIndex = -1;
                    _dependenciesView.SetFocus();
                    return true;
                }

                return base.ProcessKey(keyEvent);
            }

            private void DependenciesView_SelectedItemChanged(ListViewItemEventArgs args)
            {
                // The ListView.SelectedItemChanged event is fired on enter (focus), see https://github.com/migueldeicaza/gui.cs/issues/831
                // To keep the current selection in the right pane (runtime, package & reverse depends lists), call UpdateLists() only if the selected item has actually been changed.
                if(_lastSelectedDependencyIndex != args.Item)
                {
                    _lastSelectedDependencyIndex = args.Item;
                    UpdateLists();
                }
            }

            private void RuntimeDependsView_OpenSelectedItem(ListViewItemEventArgs args)
            {
                if(_assembliesVisible)
                {
                    SetSelectedDependency((Node)args.Value);
                }
                // else: would be nice to provide a feedback so that the user understands that navigation is not possible.
            }

            private void PackageDependsView_OpenSelectedItem(ListViewItemEventArgs args)
            {
                var node = ((DependsListItemModel)args.Value).Node;
                SetSelectedDependency(node);
            }

            private void ReverseDependsView_OpenSelectedItem(ListViewItemEventArgs args)
            {
                var node = ((DependsListItemModel)args.Value).Node;
                SetSelectedDependency(node);
            }

            private void SetSelectedDependency(Node node)
            {
                var index = _visibleDependencies.FindIndex(x => x.Equals(node));
                _dependenciesView.SelectedItem = index;
                _dependenciesView.SetFocus();
            }

            private void UpdateLists()
            {
                var selectedNode = _visibleDependencies[_dependenciesView.SelectedItem];

                _runtimeDependsView.SetSource(_graph.Edges.Where(x => x.Start.Equals(selectedNode) && x.End is AssemblyReferenceNode)
                    .Select(x => x.End).ToImmutableList());
                _packageDependsView.SetSource(_graph.Edges.Where(x => x.Start.Equals(selectedNode) && x.End is PackageReferenceNode)
                    .Select(x => new DependsListItemModel(x.End, x.Label)).ToImmutableList());
                _reverseDependsView.SetSource(_graph.Edges.Where(x => x.End.Equals(selectedNode))
                    .Select(x => new DependsListItemModel(x.Start, x.Label)).ToImmutableList());
            }
        }
    }
}
