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
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Echosync.Helper
{
    public unsafe class AddonTalkHelper
    {
        private record struct AddonTalkState(string? Speaker, string? Text);

        public static string ActiveNpcId = "";
        public static string ActiveDialogue = "";
        public static Vector2 AddonPos = new Vector2(0, 0);
        public static float AddonWidth = 0;
        public static float AddonScale = 1f;
        private OnUpdateDelegate updateHandler;
        private readonly ICondition condition;
        private readonly IFramework framework;
        private readonly IAddonLifecycle addonLifecycle;
        private readonly IClientState clientState;
        private readonly IObjectTable objectTable;
        private readonly Configuration configuration;
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
            this.configuration = config;
            this.objectTable = objectTable;

            HookIntoFrameworkUpdate();
        }

        private void HookIntoFrameworkUpdate()
        {
            addonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "Talk", OnPreReceiveEvent);
            addonLifecycle.RegisterListener(AddonEvent.PostDraw, "Talk", OnPostDraw);
        }

        private unsafe void OnPostDraw(AddonEvent type, AddonArgs args)
        {
            if (!configuration.Enabled) return;
            if (condition[ConditionFlag.OccupiedSummoningBell]) return;
            if (configuration.OnlySpecialNPCs)
            {
                bool isSpecial = ((GameObject*)clientState.LocalPlayer.TargetObject.Address)->NamePlateIconId is not 0;

                if (!isSpecial)
                    return;
            }

            var addonTalk = (AddonTalk*)args.Addon.ToPointer();
            if (addonTalk != null)
            {

                AddonPos = new Vector2(addonTalk->GetX(), addonTalk->GetY());
                AddonWidth = addonTalk->GetScaledWidth(true);
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
                                SyncClientHelper.CurrentEvent = LogHelper.EventId(MethodBase.GetCurrentMethod().Name, Enums.TextSource.Sync);
                                SyncClientHelper.CreateMessage(SyncMessages.StartNpc);
                            }
                        }
                    }
                    ActiveDialogue = dialogue;
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
                    SyncClientHelper.CurrentEvent = null;
                    readySend = false;
                    SyncClientHelper.AllReady = false;
                    SyncClientHelper.ConnectedPlayersDialogue = 0;
                    SyncClientHelper.ConnectedPlayersReady = 0;
                    ActiveDialogue = "";
                    ActiveNpcId = "";
                }

                if (!visible)
                {
                    if (plugin.ReadyStateWindow.IsOpen)
                        plugin.ReadyStateWindow.Toggle();
                }
            }
            else
            {
                LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Weird stuff happening", SyncClientHelper.CurrentEvent);
            }
        }

        private void OnPreReceiveEvent(AddonEvent type, AddonArgs args)
        {
            if (!configuration.Enabled) return;
            if (condition[ConditionFlag.OccupiedSummoningBell]) return;
            if (!condition[ConditionFlag.OccupiedInQuestEvent] && !condition[ConditionFlag.OccupiedInCutSceneEvent] && !condition[ConditionFlag.OccupiedInEvent]) return;
            if (!SyncClientHelper.Connected) return;
            if (args is not AddonReceiveEventArgs eventArgs)
                return;

            var eventData = (AtkEventData*)eventArgs.Data;
            if (eventData == null)
                return;

            var eventType = (AtkEventType)eventArgs.AtkEventType;
            var isControllerButtonClick = eventType == AtkEventType.InputReceived && eventData->InputData.InputId == 1;
            var isLeftClick = eventType == AtkEventType.MouseClick && ((byte)eventData->MouseData.Modifier & 0b0001_0000) == 0 && !eventData->MouseData.IsRightClick; // not dragging, not right click

            LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Param: {eventArgs.EventParam} Type: {eventArgs.AtkEventType} B: {eventArgs.AtkEvent}", SyncClientHelper.CurrentEvent);
            if (isControllerButtonClick || isLeftClick)
            {
                if (allowClick)
                {
                    allowClick = false;
                    if (configuration.WaitForNearbyUsers)
                    {
                        var closePlayers = DalamudHelper.GetClosePlayers(SyncClientHelper.ConnectedPlayers, configuration.MaxPlayerDistance);

                        if (closePlayers > SyncClientHelper.ConnectedPlayersDialogue - 1)
                        {
                            eventArgs.AtkEventType = 0;
                            LogHelper.Info(MethodBase.GetCurrentMethod().Name, $"Waiting for other players to start dialogue", SyncClientHelper.CurrentEvent);
                        }
                        else
                        {
                            joinedDialogue = false;
                            SyncClientHelper.CreateMessage(SyncMessages.ClickSuccess);
                            LogHelper.End(MethodBase.GetCurrentMethod().Name, SyncClientHelper.CurrentEvent);
                            SyncClientHelper.CurrentEvent = null;
                        }
                    }
                    else
                    {
                        joinedDialogue = false;
                        SyncClientHelper.CreateMessage(SyncMessages.ClickSuccess);
                        LogHelper.End(MethodBase.GetCurrentMethod().Name, SyncClientHelper.CurrentEvent);
                        SyncClientHelper.CurrentEvent = null;
                    }
                    return;
                }

                if (!readySend && joinedDialogue)
                {
                    if (!(eventArgs.AtkEventType == (byte)AtkEventType.InputReceived))
                        SyncClientHelper.CreateMessage(SyncMessages.Click);
                    readySend = true;
                }
                if (readySend && joinedDialogue && eventArgs.AtkEventType == (byte)AtkEventType.InputReceived)
                {
                    SyncClientHelper.CreateMessage(SyncMessages.ClickForce);
                }
            }

            eventArgs.AtkEventType = 0;
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
