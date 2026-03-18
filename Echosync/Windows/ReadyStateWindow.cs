using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Echosync.DataClasses;
using Echotools.Logging.DataClasses;
using Echotools.Logging.Enums;
using Echotools.Logging.Services;
using Echosync.Helper;
using Lumina.Data.Files;

namespace Echosync.Windows;

public class ReadyStateWindow : Window, IDisposable
{
    private readonly Configuration _configuration;
    private readonly SyncClientHelper _syncClient;
    private readonly AddonTalkHelper _addonTalkHelper;
    private readonly ILogService _log;

    private IDalamudTextureWrap? ReadyCheckIconTexture { get; }

    public ReadyStateWindow(
        IDalamudPluginInterface pluginInterface,
        ITextureProvider textureProvider,
        IDataManager dataManager,
        Configuration configuration,
        SyncClientHelper syncClient,
        AddonTalkHelper addonTalkHelper,
        ILogService log)
        : base($"Echosync {Plugin.PluginVersion}###EchosyncReadyState")
    {
        _configuration = configuration;
        _syncClient = syncClient;
        _addonTalkHelper = addonTalkHelper;
        _log = log;

        Flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBackground |
                ImGuiWindowFlags.NoNav;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(100, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        ForceMainWindow = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;

        ReadyCheckIconTexture = textureProvider.CreateFromTexFile(dataManager.GetFile<TexFile>("ui/uld/ReadyCheck_hr1.tex")!);
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        Size = ImGuiHelpers.MainViewport.Size;
        Position = ImGuiHelpers.MainViewport.Pos;
    }

    public override void Draw()
    {
        var drawList = ImGui.GetWindowDrawList();
        var iconSize = new Vector2(24, 24) * _addonTalkHelper.AddonScale;
        var offsetX = 16;

        var connectedOthers = _syncClient.ConnectedPlayerCount;
        if (connectedOthers <= 0 || !_syncClient.Connected || string.IsNullOrWhiteSpace(_addonTalkHelper.ActiveDialogue)) return;

        var totalCount = _syncClient.SyncGroup.TotalCount > 0
            ? _syncClient.SyncGroup.TotalCount
            : connectedOthers + 1;
        var readyCount = _syncClient.SyncGroup.ReadyCount;

        var xPos = (_addonTalkHelper.AddonPos.X + _addonTalkHelper.AddonWidth) - ((offsetX + iconSize.X) * (totalCount + 1));
        _log.Debug(nameof(Draw), $"{xPos}", new EKEventId(0, TextSource.None));

        for (var i = 1; i <= totalCount; i++)
        {
            var iconPos = new Vector2(xPos * _addonTalkHelper.AddonScale, _addonTalkHelper.AddonPos.Y + 120 * _addonTalkHelper.AddonScale);
            _log.Debug(nameof(Draw), $"{iconPos}", new EKEventId(0, TextSource.None));
            var iconOffset = new Vector2(offsetX * (i - 1), 0) * _addonTalkHelper.AddonScale;
            iconPos += iconOffset;
            if (i <= readyCount)
                drawList.AddImage(ReadyCheckIconTexture!.Handle, iconPos, iconPos + iconSize, new Vector2(0.0f, 0.0f), new Vector2(0.5f, 1.0f));
            else
                drawList.AddImage(ReadyCheckIconTexture!.Handle, iconPos, iconPos + iconSize, new Vector2(0.5f, 0.0f), new Vector2(1.0f));
        }
    }
}
