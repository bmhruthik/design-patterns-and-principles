namespace SolidCookbook.Principles.DIP;



public interface Ilogger
{
    void Log(string message);
}

public class ConsoleLogger : Ilogger
{
    public void Log(string message)
    {
        Console.WriteLine($"ConsoleLogger: {message}");
    }
}

public class FileLogger : Ilogger
{
    public void Log(string message)
    {
        // Simulate logging to a file
        Console.WriteLine($"FileLogger: {message}");
    }
}

public class Application
{
    private readonly Ilogger _logger;

    public Application(Ilogger logger)
    {
        _logger = logger;
    }

    public void Run()
    {
        _logger.Log("Application is running.");
    }
}



public static class Levelone
{
    public static void Run()
    {

        var consoleLogger = new ConsoleLogger();
        var fileLogger = new FileLogger();

        var appWithConsoleLogger = new Application(consoleLogger);
        var appWithFileLogger = new Application(fileLogger);

        appWithConsoleLogger.Run();
        appWithFileLogger.Run();

        Console.WriteLine("Running DIP Level One Demo");

        // create objects

        // call methods

        // print output
    }
}