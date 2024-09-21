using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace Echosync.DataClasses;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool IsConfigWindowMovable { get; set; } = true;
    public bool SomePropertyToBeSavedAndWithADefault { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public bool ConnectAtStart { get; set; } = false;
    public string SyncServer { get; set; } = "wss://echosync.echotools.cloud";
    public string SyncChannel { get; set; } = "";
    public LogConfig logConfig { get; set; } = new LogConfig();

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
