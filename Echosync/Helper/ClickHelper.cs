using Echotools.Logging.DataClasses;
using Echotools.Logging.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Echosync.Helper;

public static unsafe class ClickHelper
{
    public static void ClickDialogue(nint addon, EKEventId eventId, ILogService log)
    {
        log.Debug(nameof(ClickDialogue), "Auto advancing...", eventId);
        var unitBase = (AtkUnitBase*)addon;

        if (unitBase != null && AtkStage.Instance() != null)
        {
            var evt = stackalloc AtkEvent[1]
            {
                new()
                {
                    Listener = (AtkEventListener*)unitBase,
                    State = new AtkEventState
                    {
                        StateFlags = AtkEventStateFlags.Pooled | AtkEventStateFlags.Unk3,
                    },
                    Target = &AtkStage.Instance()->AtkEventTarget 
                }
            };
            var data = stackalloc AtkEventData[1];

            unitBase->ReceiveEvent(AtkEventType.MouseClick, 0, evt, data);
        }
    }
}
