using System.Reflection;
using Echosync.Data.Enums;
using Echosync.Server.Helper;
using WebSocketSharp.NetCore;
using WebSocketSharp.NetCore.Server;

namespace Echosync.Server.Behaviours;

public class EchosyncChannelBehaviour : WebSocketBehavior
{
    private UserState? _userState;
    private HttpServer? _server;
    private string _channelName = "";
    private string _password = "";
    private ChannelState? _channelState;
    private Timer? _timeoutTimer;

    public void Setup(HttpServer server, string channelName, string password, ChannelState channelState)
    {
        _server = server;
        _channelName = channelName;
        _password = password;
        _channelState = channelState;

        if (_timeoutTimer == null)
        {
            _timeoutTimer = new Timer(_ => OnTimeoutTick(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
    }

    protected override void OnOpen()
    {
        try
        {
            LogHelper.Log(_channelName, $"Client '{Context.UserEndPoint.Address}' connected");
            UpdateTitle();
        }
        catch (Exception ex)
        {
            LogHelper.Log(_channelName, $"Error while client '{Context.UserEndPoint.Address}' connected: {ex}");
        }

        base.OnOpen();
    }

    protected override void OnClose(CloseEventArgs e)
    {
        try
        {
            LogHelper.Log(_channelName, $"Client '{Context.UserEndPoint.Address}' disconnected!");

            if (_userState != null)
            {
                HandleDialogueExitInternal();
                _channelState!.RemoveUser(_userState.WebSocketId);
                SendChannelState();
            }

            if (Sessions.Count == 0)
            {
                LogHelper.Log(_channelName, "Last client disconnected, closing channel!");
                _timeoutTimer?.Dispose();
                _timeoutTimer = null;
                _server!.RemoveWebSocketService($"/{_channelName}");
            }

            UpdateTitle();
        }
        catch (Exception ex)
        {
            LogHelper.Log(_channelName, $"Error while client '{Context.UserEndPoint.Address}' disconnected: {ex}", true);
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
            if (messageEnum != SyncMessages.C2S_PositionReport && messageEnum != SyncMessages.C2S_Ping)
                LogHelper.Log(_channelName, $"Message received: '{messageEnum}' from '{Context.UserEndPoint.Address}'");

            if (messageEnum != SyncMessages.Authenticate && _userState == null)
            {
                Sessions.CloseSession(ID, CloseStatusCode.PolicyViolation, "Not authenticated");
                return;
            }

            switch (messageEnum)
            {
                case SyncMessages.Authenticate:
                    HandleAuthenticate(messageSplit);
                    break;
                case SyncMessages.C2S_DialogueEnter:
                    HandleDialogueEnter(messageSplit);
                    break;
                case SyncMessages.C2S_DialogueAdvance:
                    HandleDialogueAdvance(messageSplit);
                    break;
                case SyncMessages.C2S_DialogueExit:
                    HandleDialogueExit();
                    break;
                case SyncMessages.C2S_PositionReport:
                    HandlePositionReport(messageSplit);
                    break;
                case SyncMessages.C2S_Ping:
                    Send($"{(int)SyncMessages.S2C_Pong}");
                    break;
                case SyncMessages.Test:
                    Send($"{(int)SyncMessages.Test}");
                    break;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Log(_channelName, $"Illegal message from '{_userState?.IpAddress ?? Context.UserEndPoint.Address.ToString()}' message: '{message}' Exception: {ex}", true);
        }
    }

    private void HandleAuthenticate(string[] messageSplit)
    {
        var password = messageSplit[1];

        if (password == _password)
        {
            LogHelper.Log(_channelName, $"Client '{Context.UserEndPoint.Address}' successfully authenticated");
            _userState = new UserState(ID, Context.UserEndPoint.Address.ToString(), _channelName);
            _channelState!.AddUser(_userState);
            Send($"{(int)SyncMessages.S2C_Authenticated}");
            SendChannelState();
        }
        else
        {
            LogHelper.Log(_channelName, $"Client '{Context.UserEndPoint.Address}' failed to authenticate");
            Send($"{(int)SyncMessages.S2C_AuthFailed}");
            Sessions.CloseSession(ID, CloseStatusCode.PolicyViolation, "Wrong password");
        }
    }

    private void HandleDialogueEnter(string[] messageSplit)
    {
        var npcId = messageSplit[1];
        var dialogueHash = messageSplit[2];

        lock (_channelState!.SyncLock)
        {
            if (_userState!.DialogueState == DialogueState.Idle)
            {
                _userState.NpcId = npcId;
                _userState.DialogueIndex = 0;
                _userState.CurrentDialogueHash = dialogueHash;
                _userState.DialogueState = DialogueState.InDialogue;
                _userState.AdvanceRequested = false;

                // Record NPC position from user's current position
                _channelState.RecordNpcPosition(npcId, _userState.PosX, _userState.PosY, _userState.PosZ);

                LogHelper.Log(_channelName, $"Client '{_userState.IpAddress}' entered dialogue with NPC '{npcId}'");
            }
            else
            {
                // Dialogue text changed (new line of dialogue)
                _userState.CurrentDialogueHash = dialogueHash;
                LogHelper.Log(_channelName, $"Client '{_userState.IpAddress}' dialogue updated for NPC '{npcId}' at index {_userState.DialogueIndex}");
            }
        }

        SendSyncGroupUpdate(_userState);
    }

    private void HandleDialogueAdvance(string[] messageSplit)
    {
        var dialogueHash = messageSplit.Length > 1 ? messageSplit[1] : "";

        lock (_channelState!.SyncLock)
        {
            _userState!.AdvanceRequested = true;
            _userState.AdvanceRequestedAt = DateTime.UtcNow;
            _userState.DialogueState = DialogueState.WaitingAdvance;

            if (!string.IsNullOrEmpty(dialogueHash))
                _userState.CurrentDialogueHash = dialogueHash;

            LogHelper.Log(_channelName, $"Client '{_userState.IpAddress}' requested advance for NPC '{_userState.NpcId}' at index {_userState.DialogueIndex}");
        }

        SendSyncGroupUpdate(_userState);
        EvaluateAndRespond(_userState);
    }

    private void HandleDialogueExit()
    {
        LogHelper.Log(_channelName, $"Client '{_userState!.IpAddress}' exited dialogue with NPC '{_userState.NpcId}'");
        var npcId = _userState.NpcId;
        var dialogueIndex = _userState.DialogueIndex;
        var dialogueHash = _userState.CurrentDialogueHash;

        HandleDialogueExitInternal();

        // Re-evaluate remaining sync group members
        lock (_channelState!.SyncLock)
        {
            var remainingGroup = _channelState.GetSyncGroup(npcId, dialogueIndex, dialogueHash);
            foreach (var member in remainingGroup)
            {
                if (member.AdvanceRequested)
                    EvaluateAndRespond(member);
            }
        }
    }

    private void HandleDialogueExitInternal()
    {
        if (_userState == null) return;

        lock (_channelState!.SyncLock)
        {
            _userState.DialogueState = DialogueState.Idle;
            _userState.NpcId = "";
            _userState.CurrentDialogueHash = "";
            _userState.DialogueIndex = 0;
            _userState.AdvanceRequested = false;
        }
    }

    private void HandlePositionReport(string[] messageSplit)
    {
        var posX = float.Parse(messageSplit[1]);
        var posY = float.Parse(messageSplit[2]);
        var posZ = float.Parse(messageSplit[3]);

        _userState!.PosX = posX;
        _userState.PosY = posY;
        _userState.PosZ = posZ;
        _userState.LastPositionUpdate = DateTime.UtcNow;
    }

    private void EvaluateAndRespond(UserState user)
    {
        lock (_channelState!.SyncLock)
        {
            var result = _channelState.EvaluateAdvance(user, out var syncGroup);

            switch (result)
            {
                case ChannelState.AdvanceResult.GrantSolo:
                    GrantAdvance(user);
                    break;

                case ChannelState.AdvanceResult.GrantAll:
                    foreach (var member in syncGroup)
                        GrantAdvance(member);
                    break;

                case ChannelState.AdvanceResult.WaitCatchup:
                    LogHelper.Log(_channelName, $"Client '{user.IpAddress}' waiting for nearby user catchup");
                    SendToUser(user, $"{(int)SyncMessages.S2C_AdvanceCatchupWait}");
                    SendSyncGroupUpdate(user);
                    break;

                case ChannelState.AdvanceResult.Wait:
                    LogHelper.Log(_channelName, $"Client '{user.IpAddress}' waiting for others in sync group");
                    SendToUser(user, $"{(int)SyncMessages.S2C_AdvanceWait}");
                    SendSyncGroupUpdate(user);
                    break;
            }
        }
    }

    private void GrantAdvance(UserState user)
    {
        LogHelper.Log(_channelName, $"Granting advance to client '{user.IpAddress}' at index {user.DialogueIndex}");
        user.AdvanceRequested = false;
        user.DialogueIndex++;
        user.DialogueState = DialogueState.InDialogue;
        SendToUser(user, $"{(int)SyncMessages.S2C_AdvanceGranted}");
    }

    private void SendSyncGroupUpdate(UserState user)
    {
        var syncGroup = _channelState!.GetSyncGroup(user.NpcId, user.DialogueIndex, user.CurrentDialogueHash);
        var readyCount = syncGroup.Count(u => u.AdvanceRequested);
        var totalCount = syncGroup.Count;
        var timeoutSeconds = 30;

        foreach (var member in syncGroup)
        {
            SendToUser(member, $"{(int)SyncMessages.S2C_SyncGroupUpdate}|{user.NpcId}|{readyCount}|{totalCount}|{timeoutSeconds}");
        }
    }

    private void SendChannelState()
    {
        LogHelper.Log(_channelName, $"Sending channel state for channel: {_channelName}");
        var userCount = _channelState!.Users.Count;

        foreach (var user in _channelState.Users.Values)
        {
            if (!Sessions.TryGetSession(user.WebSocketId, out var session)) continue;
            // Send count of other connected users (total minus self)
            session.Context.WebSocket.Send($"{(int)SyncMessages.S2C_ChannelState}|{userCount - 1}");
        }
    }

    private void SendToUser(UserState user, string message)
    {
        if (Sessions.TryGetSession(user.WebSocketId, out var session))
            session.Context.WebSocket.Send(message);
    }

    private void OnTimeoutTick()
    {
        try
        {
            // Process pending evaluations (from bot users)
            while (_channelState!.PendingEvaluations.TryDequeue(out var wsId))
            {
                var user = _channelState.GetUser(wsId);
                if (user is { AdvanceRequested: true })
                    EvaluateAndRespond(user);
            }

            // Process pending sync group updates (from bot enter/exit)
            while (_channelState.PendingSyncUpdates.TryDequeue(out var syncWsId))
            {
                var user = _channelState.GetUser(syncWsId);
                if (user is { DialogueState: not DialogueState.Idle })
                    SendSyncGroupUpdate(user);
            }

            // Check advance timeouts (30s)
            var timedOut = _channelState.CheckTimeouts();
            foreach (var user in timedOut)
            {
                LogHelper.Log(_channelName, $"Client '{user.IpAddress}' advance request timed out");
                SendToUser(user, $"{(int)SyncMessages.S2C_AdvanceGranted}");

                // Notify others in the sync group
                var syncGroup = _channelState.GetSyncGroup(user.NpcId, user.DialogueIndex, user.CurrentDialogueHash);
                foreach (var member in syncGroup)
                {
                    if (member.WebSocketId != user.WebSocketId)
                        SendToUser(member, $"{(int)SyncMessages.S2C_UserTimedOut}|{user.SessionId}");
                }
            }

            // Check catchup timeouts (5s)
            var catchupExpired = _channelState.CheckCatchupTimeouts();
            foreach (var user in catchupExpired)
            {
                LogHelper.Log(_channelName, $"Client '{user.IpAddress}' catchup window expired, granting advance");
                GrantAdvance(user);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Log(_channelName, $"Error in timeout tick: {ex}", true);
        }
    }

    private void UpdateTitle()
    {
        Console.Title = $"Channels: {_server!.WebSocketServices.Count - 1} | Users: {_server.WebSocketServices.Count} | v.{Assembly.GetEntryAssembly()!.GetName().Version}";
    }
}
