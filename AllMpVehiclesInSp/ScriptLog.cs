using Utilities;

public static class ScriptLog
{
    private static LogFile LogFile;

    public static bool EnableDebugLogging {
        get => LogFile?.EnableDebugLogging ?? false;
        set => LogFile.EnableDebugLogging = value;
    }

    public static void Open(string filename)
    {
        LogFile = new LogFile(filename);
    }

    public static void Close()
    {
        LogFile?.Dispose();
        LogFile = null;
    }

    public static void Message(string message)
    {
        LogFile?.Message(message);
    }

    public static void ErrorMessage(string message)
    {
        LogFile?.ErrorMessage(message);
    }

    public static void DebugMessage(string message)
    {
        LogFile?.DebugMessage(message);
    }
}
