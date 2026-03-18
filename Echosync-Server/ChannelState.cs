using System.Collections.Concurrent;

namespace Echosync.Server;

public class NpcPosition
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class ChannelState
{
    public ConcurrentDictionary<string, UserState> Users { get; } = new();
    public readonly object SyncLock = new();
    public Dictionary<string, NpcPosition> NpcPositions { get; } = new();
    public ConcurrentQueue<string> PendingEvaluations { get; } = new();

    private const float ProximityDistance = 3f;
    private static readonly TimeSpan StalePositionThreshold = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan AdvanceTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CatchupJoinWindow = TimeSpan.FromSeconds(5);

    public void AddUser(UserState user)
    {
        Users[user.WebSocketId] = user;
    }

    public void RemoveUser(string webSocketId)
    {
        Users.TryRemove(webSocketId, out _);
    }

    public UserState? GetUser(string webSocketId)
    {
        Users.TryGetValue(webSocketId, out var user);
        return user;
    }

    public void RecordNpcPosition(string npcId, float x, float y, float z)
    {
        lock (SyncLock)
        {
            NpcPositions[npcId] = new NpcPosition { X = x, Y = y, Z = z, RecordedAt = DateTime.UtcNow };
        }
    }

    public List<UserState> GetSyncGroup(string npcId, int dialogueIndex, string dialogueHash)
    {
        return Users.Values
            .Where(u => u.NpcId == npcId
                        && u.DialogueIndex == dialogueIndex
                        && u.CurrentDialogueHash == dialogueHash
                        && u.DialogueState != DialogueState.Idle)
            .ToList();
    }

    public List<UserState> GetNearbyIdleUsers(string npcId)
    {
        if (!NpcPositions.TryGetValue(npcId, out var npcPos))
            return [];

        var now = DateTime.UtcNow;
        return Users.Values
            .Where(u => u.DialogueState == DialogueState.Idle
                        && (now - u.LastPositionUpdate) < StalePositionThreshold
                        && ManhattanDistance(u.PosX, u.PosY, u.PosZ, npcPos.X, npcPos.Y, npcPos.Z) < ProximityDistance)
            .ToList();
    }

    public enum AdvanceResult
    {
        GrantAll,
        GrantSolo,
        WaitCatchup,
        Wait,
    }

    public AdvanceResult EvaluateAdvance(UserState user, out List<UserState> syncGroup)
    {
        lock (SyncLock)
        {
            syncGroup = GetSyncGroup(user.NpcId, user.DialogueIndex, user.CurrentDialogueHash);

            // Solo user
            if (syncGroup.Count <= 1)
            {
                var nearbyIdle = GetNearbyIdleUsers(user.NpcId);
                if (nearbyIdle.Count > 0)
                    return AdvanceResult.WaitCatchup;
                return AdvanceResult.GrantSolo;
            }

            // Check if all in group have requested advance
            if (syncGroup.All(u => u.AdvanceRequested))
                return AdvanceResult.GrantAll;

            return AdvanceResult.Wait;
        }
    }

    public List<UserState> CheckTimeouts()
    {
        var timedOut = new List<UserState>();
        var now = DateTime.UtcNow;

        lock (SyncLock)
        {
            foreach (var user in Users.Values)
            {
                if (user.AdvanceRequested && (now - user.AdvanceRequestedAt) > AdvanceTimeout)
                {
                    user.AdvanceRequested = false;
                    user.DialogueState = DialogueState.InDialogue;
                    timedOut.Add(user);
                }
            }
        }

        return timedOut;
    }

    public List<UserState> CheckCatchupTimeouts()
    {
        var expired = new List<UserState>();
        var now = DateTime.UtcNow;

        lock (SyncLock)
        {
            foreach (var user in Users.Values)
            {
                if (user.DialogueState == DialogueState.WaitingAdvance
                    && user.AdvanceRequested
                    && (now - user.AdvanceRequestedAt) > CatchupJoinWindow)
                {
                    // Re-evaluate: if still solo after catchup window, grant
                    var syncGroup = GetSyncGroup(user.NpcId, user.DialogueIndex, user.CurrentDialogueHash);
                    if (syncGroup.Count <= 1)
                        expired.Add(user);
                }
            }
        }

        return expired;
    }

    private static float ManhattanDistance(float x1, float y1, float z1, float x2, float y2, float z2)
    {
        return Math.Abs(x1 - x2) + Math.Abs(y1 - y2) + Math.Abs(z1 - z2);
    }
}
