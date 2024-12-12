using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp.Server;
using WebSocketSharp;
using Echosync_Data.Enums;
using Echosync_Server.Helper;

namespace Echosync_Server.Behaviours
{
    public class EchosyncChannelBehaviour : WebSocketBehavior
    {
        protected static Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, bool>>>> UsersReadyState = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, bool>>>>();
        protected static Dictionary<string, Dictionary<string, List<string>>> UsersDialogNpc = new Dictionary<string, Dictionary<string, List<string>>>();
        protected static Dictionary<string, Dictionary<string, Dictionary<string, int>>> UsersDialogNpcCount = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();
        protected HttpServer _server;
        protected string _channelName;

        public EchosyncChannelBehaviour(HttpServer server, string channelName)
        {
            _server = server;
            _channelName = channelName;

            UsersReadyState.TryAdd(channelName, new Dictionary<string, Dictionary<string, Dictionary<string, bool>>>());
            UsersDialogNpc.TryAdd(channelName, new Dictionary<string, List<string>>());
            UsersDialogNpcCount.TryAdd(channelName, new Dictionary<string, Dictionary<string, int>>());
        }

        protected override void OnOpen()
        {
            try
            {
                LogHelper.Log(_channelName, $"Client with guid '{ID}' connected!");
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

                if (Sessions.Count == 0)
                {
                    _server.RemoveWebSocketService($"/{_channelName}");
                    LogHelper.Log(_channelName, $"Last client disconnected, closing channel!");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log(_channelName, $"Error while client '{ID}' disconnected: {ex}", true);
            }

            base.OnClose(e);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var clientID = ID;
            var message = e.Data;
            var userName = "";
            try
            {
                var messageSplit = message.Split('|');
                var messageEnum = (SyncMessages)Convert.ToInt32(messageSplit[0]);

                switch (messageEnum)
                {
                    case SyncMessages.StartNpc:
                        userName = messageSplit[1];
                        var npcId = messageSplit[2];
                        LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{userName}/{clientID}' for npc '{npcId}'");

                        UsersDialogNpc[_channelName].TryAdd(npcId, new List<string>());
                        if (!UsersDialogNpc[_channelName][npcId].Contains(userName))
                            UsersDialogNpc[_channelName][npcId].Add(userName);
                        UsersDialogNpcCount[_channelName].TryAdd(npcId, new Dictionary<string, int>());
                        UsersDialogNpcCount[_channelName][npcId].TryAdd(userName, 0);
                        UsersDialogNpcCount[_channelName][npcId][userName] = 0;
                        break;
                    case SyncMessages.EndNpc:
                        userName = messageSplit[1];
                        npcId = messageSplit[2];
                        LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{userName}/{clientID}' for npc '{npcId}'");

                        UsersReadyState[_channelName][npcId].Values.ToList().ForEach(x => x.Remove(userName));
                        UsersDialogNpc[_channelName][npcId].Remove(userName);
                        UsersDialogNpcCount[_channelName].Remove(userName);
                        if (UsersDialogNpc[_channelName][npcId].Count == 0)
                            UsersDialogNpc[_channelName].Remove(npcId);
                        break;
                    case SyncMessages.JoinDialogue:
                        userName = messageSplit[1];
                        npcId = messageSplit[2];
                        var dialogue = messageSplit[3];
                        LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{userName}/{clientID}' for dialogue '{dialogue}'");

                        UsersDialogNpcCount[_channelName][npcId][userName] += 1;
                        UsersReadyState[_channelName].TryAdd(npcId, new Dictionary<string, Dictionary<string, bool>>());
                        UsersReadyState[_channelName][npcId].TryAdd(dialogue, new Dictionary<string, bool>());
                        UsersReadyState[_channelName][npcId][dialogue].TryAdd(userName, false);
                        Sessions.Broadcast($"{(int)SyncMessages.ConnectedPlayersDialogue}|{npcId}|{dialogue}|{UsersReadyState[_channelName][npcId][dialogue].Count}");
                        Sessions.Broadcast($"{(int)SyncMessages.ConnectedPlayersReady}|{npcId}|{dialogue}|{UsersReadyState[_channelName][npcId][dialogue].Values.ToList().FindAll(p => p == true).Count}");
                        break;
                    case SyncMessages.Click:
                        userName = messageSplit[1];
                        npcId = messageSplit[2];
                        dialogue = messageSplit[3];
                        LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{userName}/{clientID}'");
                        LogHelper.Log(_channelName, $"User '{userName}/{clientID}' in channel '{_channelName}' is ready");
                        if (!UserClick(clientID, userName, npcId, dialogue))
                        {
                            Sessions.Broadcast($"{(int)SyncMessages.ConnectedPlayersDialogue}|{npcId}|{dialogue}|{UsersReadyState[_channelName][npcId][dialogue].Count}");
                            Sessions.Broadcast($"{(int)SyncMessages.ConnectedPlayersReady}|{npcId}|{dialogue}|{UsersReadyState[_channelName][npcId][dialogue].Values.ToList().FindAll(p => p == true).Count}");
                        }
                        break;
                    case SyncMessages.ClickForce:
                        userName = messageSplit[1];
                        npcId = messageSplit[2];
                        dialogue = messageSplit[3];
                        LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{userName}/{clientID}'");
                        LogHelper.Log(_channelName, $"User '{userName}/{clientID}' is forcing click for itself");
                        if (!UserClick(clientID, userName, npcId, dialogue))
                        {
                            Send($"{(int)SyncMessages.ClickDone}");
                            UsersReadyState[_channelName][npcId][dialogue].Remove(userName);
                            if (UsersReadyState[_channelName][npcId][dialogue].Count == 0)
                                UsersReadyState[_channelName][npcId].Remove(dialogue);
                            else
                            {
                                Sessions.Broadcast($"{(int)SyncMessages.ConnectedPlayersDialogue}|{npcId}|{dialogue}|{UsersReadyState[_channelName][npcId][dialogue].Count}");
                                Sessions.Broadcast($"{(int)SyncMessages.ConnectedPlayersReady}|{npcId}|{dialogue}|{UsersReadyState[_channelName][npcId][dialogue].Values.ToList().FindAll(p => p == true).Count}");
                            }
                        }
                        break;
                    case SyncMessages.Test:
                        LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{userName}/{clientID}'");
                        Send($"{(int)SyncMessages.Test}");
                        break;
                }

            }
            catch (Exception ex)
            {
                LogHelper.Log(_channelName, $"Illegal message from '{userName}/{clientID}' message: '{message}' Exception: {ex}", true);
            }
        }

        protected bool UserClick(string clientID, string userName, string npcId, string dialogue)
        {
            UsersReadyState[_channelName][npcId][dialogue][userName] = true;
            var allReady = true;
            foreach (var dialogUser in UsersReadyState[_channelName][npcId][dialogue])
            {
                if (!dialogUser.Value)
                {
                    allReady = false;
                    break;
                }
            }

            if (allReady)
            {
                var userCount = UsersDialogNpcCount[_channelName][npcId][userName];
                if (UsersDialogNpcCount[_channelName][npcId].Values.ToList().FindIndex(p => p < userCount) >= 0)
                {
                    LogHelper.Log(_channelName, $"User '{userName}/{clientID}' is waiting for catchup");
                    Send($"{(int)SyncMessages.ClickWaitCatchup}");
                    return false;
                }
            }

            if (allReady)
            {
                LogHelper.Log(_channelName, $"All users in channel for dialogue '{dialogue}' are ready sending advance command'");
                Sessions.Broadcast($"{(int)SyncMessages.ClickDone}");
                UsersReadyState[_channelName][npcId].Remove(dialogue);
            }
            else
            {
                LogHelper.Log(_channelName, $"User '{userName}/{clientID}' is waiting for the others");
                Send($"{(int)SyncMessages.ClickWait}");
            }

            return allReady;
        }
    }
}
