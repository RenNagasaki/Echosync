using System.Reflection;
using Echosync.Data.Enums;
using Echosync.Server;
using Echosync.Server.Behaviours;
using Echosync.Server.Helper;
using WebSocketSharp.NetCore.Server;

const int port = 2053;

LogHelper.Log("Main", $"Starting server with port '{port}'!");
var server = new HttpServer(port);

LogHelper.Log("Main", "Starting main thread!");
server.WebSocketServices.AddService<EchosyncBehaviour>("/main", t => t.Setup(server));
server.Start();

Console.Title = $"Channels: {server.WebSocketServices.Count - 1} | Users: {server.WebSocketServices.Count} | v.{Assembly.GetEntryAssembly()!.GetName().Version}";

BotUser? activeBot = null;

var command = "";
while (command != "quit")
{
    command = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(command)) continue;

    LogHelper.Log("Main", $"Command '{command}' entered");

    var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    switch (parts[0].ToLowerInvariant())
    {
        case "bot" when parts.Length >= 2:
            var channelName = parts[1];

            if (activeBot != null)
            {
                activeBot.Dispose(); 
                activeBot = null;
                LogHelper.Log("Main", "Previous bot removed");
            }

            if (EchosyncBehaviour.ChannelStates.TryGetValue(channelName, out var channelState))
            {
                var delayMs = parts.Length >= 3 && int.TryParse(parts[2], out var d) ? d : 500;
                activeBot = new BotUser(channelState, channelName, delayMs);
                LogHelper.Log("Main", $"Bot joined channel '{channelName}' with {delayMs}ms advance delay");
            }
            else
            {
                LogHelper.Log("Main", $"Channel '{channelName}' not found. Available: {string.Join(", ", EchosyncBehaviour.ChannelStates.Keys)}", true);
            }
            break;

        case "botoff":
            if (activeBot != null)
            {
                activeBot.Dispose();
                activeBot = null;
                LogHelper.Log("Main", "Bot removed");
            }
            else
            {
                LogHelper.Log("Main", "No active bot");
            }
            break;

        case "quit":
            break;

        default:
            Console.WriteLine("Commands: bot <channel> [delayMs] | botoff | quit");
            break;
    }
}

activeBot?.Dispose();

#pragma warning disable CS0618
server.WebSocketServices.Broadcast($"{(int)SyncMessages.ServerShutdown}");
#pragma warning restore CS0618
server.Stop();
