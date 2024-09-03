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

namespace Echosync.Helper
{
    public class AddonTalkHelper
    {
        private record struct AddonTalkState(string? Speaker, string? Text);

        private OnUpdateDelegate updateHandler;
        private readonly ICondition condition;
        private readonly IAddonLifecycle addonLifecycle;
        private readonly IClientState clientState;
        private readonly IObjectTable objects;
        private readonly Configuration config;
        private readonly Plugin plugin;
        public DateTime timeNextVoice = DateTime.Now;
        private bool readySend = false;
        private bool wasTalking = false;

        public static nint Address { get; set; }
        private AddonTalkState lastValue;

        public AddonTalkHelper(Plugin plugin, ICondition condition, IAddonLifecycle addonLifecycle, IClientState clientState, IObjectTable objectTable, Configuration config)
        {
            this.plugin = plugin;
            this.condition = condition;
            this.addonLifecycle = addonLifecycle;
            this.clientState = clientState;
            this.config = config;
            objects = objectTable;

            HookIntoFrameworkUpdate();
        }

        private void HookIntoFrameworkUpdate()
        {
            addonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "Talk", OnPreReceiveEvent);
        }

        private unsafe void OnPreReceiveEvent(AddonEvent type, AddonArgs args)
        {
            if (!config.Enabled) return;
            if (condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedSummoningBell]) return;
            if (!condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInQuestEvent | Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedInCutSceneEvent]) return;
            if (args is not AddonReceiveEventArgs receiveEventArgs) return;

            if (receiveEventArgs.AtkEventType == (byte)AtkEventType.MouseClick && receiveEventArgs.EventParam == 0)
            {
                var eventId = LogHelper.EventId(MethodBase.GetCurrentMethod().Name, Enums.TextSource.Sync);
                if (!readySend)
                {
                    RabbitMQHelper.CreateMessage(Enums.RabbitMQMessages.Click, eventId);
                    readySend = true;
                }

                if (!SyncHelper.AllReady)
                {
                    receiveEventArgs.AtkEventType = 0;
                }
                else
                {
                    SyncHelper.Reset(eventId);
                    readySend = false;
                }
            }
        }

        public unsafe bool IsVisible()
        {
            var addonTalk = GetAddonTalk();
            return addonTalk != null && addonTalk->AtkUnitBase.IsVisible;
        }

        private unsafe AddonTalk* GetAddonTalk()
        {
            return (AddonTalk*)Address.ToPointer();
        }

        public void Click(EKEventId eventId)
        {
            ClickHelper.ClickDialogue(Address, eventId);
        }

        public void Dispose()
        {
            addonLifecycle.UnregisterListener(AddonEvent.PreReceiveEvent, "Talk", OnPreReceiveEvent);
        }
    }
}
