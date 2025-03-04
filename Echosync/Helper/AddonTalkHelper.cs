using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Echosync.DataClasses;
using System.Reflection;
using Echosync_Data.Enums;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Echosync.Helper
{
    public unsafe class AddonTalkHelper
    {

        public static string ActiveNpcId { get; private set; } = "";
        public static string? ActiveDialogue { get; private set; } = "";
        public static Vector2 AddonPos { get; private set; }
        public static float AddonWidth { get; private set; }
        public static float AddonScale { get; private set; } = 1f;
        private readonly ICondition _condition;
        private readonly IFramework _framework;
        private readonly IAddonLifecycle _addonLifecycle;
        private readonly IClientState _clientState;
        private readonly Configuration _configuration;
        private readonly Plugin _plugin;
        private bool _readySend;
        private bool _allowClick;
        private bool _joinedDialogue;

        public AddonTalkHelper(Plugin plugin, ICondition condition, IFramework framework, IAddonLifecycle addonLifecycle, IClientState clientState, Configuration configuration)
        {
            _plugin = plugin;
            _condition = condition;
            _framework = framework;
            _addonLifecycle = addonLifecycle;
            _clientState = clientState;
            _configuration = configuration;

            HookIntoFrameworkUpdate();
        }

        private void HookIntoFrameworkUpdate()
        {
            _addonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "Talk", OnPreReceiveEvent);
            _addonLifecycle.RegisterListener(AddonEvent.PostDraw, "Talk", OnPostDraw);
        }

        private void OnPostDraw(AddonEvent type, AddonArgs args)
        {
            if (!_configuration.Enabled) return;
            if (_condition[ConditionFlag.OccupiedSummoningBell]) return;
            if (_configuration.OnlySpecialNpcs)
            {
                var isSpecial = ((GameObject*)_clientState.LocalPlayer?.TargetObject?.Address)->NamePlateIconId is not 0;

                if (!isSpecial)
                    return;
            }

            var addonTalk = (AddonTalk*)args.Addon.ToPointer();
            if (addonTalk != null)
            {

                AddonPos = new Vector2(addonTalk->GetX(), addonTalk->GetY());
                AddonWidth = addonTalk->GetScaledWidth(true);
                AddonScale = addonTalk->Scale;
                var visible = IsVisible(addonTalk);

                var dialogue = GetTalkAddonText((AddonTalk*)args.Addon.ToPointer());
                if (visible && ActiveDialogue != dialogue && SyncClientHelper.Connected)
                {
                    if (string.IsNullOrWhiteSpace(ActiveNpcId))
                    {
                        var localPlayer = _clientState.LocalPlayer;
                        var target = localPlayer?.TargetObject;

                        if (target != null)
                        {
                            ActiveNpcId = target.GameObjectId.ToString();
                            SyncClientHelper.CurrentEvent = LogHelper.EventId(MethodBase.GetCurrentMethod()!.Name, Enums.TextSource.Sync);
                            SyncClientHelper.CreateMessage(SyncMessages.StartNpc);
                        }
                    }
                    ActiveDialogue = dialogue;
                    _joinedDialogue = true;
                    if (!_plugin.ReadyStateWindow.IsOpen)
                        _plugin.ReadyStateWindow.Toggle();
                }


                if (SyncClientHelper.AllReady && SyncClientHelper.Connected)
                {
                    SyncClientHelper.AllReady = false;
                    _readySend = false;
                    _allowClick = true;
                    _framework.RunOnFrameworkThread(() => Click(args.Addon, SyncClientHelper.CurrentEvent!));
                }

                if (!visible && !string.IsNullOrWhiteSpace(ActiveDialogue) && SyncClientHelper.Connected)
                {
                    LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Addon closed", SyncClientHelper.CurrentEvent!);
                    SyncClientHelper.CreateMessage(SyncMessages.EndNpc);
                    LogHelper.End(MethodBase.GetCurrentMethod()!.Name, SyncClientHelper.CurrentEvent!);
                    SyncClientHelper.CurrentEvent = null;
                    _readySend = false;
                    SyncClientHelper.AllReady = false;
                    SyncClientHelper.ConnectedPlayersDialogue = 0;
                    SyncClientHelper.ConnectedPlayersReady = 0;
                    ActiveDialogue = "";
                    ActiveNpcId = "";
                }

                if (!visible)
                {
                    if (_plugin.ReadyStateWindow.IsOpen)
                        _plugin.ReadyStateWindow.Toggle();
                }
            }
            else
            {
                LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Weird stuff happening", SyncClientHelper.CurrentEvent!);
            }
        }

        private void OnPreReceiveEvent(AddonEvent type, AddonArgs args)
        {
            if (!_configuration.Enabled) return;
            if (_condition[ConditionFlag.OccupiedSummoningBell]) return;
            if (!_condition[ConditionFlag.OccupiedInQuestEvent] && !_condition[ConditionFlag.OccupiedInCutSceneEvent] && !_condition[ConditionFlag.OccupiedInEvent]) return;
            if (!SyncClientHelper.Connected) return;
            if (args is not AddonReceiveEventArgs eventArgs)
                return;

            var eventData = (AtkEventData*)eventArgs.Data;
            if (eventData == null)
                return;

            var eventType = (AtkEventType)eventArgs.AtkEventType;
            var isControllerButtonClick = eventType == AtkEventType.InputReceived && eventData->InputData.InputId == 1;
            var isDialogueAdvancing = 
                (eventType == AtkEventType.MouseClick && ((byte)eventData->MouseData.Modifier & 0b0001_0000) == 0) || 
                eventArgs.AtkEventType == (byte)AtkEventType.InputReceived;

            LogHelper.Info(
                MethodBase.GetCurrentMethod()!.Name, 
                $"Param: {eventArgs.EventParam} Type: {eventArgs.AtkEventType} B: {eventArgs.AtkEvent}", 
                SyncClientHelper.CurrentEvent!);
            
            if (isControllerButtonClick || isDialogueAdvancing)
            {
                if (_allowClick)
                {
                    _allowClick = false;
                    if (_configuration.WaitForNearbyUsers)
                    {
                        var closePlayers = DalamudHelper.GetClosePlayers(SyncClientHelper.ConnectedPlayers, _configuration.MaxPlayerDistance);

                        if (closePlayers > SyncClientHelper.ConnectedPlayersDialogue - 1)
                        {
                            eventArgs.AtkEventType = 0;
                            LogHelper.Info(MethodBase.GetCurrentMethod()!.Name, $"Waiting for other players to start dialogue", SyncClientHelper.CurrentEvent!);
                        }
                        else
                        {
                            _joinedDialogue = false;
                            SyncClientHelper.CreateMessage(SyncMessages.ClickSuccess);
                            LogHelper.End(MethodBase.GetCurrentMethod()!.Name, SyncClientHelper.CurrentEvent!);
                            SyncClientHelper.CurrentEvent = null;
                        }
                    }
                    else
                    {
                        _joinedDialogue = false;
                        SyncClientHelper.CreateMessage(SyncMessages.ClickSuccess);
                        LogHelper.End(MethodBase.GetCurrentMethod()!.Name, SyncClientHelper.CurrentEvent!);
                        SyncClientHelper.CurrentEvent = null;
                    }
                    return;
                }

                if (!_readySend && _joinedDialogue)
                {
                    if (eventArgs.AtkEventType != (byte)AtkEventType.InputReceived)
                        SyncClientHelper.CreateMessage(SyncMessages.Click);
                    _readySend = true;
                }
                if (_readySend && _joinedDialogue && eventArgs.AtkEventType == (byte)AtkEventType.InputReceived)
                {
                    SyncClientHelper.CreateMessage(SyncMessages.ClickForce);
                }
            }

            eventArgs.AtkEventType = 0;
        }

        private static string? GetTalkAddonText(AddonTalk* addonTalk)
        {
            return ReadText(addonTalk);
        }

        private static string? ReadText(AddonTalk* addonTalk)
        {
            return addonTalk is null ? null : ReadTalkAddon(addonTalk);
        }
        private static string? ReadTalkAddon(AddonTalk* talkAddon)
        {
            return talkAddon is null ? null : ReadTextNode(talkAddon!->AtkTextNode228!);
        }

        private static string ReadTextNode(AtkTextNode* textNode)
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

        private static bool IsVisible(AddonTalk* addonTalk)
        {
            return addonTalk != null && addonTalk->AtkUnitBase.IsVisible;
        }

        private static void Click(nint addonTalk, EKEventId eventId)
        {
            ClickHelper.ClickDialogue(addonTalk, eventId);
        }

        public void Dispose()
        {
            _addonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, "Talk", OnPreReceiveEvent);
            _addonLifecycle.UnregisterListener(AddonEvent.PostDraw, "Talk", OnPostDraw);
        }
    }
}