
// Create a new websocket server
using Echosync_Data.Enums;
using Echosync_Server.Behaviours;
using Echosync_Server.Helper;
using WebSocketSharp.Server;


public class Program
{
    private static int Port = 2053;

    static void Main(string[] args)
    {
        LogHelper.Log("Main", $"Starting server with port '{Port}'!");
        var wssv = new HttpServer(Port);
        LogHelper.Log("Main", $"Starting main Thread!");
        wssv.AddWebSocketService<EchosyncBehaviour>("/main", () => new EchosyncBehaviour(wssv));
        wssv.Start();

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
