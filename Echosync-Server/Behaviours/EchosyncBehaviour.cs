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
    public class EchosyncBehaviour : WebSocketBehavior
    {
        private HttpServer _server;

        public EchosyncBehaviour(HttpServer server)
        {
            _server = server;
        }

        protected override void OnOpen()
        {
            try
            {
                LogHelper.Log("Main", $"Client with guid '{ID}' connected to main service!");
            }
            catch (Exception ex)
            {
                LogHelper.Log("Main", $"Error while client '{ID}' connected to main service: {ex}", true);
            }

            base.OnOpen();
        }

        protected override void OnClose(CloseEventArgs e)
        {
            try
            {
                LogHelper.Log("Main", $"Client with guid '{ID}' disconnected from main service!");
            }
            catch (Exception ex)
            {
                LogHelper.Log("Main", $"Error while client '{ID}' disconnected from main service: {ex}", true);
            }

            base.OnClose(e);
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            var clientID = ID;
            try
            {
                var message = e.Data;
                var messageSplit = message.Split('|');
                var messageEnum = (SyncMessages)Convert.ToInt32(messageSplit[0]);

                LogHelper.Log("Main", $"Message received: '{messageEnum}' from '{clientID}'");
                switch (messageEnum)
                {
                    case SyncMessages.CreateChannel:
                        var channel = messageSplit[1];
                        var password = messageSplit[2];
                        if (_server.WebSocketServices.Hosts.ToList().Find(p => p.Path == $"/{channel}") == null)
                        {
                            _server.AddWebSocketService($"/{channel}", () => new EchosyncChannelBehaviour(_server, channel, password));
                            LogHelper.Log("Main", $"User '{clientID}' created channel '{channel}'");
                        }
                        else
                            LogHelper.Log("Main", $"User '{clientID}' requested existing channel '{channel}'");
                        Send($"{(int)SyncMessages.CreateChannel}");
                        break;
                    case SyncMessages.Test:
                        break;
                }

            }
            catch (Exception ex)
            {
                LogHelper.Log("Main", $"Illegal message from '{clientID}': {ex}", true);
            }
        }
    }
}
