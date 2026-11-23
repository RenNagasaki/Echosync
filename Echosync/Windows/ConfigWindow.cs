using System;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Echosync.DataClasses;
using Echotools.Logging.DataClasses;
using Echotools.Logging.Enums;
using Echotools.Logging.Services;
using Echosync.Helper;
using Echosync.Localization;

namespace Echosync.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration _configuration;
    private readonly SyncClientHelper _syncClient;
    private readonly ILogService _log;

    private List<LogMessage> _filteredLogsGeneral = [];
    private string _filterLogsGeneralMethod = "";
    private string _filterLogsGeneralMessage = "";
    private string _filterLogsGeneralId = "";
    private bool _resetLogGeneralFilter = true;
    private bool _updateLogGeneralFilter = true;

    private List<LogMessage> _filteredLogsSync = [];
    private string _filterLogsSyncMethod = "";
    private string _filterLogsSyncMessage = "";
    private string _filterLogsSyncId = "";
    private bool _resetLogSyncFilter = true;
    private bool _updateLogSyncFilter = true;

    public ConfigWindow(Configuration configuration, SyncClientHelper syncClient, ILogService log)
        : base($"Echosync {Plugin.PluginVersion} {Loc.S("Configuration")}###EchosyncConfig")
    {
        _configuration = configuration;
        _syncClient = syncClient;
        _log = log;

        Flags = ImGuiWindowFlags.AlwaysVerticalScrollbar & ImGuiWindowFlags.HorizontalScrollbar & ImGuiWindowFlags.AlwaysHorizontalScrollbar;
        Size = new Vector2(540, 480);
        SizeCondition = ImGuiCond.FirstUseEver;

        _log.LogUpdated += source =>
        {
            if (source == TextSource.None) _updateLogGeneralFilter = true;
            if (source == TextSource.Sync) _updateLogSyncFilter = true;
        };
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (_configuration.IsConfigWindowMovable)
            Flags &= ~ImGuiWindowFlags.NoMove;
        else
            Flags |= ImGuiWindowFlags.NoMove;
    }

    public override void Draw()
    {
        try
        {
            if (ImGui.BeginTabBar("Settings##ESSettingsTab"))
            {
                if (ImGui.BeginTabItem($"{Loc.S("General")}##ESGeneralTab"))
                {
                    DrawGeneral();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem($"{Loc.S("Logs")}##ESLogsTab"))
                {
                    DrawLogs();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem($"{Loc.S("Fakeuser")}##ESFakeuserTab"))
                {
                    DrawFakeUser();
                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }
        catch (Exception ex)
        {
            _log.Error(nameof(Draw), $"Something went wrong: {ex}", new EKEventId(0, TextSource.None));
        }
    }

    private void DrawGeneral()
    {
        var enabled = _configuration.Enabled;
        if (ImGui.Checkbox($"{Loc.S("Enabled")}##ESEnabled", ref enabled))
        {
            _configuration.Enabled = enabled;
            _configuration.Save();
        }

        using (ImRaii.Disabled(!enabled))
        {
            var onlySpecialNpCs = _configuration.OnlySpecialNpcs;
            if (ImGui.Checkbox($"{Loc.S("Only special NPCs (Any marker above head)")}##ESOnlySpecial", ref onlySpecialNpCs))
            {
                _configuration.OnlySpecialNpcs = onlySpecialNpCs;
                _configuration.Save();
            }

            var syncServer = _configuration.SyncServer;
            if (ImGui.InputText($"{Loc.S("Sync server")}##ESserver", ref syncServer, 80))
            {
                _configuration.SyncServer = syncServer;
                _configuration.Save();
            }

            var syncChannel = _configuration.SyncChannel;
            if (ImGui.InputText($"{Loc.S("Sync channel")}##ESchannel", ref syncChannel, 80))
            {
                _configuration.SyncChannel = syncChannel;
                _configuration.Save();
            }

            var syncPassword = _configuration.SyncPassword;
            if (ImGui.InputText($"{Loc.S("Sync password")}##ESpassword", ref syncPassword, 80))
            {
                _configuration.SyncPassword = syncPassword;
                _configuration.Save();
            }

            if (_syncClient.Connected)
            {
                if (ImGui.Button($"{Loc.S("Disconnect")}##ESDisconnect"))
                    _syncClient.Disconnect();
            }
            else
            {
                if (ImGui.Button($"{Loc.S("Connect")}##ESConnect"))
                    _syncClient.Connect();
            }

            ImGui.SameLine();
            var connectAtStart = _configuration.ConnectAtStart;
            if (ImGui.Checkbox($"{Loc.S("Connect at start")}##ESConnectAtStart", ref connectAtStart))
            {
                _configuration.ConnectAtStart = connectAtStart;
                _configuration.Save();

                if (connectAtStart)
                    _syncClient.Connect();
            }
        }
    }

    private void DrawLogs()
    {
        try
        {
            _log.UpdateMainThreadLogs();

            if (ImGui.BeginTabBar("Logs##ESLogsTab"))
            {
                if (ImGui.BeginTabItem($"{Loc.S("General")}##ESLogGeneralTab"))
                {
                    var cfg = _configuration.GetLogConfig(TextSource.None);
                    if (ImGui.CollapsingHeader($"{Loc.S("Options:")}##ESLogGeneralOptions"))
                    {
                        var showDebugLog = cfg.ShowDebugLog;
                        if (ImGui.Checkbox($"{Loc.S("Show debug logs")}##ESGenShowDebug", ref showDebugLog))
                        {
                            cfg.ShowDebugLog = showDebugLog;
                            _configuration.Save();
                            _updateLogGeneralFilter = true;
                        }
                        var showErrorLog = cfg.ShowErrorLog;
                        if (ImGui.Checkbox($"{Loc.S("Show error logs")}##ESGenShowError", ref showErrorLog))
                        {
                            cfg.ShowErrorLog = showErrorLog;
                            _configuration.Save();
                            _updateLogGeneralFilter = true;
                        }
                        var jumpToBottom = cfg.JumpToBottom;
                        if (ImGui.Checkbox($"{Loc.S("Always jump to bottom")}##ESGenJumpBottom", ref jumpToBottom))
                        {
                            cfg.JumpToBottom = jumpToBottom;
                            _configuration.Save();
                        }
                    }
                    DrawLogTable("General", TextSource.None, cfg.JumpToBottom,
                        ref _filteredLogsGeneral!, ref _updateLogGeneralFilter, ref _resetLogGeneralFilter,
                        ref _filterLogsGeneralMethod, ref _filterLogsGeneralMessage, ref _filterLogsGeneralId);

                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Sync##ESLogSyncTab"))
                {
                    var cfg = _configuration.GetLogConfig(TextSource.Sync);
                    if (ImGui.CollapsingHeader($"{Loc.S("Options:")}##ESLogSyncOptions"))
                    {
                        var showDebugLog = cfg.ShowDebugLog;
                        if (ImGui.Checkbox($"{Loc.S("Show debug logs")}##ESSyncShowDebug", ref showDebugLog))
                        {
                            cfg.ShowDebugLog = showDebugLog;
                            _configuration.Save();
                            _updateLogSyncFilter = true;
                        }
                        var showErrorLog = cfg.ShowErrorLog;
                        if (ImGui.Checkbox($"{Loc.S("Show error logs")}##ESSyncShowError", ref showErrorLog))
                        {
                            cfg.ShowErrorLog = showErrorLog;
                            _configuration.Save();
                            _updateLogSyncFilter = true;
                        }
                        var showId0 = cfg.ShowId0;
                        if (ImGui.Checkbox($"{Loc.S("Show ID: 0")}##ESSyncShowId0", ref showId0))
                        {
                            cfg.ShowId0 = showId0;
                            _configuration.Save();
                            _updateLogSyncFilter = true;
                        }
                        var jumpToBottom = cfg.JumpToBottom;
                        if (ImGui.Checkbox($"{Loc.S("Always jump to bottom")}##ESSyncJumpBottom", ref jumpToBottom))
                        {
                            cfg.JumpToBottom = jumpToBottom;
                            _configuration.Save();
                        }
                    }
                    DrawLogTable("Sync", TextSource.Sync, cfg.JumpToBottom,
                        ref _filteredLogsSync!, ref _updateLogSyncFilter, ref _resetLogSyncFilter,
                        ref _filterLogsSyncMethod, ref _filterLogsSyncMessage, ref _filterLogsSyncId);

                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }
        catch (Exception ex)
        {
            _log.Error(nameof(DrawLogs), $"Something went wrong: {ex}", new EKEventId(0, TextSource.None));
        }
    }

    private void DrawLogTable(string logType, TextSource source, bool scrollToBottom,
        ref List<LogMessage>? filteredLogs, ref bool updateLogs, ref bool resetLogs,
        ref string filterMethod, ref string filterMessage, ref string filterId)
    {
        var newData = false;
        if (ImGui.CollapsingHeader($"{Loc.S("Log:")}##{logType}LogHeader"))
        {
            if (filteredLogs == null)
                updateLogs = true;

            if (updateLogs || (resetLogs && (filterMethod.Length == 0 || filterMessage.Length == 0 || filterId.Length == 0)))
            {
                filteredLogs = new List<LogMessage>(_log.GetLogsForSource(source));
                updateLogs = true;
                resetLogs = false;
                newData = true;
            }
            if (ImGui.BeginTable($"Log Table##{logType}LogTable", 4, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY))
            {
                ImGui.TableSetupScrollFreeze(0, 2);
                ImGui.TableSetupColumn("Timestamp", ImGuiTableColumnFlags.WidthFixed, 75f);
                ImGui.TableSetupColumn("Method", ImGuiTableColumnFlags.WidthFixed, 150f);
                ImGui.TableSetupColumn("Message", ImGuiTableColumnFlags.None, 500f);
                ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 40f);
                ImGui.TableHeadersRow();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.InputText($"##ESFilter{logType}LogMethod", ref filterMethod, 40) || (filterMethod.Length > 0 && updateLogs))
                {
                    var method = filterMethod;
                    filteredLogs = filteredLogs!.FindAll(p => p.Method.Contains(method, StringComparison.OrdinalIgnoreCase));
                    updateLogs = true;
                    resetLogs = true;
                    newData = true;
                }
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.InputText($"##ESFilter{logType}LogMessage", ref filterMessage, 80) || (filterMessage.Length > 0 && updateLogs))
                {
                    var message = filterMessage;
                    filteredLogs = filteredLogs!.FindAll(p => p.Message.Contains(message, StringComparison.OrdinalIgnoreCase));
                    updateLogs = true;
                    resetLogs = true;
                    newData = true;
                }
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.InputText($"##ESFilter{logType}LogId", ref filterId, 40) || (filterId.Length > 0 && updateLogs))
                {
                    var id = filterId;
                    filteredLogs = filteredLogs!.FindAll(p => p.EventId.Id.ToString().Contains(id, StringComparison.OrdinalIgnoreCase));
                    updateLogs = true;
                    resetLogs = true;
                    newData = true;
                }
                var sortSpecs = ImGui.TableGetSortSpecs();
                if (sortSpecs.SpecsDirty || updateLogs)
                {
                    switch (sortSpecs.Specs.ColumnIndex)
                    {
                        case 0:
                            if (sortSpecs.Specs.SortDirection == ImGuiSortDirection.Ascending)
                                filteredLogs!.Sort((a, b) => DateTime.Compare(a.TimeStamp, b.TimeStamp));
                            else
                                filteredLogs!.Sort((a, b) => DateTime.Compare(b.TimeStamp, a.TimeStamp));
                            break;
                        case 1:
                            if (sortSpecs.Specs.SortDirection == ImGuiSortDirection.Ascending)
                                filteredLogs!.Sort((a, b) => string.CompareOrdinal(a.Method, b.Method));
                            else
                                filteredLogs!.Sort((a, b) => string.CompareOrdinal(b.Method, a.Method));
                            break;
                        case 2:
                            if (sortSpecs.Specs.SortDirection == ImGuiSortDirection.Ascending)
                                filteredLogs!.Sort((a, b) => string.CompareOrdinal(a.Message, b.Message));
                            else
                                filteredLogs!.Sort((a, b) => string.CompareOrdinal(b.Message, a.Message));
                            break;
                        case 3:
                            if (sortSpecs.Specs.SortDirection == ImGuiSortDirection.Ascending)
                                filteredLogs!.Sort((a, b) => string.CompareOrdinal(a.EventId.Id.ToString(), b.EventId.Id.ToString()));
                            else
                                filteredLogs!.Sort((a, b) => string.CompareOrdinal(b.EventId.Id.ToString(), a.EventId.Id.ToString()));
                            break;
                    }

                    updateLogs = false;
                    sortSpecs.SpecsDirty = false;
                }
                foreach (var logMessage in filteredLogs!)
                {
                    ImGui.TableNextRow();
                    ImGui.PushStyleColor(ImGuiCol.Text, logMessage.Color);
                    ImGui.PushTextWrapPos();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(logMessage.TimeStamp.ToString("HH:mm:ss.fff"));
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(logMessage.Method);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(logMessage.Message);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(logMessage.EventId.Id.ToString());
                    ImGui.PopStyleColor();
                }

                if (scrollToBottom && newData)
                    ImGui.SetScrollHereY();

                ImGui.EndTable();
            }
        }
    }

    private void DrawFakeUser()
    {
        if (ImGui.Button($"{Loc.S("Enter Dialogue")}##ESEnterDialogue"))
            _syncClient.SendDialogueEnter("fake-npc", "fake-hash");

        ImGui.SameLine();
        if (ImGui.Button($"{Loc.S("Exit Dialogue")}##ESExitDialogue"))
            _syncClient.SendDialogueExit();

        if (ImGui.Button($"{Loc.S("Request Advance")}##ESRequestAdvance"))
            _syncClient.SendDialogueAdvance("fake-hash");
    }
}
