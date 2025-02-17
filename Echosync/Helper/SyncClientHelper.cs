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
using static Dalamud.Interface.Utility.Raii.ImRaii;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Dalamud.Game.ClientState.Objects.Types;

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
        public static int ConnectedPlayersDialogue = 0;
        public static List<uint> ConnectedPlayers = new List<uint>();
        public static List<uint> ConnectedPlayersNpc = new List<uint>();
        public static int ConnectedPlayersReady = 0;
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
                if (configuration.ConnectAtStart && configuration.Enabled)
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
                Authenticate(Configuration.SyncPassword, EntityId);
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

        public static void RequestChannel(SyncMessages message, string channel, string password)
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Sending '{message.ToString()}' for channel '{channel}'", CurrentEvent);

                var bodyString = $"{((int)message)}|{channel}|{password}";
                WebSocket.Send(bodyString);
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Sent '{message.ToString()}' to main", CurrentEvent);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while sending message: {ex}", CurrentEvent);
            }
        }

        public static void Authenticate(string password, string networkId)
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Sending '{SyncMessages.Authenticate.ToString()}' to channel: {ActiveChannel}", CurrentEvent);
                if (!Connected)
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Not connected: {WebSocket.ReadyState}", CurrentEvent);
                    return;
                }

                var worldId = ClientState.LocalPlayer?.HomeWorld.Value.Name;
                var characterName = "|" + (ClientState.LocalPlayer?.Name.TextValue + "@" + worldId) ?? "TEST";
                var bodyString = $"{((int)SyncMessages.Authenticate)}{characterName}|{password}|{networkId}";
                WebSocket.Send(bodyString);
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Sent '{SyncMessages.Authenticate.ToString()}' to channel: {ActiveChannel}", CurrentEvent);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while sending message: {ex}", CurrentEvent);
            }
        }

        public static void CreateMessage(SyncMessages message)
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Sending '{message.ToString()}' to channel: {ActiveChannel}", CurrentEvent);
                if (!Connected)
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Not connected: {WebSocket.ReadyState}", CurrentEvent);
                    return;
                }

                var bodyString = "";
                if (message == SyncMessages.StartNpc)
                {
                    var npcId = !string.IsNullOrWhiteSpace(AddonTalkHelper.ActiveNpcId) ? "|" + AddonTalkHelper.ActiveNpcId : "";
                    bodyString = $"{((int)message)}{npcId}";
                }
                else
                    bodyString = $"{((int)message)}";

                WebSocket.Send(bodyString);
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Sent '{message.ToString()}' to channel: {ActiveChannel}", CurrentEvent);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Error while sending message: {ex}", CurrentEvent);
            }
        }

        public static void CreateMessageFake(SyncMessages message, string dialogue = "")
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Sending '{message.ToString()}' to channel: {ActiveChannel}", CurrentEvent);
                if (!Connected)
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Not connected: {WebSocket.ReadyState}", CurrentEvent);
                    return;
                }

                var bodyString = "";
                if (message == SyncMessages.StartNpc)
                {
                    var npcId = !string.IsNullOrWhiteSpace(AddonTalkHelper.ActiveNpcId) ? "|" + AddonTalkHelper.ActiveNpcId : "";
                    bodyString = $"{((int)message)}{npcId}";
                }
                else
                    bodyString = $"{((int)message)}";

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
            RequestChannel(SyncMessages.CreateChannel, ActiveChannel, Configuration.SyncPassword);
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
                var messageSplit = textMessage.Split('|');
                var messageEnum = (SyncMessages)Convert.ToInt32(messageSplit[0]);

                switch (messageEnum)
                {
                    case SyncMessages.ConnectedPlayersChannel:
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Received message '{messageEnum}' in channel '{ActiveChannel}'", CurrentEvent);
                        ConnectedPlayers.Clear();
                        for (int i = 1; i < messageSplit.Length; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(messageSplit[i]))
                                ConnectedPlayers.Add(Convert.ToUInt32(messageSplit[i]));
                        }
                        break;
                    case SyncMessages.ConnectedPlayersNpc:
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Received message '{messageEnum}' in channel '{ActiveChannel}'", CurrentEvent);
                        ConnectedPlayersNpc.Clear();
                        for (int i = 1; i < messageSplit.Length; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(messageSplit[i]))
                                ConnectedPlayersNpc.Add(Convert.ToUInt32(messageSplit[i]));
                        }
                        break;
                    case SyncMessages.RequestAuthentication:
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Received message '{messageEnum}' in channel '{ActiveChannel}'", CurrentEvent);
                        Authenticate(Configuration.SyncPassword, EntityId);
                        break;
                    case SyncMessages.ClickDone:
                        AllReady = true;
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Received message '{messageEnum}' in channel '{ActiveChannel}'", CurrentEvent);
                        break;
                    case SyncMessages.ClickWait:
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Received message '{messageEnum}' in channel '{ActiveChannel}'", CurrentEvent);
                        LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Waiting for other users", CurrentEvent);
                        break;
                    case SyncMessages.ClickWaitCatchup:
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Received message '{messageEnum}' in channel '{ActiveChannel}'", CurrentEvent);
                        LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Waiting for other users to catch up", CurrentEvent);
                        break;
                    case SyncMessages.ConnectedPlayersDialogue:
                        ConnectedPlayersDialogue = Convert.ToInt32(messageSplit[1]);
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Received message '{messageEnum} - {ConnectedPlayersDialogue}' in channel '{ActiveChannel}'", CurrentEvent);
                        break;
                    case SyncMessages.ConnectedPlayersReady:
                        ConnectedPlayersReady = Convert.ToInt32(messageSplit[1]);
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Received message '{messageEnum} - {ConnectedPlayersReady}' in channel '{ActiveChannel}'", CurrentEvent);
                        break;
                    case SyncMessages.ServerShutdown:
                        LogHelper.Debug(MethodBase.GetCurrentMethod().Name, $"Received message '{messageEnum}' in channel '{ActiveChannel}'", CurrentEvent);
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
