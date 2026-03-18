using Echosync.Server.Helper;

namespace Echosync.Server;

public class BotUser : IDisposable
{
    private readonly ChannelState _channelState;
    private readonly UserState _userState;
    private readonly Timer _timer;
    private readonly string _channelName;
    private readonly int _advanceDelayMs;

    private string _lastMirroredNpcId = "";
    private int _lastMirroredIndex = -1;
    private string _lastMirroredHash = "";
    private DateTime _advanceScheduledAt = DateTime.MaxValue;
    private bool _advancePending;

    public BotUser(ChannelState channelState, string channelName, int advanceDelayMs = 500)
    {
        _channelState = channelState;
        _channelName = channelName;
        _advanceDelayMs = advanceDelayMs;

        var botId = $"bot-{Guid.NewGuid():N}";
        _userState = new UserState(botId, "bot", channelName)
        {
            PosX = 0f,
            PosY = 0f,
            PosZ = 0f,
            LastPositionUpdate = DateTime.UtcNow,
        };

        _channelState.AddUser(_userState);
        LogHelper.Log(_channelName, $"Bot '{_userState.SessionId}' joined channel");

        _timer = new Timer(_ => Tick(), null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100));
    }

    private void Tick()
    {
        try
        {
            // Keep position fresh so proximity checks don't consider us stale
            _userState.LastPositionUpdate = DateTime.UtcNow;

            var realUser = FindRealUserInDialogue();

            if (realUser == null)
            {
                // No real user in dialogue — if bot is in dialogue, exit
                if (_userState.DialogueState != DialogueState.Idle)
                {
                    LogHelper.Log(_channelName, $"Bot '{_userState.SessionId}' exiting dialogue (real user left)");
                    ResetBotState();
                }
                return;
            }

            // Mirror position near the NPC
            MirrorPosition(realUser);

            // Mirror dialogue enter
            if (_userState.DialogueState == DialogueState.Idle)
            {
                EnterDialogue(realUser);
                return;
            }

            // Keep hash/index synced with real user
            if (_userState.NpcId == realUser.NpcId &&
                _userState.DialogueIndex == realUser.DialogueIndex &&
                _userState.CurrentDialogueHash != realUser.CurrentDialogueHash)
            {
                _userState.CurrentDialogueHash = realUser.CurrentDialogueHash;
            }

            // Mirror advance request with delay
            if (realUser.AdvanceRequested && !_userState.AdvanceRequested && !_advancePending)
            {
                _advancePending = true;
                _advanceScheduledAt = DateTime.UtcNow.AddMilliseconds(_advanceDelayMs);
                LogHelper.Log(_channelName, $"Bot '{_userState.SessionId}' scheduling advance in {_advanceDelayMs}ms");
            }

            // Fire delayed advance
            if (_advancePending && DateTime.UtcNow >= _advanceScheduledAt)
            {
                _advancePending = false;
                RequestAdvance();
            }

            // If bot got granted (by timeout tick evaluation), sync index with real user
            if (_userState.DialogueState == DialogueState.InDialogue &&
                _userState.DialogueIndex < realUser.DialogueIndex)
            {
                _userState.DialogueIndex = realUser.DialogueIndex;
                _userState.CurrentDialogueHash = realUser.CurrentDialogueHash;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Log(_channelName, $"Bot tick error: {ex}", true);
        }
    }

    private void EnterDialogue(UserState realUser)
    {
        lock (_channelState.SyncLock)
        {
            _userState.NpcId = realUser.NpcId;
            _userState.DialogueIndex = realUser.DialogueIndex;
            _userState.CurrentDialogueHash = realUser.CurrentDialogueHash;
            _userState.DialogueState = DialogueState.InDialogue;
            _userState.AdvanceRequested = false;

            _lastMirroredNpcId = realUser.NpcId;
            _lastMirroredIndex = realUser.DialogueIndex;
            _lastMirroredHash = realUser.CurrentDialogueHash;
        }

        LogHelper.Log(_channelName, $"Bot '{_userState.SessionId}' entered dialogue with NPC '{realUser.NpcId}'");
    }

    private void RequestAdvance()
    {
        lock (_channelState.SyncLock)
        {
            _userState.AdvanceRequested = true;
            _userState.AdvanceRequestedAt = DateTime.UtcNow;
            _userState.DialogueState = DialogueState.WaitingAdvance;
        }

        // Queue evaluation so the timeout tick processes it
        _channelState.PendingEvaluations.Enqueue(_userState.WebSocketId);
        LogHelper.Log(_channelName, $"Bot '{_userState.SessionId}' requested advance");
    }

    private void MirrorPosition(UserState realUser)
    {
        // Stay at the same position as the real user (near the NPC)
        _userState.PosX = realUser.PosX + 0.5f;
        _userState.PosY = realUser.PosY;
        _userState.PosZ = realUser.PosZ;
    }

    private void ResetBotState()
    {
        lock (_channelState.SyncLock)
        {
            _userState.DialogueState = DialogueState.Idle;
            _userState.NpcId = "";
            _userState.CurrentDialogueHash = "";
            _userState.DialogueIndex = 0;
            _userState.AdvanceRequested = false;
        }

        _advancePending = false;
        _advanceScheduledAt = DateTime.MaxValue;
        _lastMirroredNpcId = "";
        _lastMirroredIndex = -1;
        _lastMirroredHash = "";
    }

    private UserState? FindRealUserInDialogue()
    {
        foreach (var user in _channelState.Users.Values)
        {
            if (user.WebSocketId == _userState.WebSocketId) continue;
            if (user.WebSocketId.StartsWith("bot-")) continue;
            if (user.DialogueState != DialogueState.Idle)
                return user;
        }
        return null;
    }

    public void Dispose()
    {
        _timer.Dispose();
        ResetBotState();
        _channelState.RemoveUser(_userState.WebSocketId);
        LogHelper.Log(_channelName, $"Bot '{_userState.SessionId}' removed from channel");
    }
}
