namespace Echosync.Server.Tests;

public class ChannelStateTests
{
    private static UserState CreateUser(string id, string channel = "test") =>
        new(id, $"127.0.0.{id}", channel);

    private static void PutInDialogue(UserState user, string npcId, string hash, int index = 0)
    {
        user.NpcId = npcId;
        user.CurrentDialogueHash = hash;
        user.DialogueIndex = index;
        user.DialogueState = DialogueState.InDialogue;
    }

    private static void SetPosition(UserState user, float x, float y, float z)
    {
        user.PosX = x;
        user.PosY = y;
        user.PosZ = z;
        user.LastPositionUpdate = DateTime.UtcNow;
    }

    // ── User management ─────────────────────────────────────────────────────

    [Fact]
    public void AddUser_StoresUser()
    {
        var state = new ChannelState();
        var user = CreateUser("1");

        state.AddUser(user);

        Assert.Single(state.Users);
        Assert.Same(user, state.GetUser("1"));
    }

    [Fact]
    public void RemoveUser_RemovesUser()
    {
        var state = new ChannelState();
        var user = CreateUser("1");
        state.AddUser(user);

        state.RemoveUser("1");

        Assert.Empty(state.Users);
        Assert.Null(state.GetUser("1"));
    }

    [Fact]
    public void RemoveUser_NonExistent_DoesNotThrow()
    {
        var state = new ChannelState();
        state.RemoveUser("nonexistent");
    }

    [Fact]
    public void GetUser_NonExistent_ReturnsNull()
    {
        var state = new ChannelState();
        Assert.Null(state.GetUser("nonexistent"));
    }

    // ── Sync group formation ────────────────────────────────────────────────

    [Fact]
    public void GetSyncGroup_GroupsByNpcId()
    {
        var state = new ChannelState();
        var a = CreateUser("1");
        var b = CreateUser("2");
        PutInDialogue(a, "npc1", "hash1");
        PutInDialogue(b, "npc2", "hash1");
        state.AddUser(a);
        state.AddUser(b);

        var group = state.GetSyncGroup("npc1", 0, "hash1");

        Assert.Single(group);
        Assert.Same(a, group[0]);
    }

    [Fact]
    public void GetSyncGroup_GroupsByDialogueIndex()
    {
        var state = new ChannelState();
        var a = CreateUser("1");
        var b = CreateUser("2");
        PutInDialogue(a, "npc1", "hash1", 0);
        PutInDialogue(b, "npc1", "hash1", 1);
        state.AddUser(a);
        state.AddUser(b);

        var group = state.GetSyncGroup("npc1", 0, "hash1");

        Assert.Single(group);
        Assert.Same(a, group[0]);
    }

    [Fact]
    public void GetSyncGroup_GroupsByDialogueHash()
    {
        var state = new ChannelState();
        var a = CreateUser("1");
        var b = CreateUser("2");
        PutInDialogue(a, "npc1", "hashA", 0);
        PutInDialogue(b, "npc1", "hashB", 0);
        state.AddUser(a);
        state.AddUser(b);

        var groupA = state.GetSyncGroup("npc1", 0, "hashA");
        var groupB = state.GetSyncGroup("npc1", 0, "hashB");

        Assert.Single(groupA);
        Assert.Single(groupB);
        Assert.Same(a, groupA[0]);
        Assert.Same(b, groupB[0]);
    }

    [Fact]
    public void GetSyncGroup_ExcludesIdleUsers()
    {
        var state = new ChannelState();
        var a = CreateUser("1");
        var b = CreateUser("2");
        PutInDialogue(a, "npc1", "hash1");
        b.NpcId = "npc1";
        b.CurrentDialogueHash = "hash1";
        b.DialogueState = DialogueState.Idle;
        state.AddUser(a);
        state.AddUser(b);

        var group = state.GetSyncGroup("npc1", 0, "hash1");

        Assert.Single(group);
    }

    [Fact]
    public void GetSyncGroup_IncludesWaitingAdvanceUsers()
    {
        var state = new ChannelState();
        var a = CreateUser("1");
        var b = CreateUser("2");
        PutInDialogue(a, "npc1", "hash1");
        PutInDialogue(b, "npc1", "hash1");
        b.DialogueState = DialogueState.WaitingAdvance;
        state.AddUser(a);
        state.AddUser(b);

        var group = state.GetSyncGroup("npc1", 0, "hash1");

        Assert.Equal(2, group.Count);
    }

    [Fact]
    public void GetSyncGroup_TwoUsersMatchingAllCriteria()
    {
        var state = new ChannelState();
        var a = CreateUser("1");
        var b = CreateUser("2");
        PutInDialogue(a, "npc1", "hash1", 2);
        PutInDialogue(b, "npc1", "hash1", 2);
        state.AddUser(a);
        state.AddUser(b);

        var group = state.GetSyncGroup("npc1", 2, "hash1");

        Assert.Equal(2, group.Count);
    }

    // ── Advance evaluation ──────────────────────────────────────────────────

    [Fact]
    public void EvaluateAdvance_SoloUser_NoNearby_GrantsSolo()
    {
        var state = new ChannelState();
        var user = CreateUser("1");
        PutInDialogue(user, "npc1", "hash1");
        user.AdvanceRequested = true;
        state.AddUser(user);

        var result = state.EvaluateAdvance(user, out var group);

        Assert.Equal(ChannelState.AdvanceResult.GrantSolo, result);
        Assert.Single(group);
    }

    [Fact]
    public void EvaluateAdvance_SoloUser_NearbyIdleUser_WaitsCatchup()
    {
        var state = new ChannelState();
        var a = CreateUser("1");
        var b = CreateUser("2");
        PutInDialogue(a, "npc1", "hash1");
        a.AdvanceRequested = true;
        // b is idle but nearby
        SetPosition(b, 1f, 0f, 0f);
        state.AddUser(a);
        state.AddUser(b);
        state.RecordNpcPosition("npc1", 0f, 0f, 0f);

        var result = state.EvaluateAdvance(a, out _);

        Assert.Equal(ChannelState.AdvanceResult.WaitCatchup, result);
    }

    [Fact]
    public void EvaluateAdvance_SoloUser_FarIdleUser_GrantsSolo()
    {
        var state = new ChannelState();
        var a = CreateUser("1");
        var b = CreateUser("2");
        PutInDialogue(a, "npc1", "hash1");
        a.AdvanceRequested = true;
        // b is idle but far away (distance > 3)
        SetPosition(b, 10f, 10f, 10f);
        state.AddUser(a);
        state.AddUser(b);
        state.RecordNpcPosition("npc1", 0f, 0f, 0f);

        var result = state.EvaluateAdvance(a, out _);

        Assert.Equal(ChannelState.AdvanceResult.GrantSolo, result);
    }

    [Fact]
    public void EvaluateAdvance_TwoUsers_BothReady_GrantsAll()
    {
        var state = new ChannelState();
        var a = CreateUser("1");
        var b = CreateUser("2");
        PutInDialogue(a, "npc1", "hash1");
        PutInDialogue(b, "npc1", "hash1");
        a.AdvanceRequested = true;
        b.AdvanceRequested = true;
        state.AddUser(a);
        state.AddUser(b);

        var result = state.EvaluateAdvance(a, out var group);

        Assert.Equal(ChannelState.AdvanceResult.GrantAll, result);
        Assert.Equal(2, group.Count);
    }

    [Fact]
    public void EvaluateAdvance_TwoUsers_OneReady_Waits()
    {
        var state = new ChannelState();
        var a = CreateUser("1");
        var b = CreateUser("2");
        PutInDialogue(a, "npc1", "hash1");
        PutInDialogue(b, "npc1", "hash1");
        a.AdvanceRequested = true;
        b.AdvanceRequested = false;
        state.AddUser(a);
        state.AddUser(b);

        var result = state.EvaluateAdvance(a, out _);

        Assert.Equal(ChannelState.AdvanceResult.Wait, result);
    }

    [Fact]
    public void EvaluateAdvance_DifferentDialogueLoop_TreatedAsSolo()
    {
        var state = new ChannelState();
        var a = CreateUser("1");
        var b = CreateUser("2");
        PutInDialogue(a, "npc1", "hashA");
        PutInDialogue(b, "npc1", "hashB"); // different dialogue loop
        a.AdvanceRequested = true;
        state.AddUser(a);
        state.AddUser(b);

        var result = state.EvaluateAdvance(a, out var group);

        Assert.Equal(ChannelState.AdvanceResult.GrantSolo, result);
        Assert.Single(group);
    }

    [Fact]
    public void EvaluateAdvance_ThreeUsers_AllReady_GrantsAll()
    {
        var state = new ChannelState();
        var users = new[] { CreateUser("1"), CreateUser("2"), CreateUser("3") };
        foreach (var u in users)
        {
            PutInDialogue(u, "npc1", "hash1");
            u.AdvanceRequested = true;
            state.AddUser(u);
        }

        var result = state.EvaluateAdvance(users[0], out var group);

        Assert.Equal(ChannelState.AdvanceResult.GrantAll, result);
        Assert.Equal(3, group.Count);
    }

    [Fact]
    public void EvaluateAdvance_ThreeUsers_TwoReady_Waits()
    {
        var state = new ChannelState();
        var users = new[] { CreateUser("1"), CreateUser("2"), CreateUser("3") };
        foreach (var u in users)
        {
            PutInDialogue(u, "npc1", "hash1");
            state.AddUser(u);
        }
        users[0].AdvanceRequested = true;
        users[1].AdvanceRequested = true;
        users[2].AdvanceRequested = false;

        var result = state.EvaluateAdvance(users[0], out _);

        Assert.Equal(ChannelState.AdvanceResult.Wait, result);
    }

    // ── Proximity / nearby idle ─────────────────────────────────────────────

    [Fact]
    public void GetNearbyIdleUsers_NoNpcPosition_ReturnsEmpty()
    {
        var state = new ChannelState();
        var user = CreateUser("1");
        SetPosition(user, 0f, 0f, 0f);
        state.AddUser(user);

        var nearby = state.GetNearbyIdleUsers("unknown-npc");

        Assert.Empty(nearby);
    }

    [Fact]
    public void GetNearbyIdleUsers_StalePosition_Excluded()
    {
        var state = new ChannelState();
        var user = CreateUser("1");
        user.PosX = 0f;
        user.PosY = 0f;
        user.PosZ = 0f;
        user.LastPositionUpdate = DateTime.UtcNow - TimeSpan.FromSeconds(10); // stale
        state.AddUser(user);
        state.RecordNpcPosition("npc1", 0f, 0f, 0f);

        var nearby = state.GetNearbyIdleUsers("npc1");

        Assert.Empty(nearby);
    }

    [Fact]
    public void GetNearbyIdleUsers_InDialogue_Excluded()
    {
        var state = new ChannelState();
        var user = CreateUser("1");
        PutInDialogue(user, "npc1", "hash1");
        SetPosition(user, 0f, 0f, 0f);
        state.AddUser(user);
        state.RecordNpcPosition("npc1", 0f, 0f, 0f);

        var nearby = state.GetNearbyIdleUsers("npc1");

        Assert.Empty(nearby);
    }

    [Fact]
    public void GetNearbyIdleUsers_IdleAndClose_Included()
    {
        var state = new ChannelState();
        var user = CreateUser("1");
        SetPosition(user, 1f, 0f, 0f);
        state.AddUser(user);
        state.RecordNpcPosition("npc1", 0f, 0f, 0f);

        var nearby = state.GetNearbyIdleUsers("npc1");

        Assert.Single(nearby);
    }

    [Fact]
    public void GetNearbyIdleUsers_BoundaryDistance_NotIncluded()
    {
        var state = new ChannelState();
        var user = CreateUser("1");
        // Manhattan distance = exactly 3.0, which is NOT < 3
        SetPosition(user, 1f, 1f, 1f);
        state.AddUser(user);
        state.RecordNpcPosition("npc1", 0f, 0f, 0f);

        var nearby = state.GetNearbyIdleUsers("npc1");

        Assert.Empty(nearby);
    }

    [Fact]
    public void GetNearbyIdleUsers_JustUnderBoundary_Included()
    {
        var state = new ChannelState();
        var user = CreateUser("1");
        SetPosition(user, 0.9f, 0.9f, 0.9f); // Manhattan = 2.7
        state.AddUser(user);
        state.RecordNpcPosition("npc1", 0f, 0f, 0f);

        var nearby = state.GetNearbyIdleUsers("npc1");

        Assert.Single(nearby);
    }

    // ── NPC position recording ──────────────────────────────────────────────

    [Fact]
    public void RecordNpcPosition_StoresPosition()
    {
        var state = new ChannelState();

        state.RecordNpcPosition("npc1", 5f, 10f, 15f);

        Assert.True(state.NpcPositions.ContainsKey("npc1"));
        Assert.Equal(5f, state.NpcPositions["npc1"].X);
        Assert.Equal(10f, state.NpcPositions["npc1"].Y);
        Assert.Equal(15f, state.NpcPositions["npc1"].Z);
    }

    [Fact]
    public void RecordNpcPosition_OverwritesPrevious()
    {
        var state = new ChannelState();
        state.RecordNpcPosition("npc1", 0f, 0f, 0f);

        state.RecordNpcPosition("npc1", 5f, 5f, 5f);

        Assert.Equal(5f, state.NpcPositions["npc1"].X);
    }

    // ── Advance timeout (30s) ───────────────────────────────────────────────

    [Fact]
    public void CheckTimeouts_RecentRequest_NotTimedOut()
    {
        var state = new ChannelState();
        var user = CreateUser("1");
        PutInDialogue(user, "npc1", "hash1");
        user.AdvanceRequested = true;
        user.AdvanceRequestedAt = DateTime.UtcNow;
        state.AddUser(user);

        var timedOut = state.CheckTimeouts();

        Assert.Empty(timedOut);
        Assert.True(user.AdvanceRequested);
    }

    [Fact]
    public void CheckTimeouts_OldRequest_TimesOut()
    {
        var state = new ChannelState();
        var user = CreateUser("1");
        PutInDialogue(user, "npc1", "hash1");
        user.AdvanceRequested = true;
        user.DialogueState = DialogueState.WaitingAdvance;
        user.AdvanceRequestedAt = DateTime.UtcNow - TimeSpan.FromSeconds(31);
        state.AddUser(user);

        var timedOut = state.CheckTimeouts();

        Assert.Single(timedOut);
        Assert.False(user.AdvanceRequested);
        Assert.Equal(DialogueState.InDialogue, user.DialogueState);
    }

    [Fact]
    public void CheckTimeouts_NonRequestingUser_NotAffected()
    {
        var state = new ChannelState();
        var user = CreateUser("1");
        PutInDialogue(user, "npc1", "hash1");
        user.AdvanceRequested = false;
        state.AddUser(user);

        var timedOut = state.CheckTimeouts();

        Assert.Empty(timedOut);
    }

    [Fact]
    public void CheckTimeouts_MultipleUsers_OnlyTimedOutReturned()
    {
        var state = new ChannelState();
        var a = CreateUser("1");
        var b = CreateUser("2");
        PutInDialogue(a, "npc1", "hash1");
        PutInDialogue(b, "npc1", "hash1");
        a.AdvanceRequested = true;
        a.AdvanceRequestedAt = DateTime.UtcNow - TimeSpan.FromSeconds(31);
        b.AdvanceRequested = true;
        b.AdvanceRequestedAt = DateTime.UtcNow; // recent
        state.AddUser(a);
        state.AddUser(b);

        var timedOut = state.CheckTimeouts();

        Assert.Single(timedOut);
        Assert.Same(a, timedOut[0]);
    }

    // ── Catchup timeout (5s) ────────────────────────────────────────────────

    [Fact]
    public void CheckCatchupTimeouts_RecentRequest_NotExpired()
    {
        var state = new ChannelState();
        var user = CreateUser("1");
        PutInDialogue(user, "npc1", "hash1");
        user.DialogueState = DialogueState.WaitingAdvance;
        user.AdvanceRequested = true;
        user.AdvanceRequestedAt = DateTime.UtcNow;
        state.AddUser(user);

        var expired = state.CheckCatchupTimeouts();

        Assert.Empty(expired);
    }

    [Fact]
    public void CheckCatchupTimeouts_OldRequest_SoloUser_Expires()
    {
        var state = new ChannelState();
        var user = CreateUser("1");
        PutInDialogue(user, "npc1", "hash1");
        user.DialogueState = DialogueState.WaitingAdvance;
        user.AdvanceRequested = true;
        user.AdvanceRequestedAt = DateTime.UtcNow - TimeSpan.FromSeconds(6);
        state.AddUser(user);

        var expired = state.CheckCatchupTimeouts();

        Assert.Single(expired);
    }

    [Fact]
    public void CheckCatchupTimeouts_OldRequest_NotSolo_DoesNotExpire()
    {
        var state = new ChannelState();
        var a = CreateUser("1");
        var b = CreateUser("2");
        PutInDialogue(a, "npc1", "hash1");
        PutInDialogue(b, "npc1", "hash1");
        a.DialogueState = DialogueState.WaitingAdvance;
        a.AdvanceRequested = true;
        a.AdvanceRequestedAt = DateTime.UtcNow - TimeSpan.FromSeconds(6);
        state.AddUser(a);
        state.AddUser(b);

        var expired = state.CheckCatchupTimeouts();

        Assert.Empty(expired);
    }

    [Fact]
    public void CheckCatchupTimeouts_InDialogueState_NotWaiting_Ignored()
    {
        var state = new ChannelState();
        var user = CreateUser("1");
        PutInDialogue(user, "npc1", "hash1");
        user.DialogueState = DialogueState.InDialogue;
        user.AdvanceRequested = true;
        user.AdvanceRequestedAt = DateTime.UtcNow - TimeSpan.FromSeconds(6);
        state.AddUser(user);

        var expired = state.CheckCatchupTimeouts();

        Assert.Empty(expired);
    }

    // ── User leaves dialogue → re-evaluation ────────────────────────────────

    [Fact]
    public void UserLeaves_RemainingUser_BecomesSolo()
    {
        var state = new ChannelState();
        var a = CreateUser("1");
        var b = CreateUser("2");
        PutInDialogue(a, "npc1", "hash1");
        PutInDialogue(b, "npc1", "hash1");
        a.AdvanceRequested = true;
        b.AdvanceRequested = false;
        state.AddUser(a);
        state.AddUser(b);

        // Initially a waits
        var result = state.EvaluateAdvance(a, out _);
        Assert.Equal(ChannelState.AdvanceResult.Wait, result);

        // b leaves dialogue
        b.DialogueState = DialogueState.Idle;
        b.NpcId = "";

        // Re-evaluate: a is now solo
        result = state.EvaluateAdvance(a, out var group);
        Assert.Equal(ChannelState.AdvanceResult.GrantSolo, result);
        Assert.Single(group);
    }
}
