using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;

namespace CryptoDrive.Core.Tests
{
    public static class Utils
    {
        public static (ILogger<CryptoDriveSyncEngine>, List<ILoggerProvider>) GetLogger(ITestOutputHelper xunitLogger)
        {
            List<ILoggerProvider> loggerProviders = null;

            var loggerFactory = LoggerFactory.Create(logging =>
            {
                logging
                    .AddSeq()
                    .AddProvider(new XunitLoggerProvider(xunitLogger))
                    .SetMinimumLevel(LogLevel.Trace);

                loggerProviders = logging.Services
                    .Where(descriptor => typeof(ILoggerProvider).IsAssignableFrom(descriptor.ImplementationInstance?.GetType()))
                    .Select(descriptor => (ILoggerProvider)descriptor.ImplementationInstance)
                    .ToList();
            });

            var logger = loggerFactory.CreateLogger<CryptoDriveSyncEngine>();

            return (logger, loggerProviders);
        }
    }
}
