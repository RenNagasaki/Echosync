
// Create a new websocket server
using Echosync_Data.Enums;
using Echosync_Server.Behaviours;
using Echosync_Server.Helper;
using System.Reflection;
using WebSocketSharp.NetCore.Server;


public class Program
{
    private static int Port = 2053;

    static void Main(string[] args)
    {
        LogHelper.Log("Main", $"Starting server with port '{Port}'!");
        var wssv = new HttpServer(Port);
        LogHelper.Log("Main", $"Starting main Thread!");
        wssv.WebSocketServices.AddService<EchosyncBehaviour>("/main", (t) => { t.Setup(wssv);});
        wssv.Start();
        Console.Title = $"Channels: {wssv.WebSocketServices.Count - 1} | Users: {wssv.WebSocketServices.Count} | v.{Assembly.GetEntryAssembly()!.GetName().Version}";

        var command = "";
        while (command != "quit")
        {
            command = Console.ReadLine();
            LogHelper.Log("Main", $"Command '{command}' entered");
        }

        wssv.WebSocketServices.Broadcast($"{((int)SyncMessages.ServerShutdown)}");
        wssv.Stop();

    }
}