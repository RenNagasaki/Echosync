using Dalamud.Plugin.Services;
using Echosync.DataClasses;
using Echosync.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using Echosync_Data.Enums;
using WebSocketSharp.NetCore;

namespace Echosync.Helper
{
    public static class SyncClientHelper
    {
        public static bool Connected;
        public static bool AllReady;
        public static int ConnectedPlayersDialogue;
        public static readonly List<uint> ConnectedPlayers = [];
        public static readonly List<uint> ConnectedPlayersNpc = [];
        public static int ConnectedPlayersReady { get; set; }
        private static Configuration? _configuration;
        private static IClientState? _clientState;
        private static WebSocket? _webSocket;
        private static string _activeChannel = "";
        private static string _entityId = "";
        private static EKEventId? _currentEvent;
        private static string _syncServerThread = "main";

        public static EKEventId? CurrentEvent
        {
            get => _currentEvent ?? new EKEventId(0, TextSource.Sync);
            set => _currentEvent = value;
        }

        public static void Setup(Configuration configuration, IClientState clientState)
        {
            _configuration = configuration;
            _clientState = clientState;
            _entityId = _clientState.LocalPlayer!.EntityId.ToString();
            try
            {
                if (configuration is { ConnectAtStart: true, Enabled: true })
                {
                    Connect();
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod()!.Name, $"Error while starting: {ex}", CurrentEvent!);
            }
        }

        private static void InitializeWebSocket()
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Initializing connection to: {_configuration!.SyncServer + "/" + _syncServerThread}", CurrentEvent!);
                if (_webSocket is { ReadyState: WebSocketState.Open })
                    _webSocket.Close();
                _webSocket = new WebSocket(_configuration.SyncServer + "/" + _syncServerThread);

                if (_syncServerThread == "main")
                {
                    _webSocket.OnMessage += Ws_OnMessageMain;
                    _webSocket.OnOpen += WebSocket_OnOpenMain;
                    _webSocket.OnClose += WebSocket_OnCloseMain;
                }
                else
                {
                    _webSocket.OnMessage += Ws_OnMessageChannel;
                    _webSocket.OnOpen += WebSocket_OnOpenChannel;
                    _webSocket.OnClose += WebSocket_OnCloseChannel;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod()!.Name, $"Error while initializing: {ex}", CurrentEvent!);
            }
        }

        public static void Test()
        {
            LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Testing connection to server", CurrentEvent!);
            _syncServerThread = "main";
            Connect();
            CreateMessage(SyncMessages.Test);
        }

        public static void Connect()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_activeChannel) && Connected)
                    Disconnect();

                InitializeWebSocket();
                LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Connecting to server", CurrentEvent!);

                _activeChannel = _configuration!.SyncChannel;
                _webSocket!.Connect();
                Authenticate(_configuration.SyncPassword, _entityId);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod()!.Name, $"Error while connecting: {ex}", CurrentEvent!);
            }
        }

        public static void Disconnect(bool silent = false)
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Disconnecting from server", CurrentEvent!);

                if (!silent && Connected)
                {
                    _webSocket!.Close();
                    LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Disconnected from channel: {_activeChannel}", CurrentEvent!);
                }
                else
                    LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Not connected: {_webSocket!.ReadyState}", CurrentEvent!);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod()!.Name, $"Error while disconnecting: {ex}", CurrentEvent!);
            }
        }

        private static void RequestChannel(SyncMessages message, string channel, string password)
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Sending '{message.ToString()}' for channel '{channel}'", CurrentEvent!);

                var bodyString = $"{((int)message)}|{channel}|{password}";
                _webSocket!.Send(bodyString);
                LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Sent '{message.ToString()}' to main", CurrentEvent!);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod()!.Name, $"Error while sending message: {ex}", CurrentEvent!);
            }
        }

        private static void Authenticate(string password, string networkId)
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Sending '{SyncMessages.Authenticate.ToString()}' to channel: {_activeChannel}", CurrentEvent!);
                if (!Connected)
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Not connected: {_webSocket!.ReadyState}", CurrentEvent!);
                    return;
                }

                var localPlayer = _clientState!.LocalPlayer;
                var worldId = localPlayer?.HomeWorld.Value.Name;
                var characterName = "|" + (localPlayer?.Name.TextValue + "@" + worldId) ?? "TEST";
                var bodyString = $"{((int)SyncMessages.Authenticate)}{characterName}|{password}|{networkId}";
                _webSocket!.Send(bodyString);
                LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Sent '{SyncMessages.Authenticate.ToString()}' to channel: {_activeChannel}", CurrentEvent!);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod()!.Name, $"Error while sending message: {ex}", CurrentEvent!);
            }
        }

        public static void CreateMessage(SyncMessages message)
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Sending '{message.ToString()}' to channel: {_activeChannel}", CurrentEvent!);
                if (!Connected)
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Not connected: {_webSocket!.ReadyState}", CurrentEvent!);
                    return;
                }

                var bodyString = $"{((int)message)}";
                if (message == SyncMessages.StartNpc)
                {
                    var npcId = !string.IsNullOrWhiteSpace(AddonTalkHelper.ActiveNpcId) ? "|" + AddonTalkHelper.ActiveNpcId : "";
                    bodyString = $"{((int)message)}{npcId}";
                }

                _webSocket!.Send(bodyString);
                LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Sent '{message.ToString()}' to channel: {_activeChannel}", CurrentEvent!);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod()!.Name, $"Error while sending message: {ex}", CurrentEvent!);
            }
        }

        public static void CreateMessageFake(SyncMessages message, string? dialogue = "")
        {
            try
            {
                LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Sending '{message.ToString()}' to channel: {_activeChannel}", CurrentEvent!);
                if (!Connected)
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Not connected: {_webSocket!.ReadyState}", CurrentEvent!);
                    return;
                }

                var bodyString = $"{((int)message)}";
                if (message == SyncMessages.StartNpc)
                {
                    var npcId = !string.IsNullOrWhiteSpace(AddonTalkHelper.ActiveNpcId) ? "|" + AddonTalkHelper.ActiveNpcId : "";
                    bodyString = $"{((int)message)}{npcId}";
                }

                _webSocket!.Send(bodyString);
                LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Sent '{message.ToString()}' to channel: {_activeChannel}", CurrentEvent!);
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod()!.Name, $"Error while sending message: {ex}", CurrentEvent!);
            }
        }

        private static void Ws_OnMessageMain(object? sender, MessageEventArgs e)
        {
            var eventId = CurrentEvent;
            var textMessage = e.Data;
            try
            {
                var messageEnum = (SyncMessages)Convert.ToInt32(textMessage);

                switch (messageEnum)
                {
                    case SyncMessages.CreateChannel:
                        LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Server created channel", CurrentEvent!);
                        _syncServerThread = _activeChannel;
                        Connect();
                        break;
                    case SyncMessages.Test:
                        LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Received command '{messageEnum}'", eventId!);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod()!.Name, $"Received illegal message '{textMessage}' from main: {ex}", CurrentEvent!);
            }
        }

        private static void WebSocket_OnOpenMain(object? sender, EventArgs e)
        {
            LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Connected to main server", CurrentEvent!);
            RequestChannel(SyncMessages.CreateChannel, _activeChannel, _configuration!.SyncPassword);
        }

        private static void WebSocket_OnCloseMain(object? sender, EventArgs e)
        {
            LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Disconnected from main server", CurrentEvent!);
        }

        private static void Ws_OnMessageChannel(object? sender, MessageEventArgs e)
        {
            var textMessage = e.Data;
            try
            {
                var messageSplit = textMessage.Split('|');
                var messageEnum = (SyncMessages)Convert.ToInt32(messageSplit[0]);

                switch (messageEnum)
                {
                    case SyncMessages.ConnectedPlayersChannel:
                        LogHelper.Debug(MethodBase.GetCurrentMethod()!.Name, $"Received message '{messageEnum}' in channel '{_activeChannel}'", CurrentEvent!);
                        ConnectedPlayers.Clear();
                        for (int i = 1; i < messageSplit.Length; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(messageSplit[i]))
                                ConnectedPlayers.Add(Convert.ToUInt32(messageSplit[i]));
                        }
                        break;
                    case SyncMessages.ConnectedPlayersNpc:
                        LogHelper.Debug(MethodBase.GetCurrentMethod()!.Name, $"Received message '{messageEnum}' in channel '{_activeChannel}'", CurrentEvent!);
                        ConnectedPlayersNpc.Clear();
                        for (int i = 1; i < messageSplit.Length; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(messageSplit[i]))
                                ConnectedPlayersNpc.Add(Convert.ToUInt32(messageSplit[i]));
                        }
                        break;
                    case SyncMessages.RequestAuthentication:
                        LogHelper.Debug(MethodBase.GetCurrentMethod()!.Name, $"Received message '{messageEnum}' in channel '{_activeChannel}'", CurrentEvent!);
                        Authenticate(_configuration!.SyncPassword, _entityId);
                        break;
                    case SyncMessages.ClickDone:
                        AllReady = true;
                        LogHelper.Debug(MethodBase.GetCurrentMethod()!.Name, $"Received message '{messageEnum}' in channel '{_activeChannel}'", CurrentEvent!);
                        break;
                    case SyncMessages.ClickWait:
                        LogHelper.Debug(MethodBase.GetCurrentMethod()!.Name, $"Received message '{messageEnum}' in channel '{_activeChannel}'", CurrentEvent!);
                        LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Waiting for other users", CurrentEvent!);
                        break;
                    case SyncMessages.ClickWaitCatchup:
                        LogHelper.Debug(MethodBase.GetCurrentMethod()!.Name, $"Received message '{messageEnum}' in channel '{_activeChannel}'", CurrentEvent!);
                        LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Waiting for other users to catch up", CurrentEvent!);
                        break;
                    case SyncMessages.ConnectedPlayersDialogue:
                        ConnectedPlayersDialogue = Convert.ToInt32(messageSplit[1]);
                        LogHelper.Debug(MethodBase.GetCurrentMethod()!.Name, $"Received message '{messageEnum} - {ConnectedPlayersDialogue}' in channel '{_activeChannel}'", CurrentEvent!);
                        break;
                    case SyncMessages.ConnectedPlayersReady:
                        ConnectedPlayersReady = Convert.ToInt32(messageSplit[1]);
                        LogHelper.Debug(MethodBase.GetCurrentMethod()!.Name, $"Received message '{messageEnum} - {ConnectedPlayersReady}' in channel '{_activeChannel}'", CurrentEvent!);
                        break;
                    case SyncMessages.ServerShutdown:
                        LogHelper.Debug(MethodBase.GetCurrentMethod()!.Name, $"Received message '{messageEnum}' in channel '{_activeChannel}'", CurrentEvent!);
                        Disconnect(true);
                        break;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(MethodBase.GetCurrentMethod()!.Name, $"Received illegal message '{textMessage}' from channel: {_activeChannel}: {ex}", CurrentEvent!);
            }
        }

        private static void WebSocket_OnOpenChannel(object? sender, EventArgs e)
        {
            LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Connected to channel '{_activeChannel}'", CurrentEvent!);
            Connected = true;
        }

        private static void WebSocket_OnCloseChannel(object? sender, EventArgs e)
        {
            LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Disconnected from channel '{_activeChannel}'", CurrentEvent!);
            _syncServerThread = "main";
            Connected = false;
        }

        public static void Dispose()
        {
            Disconnect();
        }
    }
}