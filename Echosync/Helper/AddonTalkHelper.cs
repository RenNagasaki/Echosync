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
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Collections.Generic;
using System.Numerics;

namespace Echosync.Helper
{
    public class AddonTalkHelper
    {
        private record struct AddonTalkState(string? Speaker, string? Text);

        public static string ActiveNpcId = "";
        public static string ActiveDialogue = "";
        public static Vector2 AddonPos = new Vector2(0, 0);
        public static float AddonScale = 1f;
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
        private bool joinedDialogue = false;
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

                AddonPos = new Vector2(addonTalk->GetX(), addonTalk->GetY());
                AddonScale = addonTalk->Scale;
                var visible = addonTalk->AtkUnitBase.IsVisible;

                var dialogue = GetTalkAddonText((AddonTalk*)args.Addon.ToPointer());
                if (visible && ActiveDialogue != dialogue && SyncClientHelper.Connected)
                {
                    if (string.IsNullOrWhiteSpace(ActiveNpcId))
                    {
                        var localPlayer = clientState.LocalPlayer;
                        if (localPlayer != null)
                        {
                            var target = localPlayer.TargetObject;

                            if (target != null)
                            {
                                ActiveNpcId = target.GameObjectId.ToString();
                                SyncClientHelper.CreateMessage(SyncMessages.StartNpc);
                            }
                        }
                    }
                    ActiveDialogue = dialogue;
                    SyncClientHelper.CurrentEvent = LogHelper.EventId(MethodBase.GetCurrentMethod().Name, Enums.TextSource.Sync);
                    SyncClientHelper.CreateMessage(SyncMessages.JoinDialogue, ActiveDialogue);
                    joinedDialogue = true;
                    if (!plugin.ReadyStateWindow.IsOpen)
                        plugin.ReadyStateWindow.Toggle();
                }


                if (SyncClientHelper.AllReady && SyncClientHelper.Connected)
                {
                    SyncClientHelper.AllReady = false;
                    readySend = false;
                    allowClick = true;
                    framework.RunOnFrameworkThread(() => Click(args.Addon, SyncClientHelper.CurrentEvent));
                }

                if (!visible && !string.IsNullOrWhiteSpace(ActiveDialogue) && SyncClientHelper.Connected)
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Addon closed", SyncClientHelper.CurrentEvent);
                    SyncClientHelper.CreateMessage(SyncMessages.EndNpc);
                    LogHelper.End(MethodBase.GetCurrentMethod().Name, SyncClientHelper.CurrentEvent);
                    readySend = false;
                    SyncClientHelper.AllReady = false;
                    SyncClientHelper.ConnectedPlayersDialogue = 0;
                    SyncClientHelper.ConnectedPlayersReady = 0;
                    ActiveDialogue = "";
                    ActiveNpcId = "";
                    if (plugin.ReadyStateWindow.IsOpen)
                        plugin.ReadyStateWindow.Toggle();
                }
            }
        }

        private unsafe void OnPreReceiveEvent(AddonEvent type, AddonArgs args)
        {
            if (!config.Enabled) return;
            if (condition[ConditionFlag.OccupiedSummoningBell]) return;
            if (!condition[ConditionFlag.OccupiedInQuestEvent] && !condition[ConditionFlag.OccupiedInCutSceneEvent] && !condition[ConditionFlag.OccupiedInEvent]) return;
            if (!SyncClientHelper.Connected || SyncClientHelper.ConnectedPlayersDialogue < 2) return;
            if (args is not AddonReceiveEventArgs receiveEventArgs) return;

            LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Param: {receiveEventArgs.EventParam} Type: {receiveEventArgs.AtkEventType} B: {receiveEventArgs.AtkEvent}", SyncClientHelper.CurrentEvent);
            if ((receiveEventArgs.AtkEventType == (byte)AtkEventType.MouseClick || receiveEventArgs.AtkEventType == (byte)AtkEventType.InputReceived) && receiveEventArgs.EventParam == 0 && SyncClientHelper.Connected)
            {
                if (allowClick)
                {
                    allowClick = false;
                    joinedDialogue = false;
                    LogHelper.End(MethodBase.GetCurrentMethod().Name, SyncClientHelper.CurrentEvent);
                    return;
                }

                if (!readySend && joinedDialogue)
                {
                    SyncClientHelper.CreateMessage(SyncMessages.Click, ActiveDialogue);
                    readySend = true;
                }
                if (readySend && joinedDialogue && receiveEventArgs.AtkEventType == (byte)AtkEventType.InputReceived)
                {
                    SyncClientHelper.CreateMessage(SyncMessages.ClickForce, ActiveDialogue);
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
