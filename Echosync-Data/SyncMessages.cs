namespace Echosync.Data.Enums;

public enum SyncMessages
{
    // Existing (channel management)
    Test,
    Authenticate,
    CreateChannel,
    ServerShutdown,

    // Client -> Server
    C2S_DialogueEnter,      // npcId|dialogueTextHash
    C2S_DialogueAdvance,    // npcId|newDialogueTextHash
    C2S_DialogueExit,       // (no params)
    C2S_PositionReport,     // posX|posY|posZ|targetNpcId
    C2S_Ping,

    // Server -> Client
    S2C_Authenticated,
    S2C_AuthFailed,
    S2C_ChannelState,       // connected member list
    S2C_AdvanceGranted,     // proceed with click
    S2C_AdvanceWait,        // hold, others not ready
    S2C_AdvanceCatchupWait, // hold, nearby user joining
    S2C_SyncGroupUpdate,    // npcId|readyCount|totalCount|timeoutSeconds
    S2C_UserTimedOut,       // userId dropped from sync
    S2C_Pong,
}
