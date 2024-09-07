using System;
using Dalamud.Plugin.Services;
using static Dalamud.Plugin.Services.IFramework;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Echosync.DataClasses;
using System.Reflection;
using Echosync.Enums;
using Echosync_Data.Enums;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Dalamud.Game.Text.SeStringHandling;

namespace Echosync.Helper
{
    public class AddonTalkHelper
    {
        private record struct AddonTalkState(string? Speaker, string? Text);

        private OnUpdateDelegate updateHandler;
        private readonly ICondition condition;
        private readonly IFramework framework;
        private readonly IAddonLifecycle addonLifecycle;
        private readonly IClientState clientState;
        private readonly IObjectTable objects;
        private readonly Configuration config;
        private readonly Plugin plugin;
        public DateTime timeNextVoice = DateTime.Now;
        private bool readySend = false;
        private bool allowClick = false;
        private string activeDialogue = "";
        private AddonTalkState lastValue;

        public AddonTalkHelper(Plugin plugin, ICondition condition, IFramework framework, IAddonLifecycle addonLifecycle, IClientState clientState, IObjectTable objectTable, Configuration config)
        {
            this.plugin = plugin;
            this.condition = condition;
            this.framework = framework;
            this.addonLifecycle = addonLifecycle;
            this.clientState = clientState;
            this.config = config;
            objects = objectTable;

            HookIntoFrameworkUpdate();
        }

        private void HookIntoFrameworkUpdate()
        {
            addonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "Talk", OnPreReceiveEvent);
            addonLifecycle.RegisterListener(AddonEvent.PostDraw, "Talk", OnPostDraw);
        }

        private unsafe void OnPostDraw(AddonEvent type, AddonArgs args)
        {
            if (!config.Enabled) return;
            if (condition[ConditionFlag.OccupiedSummoningBell]) return;

            var addonTalk = (AddonTalk*)args.Addon.ToPointer();
            if (addonTalk != null)
            {
                var visible = addonTalk->AtkUnitBase.IsVisible;
                var dialogue = GetTalkAddonText((AddonTalk*)args.Addon.ToPointer());
                if (visible && activeDialogue != dialogue && SyncClientHelper.Connected)
                {
                    activeDialogue = dialogue;
                    SyncClientHelper.CurrentEvent = LogHelper.EventId(MethodBase.GetCurrentMethod().Name, Enums.TextSource.Sync);
                    SyncClientHelper.CreateMessage(SyncMessages.JoinDialogue, clientState.LocalPlayer?.Name.TextValue ?? "TEST", activeDialogue);
                }


                if (SyncClientHelper.AllReady && SyncClientHelper.Connected)
                {
                    SyncClientHelper.AllReady = false;
                    readySend = false;
                    allowClick = true;
                    framework.RunOnFrameworkThread(() => Click(args.Addon, SyncClientHelper.CurrentEvent));
                }

                if (!visible && !string.IsNullOrWhiteSpace(activeDialogue) && SyncClientHelper.Connected)
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Addon closed", SyncClientHelper.CurrentEvent);
                    SyncClientHelper.CreateMessage(SyncMessages.LeaveDialogue, clientState.LocalPlayer?.Name.TextValue ?? "TEST", activeDialogue);
                    LogHelper.End(MethodBase.GetCurrentMethod().Name, SyncClientHelper.CurrentEvent);
                    readySend = false;
                    SyncClientHelper.AllReady = false;
                    activeDialogue = "";
                }

            }
        }

        private unsafe void OnPreReceiveEvent(AddonEvent type, AddonArgs args)
        {
            if (!config.Enabled) return;
            if (condition[ConditionFlag.OccupiedSummoningBell]) return;
            if (!condition[ConditionFlag.OccupiedInQuestEvent] && !condition[ConditionFlag.OccupiedInCutSceneEvent] && !condition[ConditionFlag.OccupiedInEvent]) return;
            if (args is not AddonReceiveEventArgs receiveEventArgs) return;

            LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Param: {receiveEventArgs.EventParam} Type: {receiveEventArgs.AtkEventType} B: {receiveEventArgs.AtkEvent}", SyncClientHelper.CurrentEvent);
            if ((receiveEventArgs.AtkEventType == (byte)AtkEventType.MouseClick || receiveEventArgs.AtkEventType == (byte)AtkEventType.InputReceived) && receiveEventArgs.EventParam == 0 && SyncClientHelper.Connected)
            {
                if (allowClick)
                {
                    allowClick = false;
                    LogHelper.End(MethodBase.GetCurrentMethod().Name, SyncClientHelper.CurrentEvent);
                    return;
                }

                if (!readySend)
                {
                    SyncClientHelper.CreateMessage(SyncMessages.Click, clientState.LocalPlayer?.Name.TextValue ?? "TEST", activeDialogue);
                    readySend = true;
                }
            }

            receiveEventArgs.AtkEventType = 0;
        }

        private unsafe string GetTalkAddonText(AddonTalk* addonTalk)
        {
            return ReadText(addonTalk);
        }

        public unsafe string ReadText(AddonTalk* addonTalk)
        {
            return addonTalk == null ? null : ReadTalkAddon(addonTalk);
        }
        public static unsafe string ReadTalkAddon(AddonTalk* talkAddon)
        {
            if (talkAddon is null) return null;
            return ReadTextNode(talkAddon->AtkTextNode228);
        }

        private static unsafe string ReadTextNode(AtkTextNode* textNode)
        {
            if (textNode == null) return "";

            var textPtr = textNode->NodeText.StringPtr;
            var textLength = textNode->NodeText.BufUsed - 1; // Null-terminated; chop off the null byte
            if (textLength is <= 0 or > int.MaxValue) return "";

            var textLengthInt = Convert.ToInt32(textLength);

            var seString = SeString.Parse(textPtr, textLengthInt);
            return seString.TextValue
                .Trim()
                .Replace("\n", "")
                .Replace("\r", "");
        }

        public unsafe bool IsVisible(AddonTalk* addonTalk)
        {
            return addonTalk != null && addonTalk->AtkUnitBase.IsVisible;
        }

        public unsafe void Click(nint addonTalk, EKEventId eventId)
        {
            ClickHelper.ClickDialogue(addonTalk, eventId);
        }

        public void Dispose()
        {
            addonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, "Talk", OnPreReceiveEvent);
            addonLifecycle.UnregisterListener(AddonEvent.PostDraw, "Talk", OnPostDraw);
        }
    }
}
