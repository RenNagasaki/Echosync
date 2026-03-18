using System;
using System.Numerics;
using Echosync.DataClasses;
using Echosync.Helper;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Classes;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;

namespace Echosync.Windows.Native;

public sealed unsafe class ReadyStateTalkController : IDisposable
{
    private const int MaxIcons = 16;

    private readonly Configuration _configuration;
    private readonly SyncClientHelper _syncClient;

    private readonly AddonController _talkController;
    private readonly ImageNode[] _iconNodes = new ImageNode[MaxIcons];

    // Texture paths for game icons
    private const string ReadyCheckTex = "ui/uld/ReadyCheck_hr1.tex";

    // ReadyCheck texture: left half = ready (green), right half = not ready (red)
    private const int ReadyCheckReadyPartId = 0;
    private const int ReadyCheckNotReadyPartId = 1;

    public ReadyStateTalkController(
        Configuration configuration,
        SyncClientHelper syncClient)
    {
        _configuration = configuration;
        _syncClient = syncClient;

        _talkController = new AddonController("Talk");
        _talkController.OnAttach += OnAttach;
        _talkController.OnUpdate += OnUpdate;
        _talkController.OnDetach += OnDetach;
        _talkController.Enable();
    }

    private void OnAttach(AtkUnitBase* addon)
    {
        for (var i = 0; i < MaxIcons; i++)
        {
            var node = new ImageNode
            {
                Size = new Vector2(24, 24),
                IsVisible = false,
                FitTexture = true,
            };

            node.AddPart(new Part { TexturePath = ReadyCheckTex, Size = new Vector2(32, 32), U = 0, V = 0 }); // ready
            node.AddPart(new Part { TexturePath = ReadyCheckTex, Size = new Vector2(32, 32), U = 32, V = 0 }); // not ready

            _iconNodes[i] = node;
            node.AttachNode(addon);
        }
    }

    private void OnUpdate(AtkUnitBase* addon)
    {
        // Show icons when Talk addon is visible and others are connected
        var connectedOthers = _syncClient.ConnectedPlayerCount;
        if (connectedOthers <= 0 || !_syncClient.Connected || !addon->IsVisible)
        {
            HideAllIcons();
            return;
        }

        // Use sync group data when available, otherwise show all connected players
        var totalCount = _syncClient.SyncGroup.TotalCount > 0
            ? _syncClient.SyncGroup.TotalCount
            : connectedOthers + 1;
        var readyCount = _syncClient.SyncGroup.ReadyCount;

        var addonScale = addon->Scale;
        var addonX = (float)addon->X;
        var addonY = (float)addon->Y;
        var addonWidth = addon->GetScaledWidth(true);

        var iconSize = new Vector2(24, 24) * addonScale;
        const float offsetX = 16;

        var xPos = (addonX + addonWidth) - ((offsetX + iconSize.X) * (totalCount + 1));
        var yPos = addonY + 120 * addonScale;

        var nodeIndex = 0;

        // Show ready check icons for sync group members
        for (var i = 1; i <= totalCount && nodeIndex < MaxIcons; i++)
        {
            var node = _iconNodes[nodeIndex];
            var iconPos = new Vector2(xPos * addonScale + offsetX * (i - 1) * addonScale, yPos);
            node.Position = iconPos;
            node.Size = iconSize;
            node.PartId = i <= readyCount
                ? (uint)ReadyCheckReadyPartId
                : (uint)ReadyCheckNotReadyPartId;
            node.IsVisible = true;
            nodeIndex++;
        }

        // Hide remaining icons
        for (; nodeIndex < MaxIcons; nodeIndex++)
            _iconNodes[nodeIndex].IsVisible = false;
    }

    private void OnDetach(AtkUnitBase* addon)
    {
        foreach (var node in _iconNodes)
            node?.Dispose();
    }

    private void HideAllIcons()
    {
        foreach (var node in _iconNodes)
        {
            if (node != null)
                node.IsVisible = false;
        }
    }

    public void Dispose()
    {
        _talkController.Dispose();
    }
}
