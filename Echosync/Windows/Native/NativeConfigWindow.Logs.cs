using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Echosync.Localization;
using Echotools.Logging.DataClasses;
using Echotools.Logging.Enums;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Nodes;

namespace Echosync.Windows.Native;

public sealed unsafe partial class NativeConfigWindow
{
    private static readonly (string Label, TextSource Source)[] LogTabs =
    {
        ("General", TextSource.None),
        ("Sync", TextSource.Sync),
    };

    private TabBarNode? _logsTabBar;
    private readonly ScrollingListNode?[] _logsPanels = new ScrollingListNode?[LogTabs.Length];
    private readonly bool[] _logsDirty = new bool[LogTabs.Length];

    private int _activeLogTab;

    private string _logsFilterMethod = "";
    private string _logsFilterMessage = "";
    private string _logsFilterId = "";
    private bool _logsFilterExpanded;

    private const float LogColTimestamp = 85f;
    private const float LogColMethod = 120f;
    private const float LogColId = 40f;

    private float LogColMessage => _contentWidth - LogColTimestamp - LogColMethod - LogColId - 3 * 4 - 20;

    private void SetupLogs()
    {
        var w = _contentWidth;

        _logsTabBar = new TabBarNode { Size = new Vector2(w, 32), Position = _topContentPos };

        var innerPos = _topContentPos + new Vector2(0, 34);
        var innerSize = _topContentSize - new Vector2(0, 34);

        for (var i = 0; i < LogTabs.Length; i++)
        {
            _logsPanels[i] = Panel(innerPos, innerSize);
            _logsDirty[i] = true;
        }

        for (var i = 0; i < LogTabs.Length; i++)
        {
            var idx = i;
            _logsTabBar.AddTab(LogTabs[i].Label, () => ShowLogPanel(idx));
        }

        _log.LogUpdated += OnLogUpdated;
    }

    private void AddLogsNodes()
    {
        AddNode(_logsTabBar!);
        foreach (var p in _logsPanels)
            if (p != null) AddNode(p);
    }

    private void ShowLogsSection(bool visible)
    {
        SetVisible(_logsTabBar, visible);
        if (visible)
        {
            ShowLogPanel(_activeLogTab);
        }
        else
        {
            foreach (var p in _logsPanels)
                SetVisible(p, false);
        }
    }

    private void ShowLogPanel(int index)
    {
        _activeLogTab = index;
        for (var i = 0; i < _logsPanels.Length; i++)
            SetVisible(_logsPanels[i], i == index);

        _logsDirty[index] = true;
    }

    private void OnLogUpdated(TextSource source)
    {
        for (var i = 0; i < LogTabs.Length; i++)
        {
            if (LogTabs[i].Source == source)
                _logsDirty[i] = true;
        }
    }

    private void UpdateLogs()
    {
        if (_activeTopTab != 1) return;

        _log.UpdateMainThreadLogs();

        if (_logsDirty[_activeLogTab])
        {
            _logsDirty[_activeLogTab] = false;
            RebuildLogPanel(_activeLogTab);
        }
    }

    private void RebuildLogPanel(int index)
    {
        var panel = _logsPanels[index];
        if (panel == null) return;

        var w = _contentWidth;
        var source = LogTabs[index].Source;
        var cfg = _configuration.GetLogConfig(source);

        panel.Clear();

        // Filter options (collapsible)
        var showDebug = cfg.ShowDebugLog;
        var showError = cfg.ShowErrorLog;
        var showId0 = cfg.ShowId0;

        var debugCheck = Check(Loc.S("Show debug logs"), w, showDebug, v =>
        {
            cfg.ShowDebugLog = v;
            _configuration.Save();
            _logsDirty[index] = true;
        });

        var errorCheck = Check(Loc.S("Show error logs"), w, showError, v =>
        {
            cfg.ShowErrorLog = v;
            _configuration.Save();
            _logsDirty[index] = true;
        });

        var id0Check = Check(Loc.S("Show ID 0 entries"), w, showId0, v =>
        {
            cfg.ShowId0 = v;
            _configuration.Save();
            _logsDirty[index] = true;
        });

        var jumpToBottom = cfg.JumpToBottom;
        var jumpCheck = Check(Loc.S("Always jump to bottom"), w, jumpToBottom, v =>
        {
            cfg.JumpToBottom = v;
            _configuration.Save();
        });

        var filterContent = new NodeBase[] { debugCheck, errorCheck, id0Check, jumpCheck };
        var filterToggle = CreateCollapsibleSection(panel, Loc.S("Filter Options"), w, !_logsFilterExpanded, filterContent);

        var prevOnClick = filterToggle.OnClick;
        filterToggle.OnClick = () =>
        {
            prevOnClick?.Invoke();
            _logsFilterExpanded = filterContent.Length > 0 && filterContent[0].IsVisible;
        };

        // Column headers
        var headerRow = new HorizontalListNode { Size = new Vector2(w, 20), ItemSpacing = 4 };
        headerRow.AddNode(Label("Timestamp", LogColTimestamp));
        headerRow.AddNode(Label("Method", LogColMethod));
        headerRow.AddNode(Label("Message", LogColMessage));
        headerRow.AddNode(Label("ID", LogColId));
        panel.AddNode(headerRow);

        // Filter inputs
        var filterRow = new HorizontalListNode { Size = new Vector2(w, 28), ItemSpacing = 4 };
        filterRow.AddNode(Spacer(LogColTimestamp, 28));
        filterRow.AddNode(Input("Filter", LogColMethod, 40, _logsFilterMethod, v =>
        {
            _logsFilterMethod = v;
            _logsDirty[_activeLogTab] = true;
        }));
        filterRow.AddNode(Input("Filter", LogColMessage, 80, _logsFilterMessage, v =>
        {
            _logsFilterMessage = v;
            _logsDirty[_activeLogTab] = true;
        }));
        filterRow.AddNode(Input("Filter", LogColId, 10, _logsFilterId, v =>
        {
            _logsFilterId = v;
            _logsDirty[_activeLogTab] = true;
        }));
        panel.AddNode(filterRow);

        panel.AddNode(Separator(w));

        // Data rows
        IEnumerable<LogMessage> filtered = _log.GetLogsForSource(source);

        if (!showDebug) filtered = filtered.Where(log => log.Type != LogType.Debug);
        if (!showError) filtered = filtered.Where(log => log.Type != LogType.Error);
        if (!showId0) filtered = filtered.Where(log => log.EventId.Id != 0);

        if (!string.IsNullOrEmpty(_logsFilterMethod))
            filtered = filtered.Where(log => log.Method.Contains(_logsFilterMethod, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(_logsFilterMessage))
            filtered = filtered.Where(log => log.Message.Contains(_logsFilterMessage, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(_logsFilterId))
            filtered = filtered.Where(log => log.EventId.Id.ToString().Contains(_logsFilterId));

        var list = filtered.OrderBy(log => log.TimeStamp).ToList();
        var toShow = list.Count > 200 ? list.Skip(list.Count - 200).ToList() : list;

        foreach (var log in toShow)
        {
            var hasColor = log.Color != Vector4.Zero;

            var methodLabel = Label(log.Method, LogColMethod);
            methodLabel.FontSize = 11;
            methodLabel.AddTextFlags(TextFlags.WordWrap, TextFlags.MultiLine);
            methodLabel.Size = new Vector2(LogColMethod, 14);
            if (hasColor) methodLabel.TextColor = log.Color;

            var msgLabel = Label(log.Message, LogColMessage);
            msgLabel.FontSize = 11;
            msgLabel.AddTextFlags(TextFlags.WordWrap, TextFlags.MultiLine);
            msgLabel.Size = new Vector2(LogColMessage, 14);
            if (hasColor) msgLabel.TextColor = log.Color;

            var methodH = methodLabel.GetTextDrawSize(false).Y;
            var msgH = msgLabel.GetTextDrawSize(false).Y;
            var rowH = Math.Max(16f, Math.Max(methodH, msgH) + 2);

            methodLabel.Size = new Vector2(LogColMethod, rowH);
            msgLabel.Size = new Vector2(LogColMessage, rowH);

            var row = new HorizontalListNode { Size = new Vector2(w, rowH), ItemSpacing = 4 };

            var tsLabel = Label(log.TimeStamp.ToString("HH:mm:ss"), LogColTimestamp);
            tsLabel.FontSize = 11;
            tsLabel.Size = new Vector2(LogColTimestamp, rowH);
            if (hasColor) tsLabel.TextColor = log.Color;
            row.AddNode(tsLabel);

            row.AddNode(methodLabel);
            row.AddNode(msgLabel);

            var idLabel = Label(log.EventId.Id.ToString(), LogColId);
            idLabel.FontSize = 11;
            idLabel.Size = new Vector2(LogColId, rowH);
            if (hasColor) idLabel.TextColor = log.Color;
            row.AddNode(idLabel);

            panel.AddNode(row);
        }

        if (toShow.Count == 0)
            panel.AddNode(Label(Loc.S("No log entries."), w));

        panel.RecalculateLayout();

        if (cfg.JumpToBottom)
            panel.ScrollPosition = int.MaxValue;
    }
}
