using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp.Server;
using WebSocketSharp;
using Echosync_Data.Enums;
using Echosync_Server.Helper;
using System.Reflection;

namespace Echosync_Server.Behaviours
{
    public class EchosyncChannelBehaviour : WebSocketBehavior
    {
        protected static List<UserState> UserStates = new List<UserState>();
        protected HttpServer _server;
        protected string _channelName;
        protected string _password;
        protected UserState _userState;

        public EchosyncChannelBehaviour(HttpServer server, string channelName, string password)
        {
            _server = server;
            _channelName = channelName;
            _password = password;
        }

        protected override void OnOpen()
        {
            try
            {
                LogHelper.Log(_channelName, $"Client with guid '{ID}' connected");
                Console.Title = $"Channels: {_server.WebSocketServices.Count - 1} | Users: {_server.WebSocketServices.SessionCount} | v.{Assembly.GetEntryAssembly().GetName().Version}";
            }
            catch (Exception ex)
            {
                LogHelper.Log(_channelName, $"Error while client '{ID}' connected: {ex}");
            }

            base.OnOpen();
        }

        protected override void OnClose(CloseEventArgs e)
        {
            try
            {
                LogHelper.Log(_channelName, $"Client with guid '{ID}' disconnected!");
                UserStates.Remove(_userState);

                if (Sessions.Count == 0)
                {
                    LogHelper.Log(_channelName, $"Last client disconnected, closing channel!");
                    _server.RemoveWebSocketService($"/{_channelName}");
                }
                Console.Title = $"Channels: {_server.WebSocketServices.Count - 1} | Users: {_server.WebSocketServices.SessionCount} | v.{Assembly.GetEntryAssembly().GetName().Version}";
            }
            catch (Exception ex)
            {
                LogHelper.Log(_channelName, $"Error while client '{ID}' disconnected: {ex}", true);
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

                if (messageEnum != SyncMessages.Authenticate && _userState == null)
                {
                    Sessions.CloseSession(ID, CloseStatusCode.PolicyViolation, "Not authenticated");
                    return;
                }

                switch (messageEnum)
                {
                    case SyncMessages.Authenticate:
                        var userName = messageSplit[1];
                        var password = messageSplit[2];
                        var networkId = messageSplit[3];
                        LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{userName}/{ID}'");

                        if (password == _password)
                        {
                            LogHelper.Log(_channelName, $"User '{userName}/{ID}' succesfully authenticated");
                            _userState = new UserState(ID, networkId, Context.UserEndPoint.Address.ToString(), userName, _channelName);
                            UserStates.Add(_userState);

                            SendConnectedUsers();
                        }
                        else
                        {
                            LogHelper.Log(_channelName, $"User '{userName}/{ID}' failed to authenticate");
                            Sessions.CloseSession(ID, CloseStatusCode.PolicyViolation, "Wrong password");
                        }
                        break;
                    case SyncMessages.StartNpc:
                        var npcId = messageSplit[1];
                        _userState.NpcId = npcId;
                        _userState.DialogueCount = 0;
                        _userState.Ready = false;
                        LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{_userState.UserName}/{_userState.WebSocketId}' for npc '{_userState.NpcId}'");
                        SendNpcState(_userState.NpcId, _userState.DialogueCount);
                        break;
                    case SyncMessages.EndNpc:
                        LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{_userState.UserName}/{_userState.WebSocketId}' for npc '{_userState.NpcId}'");

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
                        LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{_userState.UserName}/{ID}'");

                        _userState.Ready = false;
                        _userState.DialogueCount++;

                        SendNpcState(_userState.NpcId, _userState.DialogueCount);
                        break;
                    case SyncMessages.Click:
                        LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{_userState.UserName}/{ID}'");
                        LogHelper.Log(_channelName, $"User '{_userState.UserName}/{ID}' in channel '{_userState.Channel}' is ready");
                        UserClick();
                        break;
                    case SyncMessages.ClickForce:
                        LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{_userState.UserName}/{ID}'");
                        LogHelper.Log(_channelName, $"User '{_userState.UserName}/{ID}' is forcing click for itself");
                        if (!UserClick())
                        {
                            Send($"{(int)SyncMessages.ClickDone}");
                        }
                        break;
                    case SyncMessages.Test:
                        LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{_userState.UserName}/{ID}'");
                        Send($"{(int)SyncMessages.Test}");
                        break;
                }

            }
            catch (Exception ex)
            {
                LogHelper.Log(_channelName, $"Illegal message from '{_userState.UserName}/{ID}' message: '{message}' Exception: {ex}", true);
            }
        }

        protected void SendNpcState(string npcId, int dialogueCount)
        {
            LogHelper.Log(_channelName, $"Sending dialogue state from channel: {_channelName} for NPC: {npcId}");
            var npcUsers = UserStates.FindAll(p => p.Channel == _channelName && p.NpcId == npcId);
            var dialogueUsers = npcUsers.FindAll(p => p.DialogueCount == dialogueCount);
            var dialogueUsersReady = dialogueUsers.FindAll(p => p.Ready);

            foreach (var npcUser in npcUsers)
            {
                IWebSocketSession session;
                if (Sessions.TryGetSession(npcUser.WebSocketId, out session))
                {
                    var networkIds = "";
                    npcUsers.ForEach(p => {
                        if (p != npcUser)
                            networkIds += p.NetworkId + "|";
                    });

                    if (!string.IsNullOrWhiteSpace(networkIds))
                        networkIds = networkIds.Substring(0, networkIds.Length - 1);

                    LogHelper.Log(_channelName, $"Sending connectedplayersnpc for channel: {_channelName} to user: {npcUser.UserName}/{npcUser.WebSocketId}\r\nData: {networkIds}");
                    session.Context.WebSocket.Send($"{(int)SyncMessages.ConnectedPlayersNpc}|{networkIds}");
                }
            }

            foreach (var dialogueUser in dialogueUsers)
            {
                IWebSocketSession session;
                if (Sessions.TryGetSession(dialogueUser.WebSocketId, out session))
                {
                    session.Context.WebSocket.Send($"{(int)SyncMessages.ConnectedPlayersDialogue}|{dialogueUsers.Count}");
                    session.Context.WebSocket.Send($"{(int)SyncMessages.ConnectedPlayersReady}|{dialogueUsers.FindAll(p => p.Ready).Count}");
                }
            }
        }

        protected void SendConnectedUsers()
        {
            LogHelper.Log(_channelName, $"Sending all connected and authenticated users for channel: {_channelName}");
            var connectedUsers = UserStates.FindAll(p => p.Channel == _channelName);
            foreach (var connectedUser in connectedUsers)
            {
                IWebSocketSession session;
                if (Sessions.TryGetSession(connectedUser.WebSocketId, out session))
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

        protected void SendClickDone(string npcId, int dialogueCount)
        {
            LogHelper.Log(_channelName, $"All users in channel for npc '{npcId}' are ready sending advance command'");
            var dialogueUsers = UserStates.FindAll(p => p.Channel == _channelName && p.DialogueCount == dialogueCount && p.NpcId == npcId);
            foreach (var dialogueUser in dialogueUsers)
            {
                IWebSocketSession session;
                if (Sessions.TryGetSession(dialogueUser.WebSocketId, out session))
                    session.Context.WebSocket.Send($"{(int)SyncMessages.ClickDone}");
            }
        }

        protected bool AllReady()
        {
            var usersNotReady = UserStates.FindAll(p => p.Channel == _userState.Channel && p.NpcId == _userState.NpcId && p.DialogueCount == _userState.DialogueCount && !p.Ready);

            return usersNotReady.Count == 0;
        }

        protected bool UserClick()
        {
            _userState.Ready = true;
            SendNpcState(_userState.NpcId, _userState.DialogueCount);
            var allReady = AllReady();

            if (allReady)
            {
                if (UserStates.FindAll(p => p.Channel == _userState.Channel && p.NpcId == _userState.NpcId && p.DialogueCount < _userState.DialogueCount).Count > 0)
                {
                    LogHelper.Log(_channelName, $"User '{_userState.UserName}/{ID}' is waiting for catchup");
                    Send($"{(int)SyncMessages.ClickWaitCatchup}");
                    return false;
                }

                SendClickDone(_userState.NpcId, _userState.DialogueCount);
            }
            else
            {
                LogHelper.Log(_channelName, $"User '{_userState.UserName}/{ID}' is waiting for the others");
                Send($"{(int)SyncMessages.ClickWait}");
            }

            return allReady;
        }
    }
}
