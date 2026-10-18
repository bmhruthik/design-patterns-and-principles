namespace DesignPatterns.Interfaces;

public interface ILoggerService
{
    void LogInformation(string message);

    void LogError(string message);
}