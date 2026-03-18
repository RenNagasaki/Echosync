using System;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Echosync.DataClasses;
using Echotools.Logging.DataClasses;
using Echotools.Logging.Enums;
using Echotools.Logging.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Echosync.Helper;

public unsafe class AddonTalkHelper : IDisposable
{
    private readonly IAddonLifecycle _addonLifecycle;
    private readonly IObjectTable _objectTable;
    private readonly ICondition _condition;
    private readonly IFramework _framework;
    private readonly Configuration _configuration;
    private readonly SyncClientHelper _syncClient;
    private readonly ILogService _log;
    public Action<bool>? OnDialogueVisibilityChanged { get; set; }

    public string ActiveNpcId { get; private set; } = "";
    public string? ActiveDialogue { get; private set; } = "";
    public Vector2 AddonPos { get; private set; }
    public float AddonWidth { get; private set; }
    public float AddonScale { get; private set; } = 1f;

    private bool _pendingClick;
    private nint _pendingClickAddon;
    private bool _allowNextClick;

    public AddonTalkHelper(
        IAddonLifecycle addonLifecycle,
        IObjectTable objectTable,
        ICondition condition,
        IFramework framework,
        Configuration configuration,
        SyncClientHelper syncClient,
        ILogService log)
    {
        _addonLifecycle = addonLifecycle;
        _objectTable = objectTable;
        _condition = condition;
        _framework = framework;
        _configuration = configuration;
        _syncClient = syncClient;
        _log = log;

        _addonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "Talk", OnPreReceiveEvent);
        _addonLifecycle.RegisterListener(AddonEvent.PostDraw, "Talk", OnPostDraw);
    }

    private void OnPostDraw(AddonEvent type, AddonArgs args)
    {
        // Execute deferred click from previous frame (must not click during PostDraw)
        if (_pendingClick)
        {
            _pendingClick = false;
            _allowNextClick = true;
            ClickHelper.ClickDialogue(_pendingClickAddon, _syncClient.CurrentEvent!, _log);
            return;
        }

        if (!_configuration.Enabled) return;
        if (_condition[ConditionFlag.OccupiedSummoningBell]) return;
        if (_configuration.OnlySpecialNpcs)
        {
            var targetAddress = _objectTable.LocalPlayer?.TargetObject?.Address ?? 0;
            if (targetAddress == 0) return;
            var isSpecial = ((GameObject*)targetAddress)->NamePlateIconId is not 0;
            if (!isSpecial) return;
        }

        var addonTalk = (AddonTalk*)args.Addon.Address.ToPointer();
        if (addonTalk == null)
        {
            _log.Info(nameof(OnPostDraw), "Weird stuff happening", _syncClient.CurrentEvent!);
            return;
        }

        AddonPos = new Vector2(addonTalk->GetX(), addonTalk->GetY());
        AddonWidth = addonTalk->GetScaledWidth(true);
        AddonScale = addonTalk->Scale;
        var visible = addonTalk->AtkUnitBase.IsVisible;

        var dialogue = GetTalkText(addonTalk);
        if (visible && ActiveDialogue != dialogue && _syncClient.Connected)
        {
            var dialogueHash = SyncClientHelper.HashDialogueText(dialogue);

            if (string.IsNullOrWhiteSpace(ActiveNpcId))
            {
                var target = _objectTable.LocalPlayer?.TargetObject;
                if (target != null)
                {
                    ActiveNpcId = target.GameObjectId.ToString();
                    _syncClient.CurrentEvent = _log.Start(nameof(OnPostDraw), TextSource.Sync);
                    _syncClient.SendDialogueEnter(ActiveNpcId, dialogueHash);
                    _syncClient.DialogueState = ClientDialogueState.InDialogue;
                }
            }
            else
            {
                // Dialogue text changed — send updated hash
                _syncClient.SendDialogueEnter(ActiveNpcId, dialogueHash);
            }

            ActiveDialogue = dialogue;
            OnDialogueVisibilityChanged?.Invoke(true);
        }

        // Server granted advance — defer click to next frame to avoid re-entrant crash
        if (_syncClient.AdvanceGranted && _syncClient.Connected)
        {
            _syncClient.AdvanceGranted = false;
            _syncClient.DialogueState = ClientDialogueState.InDialogue;
            _pendingClick = true;
            _pendingClickAddon = args.Addon;
        }

        if (!visible && !string.IsNullOrWhiteSpace(ActiveDialogue) && _syncClient.Connected)
        {
            _log.Info(nameof(OnPostDraw), "Addon closed", _syncClient.CurrentEvent!);
            _syncClient.SendDialogueExit();
            _log.End(nameof(OnPostDraw), _syncClient.CurrentEvent!);
            _syncClient.CurrentEvent = null;
            ActiveDialogue = "";
            ActiveNpcId = "";
        }

        if (!visible)
            OnDialogueVisibilityChanged?.Invoke(false);
    }

    private void OnPreReceiveEvent(AddonEvent type, AddonArgs args)
    {
        if (!_configuration.Enabled) return;
        if (_condition[ConditionFlag.OccupiedSummoningBell]) return;
        if (!_condition[ConditionFlag.OccupiedInQuestEvent] && !_condition[ConditionFlag.OccupiedInCutSceneEvent] && !_condition[ConditionFlag.OccupiedInEvent]) return;
        if (!_syncClient.Connected) return;
        if (args is not AddonReceiveEventArgs eventArgs) return;

        var eventData = (AtkEventData*)eventArgs.AtkEventData;
        if (eventData == null) return;

        var eventType = (AtkEventType)eventArgs.AtkEventType;
        var isControllerButtonClick = eventType == AtkEventType.InputReceived && eventData->InputData.InputId == 1;
        var isDialogueAdvancing =
            (eventType == AtkEventType.MouseClick && ((byte)eventData->MouseData.Modifier & 0b0001_0000) == 0) ||
            eventArgs.AtkEventType == (byte)AtkEventType.InputReceived;

        _log.Info(nameof(OnPreReceiveEvent), $"Param: {eventArgs.EventParam} Type: {eventArgs.AtkEventType} B: {eventArgs.AtkEvent}", _syncClient.CurrentEvent!);

        if (isControllerButtonClick || isDialogueAdvancing)
        {
            // Allow programmatic click from server grant through
            if (_allowNextClick)
            {
                _allowNextClick = false;
                return;
            }

            // Block and send advance request to server
            if (_syncClient.DialogueState != ClientDialogueState.AwaitingGrant)
            {
                var dialogueHash = SyncClientHelper.HashDialogueText(ActiveDialogue);
                _syncClient.SendDialogueAdvance(dialogueHash);
                _log.Info(nameof(OnPreReceiveEvent), "Sent advance request, awaiting server grant", _syncClient.CurrentEvent!);
            }
        }

        // Block all non-granted clicks — server will grant via S2C_AdvanceGranted
        eventArgs.AtkEventType = 0;
    }

    private static string? GetTalkText(AddonTalk* addonTalk)
    {
        if (addonTalk == null) return null;
        var textNode = addonTalk->AtkTextNode228;
        if (textNode == null) return "";

        var textLength = textNode->NodeText.BufUsed - 1;
        if (textLength is <= 0 or > int.MaxValue) return "";

        var seString = SeString.Parse(textNode->NodeText.StringPtr, Convert.ToInt32(textLength));
        return seString.TextValue.Trim().Replace("\n", "").Replace("\r", "");
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, "Talk", OnPreReceiveEvent);
        _addonLifecycle.UnregisterListener(AddonEvent.PostDraw, "Talk", OnPostDraw);
    }
}
