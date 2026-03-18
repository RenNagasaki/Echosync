namespace Echosync.Server.Tests;

public class UserStateTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var user = new UserState("ws1", "127.0.0.1", "channel1");

        Assert.Equal("ws1", user.WebSocketId);
        Assert.Equal("127.0.0.1", user.IpAddress);
        Assert.Equal("channel1", user.Channel);
        Assert.NotEmpty(user.SessionId);
    }

    [Fact]
    public void Defaults_AreIdle()
    {
        var user = new UserState("ws1", "127.0.0.1", "ch1");

        Assert.Equal(DialogueState.Idle, user.DialogueState);
        Assert.Equal("", user.NpcId);
        Assert.Equal("", user.CurrentDialogueHash);
        Assert.Equal(0, user.DialogueIndex);
        Assert.False(user.AdvanceRequested);
        Assert.Equal(0f, user.PosX);
        Assert.Equal(0f, user.PosY);
        Assert.Equal(0f, user.PosZ);
        Assert.Equal(DateTime.MinValue, user.LastPositionUpdate);
    }

    [Fact]
    public void SessionId_IsDeterministic()
    {
        var a = new UserState("ws1", "127.0.0.1", "ch1");
        var b = new UserState("ws1", "127.0.0.1", "ch1");
        Assert.Equal(a.SessionId, b.SessionId);
    }

    [Fact]
    public void SessionId_DiffersByWebSocketId()
    {
        var a = new UserState("ws1", "127.0.0.1", "ch1");
        var b = new UserState("ws2", "127.0.0.1", "ch1");
        Assert.NotEqual(a.SessionId, b.SessionId);
    }

    [Fact]
    public void DialogueState_Transitions()
    {
        var user = new UserState("ws1", "127.0.0.1", "ch1");

        user.DialogueState = DialogueState.InDialogue;
        Assert.Equal(DialogueState.InDialogue, user.DialogueState);

        user.DialogueState = DialogueState.WaitingAdvance;
        Assert.Equal(DialogueState.WaitingAdvance, user.DialogueState);

        user.DialogueState = DialogueState.Idle;
        Assert.Equal(DialogueState.Idle, user.DialogueState);
    }
}
