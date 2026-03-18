namespace Echosync.Server.Helper;

public static class LogHelper
{
    private static string _fileName = "";
    private static readonly object WriteLock = new();

    public static void Log(string channelName, string log, bool error = false, [System.Runtime.CompilerServices.CallerMemberName] string methodName = "")
    {
        var timeStamp = DateTime.Now;
        log = $"{timeStamp.ToShortDateString()} {timeStamp.ToShortTimeString()}: [{methodName}] {log}";

        lock (WriteLock)
        {
            if (!Path.Exists("Logs"))
                Directory.CreateDirectory("Logs");

            if (string.IsNullOrWhiteSpace(_fileName))
                _fileName = $"Logs/{timeStamp:yyyy-MM-dd}_{channelName}.log";
            File.AppendAllLines(_fileName, [log]);
        }

        Console.ForegroundColor = error ? ConsoleColor.Red : ConsoleColor.White;
        Console.WriteLine($"{log} - Channel: {channelName}");
    }
}
