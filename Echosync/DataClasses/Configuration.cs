using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;
using Echotools.Logging.DataClasses;
using Echotools.Logging.Enums;

namespace Echosync.DataClasses;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool IsConfigWindowMovable { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public bool ConnectAtStart { get; set; }
    public bool OnlySpecialNpcs { get; set; }
    public string SyncServer { get; set; } = "wss://sync.echotools.cloud";
    public string SyncChannel { get; set; } = "";
    public string SyncPassword { get; set; } = "";
    public Dictionary<TextSource, LogSourceConfig> LogConfigs { get; set; } = new()
    {
        [TextSource.None] = new LogSourceConfig(),
        [TextSource.Sync] = new LogSourceConfig(),
    };

    [NonSerialized]
    private IDalamudPluginInterface? _pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
    }

    public LogSourceConfig GetLogConfig(TextSource source)
    {
        if (!LogConfigs.ContainsKey(source))
            LogConfigs[source] = new LogSourceConfig();
        return LogConfigs[source];
    }

    public void Save()
    {
        _pluginInterface?.SavePluginConfig(this);
    }
}
