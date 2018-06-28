using System;
using System.IO;
using Depends.Core;
using Depends.Core.Output;
using Microsoft.Extensions.Logging;

namespace Depends
{
    class Program
    {
        public static int Main(string[] args)
        {
            var loggerFactory = new LoggerFactory().AddConsole();
            var analyzer = new DependencyAnalyzer(loggerFactory);
            var graph = analyzer.Analyze(args[0]);

            using (var writer = File.CreateText(Path.ChangeExtension(args[0], ".gv")))
            {
                new DotFileWriter
                {
                    GraphName = Path.GetFileName(args[0])
                }.Write(graph, writer);
            }

            return 0;
        }
    }
}
