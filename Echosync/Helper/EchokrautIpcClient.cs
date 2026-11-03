using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;

namespace Echosync.Helper;

/// <summary>
/// Subscribes to Echokraut's <c>Echokraut.IsClickInToolbar(int x, int y) → bool</c> IPC so we
/// can filter out clicks that landed on Echokraut's DialogExtraOptions toolbar (Play/Stop,
/// Mute, Voice dropdown, Settings) instead of treating them as dialog-advance and round-tripping
/// them through the sync server. Listener order across plugins is non-deterministic, so neither
/// PreventOriginal nor AtkEventType=0 from Echokraut would reliably reach us first.
///
/// Gracefully degrades to "always false" when Echokraut isn't installed.
/// </summary>
public sealed class EchokrautIpcClient
{
    private const string ClickInToolbarIdent = "Echokraut.IsClickInToolbar";

    private readonly ICallGateSubscriber<int, int, bool> _subscriber;

    public EchokrautIpcClient(IDalamudPluginInterface pluginInterface)
    {
        _subscriber = pluginInterface.GetIpcSubscriber<int, int, bool>(ClickInToolbarIdent);
    }

    /// <summary>
    /// Returns true when Echokraut reports that the screen-space click hit its toolbar.
    /// Returns false (i.e. let Echosync keep handling the click) whenever Echokraut isn't
    /// loaded, hasn't registered its provider yet, or any IPC error happens.
    /// </summary>
    public bool IsClickInEchokrautToolbar(int x, int y)
    {
        try
        {
            return _subscriber.InvokeFunc(x, y);
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
