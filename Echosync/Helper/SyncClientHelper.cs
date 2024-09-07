using Dalamud.Plugin.Services;
using Echosync.DataClasses;
using Echosync.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using Echosync_Data.Enums;
using WebSocketSharp;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Echosync.Helper
{
    public static class SyncClientHelper
    {
        private static Configuration Configuration;
        private static IClientState ClientState;
        private static WebSocket WebSocket;
        private static string ActiveChannel = "";
        private static string EntityId = "";
        public static bool Connected = false;
        public static bool AllReady = false;
        private static EKEventId currentEvent;
        private static string SyncServerThread = "main";

        public static EKEventId CurrentEvent
        {
            get { return currentEvent == null ? new EKEventId(0, TextSource.Sync) : currentEvent; }
            set { currentEvent = value; }
        }

        public static void Setup(Configuration configuration, IClientState clientState)
        {
            Configuration = configuration;
            ClientState = clientState;
            EntityId = ClientState.LocalPlayer?.EntityId.ToString();
            try
            {
                if (configuration.ConnectAtStart)
                {
                    Connect();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while starting: {ex}", CurrentEvent);
            }
        }

        private static void InitializeWebSocket()
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Initializing connection to: {Configuration.SyncServer + "/" + SyncServerThread}", CurrentEvent);
                if (WebSocket != null && WebSocket.ReadyState == WebSocketState.Open)
                    WebSocket.Close();
                WebSocket = new WebSocket(Configuration.SyncServer + "/" + SyncServerThread);

                if (SyncServerThread == "main")
                {
                    WebSocket.OnMessage += Ws_OnMessageMain;
                    WebSocket.OnOpen += WebSocket_OnOpenMain;
                    WebSocket.OnClose += WebSocket_OnCloseMain;
                }
                else
                {
                    WebSocket.OnMessage += Ws_OnMessageChannel;
                    WebSocket.OnOpen += WebSocket_OnOpenChannel;
                    WebSocket.OnClose += WebSocket_OnCloseChannel;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while initializing: {ex}", CurrentEvent);
            }
        }

        public static void Test()
        {
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Testing connection to server", CurrentEvent);
            SyncServerThread = "main";
            Connect();
            CreateMessage(SyncMessages.Test);
        }

        public static void Connect()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(ActiveChannel) && Connected)
                    Disconnect();

                InitializeWebSocket();
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Connecting to server", CurrentEvent);

                ActiveChannel = Configuration.SyncChannel;
                WebSocket.Connect();
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while connecting: {ex}", CurrentEvent);
            }
        }

        public static void Disconnect(bool silent = false)
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Disconnecting from server", CurrentEvent);

                if (!silent && Connected)
                {
                    WebSocket.Close();
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Disconnected from channel: {ActiveChannel}", CurrentEvent);
                }
                else
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Not connected: {WebSocket.ReadyState}", CurrentEvent);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while disconnecting: {ex}", CurrentEvent);
            }
        }

        public static void RequestChannel(SyncMessages message, string channel)
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Sending '{message.ToString()}' for channel '{channel}'", CurrentEvent);

                var bodyString = $"{((int)message)}|{channel}";
                WebSocket.Send(bodyString);
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Sent '{message.ToString()}' to main", CurrentEvent);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while sending message: {ex}", CurrentEvent);
            }
        }

        public static void CreateMessage(SyncMessages message, string extra = "", string extra2 = "")
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Sending '{message.ToString()}' to channel: {ActiveChannel}", CurrentEvent);
                if (!Connected)
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Not connected: {WebSocket.ReadyState}", CurrentEvent);
                    return;
                }

                extra = !string.IsNullOrWhiteSpace(extra) ? "|" + extra : "";
                extra2 = !string.IsNullOrWhiteSpace(extra2) ? "|" + extra2 : "";

                var bodyString = $"{((int)message)}{extra}{extra2}";
                WebSocket.Send(bodyString);
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Sent '{message.ToString()}' to channel: {ActiveChannel}", CurrentEvent);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while sending message: {ex}", CurrentEvent);
            }
        }

        private static void Ws_OnMessageMain(object? sender, MessageEventArgs e)
        {
            var bodyString = "";
            var eventId = CurrentEvent;
            try
            {
                var textMessage = e.Data;
                var messageEnum = (SyncMessages)Convert.ToInt32(textMessage);

                switch (messageEnum)
                {
                    case SyncMessages.CreateChannel:
                        LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Server created channel", CurrentEvent);
                        SyncServerThread = ActiveChannel;
                        Connect();
                        break;
                    case SyncMessages.Test:
                        LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Received command '{messageEnum}'", eventId);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Received illegal message '{bodyString}' from main: {ex}", CurrentEvent);
            }
        }

        private static void WebSocket_OnOpenMain(object? sender, EventArgs e)
        {
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Connected to main server", CurrentEvent);
            RequestChannel(SyncMessages.CreateChannel, ActiveChannel);
        }

        private static void WebSocket_OnCloseMain(object? sender, EventArgs e)
        {
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Disconnected from main server", CurrentEvent);
        }

        private static void Ws_OnMessageChannel(object? sender, MessageEventArgs e)
        {
            var bodyString = "";
            try
            {
                var textMessage = e.Data;
                var messageEnum = (SyncMessages)Convert.ToInt32(textMessage);

                switch (messageEnum)
                {
                    case SyncMessages.ClickDone:
                        AllReady = true;
                        LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Received command '{messageEnum}' in channel '{ActiveChannel}'", CurrentEvent);
                        break;
                    case SyncMessages.ServerShutdown:
                        LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Received command '{messageEnum}' in channel '{ActiveChannel}'", CurrentEvent);
                        Disconnect(true);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Received illegal message '{bodyString}' from channel: {ActiveChannel}: {ex}", CurrentEvent);
            }
        }

        private static void WebSocket_OnOpenChannel(object? sender, EventArgs e)
        {
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Connected to channel '{ActiveChannel}'", CurrentEvent);
            Connected = true;
        }

        private static void WebSocket_OnCloseChannel(object? sender, EventArgs e)
        {
            LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Disconnected from channel '{ActiveChannel}'", CurrentEvent);
            SyncServerThread = "main";
            Connected = false;
        }

        public static void Dispose()
        {
            Disconnect();
        }
    }
}
