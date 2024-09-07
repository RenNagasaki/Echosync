namespace Echosync_Server.Helper
{
    public static class LogHelper
    {
        private static string FileName;

        public static void Log(string log)
        {
            var timeStamp = DateTime.Now;
            log = $"{timeStamp.ToShortDateString()} {timeStamp.ToShortTimeString()}: {log}";

            if (string.IsNullOrWhiteSpace(FileName))
                FileName = $"{timeStamp.ToString("yyyy-MM-dd")}_Server.log";
            File.AppendAllLines(FileName, new string[] { log });
            Console.WriteLine(log);
        }
    }
}
