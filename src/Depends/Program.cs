using System;
using Depends.Core;
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

            return 0;
        }
    }
}
