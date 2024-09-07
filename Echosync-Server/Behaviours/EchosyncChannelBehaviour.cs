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
        public static Dictionary<string, Dictionary<string, Dictionary<string, bool>>> UsersReadyState = new Dictionary<string, Dictionary<string, Dictionary<string, bool>>>();
        private HttpServer _server;
        private string _channelName;

        public EchosyncChannelBehaviour(HttpServer server, string channelName)
        {
            _server = server;
            _channelName = channelName;

            if (!UsersReadyState.ContainsKey(channelName))
                UsersReadyState.Add(channelName, new Dictionary<string, Dictionary<string, bool>>());
        }

        protected override void OnOpen()
        {
            try
            {
                LogHelper.Log($"Client with guid '{ID}' connected to channel '{_channelName}'!");
            }
            catch (Exception ex)
            {
                LogHelper.Log($"Error while client '{ID}' connected to channel '{_channelName}': {ex}");
            }

            base.OnOpen();
        }

        protected override void OnClose(CloseEventArgs e)
        {
            try
            {
                LogHelper.Log($"Client with guid '{ID}' disconnected from channel '{_channelName}'!");

                if (Sessions.Count == 0)
                {
                    _server.RemoveWebSocketService($"/{_channelName}");
                    LogHelper.Log($"Last client disconnected from channel '{_channelName}' closing channel!");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log($"Error while client '{ID}' disconnected from channel '{_channelName}': {ex}");
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
                    case SyncMessages.JoinDialogue:
                        userName = messageSplit[1];
                        var dialogue = messageSplit[2];
                        LogHelper.Log($"Message received: '{messageEnum}' from '{userName}/{clientID}' in channel '{_channelName}' for dialogue '{dialogue}'");

                        UsersReadyState[_channelName].TryAdd(dialogue, new Dictionary<string, bool>());
                        UsersReadyState[_channelName][dialogue].TryAdd(userName, false);
                        break;
                    case SyncMessages.Click:
                        userName = messageSplit[1];
                        dialogue = messageSplit[2];
                        UsersReadyState[_channelName][dialogue][userName] = true;
                        LogHelper.Log($"Message received: '{messageEnum}' from '{userName}/{clientID}' in channel '{_channelName}'");
                        LogHelper.Log($"User '{userName}/{clientID}' in channel '{_channelName}' is ready");
                        var allReady = true;
                        foreach (var dialogUser in UsersReadyState[_channelName][dialogue])
                        {
                            if (!dialogUser.Value)
                            {
                                allReady = false;
                                break;
                            }
                        }

                        if (allReady)
                        {
                            LogHelper.Log($"All users in channel '{_channelName}' for dialogue '{dialogue}' are ready sending advance command'");
                            Sessions.Broadcast($"{(int)SyncMessages.ClickDone}");
                            UsersReadyState[_channelName].Remove(dialogue);
                        }
                        break;
                    case SyncMessages.Test:
                        LogHelper.Log($"Message received: '{messageEnum}' from '{userName}/{clientID}' in channel '{_channelName}'");
                        Send($"{(int)SyncMessages.Test}");
                        break;
                }

            }
            catch (Exception ex)
            {
                LogHelper.Log($"Illegal message from '{userName}/{clientID}' message: '{message}' Exception: {ex}");
            }
        }
    }
}
