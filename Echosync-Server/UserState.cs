using System.Security.Cryptography;
using System.Text;

namespace Echosync.Server;

public enum DialogueState
{
    Idle,
    InDialogue,
    WaitingAdvance,
}

public class UserState
{
    public string WebSocketId { get; set; }
    public string SessionId { get; set; }
    public string IpAddress { get; set; }
    public string Channel { get; set; }

    // Position tracking
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }
    public DateTime LastPositionUpdate { get; set; } = DateTime.MinValue;

    // Dialogue state
    public DialogueState DialogueState { get; set; } = DialogueState.Idle;
    public string NpcId { get; set; } = "";
    public string CurrentDialogueHash { get; set; } = "";
    public int DialogueIndex { get; set; }

    // Advance request tracking
    public bool AdvanceRequested { get; set; }
    public DateTime AdvanceRequestedAt { get; set; } = DateTime.MinValue;

    public UserState(string webSocketId, string ipAddress, string channel)
    {
        WebSocketId = webSocketId;
        IpAddress = ipAddress;
        Channel = channel;
        SessionId = GenerateSessionId(webSocketId);
    }

    private static string GenerateSessionId(string webSocketId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(webSocketId));
        return Convert.ToHexString(bytes)[..8];
    }
}
