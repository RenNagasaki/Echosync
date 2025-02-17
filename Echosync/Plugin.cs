using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Game;
using Echosync.DataClasses;
using Echosync.Helper;
using Dalamud.Interface;

namespace Echosync;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;

    private const string CommandName = "/es";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("Echosync");
    private ConfigWindow ConfigWindow { get; init; }
    internal ReadyStateWindow ReadyStateWindow { get; init; }
    private AddonTalkHelper addonTalkHelper { get; set; }

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        ICommandManager commandManager,
        IFramework framework,
        IClientState clientState,
        ICondition condition,
        IObjectTable objectTable,
        IDataManager dataManager,
        IChatGui chatGui,
        IGameGui gameGui,
        ISigScanner sigScanner,
        IGameInteropProvider gameInterop,
        IGameConfig gameConfig,
        IAddonLifecycle addonLifecycle)
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        ReadyStateWindow = new ReadyStateWindow(this, dataManager, pluginInterface, Configuration);

        this.addonTalkHelper = new AddonTalkHelper(this, condition, framework, addonLifecycle, clientState, objectTable, Configuration);
        LogHelper.Setup(log, Configuration);
        SyncClientHelper.Setup(Configuration, clientState);
        DalamudHelper.Setup(objectTable, clientState, framework);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(ReadyStateWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the config window"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
    }

    public void Dispose()
    {
        addonTalkHelper.Dispose();
        SyncClientHelper.Dispose();
        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        ReadyStateWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleConfigUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
}
