using System.Collections.Concurrent;
using System.Reflection;
using Echosync.Data.Enums;
using Echosync.Server.Helper;
using WebSocketSharp.NetCore;
using WebSocketSharp.NetCore.Server;

namespace Echosync.Server.Behaviours;

public class EchosyncBehaviour : WebSocketBehavior
{
    private HttpServer? _server;
    public static readonly ConcurrentDictionary<string, ChannelState> ChannelStates = new();

    public void Setup(HttpServer server) => _server = server;

    protected override void OnOpen()
    {
        try
        {
            LogHelper.Log("Main", $"Client with guid '{ID}' connected to main service!");
            UpdateTitle();
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
            UpdateTitle();
        }
        catch (Exception ex)
        {
            LogHelper.Log("Main", $"Error while client '{ID}' disconnected from main service: {ex}", true);
        }

        base.OnClose(e);
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        try
        {
            var messageSplit = e.Data.Split('|');
            var messageEnum = (SyncMessages)Convert.ToInt32(messageSplit[0]);

            LogHelper.Log("Main", $"Message received: '{messageEnum}' from '{Context.UserEndPoint.Address}'");

            switch (messageEnum)
            {
                case SyncMessages.CreateChannel:
                    var channel = messageSplit[1];
                    var password = messageSplit[2];

                    if (_server!.WebSocketServices.Hosts.All(p => p.Path != $"/{channel}"))
                    {
                        var channelState = ChannelStates.GetOrAdd(channel, _ => new ChannelState());
                        _server.WebSocketServices.AddService<EchosyncChannelBehaviour>(
                            $"/{channel}", t => t.Setup(_server, channel, password, channelState));
                        LogHelper.Log("Main", $"User '{Context.UserEndPoint.Address}' created channel '{channel}'");
                    }
                    else
                    {
                        LogHelper.Log("Main", $"User '{Context.UserEndPoint.Address}' requested existing channel '{channel}'");
                    }

                    Send($"{(int)SyncMessages.CreateChannel}");
                    break;

                case SyncMessages.Test:
                    break;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Log("Main", $"Illegal message from '{Context.UserEndPoint.Address}': {ex}", true);
        }
    }

    private void UpdateTitle()
    {
        Console.Title = $"Channels: {_server!.WebSocketServices.Count - 1} | Users: {_server.WebSocketServices.Count} | v.{Assembly.GetEntryAssembly()!.GetName().Version}";
    }
}
