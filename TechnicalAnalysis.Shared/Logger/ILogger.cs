namespace TechnicalAnalysis.Shared;

public interface ILogger
{
    void LogDebug(string message, params object[] args);
    void LogInfo(string message, params object[] args);
    void LogWarn(string message, params object[] args);
    void LogError(Exception exception, string message, params object[] args);
}

