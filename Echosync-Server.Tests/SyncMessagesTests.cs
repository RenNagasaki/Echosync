using Echosync.Data.Enums;

namespace Echosync.Server.Tests;

public class SyncMessagesTests
{
    [Theory]
    [InlineData(SyncMessages.Test, 0)]
    [InlineData(SyncMessages.Authenticate, 1)]
    [InlineData(SyncMessages.CreateChannel, 2)]
    [InlineData(SyncMessages.ServerShutdown, 3)]
    public void ExistingMessages_HaveExpectedValues(SyncMessages message, int expected)
    {
        Assert.Equal(expected, (int)message);
    }

    [Fact]
    public void C2S_Messages_AreDefined()
    {
        Assert.True(Enum.IsDefined(typeof(SyncMessages), SyncMessages.C2S_DialogueEnter));
        Assert.True(Enum.IsDefined(typeof(SyncMessages), SyncMessages.C2S_DialogueAdvance));
        Assert.True(Enum.IsDefined(typeof(SyncMessages), SyncMessages.C2S_DialogueExit));
        Assert.True(Enum.IsDefined(typeof(SyncMessages), SyncMessages.C2S_PositionReport));
        Assert.True(Enum.IsDefined(typeof(SyncMessages), SyncMessages.C2S_Ping));
    }

    [Fact]
    public void S2C_Messages_AreDefined()
    {
        Assert.True(Enum.IsDefined(typeof(SyncMessages), SyncMessages.S2C_Authenticated));
        Assert.True(Enum.IsDefined(typeof(SyncMessages), SyncMessages.S2C_AuthFailed));
        Assert.True(Enum.IsDefined(typeof(SyncMessages), SyncMessages.S2C_ChannelState));
        Assert.True(Enum.IsDefined(typeof(SyncMessages), SyncMessages.S2C_AdvanceGranted));
        Assert.True(Enum.IsDefined(typeof(SyncMessages), SyncMessages.S2C_AdvanceWait));
        Assert.True(Enum.IsDefined(typeof(SyncMessages), SyncMessages.S2C_AdvanceCatchupWait));
        Assert.True(Enum.IsDefined(typeof(SyncMessages), SyncMessages.S2C_SyncGroupUpdate));
        Assert.True(Enum.IsDefined(typeof(SyncMessages), SyncMessages.S2C_UserTimedOut));
        Assert.True(Enum.IsDefined(typeof(SyncMessages), SyncMessages.S2C_Pong));
    }

    [Fact]
    public void MessageIntValues_CanRoundTrip()
    {
        foreach (SyncMessages msg in Enum.GetValues(typeof(SyncMessages)))
        {
            var intValue = (int)msg;
            var parsed = (SyncMessages)intValue;
            Assert.Equal(msg, parsed);
        }
    }
}
