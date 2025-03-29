using Echosync_Data.Enums;
using Echosync_Server.Helper;
using System.Reflection;
using WebSocketSharp.NetCore;
using WebSocketSharp.NetCore.Server;

namespace Echosync_Server.Behaviours
{
    public class EchosyncChannelBehaviour : WebSocketBehavior
    {
        private static readonly List<UserState> UserStates = [];
        private UserState? _userState;
        private HttpServer? _server;
        private string _channelName = "";
        private string _password = "";

        public void Setup(HttpServer server, string channelName, string password)
        {
            _server = server;
            _channelName = channelName;
            _password = password;
        }

        protected override void OnOpen()
        {
            try
            {
                LogHelper.Log(_channelName, $"Client with guid '{Context.UserEndPoint.Address.ToString()}' connected");
                Console.Title = $"Channels: {_server!.WebSocketServices.Count - 1} | Users: {_server.WebSocketServices.Count} | v.{Assembly.GetEntryAssembly()!.GetName().Version}";
            }
            catch (Exception ex)
            {
                LogHelper.Log(_channelName, $"Error while client '{Context.UserEndPoint.Address.ToString()}' connected: {ex}");
            }

            base.OnOpen();
        }

        protected override void OnClose(CloseEventArgs e)
        {
            try
            {
                LogHelper.Log(_channelName, $"Client with guid '{Context.UserEndPoint.Address.ToString()}' disconnected!");

                if (_userState != null)
                {
                    UserStates.Remove(_userState);
                    SendNpcState(_userState.NpcId, _userState.DialogueCount);
                }

                if (Sessions.Count == 0)
                {
                    LogHelper.Log(_channelName, $"Last client disconnected, closing channel!");
                    _server!.RemoveWebSocketService($"/{_channelName}");
                }
                Console.Title = $"Channels: {_server!.WebSocketServices.Count - 1} | Users: {_server.WebSocketServices.Count} | v.{Assembly.GetEntryAssembly()!.GetName().Version}";
            }
            catch (Exception ex)
            {
                LogHelper.Log(_channelName, $"Error while client '{Context.UserEndPoint.Address.ToString()}' disconnected: {ex}", true);
            }

            base.OnClose(e);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var message = e.Data;
            try
            {
                var messageSplit = message.Split('|');
                var messageEnum = (SyncMessages)Convert.ToInt32(messageSplit[0]);
                LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{Context.UserEndPoint.Address.ToString()}'");

                if (messageEnum != SyncMessages.Authenticate && _userState == null)
                {
                    Sessions.CloseSession(ID, CloseStatusCode.PolicyViolation, "Not authenticated");
                    return;
                }

                switch (messageEnum)
                {
                    case SyncMessages.Authenticate:
                        var userName = messageSplit[1];
                        var password1 = messageSplit[2];
                        var networkId = messageSplit[3];

                        if (password1 == _password)
                        {
                            LogHelper.Log(_channelName, $"User '{Context.UserEndPoint.Address.ToString()}' succesfully authenticated");
                            _userState = new UserState(ID, networkId, Context.UserEndPoint.Address.ToString(), userName, _channelName);
                            UserStates.Add(_userState);

                            SendConnectedUsers();
                        }
                        else
                        {
                            LogHelper.Log(_channelName, $"User '{_userState?.IpAdress ?? Context.UserEndPoint.Address.ToString()}' failed to authenticate");
                            Sessions.CloseSession(ID, CloseStatusCode.PolicyViolation, "Wrong password");
                        }
                        break;
                    case SyncMessages.StartNpc:
                        var npcId = messageSplit[1];
                        _userState!.NpcId = npcId;
                        _userState.DialogueCount = 0;
                        _userState.Ready = false;
                        LogHelper.Log(_channelName, $"Message for npc '{_userState.NpcId}'");
                        SendNpcState(_userState.NpcId, _userState.DialogueCount);
                        break;
                    case SyncMessages.EndNpc:
                        LogHelper.Log(_channelName, $"Message for npc '{_userState!.NpcId}'");

                        npcId = _userState.NpcId;
                        var dialogueCount = _userState.DialogueCount;
                        _userState.NpcId = "";
                        _userState.DialogueCount = 0;
                        _userState.Ready = false;

                        SendNpcState(npcId, dialogueCount);
                        if (AllReady())
                            SendClickDone(npcId, dialogueCount);
                        break;
                    case SyncMessages.ClickSuccess:

                        _userState!.Ready = false;
                        _userState.DialogueCount++;

                        SendNpcState(_userState.NpcId, _userState.DialogueCount);
                        break;
                    case SyncMessages.Click:
                        LogHelper.Log(_channelName, $"User '{_userState!.IpAdress}' in channel '{_userState.Channel}' is ready");
                        UserClick();
                        break;
                    case SyncMessages.ClickForce:
                        LogHelper.Log(_channelName, $"User '{_userState!.IpAdress}' is forcing click for itself");
                        if (!UserClick())
                        {
                            Send($"{(int)SyncMessages.ClickDone}");
                        }
                        break;
                    case SyncMessages.Test:
                        Send($"{(int)SyncMessages.Test}");
                        break;
                }

            }
            catch (Exception ex)
            {
                LogHelper.Log(_channelName, $"Illegal message from '{_userState!.IpAdress}' message: '{message}' Exception: {ex}", true);
            }
        }

        private void SendNpcState(string npcId, int dialogueCount)
        {
            LogHelper.Log(_channelName, $"Sending dialogue state from channel: {_channelName} for NPC: {npcId}");
            var npcUsers = UserStates.FindAll(p => p.Channel == _channelName && p.NpcId == npcId);
            var dialogueUsers = npcUsers.FindAll(p => p.DialogueCount == dialogueCount);

            foreach (var npcUser in npcUsers)
            {
                if (!Sessions.TryGetSession(npcUser.WebSocketId, out var session)) continue;
                var networkIds = "";
                npcUsers.ForEach(p => {
                    if (p != npcUser)
                        networkIds += p.NetworkId + "|";
                });

                if (!string.IsNullOrWhiteSpace(networkIds))
                    networkIds = networkIds[..^1];

                LogHelper.Log(_channelName, $"Sending connectedplayersnpc for channel: {_channelName} to user: {npcUser.IpAdress}\r\nData: {networkIds}");
                session.Context.WebSocket.Send($"{(int)SyncMessages.ConnectedPlayersNpc}|{networkIds}");
            }

            foreach (var dialogueUser in dialogueUsers)
            {
                if (Sessions.TryGetSession(dialogueUser.WebSocketId, out var session))
                {
                    session.Context.WebSocket.Send($"{(int)SyncMessages.ConnectedPlayersDialogue}|{dialogueUsers.Count}");
                    session.Context.WebSocket.Send($"{(int)SyncMessages.ConnectedPlayersReady}|{dialogueUsers.FindAll(p => p.Ready).Count}");
                }
            }
        }

        private void SendConnectedUsers()
        {
            LogHelper.Log(_channelName, $"Sending all connected and authenticated users for channel: {_channelName}");
            var connectedUsers = UserStates.FindAll(p => p.Channel == _channelName);
            foreach (var connectedUser in connectedUsers)
            {
                if (Sessions.TryGetSession(connectedUser.WebSocketId, out var session))
                {
                    var networkIds = "";
                    connectedUsers.ForEach(p => {
                        if (p != connectedUser)
                            networkIds += p.NetworkId + "|";
                    });

                    if (!string.IsNullOrWhiteSpace(networkIds))
                        networkIds = networkIds.Substring(0, networkIds.Length - 1);

                    session.Context.WebSocket.Send($"{(int)SyncMessages.ConnectedPlayersChannel}|{networkIds}");
                }
            }
        }

        private void SendClickDone(string npcId, int dialogueCount)
        {
            LogHelper.Log(_channelName, $"All users in channel for npc '{npcId}' are ready sending advance command'");
            var dialogueUsers = UserStates.FindAll(p => p.Channel == _channelName && p.DialogueCount == dialogueCount && p.NpcId == npcId);
            foreach (var dialogueUser in dialogueUsers)
            {
                if (Sessions.TryGetSession(dialogueUser.WebSocketId, out var session))
                    session.Context.WebSocket.Send($"{(int)SyncMessages.ClickDone}");
            }
        }

        private bool AllReady()
        {
            var usersNotReady = UserStates.FindAll(p => p.Channel == _userState!.Channel && p.NpcId == _userState.NpcId && p.DialogueCount == _userState.DialogueCount && !p.Ready);

            return usersNotReady.Count == 0;
        }

        private bool UserClick()
        {
            _userState!.Ready = true;
            SendNpcState(_userState.NpcId, _userState.DialogueCount);
            var allReady = AllReady();

            if (allReady)
            {
                if (UserStates.FindAll(p => p.Channel == _userState.Channel && p.NpcId == _userState.NpcId && p.DialogueCount < _userState.DialogueCount).Count > 0)
                {
                    LogHelper.Log(_channelName, $"User '{_userState.IpAdress}' is waiting for catchup");
                    Send($"{(int)SyncMessages.ClickWaitCatchup}");
                    return false;
                }

                SendClickDone(_userState.NpcId, _userState.DialogueCount);
            }
            else
            {
                LogHelper.Log(_channelName, $"User '{_userState.IpAdress}' is waiting for the others");
                Send($"{(int)SyncMessages.ClickWait}");
            }

            return allReady;
        }
    }
}