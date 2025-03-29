using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Echosync.DataClasses;
using Echosync.Helper;
using Echosync.Windows;

namespace Echosync;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] private static ICommandManager CommandManager { get; set; } = null!;

    private const string CommandName = "/es";

    public Configuration Configuration { get; }

    private readonly WindowSystem _windowSystem = new("Echosync");
    private ConfigWindow ConfigWindow { get; }
    internal ReadyStateWindow ReadyStateWindow { get; }
    private AddonTalkHelper AddonTalkHelper { get; }

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        IFramework framework,
        IClientState clientState,
        ICondition condition,
        IObjectTable objectTable,
        IDataManager dataManager,
        IAddonLifecycle addonLifecycle)
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        ReadyStateWindow = new ReadyStateWindow(dataManager, pluginInterface, Configuration);

        this.AddonTalkHelper = new AddonTalkHelper(this, condition, framework, addonLifecycle, clientState, Configuration);
        LogHelper.Setup(log, Configuration);
        SyncClientHelper.Setup(Configuration, clientState, framework);
        DalamudHelper.Setup(objectTable, clientState, framework);

        _windowSystem.AddWindow(ConfigWindow);
        _windowSystem.AddWindow(ReadyStateWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the config window"
        });

        PluginInterface.UiBuilder.Draw += DrawUi;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
    }

    public void Dispose()
    {
        AddonTalkHelper.Dispose();
        SyncClientHelper.Dispose();
        _windowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        ReadyStateWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleConfigUi();
    }

    private void DrawUi() => _windowSystem.Draw();

    private void ToggleConfigUi() => ConfigWindow.Toggle();
}