using DesignPatterns.Interfaces; 
namespace DesignPatterns.Logging;

public class ConsoleLogger : ILoggerService
{
    public void LogInformation(string message)
    {
        Console.WriteLine($"INFO: {message}");
    }

    public void LogError(string message)
    {
        Console.WriteLine($"ERROR: {message}");
    }   
}


public class FileLogger : ILoggerService
{
    private readonly string _filePath;

    public FileLogger(string filePath)
    {
        _filePath = filePath;
    }

    public void LogInformation(string message)
    {
        LogToFile($"INFO: {message}");
    }

    public void LogError(string message)
    {
        LogToFile($"ERROR: {message}");
    }

    private void LogToFile(string message)
    {
        using StreamWriter writer = new(_filePath, append: true);
        writer.WriteLine($"{DateTime.Now}: {message}");
    }
}