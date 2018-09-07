using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Common;
using Microsoft.Extensions.Logging;
using MEL = Microsoft.Extensions.Logging;
using ILogger = NuGet.Common.ILogger;
using LogLevel = NuGet.Common.LogLevel;

namespace Depends.Core.Extensions
{
    internal static class LoggerExtensions
    {
        public static ILogger AsNuGetLogger(this MEL.ILogger logger)
        {
            return new NuGetLogger(logger);
        }

        private class NuGetLogger : ILogger
        {
            private MEL.ILogger _logger;
            private Dictionary<LogLevel, MEL.LogLevel> _logLevelMap = new Dictionary<LogLevel, MEL.LogLevel>
            {
                [LogLevel.Debug] = MEL.LogLevel.Debug,
                [LogLevel.Error] = MEL.LogLevel.Error,
                [LogLevel.Information] = MEL.LogLevel.Information,
                [LogLevel.Minimal] = MEL.LogLevel.Critical,
                [LogLevel.Verbose] = MEL.LogLevel.Trace,
                [LogLevel.Warning] = MEL.LogLevel.Warning
            };

            public NuGetLogger(Microsoft.Extensions.Logging.ILogger logger)
            {
                _logger = logger;
            }

            public void Log(LogLevel level, string data) => _logger.Log(_logLevelMap[level], 0, data, null, (s, _) => s);

            public void Log(ILogMessage message) => Log(message.Level, message.Message);

            public Task LogAsync(LogLevel level, string data)
            {
                Log(level, data);
                return Task.CompletedTask;
            }

            public Task LogAsync(ILogMessage message)
            {
                Log(message);
                return Task.CompletedTask;
            }

            public void LogDebug(string data) => Log(LogLevel.Debug, data);

            public void LogError(string data) => Log(LogLevel.Error, data);

            public void LogInformation(string data)
            {
                Log(LogLevel.Information, data);
            }

            public void LogInformationSummary(string data) => Log(LogLevel.Information, data);

            public void LogMinimal(string data) => Log(LogLevel.Minimal, data);

            public void LogVerbose(string data) => Log(LogLevel.Verbose, data);

            public void LogWarning(string data) => Log(LogLevel.Warning, data);
        }
    }
}
