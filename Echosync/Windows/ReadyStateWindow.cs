using System;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using System.Reflection;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Echosync.DataClasses;
using Echosync.Enums;
using Echosync.Helper;
using System.Collections.Generic;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin.Services;
using Lumina.Data.Files;

namespace Echosync;

public class ReadyStateWindow : Window, IDisposable
{
    IDalamudTextureWrap? ReadyCheckIconTexture { get; set; }

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ReadyStateWindow(Plugin plugin, IDataManager dataManager) : base("Echosync-ReadyState")
    {
        Flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoMouseInputs | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBackground |
                ImGuiWindowFlags.NoNav;

        SizeConstraints = new WindowSizeConstraints()
        {
            MinimumSize = new Vector2(100, 100),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        ForceMainWindow = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ReadyCheckIconTexture = Plugin.TextureProvider.CreateFromTexFile(dataManager.GetFile<TexFile>("ui/uld/ReadyCheck_hr1.tex")!);
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
        var offsetX = 16;

        if (SyncClientHelper.ConnectedPlayersDialogue > 0)
        {
            for (int i = 1; i <= SyncClientHelper.ConnectedPlayersDialogue; i++)
            {
                var iconPos = new Vector2(AddonTalkHelper.AddonPos.X + (480 + offsetX * (8 - SyncClientHelper.ConnectedPlayersDialogue)) * AddonTalkHelper.AddonScale, AddonTalkHelper.AddonPos.Y + 120 * AddonTalkHelper.AddonScale);
                var iconOffset = new Vector2(offsetX * (i - 1), 0) * AddonTalkHelper.AddonScale;
                iconPos += iconOffset;
                if (i <= SyncClientHelper.ConnectedPlayersReady)
                    drawList.AddImage(ReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2(0.0f, 0.0f), new Vector2(0.5f, 1.0f));
                else
                    drawList.AddImage(ReadyCheckIconTexture.ImGuiHandle, iconPos, iconPos + iconSize, new Vector2(0.5f, 0.0f), new Vector2(1.0f));
            }
        }
    }
}
