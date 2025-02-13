using Serilog;

namespace TraiderAssistant.Infrastructure;

public class SerilogLoggerAdapter : ILogger
{
    private readonly Serilog.ILogger _logger;

    public SerilogLoggerAdapter(string context)
    {
        _logger = Log.ForContext("SourceContext", context); 
    }

    public void LogDebug(string message, params object[] args)
    {
        _logger.Debug(message);
    }

    public void LogInfo(string message, params object[] args)
    {
        _logger.Information(message, args);
    }

    public void LogWarn(string message, params object[] args)
    {
        _logger.Warning(message, args);
    }

    public void LogError(Exception exception, string message, params object[] args)
    {
        _logger.Error(exception, message, args);
    }
}