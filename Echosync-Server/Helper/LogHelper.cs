namespace Echosync_Server.Helper
{
    public static class LogHelper
    {
        private static string FileName;

        public static void Log(string channelName, string log, bool error = false)
        {
            var timeStamp = DateTime.Now;
            log = $"{timeStamp.ToShortDateString()} {timeStamp.ToShortTimeString()}: {log}";

            if (!Path.Exists("Logs"))
                Directory.CreateDirectory("Logs");

            if (string.IsNullOrWhiteSpace(FileName))
                FileName = $"Logs\\{timeStamp.ToString("yyyy-MM-dd")}_{channelName}.log";
            File.AppendAllLines(FileName, new string[] { log });

            if (error)
                Console.ForegroundColor = ConsoleColor.Red;
            else
                Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine($"{log} - Channel: {channelName}");
        }
    }
}
