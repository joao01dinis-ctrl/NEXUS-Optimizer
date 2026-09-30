using Serilog;
namespace Nexus.Diagnostics;

public static class AppLog
{
    public static void Initialize(string directory)
    {
        Directory.CreateDirectory(directory);
        Log.Logger = new LoggerConfiguration().MinimumLevel.Information().WriteTo.File(Path.Combine(directory, "nexus-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7).CreateLogger();
    }
    public static void Info(string message) => Log.Information("{Event}", message);
    public static void Error(Exception error, string message) => Log.Error(error, "{Event}", message);
    public static void Close() => Log.CloseAndFlush();
}
