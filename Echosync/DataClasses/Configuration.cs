using Dalamud.Configuration;
using System;

namespace Echosync.DataClasses;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool IsConfigWindowMovable { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public bool ConnectAtStart { get; set; }
    public bool OnlySpecialNpcs { get; set; }
    public bool WaitForNearbyUsers { get; set; }
    public float MaxPlayerDistance { get; set; } = 10f;
    public string SyncServer { get; set; } = "wss://sync.echotools.cloud";
    public string SyncChannel { get; set; } = "";
    public string SyncPassword { get; set; } = "";
    public LogConfig? LogConfig { get; set; }

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}