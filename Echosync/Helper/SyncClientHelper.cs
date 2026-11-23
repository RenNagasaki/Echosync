using Dalamud.Plugin.Services;
using Echosync.DataClasses;
using Echotools.Logging.DataClasses;
using Echotools.Logging.Enums;
using Echotools.Logging.Services;
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Echosync.Data.Enums;
using WebSocketSharp.NetCore;

namespace Echosync.Helper;

public enum ClientDialogueState
{
    Idle,
    InDialogue,
    AwaitingGrant,
}

public class SyncGroupInfo
{
    public int ReadyCount { get; set; }
    public int TotalCount { get; set; }
    public int TimeoutSeconds { get; set; }
    public string NpcId { get; set; } = "";
}

public class SyncClientHelper : IDisposable
{
    private readonly IFramework _framework;
    private readonly IObjectTable _objectTable;
    private readonly Configuration _configuration;
    private readonly ILogService _log;

    public volatile bool Connected;
    public volatile bool AdvanceGranted;
    public ClientDialogueState DialogueState { get; set; } = ClientDialogueState.Idle;
    public SyncGroupInfo SyncGroup { get; } = new();
    public int ConnectedPlayerCount { get; set; }

    private WebSocket? _webSocket;
    private string _activeChannel = "";
    private EKEventId? _currentEvent;
    private string _syncServerThread = "main";
    private System.Threading.Timer? _positionTimer;

    public EKEventId? CurrentEvent
    {
        get => _currentEvent ?? new EKEventId(0, TextSource.Sync);
        set => _currentEvent = value;
    }

    public SyncClientHelper(IFramework framework, IObjectTable objectTable, Configuration configuration, ILogService log)
    {
        _framework = framework;
        _objectTable = objectTable;
        _configuration = configuration;
        _log = log;
    }

    public void Setup()
    {
        try
        {
            if (_configuration is { ConnectAtStart: true, Enabled: true })
                Connect();
        }
        catch (Exception ex)
        {
            _log.Error(nameof(Setup), $"Error while starting: {ex}", CurrentEvent!);
        }
    }

    private void InitializeWebSocket()
    {
        try
        {
            _log.Info(nameof(InitializeWebSocket), $"Initializing connection to: {_configuration.SyncServer}/{_syncServerThread}", CurrentEvent!);
            if (_webSocket is { ReadyState: WebSocketState.Open })
                _webSocket.Close();
            _webSocket = new WebSocket($"{_configuration.SyncServer}/{_syncServerThread}");

            if (_syncServerThread == "main")
            {
                _webSocket.OnMessage += OnMessageMain;
                _webSocket.OnOpen += OnOpenMain;
                _webSocket.OnClose += OnCloseMain;
            }
            else
            {
                _webSocket.OnMessage += OnMessageChannel;
                _webSocket.OnOpen += OnOpenChannel;
                _webSocket.OnClose += OnCloseChannel;
            }
        }
        catch (Exception ex)
        {
            _log.Error(nameof(InitializeWebSocket), $"Error while initializing: {ex}", CurrentEvent!);
        }
    }

    public void Test()
    {
        _log.Info(nameof(Test), "Testing connection to server", CurrentEvent!);
        _syncServerThread = "main";
        Connect();
        SendRaw($"{(int)SyncMessages.Test}");
    }

    public void Connect()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_activeChannel) && Connected)
                Disconnect();

            InitializeWebSocket();
            _log.Info(nameof(Connect), "Connecting to server", CurrentEvent!);

            _activeChannel = _configuration.SyncChannel;
            _webSocket!.Connect();
            Authenticate(_configuration.SyncPassword);
        }
        catch (Exception ex)
        {
            _log.Error(nameof(Connect), $"Error while connecting: {ex}", CurrentEvent!);
        }
    }

    public void Disconnect(bool silent = false)
    {
        try
        {
            _log.Info(nameof(Disconnect), "Disconnecting from server", CurrentEvent!);
            StopPositionReports();

            if (!silent && Connected)
            {
                _webSocket!.Close();
                _log.Info(nameof(Disconnect), $"Disconnected from channel: {_activeChannel}", CurrentEvent!);
            }
            else
                _log.Info(nameof(Disconnect), $"Not connected: {_webSocket!.ReadyState}", CurrentEvent!);
        }
        catch (Exception ex)
        {
            _log.Error(nameof(Disconnect), $"Error while disconnecting: {ex}", CurrentEvent!);
        }
    }

    private void RequestChannel(SyncMessages message, string channel, string password)
    {
        try
        {
            _log.Info(nameof(RequestChannel), $"Sending '{message}' for channel '{channel}'", CurrentEvent!);
            _webSocket!.Send($"{(int)message}|{channel}|{password}");
            _log.Info(nameof(RequestChannel), $"Sent '{message}' to main", CurrentEvent!);
        }
        catch (Exception ex)
        {
            _log.Error(nameof(RequestChannel), $"Error while sending message: {ex}", CurrentEvent!);
        }
    }

    private void Authenticate(string password)
    {
        try
        {
            _log.Info(nameof(Authenticate), $"Sending '{SyncMessages.Authenticate}' to channel: {_activeChannel}", CurrentEvent!);
            if (!Connected)
            {
                _log.Info(nameof(Authenticate), $"Not connected: {_webSocket!.ReadyState}", CurrentEvent!);
                return;
            }

            _webSocket!.Send($"{(int)SyncMessages.Authenticate}|{password}");
            _log.Info(nameof(Authenticate), $"Sent '{SyncMessages.Authenticate}' to channel: {_activeChannel}", CurrentEvent!);
        }
        catch (Exception ex)
        {
            _log.Error(nameof(Authenticate), $"Error while sending message: {ex}", CurrentEvent!);
        }
    }

    public void SendDialogueEnter(string npcId, string dialogueTextHash)
    {
        SendRaw($"{(int)SyncMessages.C2S_DialogueEnter}|{npcId}|{dialogueTextHash}");
    }

    public void SendDialogueAdvance(string? dialogueTextHash = null)
    {
        var body = $"{(int)SyncMessages.C2S_DialogueAdvance}";
        if (!string.IsNullOrEmpty(dialogueTextHash))
            body += $"|{dialogueTextHash}";
        SendRaw(body);
        DialogueState = ClientDialogueState.AwaitingGrant;
    }

    public void SendDialogueExit()
    {
        SendRaw($"{(int)SyncMessages.C2S_DialogueExit}");
        DialogueState = ClientDialogueState.Idle;
    }

    private void SendRaw(string body)
    {
        try
        {
            if (!Connected)
            {
                _log.Info(nameof(SendRaw), $"Not connected: {_webSocket!.ReadyState}", CurrentEvent!);
                return;
            }

            _webSocket!.Send(body);
        }
        catch (Exception ex)
        {
            _log.Error(nameof(SendRaw), $"Error while sending message: {ex}", CurrentEvent!);
        }
    }

    public void StartPositionReports()
    {
        if (_positionTimer != null) return;
        _positionTimer = new System.Threading.Timer(_ => SendPositionReport(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
    }

    public void StopPositionReports()
    {
        _positionTimer?.Dispose();
        _positionTimer = null;
    }

    private void SendPositionReport()
    {
        try
        {
            if (!Connected) return;

            _framework.RunOnFrameworkThread(() =>
            {
                var player = _objectTable.LocalPlayer;
                if (player == null) return;

                var pos = player.Position;
                var targetNpcId = player.TargetObject?.GameObjectId.ToString() ?? "";
                SendRaw($"{(int)SyncMessages.C2S_PositionReport}|{pos.X.ToString(CultureInfo.InvariantCulture)}|{pos.Y.ToString(CultureInfo.InvariantCulture)}|{pos.Z.ToString(CultureInfo.InvariantCulture)}|{targetNpcId}");
            });
        }
        catch (Exception ex)
        {
            _log.Error(nameof(SendPositionReport), $"Error sending position: {ex}", CurrentEvent!);
        }
    }

    public static string HashDialogueText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes)[..16];
    }

    private void OnMessageMain(object? sender, MessageEventArgs e)
    {
        var textMessage = e.Data;
        try
        {
            var messageEnum = (SyncMessages)Convert.ToInt32(textMessage);

            switch (messageEnum)
            {
                case SyncMessages.CreateChannel:
                    _log.Info(nameof(OnMessageMain), "Server created channel", CurrentEvent!);
                    _syncServerThread = _activeChannel;
                    Connect();
                    break;
                case SyncMessages.Test:
                    _log.Info(nameof(OnMessageMain), $"Received command '{messageEnum}'", CurrentEvent!);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.Error(nameof(OnMessageMain), $"Received illegal message '{textMessage}' from main: {ex}", CurrentEvent!);
        }
    }

    private void OnOpenMain(object? sender, EventArgs e)
    {
        _log.Info(nameof(OnOpenMain), "Connected to main server", CurrentEvent!);
        RequestChannel(SyncMessages.CreateChannel, _activeChannel, _configuration.SyncPassword);
    }

    private void OnCloseMain(object? sender, EventArgs e)
    {
        _log.Info(nameof(OnCloseMain), "Disconnected from main server", CurrentEvent!);
    }

    private void OnMessageChannel(object? sender, MessageEventArgs e)
    {
        var textMessage = e.Data;
        try
        {
            var messageSplit = textMessage.Split('|');
            var messageEnum = (SyncMessages)Convert.ToInt32(messageSplit[0]);

            switch (messageEnum)
            {
                case SyncMessages.S2C_Authenticated:
                    _log.Info(nameof(OnMessageChannel), "Authenticated successfully", CurrentEvent!);
                    StartPositionReports();
                    break;

                case SyncMessages.S2C_AuthFailed:
                    _log.Error(nameof(OnMessageChannel), "Authentication failed", CurrentEvent!);
                    break;

                case SyncMessages.S2C_ChannelState:
                    ConnectedPlayerCount = messageSplit.Length > 1 ? Convert.ToInt32(messageSplit[1]) : 0;
                    _log.Debug(nameof(OnMessageChannel), $"Channel state: {ConnectedPlayerCount} other users in '{_activeChannel}'", CurrentEvent!);
                    break;

                case SyncMessages.S2C_AdvanceGranted:
                    _log.Debug(nameof(OnMessageChannel), "Advance granted", CurrentEvent!);
                    AdvanceGranted = true;
                    break;

                case SyncMessages.S2C_AdvanceWait:
                    _log.Debug(nameof(OnMessageChannel), "Waiting for others", CurrentEvent!);
                    break;

                case SyncMessages.S2C_AdvanceCatchupWait:
                    _log.Debug(nameof(OnMessageChannel), "Waiting for nearby user catchup", CurrentEvent!);
                    break;

                case SyncMessages.S2C_SyncGroupUpdate:
                    if (messageSplit.Length >= 5)
                    {
                        SyncGroup.NpcId = messageSplit[1];
                        SyncGroup.ReadyCount = Convert.ToInt32(messageSplit[2]);
                        SyncGroup.TotalCount = Convert.ToInt32(messageSplit[3]);
                        SyncGroup.TimeoutSeconds = Convert.ToInt32(messageSplit[4]);
                    }
                    _log.Debug(nameof(OnMessageChannel), $"Sync group: {SyncGroup.ReadyCount}/{SyncGroup.TotalCount}", CurrentEvent!);
                    break;

                case SyncMessages.S2C_UserTimedOut:
                    var timedOutUser = messageSplit.Length > 1 ? messageSplit[1] : "unknown";
                    _log.Info(nameof(OnMessageChannel), $"User '{timedOutUser}' timed out", CurrentEvent!);
                    break;

                case SyncMessages.S2C_Pong:
                    _log.Debug(nameof(OnMessageChannel), "Pong received", CurrentEvent!);
                    break;

                case SyncMessages.ServerShutdown:
                    _log.Debug(nameof(OnMessageChannel), "Server shutdown", CurrentEvent!);
                    Disconnect(true);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log.Error(nameof(OnMessageChannel), $"Received illegal message '{textMessage}' from channel: {_activeChannel}: {ex}", CurrentEvent!);
        }
    }

    private void OnOpenChannel(object? sender, EventArgs e)
    {
        _log.Info(nameof(OnOpenChannel), $"Connected to channel '{_activeChannel}'", CurrentEvent!);
        Connected = true;
    }

    private void OnCloseChannel(object? sender, EventArgs e)
    {
        _log.Info(nameof(OnCloseChannel), $"Disconnected from channel '{_activeChannel}'", CurrentEvent!);
        _syncServerThread = "main";
        Connected = false;
        StopPositionReports();
    }

    public void Dispose()
    {
        StopPositionReports();
        Disconnect();
    }
}
