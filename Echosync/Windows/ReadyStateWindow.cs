using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Echosync.DataClasses;
using Echosync.Enums;
using Echosync.Helper;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;
using Dalamud.Plugin;

namespace Echosync.Windows;

public class ReadyStateWindow : Window, IDisposable
{
    private IDalamudTextureWrap? ReadyCheckIconTexture { get; }
    private IDalamudTextureWrap? WindowAButtonIconTexture { get; }
    private IDalamudTextureWrap? ConfigPadCalibrationXInputIconTexture { get; }
    private readonly Configuration _configuration;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ReadyStateWindow(IDataManager dataManager, IDalamudPluginInterface pluginInterface, Configuration configuration) : base("Echosync-ReadyState")
    {
        Flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBackground |
                ImGuiWindowFlags.NoNav;

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(100, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        _configuration = configuration;
        ForceMainWindow = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ReadyCheckIconTexture = Plugin.TextureProvider.CreateFromTexFile(dataManager.GetFile<TexFile>("ui/uld/ReadyCheck_hr1.tex")!);
        var uldWrapper = pluginInterface.UiBuilder.LoadUld("ui/uld/PartyMemberList.uld");
        WindowAButtonIconTexture = uldWrapper.LoadTexturePart("ui/uld/WindowA_Button.tex", 1);
        uldWrapper = pluginInterface.UiBuilder.LoadUld("ui/uld/PerformanceGamePadGuide.uld");
        ConfigPadCalibrationXInputIconTexture = uldWrapper.LoadTexturePart("ui/uld/ConfigPadCalibrationXinput.tex", 2);
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        Size = ImGuiHelpers.MainViewport.Size;
        Position = ImGuiHelpers.MainViewport.Pos;
    }

    public override void Draw()
    {
        DrawReadyStates();
    }

    private void DrawReadyStates()
    {
        var drawList = ImGui.GetWindowDrawList();
        var iconSize = new Vector2(24, 24) * AddonTalkHelper.AddonScale;
        var iconSizeSmall = new Vector2(20, 20) * AddonTalkHelper.AddonScale;
        var offsetX = 16;

        if (SyncClientHelper.ConnectedPlayersDialogue > 0)
        {
            var closePlayers = DalamudHelper.GetClosePlayers(SyncClientHelper.ConnectedPlayers, _configuration.MaxPlayerDistance);
            var xPos = (AddonTalkHelper.AddonPos.X + AddonTalkHelper.AddonWidth) - ((offsetX + iconSize.X) * (SyncClientHelper.ConnectedPlayers.Count + 2));
            LogHelper.Debug("XPOS", $"{xPos}", new EKEventId(0, TextSource.None));
            for (int i = 1; i <= SyncClientHelper.ConnectedPlayersDialogue; i++)
            {
                var iconPos = new Vector2(xPos * AddonTalkHelper.AddonScale, AddonTalkHelper.AddonPos.Y + 120 * AddonTalkHelper.AddonScale);
                LogHelper.Debug("XPOS", $"{iconPos}", new EKEventId(0, TextSource.None));
                var iconOffset = new Vector2(offsetX * (i - 1), 0) * AddonTalkHelper.AddonScale;
                iconPos += iconOffset;
                if (i <= SyncClientHelper.ConnectedPlayersReady)
                    drawList.AddImage(ReadyCheckIconTexture!.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2(0.0f, 0.0f), new Vector2(0.5f, 1.0f));
                else
                    drawList.AddImage(ReadyCheckIconTexture!.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2(0.5f, 0.0f), new Vector2(1.0f));
            }

            for (int i = SyncClientHelper.ConnectedPlayersDialogue + 1; i <= SyncClientHelper.ConnectedPlayersNpc.Count + 1; i++)
            {
                var iconPos = new Vector2(xPos * AddonTalkHelper.AddonScale, AddonTalkHelper.AddonPos.Y + 120 * AddonTalkHelper.AddonScale);
                var iconOffset = new Vector2(offsetX * (i - 1), 0) * AddonTalkHelper.AddonScale;
                iconPos += iconOffset;
                drawList.AddImage(ConfigPadCalibrationXInputIconTexture!.ImGuiHandle, iconPos, iconPos + iconSizeSmall, new Vector2(0.0f, 0.0f), new Vector2(1.0f, 1.0f));
            }

            if (_configuration.WaitForNearbyUsers)
            {
                for (int i = 1; i <= closePlayers; i++)
                {
                    var iconPos = new Vector2(xPos * AddonTalkHelper.AddonScale, AddonTalkHelper.AddonPos.Y + 120 * AddonTalkHelper.AddonScale);
                    var iconOffset = new Vector2(offsetX * (SyncClientHelper.ConnectedPlayersNpc.Count + i), 0) * AddonTalkHelper.AddonScale;
                    iconPos += iconOffset;
                    drawList.AddImage(WindowAButtonIconTexture!.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2(0.0f, 0.0f), new Vector2(1.0f, 1.0f));
                }
            }
        }
    }
}