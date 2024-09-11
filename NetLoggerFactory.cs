using Microsoft.Extensions.Logging;

namespace LogicalServerUdp
{
    internal static class NetLoggerFactory
    {
        public static ILogger<T> CreateLogger<T>() where T : class
        {
            using ILoggerFactory factory = LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });

                builder.SetMinimumLevel(LogLevel.Debug);
            });

            return factory.CreateLogger<T>();
        }
    }
}
