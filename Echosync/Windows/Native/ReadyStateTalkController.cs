using System;
using System.Numerics;
using System.Text;
using Echosync.DataClasses;
using Echosync.Helper;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Controllers;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace Echosync.Windows.Native;

public sealed unsafe class ReadyStateTalkController : IDisposable
{
    private readonly Configuration _configuration;
    private readonly SyncClientHelper _syncClient;

    private readonly AddonController _talkController;
    private TextNode? _readyText;

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
        _readyText?.Dispose();

        _readyText = new TextNode
        {
            Size = new Vector2(120, 24),
            IsVisible = false,
            FontSize = 14,
            AlignmentType = AlignmentType.Right,
        };

        _readyText.NodeId = (uint)(addon->UldManager.NodeListCount + 1);
        _readyText.AttachNode(addon);
    }

    private void OnUpdate(AtkUnitBase* addon)
    {
        if (_readyText == null) return;

        if (!_syncClient.Connected || !addon->IsVisible || _syncClient.SyncGroup.TotalCount <= 0)
        {
            _readyText.IsVisible = false;
            return;
        }

        var totalCount = _syncClient.SyncGroup.TotalCount;
        var readyCount = _syncClient.SyncGroup.ReadyCount;

        // Position next to the advance cursor at bottom-right of the Talk addon
        var rootNode = addon->RootNode;
        var addonWidth = (float)rootNode->Width;
        var addonHeight = (float)rootNode->Height;

        _readyText.Position = new Vector2(addonWidth - 195, addonHeight - 73);
        _readyText.String = new ReadOnlySeString(Encoding.UTF8.GetBytes($"{readyCount}/{totalCount}"));
        _readyText.IsVisible = true;
    }

    private void OnDetach(AtkUnitBase* addon)
    {
        _readyText?.Dispose();
        _readyText = null;
    }

    public void Dispose()
    {
        _talkController.Dispose();
    }
}
