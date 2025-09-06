using Dalamud.Plugin.Services;
using Echosync.DataClasses;
using Echosync.Enums;
using System;
using System.Collections.Generic;
using Echosync.Windows;

namespace Echosync.Helper
{
    public static class LogHelper
    {
        private static int _nextEventId = 1;
        private static readonly List<LogMessage> GeneralLogs = [];
        private static readonly List<LogMessage> SyncLogs = [];
        private static readonly List<LogMessage> GeneralLogsFiltered = [];
        private static readonly List<LogMessage> SyncLogsFiltered = [];

        private static void Start(string method, EKEventId eventId)
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
            SortLogEntry(new LogMessage() { Type = LogType.Info, EventId = eventId, Method = method, Message = $"{text}", Color = Constants.INFOLOGCOLOR, TimeStamp = DateTime.Now });

            Plugin.Log!.Info($"{method} - {text} - ID: {eventId.Id}");
        }

        public static void Debug(string method, string text, EKEventId eventId)
        {
            text = $"{text}";
            SortLogEntry(new LogMessage() { Type = LogType.Debug, EventId = eventId, Method = method, Message = $"{text}", Color = Constants.DEBUGLOGCOLOR, TimeStamp = DateTime.Now });

            Plugin.Log!.Debug($"{method} - {text} - ID: {eventId.Id}");
        }

        public static void Error(string method, string text, EKEventId eventId)
        {
            text = $"{text}";
            SortLogEntry(new LogMessage() { Type = LogType.Error, EventId = eventId, Method = method, Message = $"{text}", Color = Constants.ERRORLOGCOLOR, TimeStamp = DateTime.Now });

            Plugin.Log!.Error($"{method} - {text} - ID: {eventId.Id}");
        }

        private static void SortLogEntry(LogMessage logMessage)
        {
            switch (logMessage.EventId.textSource)
            {
                case TextSource.None:
                    GeneralLogs.Add(logMessage);
                    if (logMessage.Type == LogType.Info
                        || logMessage.Type == LogType.Debug && Plugin.Configuration!.LogConfig!.ShowGeneralDebugLog
                        || logMessage.Type == LogType.Error && Plugin.Configuration!.LogConfig!.ShowGeneralErrorLog)
                        GeneralLogsFiltered.Add(logMessage);
                    ConfigWindow.UpdateLogGeneralFilter = true;
                    break;
                case TextSource.Sync:
                    SyncLogs.Add(logMessage);
                    if (logMessage.Type == LogType.Info
                        || logMessage.Type == LogType.Debug && Plugin.Configuration!.LogConfig!.ShowSyncDebugLog
                        || logMessage.Type == LogType.Error && Plugin.Configuration!.LogConfig!.ShowSyncErrorLog)
                        SyncLogsFiltered.Add(logMessage);
                    ConfigWindow.UpdateLogSyncFilter = true;
                    break;
            }
        }

        public static List<LogMessage> RecreateLogList(TextSource textSource)
        {
            List<LogMessage> logListFiltered;
            bool showDebug;
            bool showError;
            bool showId0;
            switch (textSource)
            {
                case TextSource.None:
                    GeneralLogsFiltered.Clear();
                    GeneralLogs.AddRange(GeneralLogs);
                    logListFiltered = GeneralLogsFiltered;
                    showDebug = Plugin.Configuration!.LogConfig!.ShowGeneralDebugLog;
                    showError = Plugin.Configuration.LogConfig.ShowGeneralErrorLog;
                    showId0 = true;
                    break;
                case TextSource.Sync:
                    SyncLogsFiltered.Clear();
                    SyncLogsFiltered.AddRange(SyncLogs);
                    logListFiltered = SyncLogsFiltered;
                    showDebug = Plugin.Configuration!.LogConfig!.ShowSyncDebugLog;
                    showError = Plugin.Configuration.LogConfig.ShowSyncErrorLog;
                    showId0 = Plugin.Configuration.LogConfig.ShowSyncId0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(textSource), textSource, null);
            }
            if (!showDebug)
            {
                logListFiltered.RemoveAll(p => p.Type == LogType.Debug);
            }
            if (!showError)
            {
                logListFiltered.RemoveAll(p => p.Type == LogType.Error);
            }
            if (!showId0)
            {
                logListFiltered.RemoveAll(p => p.EventId.Id == 0);
            }

            logListFiltered.Sort((p, q) => p.TimeStamp.CompareTo(q.TimeStamp));

            return [..logListFiltered];
        }

        public static EKEventId EventId(string methodName, TextSource textSource)
        {
            var eventId = new EKEventId(_nextEventId, textSource);
            _nextEventId++;

            LogHelper.Start(methodName, eventId);
            return eventId;
        }
    }
}