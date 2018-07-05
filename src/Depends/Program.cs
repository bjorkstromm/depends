using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using Depends.Core;
using Depends.Core.Graph;
using Microsoft.Extensions.Logging;
using Spectre.Cli;
using Terminal.Gui;

namespace Depends
{
    internal class Program
    {
        public static int Main(string[] args) => new CommandApp<RootCommand>().Run(args);
    }

    [Description("")]
    internal class RootCommand : Command<Settings>
    {
        public override int Execute(CommandContext context, Settings settings)
        {
            var loggerFactory = new LoggerFactory()
                .AddConsole(settings.Verbosity);
            var analyzer = new DependencyAnalyzer(loggerFactory);
            var graph = analyzer.Analyze(settings.ProjectPath);

            Application.Init();

            var top = Application.Top;

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
                AllowsMarking = true
            };
            left.Add(dependenciesView);
            var runtimeDependsView = new ListView(Array.Empty<Node>())
            {
                CanFocus = true,
                AllowsMarking = true
            };
            runtimeDepends.Add(runtimeDependsView);
            var packageDependsView = new ListView(Array.Empty<Node>())
            {
                CanFocus = true,
                AllowsMarking = true
            };
            packageDepends.Add(packageDependsView);
            var reverseDependsView = new ListView(Array.Empty<Node>())
            {
                CanFocus = true,
                AllowsMarking = true
            };
            reverseDepends.Add(reverseDependsView);

            right.Add(runtimeDepends, packageDepends, reverseDepends);
            top.Add(left, right);

            dependenciesView.SelectedItem = 0;
            UpdateLists();

            dependenciesView.SelectedChanged += UpdateLists;

            Application.Run();

            return 0;

            void UpdateLists()
            {
                var selectedNode = orderedDependencyList[dependenciesView.SelectedItem];

                runtimeDependsView.SetSource(graph.Edges.Where(x => x.Start.Equals(selectedNode) && x.End is AssemblyReferenceNode)
                    .Select(x => x.End).ToImmutableList());
                packageDependsView.SetSource(graph.Edges.Where(x => x.Start.Equals(selectedNode) && x.End is PackageReferenceNode)
                    .Select(x => $"{x.End}{(string.IsNullOrEmpty(x.Label) ? string.Empty : " (Wanted: " + x.Label + ")")}").ToImmutableList());
                reverseDependsView.SetSource(graph.Edges.Where(x => x.End.Equals(selectedNode))
                    .Select(x => $"{x.Start}{(string.IsNullOrEmpty(x.Label) ? string.Empty : " (Wanted: " + x.Label + ")")}").ToImmutableList());
            }
        }
    }

    internal class Settings : CommandSettings
    {
        public override ValidationResult Validate()
        {
            if (string.IsNullOrEmpty(ProjectPath))
            {
                // TODO: Look in dir
                return ValidationResult.Error("");
            }

            return ValidationResult.Success();
        }

        [CommandArgument(0, "")]
        public string ProjectPath { get; set; }

        [CommandOption("-v|--verbosity")]
        public LogLevel Verbosity { get; set; }
    }
}
