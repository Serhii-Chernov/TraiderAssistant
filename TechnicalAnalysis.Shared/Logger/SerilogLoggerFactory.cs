using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Settings.Configuration;

namespace TechnicalAnalysis.Shared;

public class SerilogLoggerFactory 
{
    public static ILogger CreateLogger<T>()
    {
        var loggerName = typeof(T).FullName;
        return new SerilogLoggerAdapter(loggerName);
    }

    public static void Configure()
    {
        var logFilePath = Path.Combine(AppContext.BaseDirectory, $"logs/log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
        var outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}";

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: outputTemplate)
            .WriteTo.File(logFilePath, outputTemplate: outputTemplate)
            .CreateLogger();
    }
}
