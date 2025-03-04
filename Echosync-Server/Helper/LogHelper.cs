namespace Echosync_Server.Helper
{
    public static class LogHelper
    {
        private static string _fileName = "";

        public static void Log(string channelName, string log, bool error = false)
        {
            var timeStamp = DateTime.Now;
            log = $"{timeStamp.ToShortDateString()} {timeStamp.ToShortTimeString()}: {log}";

            if (!Path.Exists("Logs"))
                Directory.CreateDirectory("Logs");

            if (string.IsNullOrWhiteSpace(_fileName))
                _fileName = $"Logs\\{timeStamp:yyyy-MM-dd}_{channelName}.log";
            File.AppendAllLines(_fileName, [log]);

            Console.ForegroundColor = error ? ConsoleColor.Red : ConsoleColor.White;

            Console.WriteLine($"{log} - Channel: {channelName}");
        }
    }
}