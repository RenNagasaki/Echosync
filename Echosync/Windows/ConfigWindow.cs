using System;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using System.Reflection;
using Dalamud.Interface.Windowing;
using Echosync.DataClasses;
using Echosync.Enums;
using Echosync.Helper;
using System.Collections.Generic;
using Echosync_Data.Enums;
using Dalamud.Bindings.ImGui;

namespace Echosync.Windows;

public class ConfigWindow : Window, IDisposable
{
    #region Logs

    private List<LogMessage> _filteredLogsGeneral = [];
    private string _filterLogsGeneralMethod = "";
    private string _filterLogsGeneralMessage = "";
    private string _filterLogsGeneralId = "";
    public static bool UpdateLogGeneralFilter = true;
    public static bool UpdateLogSyncFilter = true;
    private List<LogMessage> _filteredLogsSync = [];
    private bool _resetLogGeneralFilter = true;
    private bool _resetLogSyncFilter = true;
    private string _filterLogsSyncMethod = "";
    private string _filterLogsSyncMessage = "";
    private string _filterLogsSyncId = "";

    #endregion

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow() : base("Echosync-Configuration")
    {
        Flags = ImGuiWindowFlags.AlwaysVerticalScrollbar & ImGuiWindowFlags.HorizontalScrollbar & ImGuiWindowFlags.AlwaysHorizontalScrollbar;
        Size = new Vector2(540, 480);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Flags must be added or removed before Draw() is being called, or they won't apply
        if (Plugin.Configuration.IsConfigWindowMovable)
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
    }

    public override void Draw()
    {
        DrawSettings();
    }
    private void DrawSettings()
    {
        try
        {
            if (ImGui.BeginTabBar($"Settings##ESSettingsTab"))
            {
                if (ImGui.BeginTabItem("General"))
                {
                    DrawGeneral();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Logs"))
                {
                    DrawLogs();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Fakeuser"))
                {
                    DrawFakeUser();
                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }
        catch (Exception ex)
        {
            LogHelper.Error(MethodBase.GetCurrentMethod()!.Name, $"Something went wrong: {ex}", new EKEventId(0, TextSource.None));
        }
    }


    #region Settings
    private void DrawGeneral()
    {
        var enabled = Plugin.Configuration.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            Plugin.Configuration.Enabled = enabled;
            Plugin.Configuration.Save();
        }

        using (ImRaii.Disabled(!enabled))
        {
            var onlySpecialNpCs = Plugin.Configuration.OnlySpecialNpcs;
            if (ImGui.Checkbox("Only special NPCs (Any marker above head)", ref onlySpecialNpCs))
            {
                Plugin.Configuration.OnlySpecialNpcs = onlySpecialNpCs;
                Plugin.Configuration.Save();
            }

            var waitForNearbyUsers = Plugin.Configuration.WaitForNearbyUsers;
            if (ImGui.Checkbox("Wait for nearby users after starting an dialogue", ref waitForNearbyUsers))
            {
                Plugin.Configuration.WaitForNearbyUsers = waitForNearbyUsers;
                Plugin.Configuration.Save();
            }

            var syncServer = Plugin.Configuration.SyncServer;
            if (ImGui.InputText($"Sync server##ESserver", ref syncServer, 80))
            {
                Plugin.Configuration.SyncServer = syncServer;
                Plugin.Configuration.Save();
            }

            var syncChannel = Plugin.Configuration.SyncChannel;
            if (ImGui.InputText($"Sync channel##ESchannel", ref syncChannel, 80))
            {
                Plugin.Configuration.SyncChannel = syncChannel;
                Plugin.Configuration.Save();
            }

            var syncPassword = Plugin.Configuration.SyncPassword;
            if (ImGui.InputText($"Sync password##ESchannel", ref syncPassword, 80))
            {
                Plugin.Configuration.SyncPassword = syncPassword;
                Plugin.Configuration.Save();
            }

            if (SyncClientHelper.Connected)
            {
                if (ImGui.Button($"Disconnect##ESDisconnect"))
                {
                    SyncClientHelper.Disconnect();
                }
            }
            else
            {
                if (ImGui.Button($"Connect##ESConnect"))
                {
                    SyncClientHelper.Connect();
                }
            }

            ImGui.SameLine();
            var connectAtStart = Plugin.Configuration.ConnectAtStart;
            if (ImGui.Checkbox("Connect at start", ref connectAtStart))
            {
                Plugin.Configuration.ConnectAtStart = connectAtStart;
                Plugin.Configuration.Save();

                if (connectAtStart)
                    SyncClientHelper.Connect();
            }
        }
    }
    #endregion
    #region Logs
    private void DrawLogs()
    {
        try
        {
            if (ImGui.BeginTabBar($"Logs##ESLogsTab"))
            {
                if (ImGui.BeginTabItem("General"))
                {
                    if (ImGui.CollapsingHeader("Options:"))
                    {
                        var showDebugLog = Plugin.Configuration.LogConfig!.ShowGeneralDebugLog;
                        if (ImGui.Checkbox("Show debug logs", ref showDebugLog))
                        {
                            Plugin.Configuration.LogConfig.ShowGeneralDebugLog = showDebugLog;
                            Plugin.Configuration.Save();
                            UpdateLogGeneralFilter = true;
                        }
                        var showErrorLog = Plugin.Configuration.LogConfig.ShowGeneralErrorLog;
                        if (ImGui.Checkbox("Show error logs", ref showErrorLog))
                        {
                            Plugin.Configuration.LogConfig.ShowGeneralErrorLog = showErrorLog;
                            Plugin.Configuration.Save();
                            UpdateLogGeneralFilter = true;
                        }
                        var jumpToBottom = Plugin.Configuration.LogConfig.GeneralJumpToBottom;
                        if (ImGui.Checkbox("Always jump to bottom", ref jumpToBottom))
                        {
                            Plugin.Configuration.LogConfig.GeneralJumpToBottom = jumpToBottom;
                            Plugin.Configuration.Save();
                        }
                    }
                    DrawLogTable("General", TextSource.None, Plugin.Configuration.LogConfig!.GeneralJumpToBottom, ref _filteredLogsGeneral!, ref UpdateLogGeneralFilter, ref _resetLogGeneralFilter, ref _filterLogsGeneralMethod, ref _filterLogsGeneralMessage, ref _filterLogsGeneralId);

                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Sync"))
                {
                    if (ImGui.CollapsingHeader("Options:"))
                    {
                        var showDebugLog = Plugin.Configuration.LogConfig!.ShowSyncDebugLog;
                        if (ImGui.Checkbox("Show debug logs", ref showDebugLog))
                        {
                            Plugin.Configuration.LogConfig.ShowSyncDebugLog = showDebugLog;
                            Plugin.Configuration.Save();
                            UpdateLogSyncFilter = true;
                        }
                        var showErrorLog = Plugin.Configuration.LogConfig.ShowSyncErrorLog;
                        if (ImGui.Checkbox("Show error logs", ref showErrorLog))
                        {
                            Plugin.Configuration.LogConfig.ShowSyncErrorLog = showErrorLog;
                            Plugin.Configuration.Save();
                            UpdateLogSyncFilter = true;
                        }
                        var showId0 = Plugin.Configuration.LogConfig.ShowSyncId0;
                        if (ImGui.Checkbox("Show ID: 0", ref showId0))
                        {
                            Plugin.Configuration.LogConfig.ShowSyncId0 = showId0;
                            Plugin.Configuration.Save();
                            UpdateLogSyncFilter = true;
                        }
                        var jumpToBottom = Plugin.Configuration.LogConfig.SyncJumpToBottom;
                        if (ImGui.Checkbox("Always jump to bottom", ref jumpToBottom))
                        {
                            Plugin.Configuration.LogConfig.SyncJumpToBottom = jumpToBottom;
                            Plugin.Configuration.Save();
                        }
                    }
                    DrawLogTable("Sync", TextSource.Sync, Plugin.Configuration.LogConfig!.SyncJumpToBottom, ref _filteredLogsSync!, ref UpdateLogSyncFilter, ref _resetLogSyncFilter, ref _filterLogsSyncMethod, ref _filterLogsSyncMessage, ref _filterLogsSyncId);

                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }
        catch (Exception ex)
        {
            LogHelper.Error(MethodBase.GetCurrentMethod()!.Name, $"Something went wrong: {ex}", new EKEventId(0, TextSource.None));
        }
    }

    private static void DrawLogTable(string logType, TextSource source, bool scrollToBottom, ref List<LogMessage>? filteredLogs, ref bool updateLogs, ref bool resetLogs, ref string filterMethod, ref string filterMessage, ref string filterId)
    {
        var newData = false;
        if (ImGui.CollapsingHeader("Log:"))
        {
            if (filteredLogs == null)
            {
                updateLogs = true;
            }

            if (updateLogs || (resetLogs && (filterMethod.Length == 0 || filterMessage.Length == 0 || filterId.Length == 0)))
            {
                filteredLogs = LogHelper.RecreateLogList(source);
                updateLogs = true;
                resetLogs = false;
                newData = true;
            }
            if (ImGui.BeginTable($"Log Table##{logType}LogTable", 4, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY))
            {
                ImGui.TableSetupScrollFreeze(0, 2); // Make top row always visible
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
                    filteredLogs = filteredLogs!.FindAll(p => p.Method.ToLower().Contains(method.ToLower()));
                    updateLogs = true;
                    resetLogs = true;
                    newData = true;
                }
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.InputText($"##ESFilter{logType}LogMessage", ref filterMessage, 80) || (filterMessage.Length > 0 && updateLogs))
                {
                    var message = filterMessage;
                    filteredLogs = filteredLogs!.FindAll(p => p.Message.ToLower().Contains(message.ToLower()));
                    updateLogs = true;
                    resetLogs = true;
                    newData = true;
                }
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.InputText($"##ESFilter{logType}LogId", ref filterId, 40) || (filterId.Length > 0 && updateLogs))
                {
                    var id = filterId;
                    filteredLogs = filteredLogs!.FindAll(p => p.EventId.Id.ToString().ToLower().Contains(id.ToLower()));
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
                {
                    ImGui.SetScrollHereY();
                }

                ImGui.EndTable();
            }
        }
    }
    #endregion
    #region FakeUser
    private void DrawFakeUser()
    {
        if (ImGui.Button($"Start NPC##ESStartNpc"))
        {
            SyncClientHelper.CreateMessageFake(SyncMessages.StartNpc);
        }
        ImGui.SameLine();
        if (ImGui.Button($"End NPC##ESEndNpc"))
        {
            SyncClientHelper.CreateMessageFake(SyncMessages.EndNpc);
        }
        if (ImGui.Button($"Join Dialogue##ESJoinDialogue"))
        {
            SyncClientHelper.CreateMessageFake(SyncMessages.ClickSuccess, AddonTalkHelper.ActiveDialogue);
        }
        ImGui.SameLine();
        if (ImGui.Button($"Click##ESClick"))
        {
            SyncClientHelper.CreateMessageFake(SyncMessages.Click, AddonTalkHelper.ActiveDialogue);
        }
    }
    #endregion
}