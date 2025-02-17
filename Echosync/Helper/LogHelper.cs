using Dalamud.Plugin.Services;
using Echosync.DataClasses;
using Echosync.Enums;
using System;
using System.Collections.Generic;

namespace Echosync.Helper
{
    public static class LogHelper
    {
        private static IPluginLog Log;
        private static Configuration Config;
        private static int NextEventId = 1;
        private static List<LogMessage> GeneralLogs = new List<LogMessage>();
        public static List<LogMessage> GeneralLogsFiltered = new List<LogMessage>();
        private static List<LogMessage> SyncLogs = new List<LogMessage>();
        public static List<LogMessage> SyncLogsFiltered = new List<LogMessage>();

        public static void Setup(IPluginLog log, Configuration config)
        {
            Log = log;
            Config = config;
        }

        public static void Start(string method, EKEventId eventId)
        {
            var text = $"---------------------------Start----------------------------------";

            Info(method, text, eventId);
        }

        public static void End(string method, EKEventId eventId)
        {
            var text = $"----------------------------End-----------------------------------";

            Info(method, text, eventId);
        }

        public static void Info(string method, string text, EKEventId eventId)
        {
            text = $"{text}";
            SortLogEntry(new LogMessage() { type = LogType.Info, eventId = eventId, method = method, message = $"{text}", color = Constants.INFOLOGCOLOR, timeStamp = DateTime.Now });

            Log.Info($"{method} - {text} - ID: {eventId.Id}");
        }

        public static void Debug(string method, string text, EKEventId eventId)
        {
            text = $"{text}";
            SortLogEntry(new LogMessage() { type = LogType.Debug, eventId = eventId, method = method, message = $"{text}", color = Constants.DEBUGLOGCOLOR, timeStamp = DateTime.Now });

            Log.Debug($"{method} - {text} - ID: {eventId.Id}");
        }

        public static void Error(string method, string text, EKEventId eventId)
        {
            text = $"{text}";
            SortLogEntry(new LogMessage() { type = LogType.Error, eventId = eventId, method = method, message = $"{text}", color = Constants.ERRORLOGCOLOR, timeStamp = DateTime.Now });

            Log.Error($"{method} - {text} - ID: {eventId.Id}");
        }

        private static void SortLogEntry(LogMessage logMessage)
        {
            switch (logMessage.eventId.textSource)
            {
                case TextSource.None:
                    GeneralLogs.Add(logMessage);
                    if (logMessage.type == LogType.Info
                        || logMessage.type == LogType.Debug && Config.logConfig.ShowGeneralDebugLog
                        || logMessage.type == LogType.Error && Config.logConfig.ShowGeneralErrorLog)
                        GeneralLogsFiltered.Add(logMessage);
                    ConfigWindow.UpdateLogGeneralFilter = true;
                    break;
                case TextSource.Sync:
                    SyncLogs.Add(logMessage);
                    if (logMessage.type == LogType.Info
                        || logMessage.type == LogType.Debug && Config.logConfig.ShowSyncDebugLog
                        || logMessage.type == LogType.Error && Config.logConfig.ShowSyncErrorLog)
                        SyncLogsFiltered.Add(logMessage);
                    ConfigWindow.UpdateLogSyncFilter = true;
                    break;
            }
        }

        public static List<LogMessage> RecreateLogList(TextSource textSource)
        {
            var logListFiltered = new List<LogMessage>();
            var showDebug = false;
            var showError = false;
            var showId0 = false;
            switch (textSource)
            {
                case TextSource.None:
                    GeneralLogsFiltered = new List<LogMessage>(GeneralLogs);
                    logListFiltered = GeneralLogsFiltered;
                    showDebug = Config.logConfig.ShowGeneralDebugLog;
                    showError = Config.logConfig.ShowGeneralErrorLog;
                    showId0 = true;
                    break;
                case TextSource.Sync:
                    SyncLogsFiltered = new List<LogMessage>(SyncLogs);
                    logListFiltered = SyncLogsFiltered;
                    showDebug = Config.logConfig.ShowSyncDebugLog;
                    showError = Config.logConfig.ShowSyncErrorLog;
                    showId0 = Config.logConfig.ShowSyncId0;
                    break;
            }
            if (!showDebug)
            {
                logListFiltered.RemoveAll(p => p.type == LogType.Debug);
            }
            if (!showError)
            {
                logListFiltered.RemoveAll(p => p.type == LogType.Error);
            }
            if (!showId0)
            {
                logListFiltered.RemoveAll(p => p.eventId.Id == 0);
            }

            logListFiltered.Sort((p, q) => p.timeStamp.CompareTo(q.timeStamp));

            return new List<LogMessage>(logListFiltered);
        }

        public static EKEventId EventId(string methodName, TextSource textSource)
        {
            var eventId = new EKEventId(NextEventId, textSource);
            NextEventId++;

            LogHelper.Start(methodName, eventId);
            return eventId;
        }
    }
}
