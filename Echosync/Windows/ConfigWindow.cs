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

namespace Echosync;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    #region Logs
    private List<LogMessage> filteredLogsGeneral;
    private string filterLogsGeneralMethod = "";
    private string filterLogsGeneralMessage = "";
    private string filterLogsGeneralId = "";
    public static bool UpdateLogGeneralFilter = true;
    private bool resetLogGeneralFilter = true;
    private List<LogMessage> filteredLogsChat;
    private string filterLogsChatMethod = "";
    private string filterLogsChatMessage = "";
    private string filterLogsChatId = "";
    public static bool UpdateLogChatFilter = true;
    private bool resetLogChatFilter = true;
    #endregion

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("Echosync")
    {
        Flags = ImGuiWindowFlags.AlwaysVerticalScrollbar & ImGuiWindowFlags.HorizontalScrollbar & ImGuiWindowFlags.AlwaysHorizontalScrollbar;
        Size = new Vector2(540, 480);
        SizeCondition = ImGuiCond.FirstUseEver;

        Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        // Flags must be added or removed before Draw() is being called, or they won't apply
        if (Configuration.IsConfigWindowMovable)
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

    #region Settings
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
            }

            ImGui.EndTabBar();
        }
        catch (Exception ex)
        {
            LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Something went wrong: {ex}", new EKEventId(0, TextSource.None));
        }
    }

    private void DrawGeneral()
    {
        var enabled = this.Configuration.Enabled;
        if (ImGui.Checkbox("Enabled", ref enabled))
        {
            this.Configuration.Enabled = enabled;
            this.Configuration.Save();
        }

        using (var disabled = ImRaii.Disabled(!enabled))
        {
            var connectAtStart = this.Configuration.ConnectAtStart;
            if (ImGui.Checkbox("Connect at start", ref connectAtStart))
            {
                this.Configuration.ConnectAtStart = connectAtStart;
                this.Configuration.Save();

                if (connectAtStart)
                    RabbitMQHelper.Connect(new EKEventId(0, TextSource.Sync));
            }

            var syncChannel = this.Configuration.SyncChannel;
            if (ImGui.InputText($"Sync channel##ESBaseUrl", ref syncChannel, 80))
            {
                this.Configuration.SyncChannel = syncChannel;
                this.Configuration.Save();
            }
            ImGui.SameLine();
            if (RabbitMQHelper.ConnectedPlayers.Count > 0)
            {
                if (ImGui.Button($"Disconnect##ESDisconnect"))
                {
                    RabbitMQHelper.Disconnect(new EKEventId(0, TextSource.Sync));
                }
            }
            else
            {
                if (ImGui.Button($"Connect##ESConnect"))
                {
                    RabbitMQHelper.Connect(new EKEventId(0, TextSource.Sync));
                }
            }

            if (ImGui.Button($"Test Connection##ESTestConnection"))
            {
                RabbitMQHelper.Test(new EKEventId(0, TextSource.None));
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
                        var showDebugLog = this.Configuration.logConfig.ShowGeneralDebugLog;
                        if (ImGui.Checkbox("Show debug logs", ref showDebugLog))
                        {
                            this.Configuration.logConfig.ShowGeneralDebugLog = showDebugLog;
                            this.Configuration.Save();
                            UpdateLogGeneralFilter = true;
                        }
                        var showErrorLog = this.Configuration.logConfig.ShowGeneralErrorLog;
                        if (ImGui.Checkbox("Show error logs", ref showErrorLog))
                        {
                            this.Configuration.logConfig.ShowGeneralErrorLog = showErrorLog;
                            this.Configuration.Save();
                            UpdateLogGeneralFilter = true;
                        }
                        var jumpToBottom = this.Configuration.logConfig.GeneralJumpToBottom;
                        if (ImGui.Checkbox("Always jump to bottom", ref jumpToBottom))
                        {
                            this.Configuration.logConfig.GeneralJumpToBottom = jumpToBottom;
                            this.Configuration.Save();
                        }
                    }
                    DrawLogTable("General", TextSource.None, Configuration.logConfig.GeneralJumpToBottom, ref filteredLogsGeneral, ref UpdateLogGeneralFilter, ref resetLogGeneralFilter, ref filterLogsGeneralMethod, ref filterLogsGeneralMessage, ref filterLogsGeneralId);

                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Chat"))
                {
                    if (ImGui.CollapsingHeader("Options:"))
                    {
                        var showDebugLog = this.Configuration.logConfig.ShowChatDebugLog;
                        if (ImGui.Checkbox("Show debug logs", ref showDebugLog))
                        {
                            this.Configuration.logConfig.ShowChatDebugLog = showDebugLog;
                            this.Configuration.Save();
                            UpdateLogChatFilter = true;
                        }
                        var showErrorLog = this.Configuration.logConfig.ShowChatErrorLog;
                        if (ImGui.Checkbox("Show error logs", ref showErrorLog))
                        {
                            this.Configuration.logConfig.ShowChatErrorLog = showErrorLog;
                            this.Configuration.Save();
                            UpdateLogChatFilter = true;
                        }
                        var showId0 = this.Configuration.logConfig.ShowChatId0;
                        if (ImGui.Checkbox("Show ID: 0", ref showId0))
                        {
                            this.Configuration.logConfig.ShowChatId0 = showId0;
                            this.Configuration.Save();
                            UpdateLogChatFilter = true;
                        }
                        var jumpToBottom = this.Configuration.logConfig.ChatJumpToBottom;
                        if (ImGui.Checkbox("Always jump to bottom", ref jumpToBottom))
                        {
                            this.Configuration.logConfig.ChatJumpToBottom = jumpToBottom;
                            this.Configuration.Save();
                        }
                    }
                    DrawLogTable("Chat", TextSource.Sync, Configuration.logConfig.ChatJumpToBottom, ref filteredLogsChat, ref UpdateLogChatFilter, ref resetLogChatFilter, ref filterLogsChatMethod, ref filterLogsChatMessage, ref filterLogsChatId);

                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
        }
        catch (Exception ex)
        {
            LogHelper.Error(MethodBase.GetCurrentMethod().Name, $"Something went wrong: {ex}", new EKEventId(0, TextSource.None));
        }
    }

    private void DrawLogTable(string logType, TextSource source, bool scrollToBottom, ref List<LogMessage> filteredLogs, ref bool updateLogs, ref bool resetLogs, ref string filterMethod, ref string filterMessage, ref string filterId)
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
                    filteredLogs = filteredLogs.FindAll(p => p.method.ToLower().Contains(method.ToLower()));
                    updateLogs = true;
                    resetLogs = true;
                    newData = true;
                }
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.InputText($"##ESFilter{logType}LogMessage", ref filterMessage, 80) || (filterMessage.Length > 0 && updateLogs))
                {
                    var message = filterMessage;
                    filteredLogs = filteredLogs.FindAll(p => p.message.ToLower().Contains(message.ToLower()));
                    updateLogs = true;
                    resetLogs = true;
                    newData = true;
                }
                ImGui.TableNextColumn();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.InputText($"##ESFilter{logType}LogId", ref filterId, 40) || (filterId.Length > 0 && updateLogs))
                {
                    var id = filterId;
                    filteredLogs = filteredLogs.FindAll(p => p.eventId.Id.ToString().ToLower().Contains(id.ToLower()));
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
                                filteredLogs.Sort((a, b) => DateTime.Compare(a.timeStamp, b.timeStamp));
                            else
                                filteredLogs.Sort((a, b) => DateTime.Compare(b.timeStamp, a.timeStamp));
                            break;
                        case 1:
                            if (sortSpecs.Specs.SortDirection == ImGuiSortDirection.Ascending)
                                filteredLogs.Sort((a, b) => string.Compare(a.method, b.method));
                            else
                                filteredLogs.Sort((a, b) => string.Compare(b.method, a.method));
                            break;
                        case 2:
                            if (sortSpecs.Specs.SortDirection == ImGuiSortDirection.Ascending)
                                filteredLogs.Sort((a, b) => string.Compare(a.message, b.message));
                            else
                                filteredLogs.Sort((a, b) => string.Compare(b.message, a.message));
                            break;
                        case 3:
                            if (sortSpecs.Specs.SortDirection == ImGuiSortDirection.Ascending)
                                filteredLogs.Sort((a, b) => string.Compare(a.eventId.Id.ToString(), b.eventId.Id.ToString()));
                            else
                                filteredLogs.Sort((a, b) => string.Compare(b.eventId.Id.ToString(), a.eventId.Id.ToString()));
                            break;
                    }

                    updateLogs = false;
                    sortSpecs.SpecsDirty = false;
                }
                foreach (var logMessage in filteredLogs)
                {
                    ImGui.TableNextRow();
                    ImGui.PushStyleColor(ImGuiCol.Text, logMessage.color);
                    ImGui.PushTextWrapPos();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(logMessage.timeStamp.ToString("HH:mm:ss.fff"));
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(logMessage.method);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(logMessage.message);
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(logMessage.eventId.Id.ToString());
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
}
