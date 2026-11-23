using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Echosync.DataClasses;
using Echosync.Helper;
using Echosync.Localization;
using Echotools.Logging.DataClasses;
using Echotools.Logging.Enums;
using Echotools.Logging.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Nodes;

namespace Echosync.Windows.Native;

public sealed unsafe partial class NativeConfigWindow : NativeAddon
{
    private readonly Configuration _configuration;
    private readonly SyncClientHelper _syncClient;
    private readonly ILogService _log;

    private Vector2 _topContentPos;
    private Vector2 _topContentSize;
    private float _contentWidth;

    private int _activeTopTab;

    // General tab controls
    private CheckboxNode? _onlySpecialNpcsCheck;
    private CheckboxNode? _connectAtStartCheck;
    private TextInputNode? _syncServerInput;
    private TextInputNode? _syncChannelInput;
    private TextInputNode? _syncPasswordInput;
    private TextButtonNode? _connectButton;

    // Tab panels
    private ScrollingListNode? _generalPanel;
    private ScrollingListNode? _fakeUserPanel;

    [SetsRequiredMembers]
    public NativeConfigWindow(
        Configuration configuration,
        SyncClientHelper syncClient,
        ILogService log)
    {
        InternalName = "EchosyncConfig";
        Title = $"Echosync {Plugin.PluginVersion}";
        Size = new Vector2(540, 480);
        RespectCloseAll = false;

        _configuration = configuration;
        _syncClient = syncClient;
        _log = log;
    }

    protected override void OnSetup(AtkUnitBase* addon)
    {
        try
        {
            var pos = ContentStartPosition;
            var size = ContentSize;
            const float tabH = 32f;

            var topTabBar = new TabBarNode { Size = new Vector2(size.X, tabH), Position = pos };

            _topContentPos = pos + new Vector2(0, tabH + 2);
            _topContentSize = size - new Vector2(0, tabH + 2);
            _contentWidth = size.X;

            _generalPanel = SetupGeneral(_topContentPos, _topContentSize);
            SetupLogs();
            _fakeUserPanel = SetupFakeUser(_topContentPos, _topContentSize);

            topTabBar.AddTab(Loc.S("General"), () => ShowTopPanel(0));
            topTabBar.AddTab(Loc.S("Logs"), () => ShowTopPanel(1));
            topTabBar.AddTab(Loc.S("Fakeuser"), () => ShowTopPanel(2));

            AddNode(topTabBar);
            AddNode(_generalPanel);
            AddLogsNodes();
            AddNode(_fakeUserPanel);

            ShowTopPanel(0);
        }
        catch (Exception ex)
        {
            _log.Error(nameof(OnSetup), $"Failed to setup native config window: {ex}", new EKEventId(0, TextSource.None));
        }
    }

    protected override void OnUpdate(AtkUnitBase* addon)
    {
        try
        {
            UpdateGeneral();
            UpdateLogs();
        }
        catch (Exception ex)
        {
            _log.Error(nameof(OnUpdate), $"Error in native config update: {ex}", new EKEventId(0, TextSource.None));
        }
    }

    private void ShowTopPanel(int index)
    {
        _activeTopTab = index;

        SetVisible(_generalPanel, index == 0);
        ShowLogsSection(index == 1);
        SetVisible(_fakeUserPanel, index == 2);
    }

    private ScrollingListNode SetupGeneral(Vector2 pos, Vector2 size)
    {
        var w = size.X;
        var list = Panel(pos, size);

        var enabledCheck = Check(Loc.S("Enabled"), w, _configuration.Enabled, v =>
        {
            _configuration.Enabled = v;
            _configuration.Save();
        });

        _onlySpecialNpcsCheck = Check(Loc.S("Only special NPCs (Any marker above head)"), w, _configuration.OnlySpecialNpcs, v =>
        {
            _configuration.OnlySpecialNpcs = v;
            _configuration.Save();
        });

        _connectAtStartCheck = Check(Loc.S("Connect at start"), w, _configuration.ConnectAtStart, v =>
        {
            _configuration.ConnectAtStart = v;
            _configuration.Save();
            if (v) _syncClient.Connect();
        });

        _syncServerInput = Input(Loc.S("Sync server"), w, 80, _configuration.SyncServer, v =>
        {
            _configuration.SyncServer = v;
            _configuration.Save();
        });

        _syncChannelInput = Input(Loc.S("Sync channel"), w, 80, _configuration.SyncChannel, v =>
        {
            _configuration.SyncChannel = v;
            _configuration.Save();
        });

        _syncPasswordInput = Input(Loc.S("Sync password"), w, 80, _configuration.SyncPassword, v =>
        {
            _configuration.SyncPassword = v;
            _configuration.Save();
        });

        _connectButton = Button(Loc.S("Disconnect"), 100, () =>
        {
            if (_syncClient.Connected)
                _syncClient.Disconnect();
            else
                _syncClient.Connect();
        });

        list.AddNode(enabledCheck);
        list.AddNode(_onlySpecialNpcsCheck);
        list.AddNode(_syncServerInput);
        list.AddNode(_syncChannelInput);
        list.AddNode(_syncPasswordInput);

        var connectRow = new HorizontalListNode { Size = new Vector2(w, 28), ItemSpacing = 8 };
        connectRow.AddNode(_connectButton);
        connectRow.AddNode(_connectAtStartCheck);
        list.AddNode(connectRow);

        return list;
    }

    private void UpdateGeneral()
    {
        var enabled = _configuration.Enabled;
        Dim(_onlySpecialNpcsCheck, enabled);
        Dim(_connectAtStartCheck, enabled);
        Dim(_syncServerInput, enabled);
        Dim(_syncChannelInput, enabled);
        Dim(_syncPasswordInput, enabled);
        Dim(_connectButton, enabled);

        if (_connectButton != null)
            _connectButton.String = _syncClient.Connected ? Loc.S("Disconnect") : Loc.S("Connect");
    }

    private ScrollingListNode SetupFakeUser(Vector2 pos, Vector2 size)
    {
        var w = size.X;
        var list = Panel(pos, size);

        var row1 = new HorizontalListNode { Size = new Vector2(w, 28), ItemSpacing = 8 };
        row1.AddNode(Button(Loc.S("Enter Dialogue"), 110, () => _syncClient.SendDialogueEnter("fake-npc", "fake-hash")));
        row1.AddNode(Button(Loc.S("Exit Dialogue"), 110, () => _syncClient.SendDialogueExit()));

        var row2 = new HorizontalListNode { Size = new Vector2(w, 28), ItemSpacing = 8 };
        row2.AddNode(Button(Loc.S("Request Advance"), 120, () => _syncClient.SendDialogueAdvance("fake-hash")));

        list.AddNode(row1);
        list.AddNode(row2);

        return list;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void Dim(NodeBase? node, bool enabled)
    {
        if (node != null) node.Alpha = enabled ? 1.0f : 0.4f;
    }

    private static void SetVisible(NodeBase? node, bool visible)
    {
        if (node != null) node.IsVisible = visible;
    }

    private static ScrollingListNode Panel(Vector2 pos, Vector2 size) => new()
    {
        Position = pos,
        Size = size,
        FitWidth = true,
        ItemSpacing = 4,
    };

    private static CheckboxNode Check(string label, float width, bool initial, Action<bool> onChange) => new()
    {
        Size = new Vector2(width, 24),
        String = label,
        IsChecked = initial,
        OnClick = onChange,
    };

    private static TextButtonNode Button(string label, float minWidth, Action onClick)
    {
        var node = new TextButtonNode { Size = new Vector2(minWidth, 24), String = label };
        var textW = node.LabelNode.GetTextDrawSize(label).X + 36;
        if (textW > minWidth) node.Size = new Vector2(textW, 24);
        node.OnClick = onClick;
        return node;
    }

    private static TextInputNode Input(string placeholder, float width, int maxChars, string initial, Action<string> onComplete)
    {
        var node = new TextInputNode
        {
            Size = new Vector2(width, 28),
            MaxCharacters = maxChars,
            PlaceholderString = placeholder,
            String = initial,
        };
        node.OnInputReceived = s => onComplete(s.ToString());
        return node;
    }

    private static TextNode Label(string text, float width) => new()
    {
        Size = new Vector2(width, 18),
        String = text,
        FontType = FontType.Axis,
        FontSize = 12,
    };

    private static HorizontalLineNode Separator(float width) => new()
    {
        Size = new Vector2(width, 4),
    };

    private static ResNode Spacer(float width, float height) => new()
    {
        Size = new Vector2(width, height),
        Alpha = 0,
    };

    private static TextButtonNode CreateCollapsibleSection(
        ScrollingListNode list, string title, float width, bool startCollapsed, NodeBase[] contentNodes)
    {
        var arrow = startCollapsed ? "[+]" : "[-]";
        TextButtonNode? toggle = null;
        toggle = new TextButtonNode { Size = new Vector2(width, 24), String = $"{arrow} {title}" };
        toggle.OnClick = () =>
        {
            var isHidden = contentNodes.Length > 0 && !contentNodes[0].IsVisible;
            foreach (var n in contentNodes)
                n.IsVisible = isHidden;
            toggle!.String = isHidden ? $"[-] {title}" : $"[+] {title}";
            list.RecalculateLayout();
        };

        if (startCollapsed)
            foreach (var n in contentNodes)
                n.IsVisible = false;

        list.AddNode(toggle);
        foreach (var n in contentNodes)
            list.AddNode(n);

        return toggle;
    }
}
