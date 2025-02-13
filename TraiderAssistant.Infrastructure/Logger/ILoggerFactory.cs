namespace TraiderAssistant.Infrastructure;

public interface ILoggerFactory
{
    ILogger CreateLogger<T>();
}