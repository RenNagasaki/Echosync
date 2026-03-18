using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Echosync.DataClasses;
using Echosync.Helper;
using Echosync.Localization;
using Echosync.Windows;
using Echosync.Windows.Native;
using Echotools.Logging.Services;
using KamiToolKit;

namespace Echosync;

public sealed class Plugin : IDalamudPlugin
{
    public static readonly string PluginVersion = $"v{Assembly.GetExecutingAssembly().GetName().Version!.ToString(3)}";

    private const string CommandName = "/es";

    private readonly ICommandManager _commandManager;
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly ConfigWindow _configWindow;
    private readonly ReadyStateWindow _readyStateWindow;
    private readonly NativeConfigWindow _nativeConfigWindow;
    private readonly ReadyStateTalkController _readyStateTalkController;
    private readonly AddonTalkHelper _addonTalkHelper;
    private readonly SyncClientHelper _syncClient;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        IPluginLog log,
        IFramework framework,
        ICondition condition,
        IObjectTable objectTable,
        IDataManager dataManager,
        IAddonLifecycle addonLifecycle,
        ICommandManager commandManager,
        ITextureProvider textureProvider,
        IClientState clientState)
    {
        _pluginInterface = pluginInterface;
        _commandManager = commandManager;

        KamiToolKitLibrary.Initialize(pluginInterface, $"Echosync {PluginVersion}");

        var configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        configuration.Initialize(pluginInterface);

        Loc.Initialize(clientState.ClientLanguage);

        var logService = new LogService(log);
        _syncClient = new SyncClientHelper(framework, objectTable, configuration, logService);

        _addonTalkHelper = new AddonTalkHelper(
            addonLifecycle, objectTable, condition, framework, configuration,
            _syncClient, logService);

        // Native UI (active)
        _nativeConfigWindow = new NativeConfigWindow(configuration, _syncClient, logService);
        _readyStateTalkController = new ReadyStateTalkController(configuration, _syncClient);

        // Legacy ImGui windows (kept but not wired to draw)
        _readyStateWindow = new ReadyStateWindow(
            pluginInterface, textureProvider, dataManager, configuration,
            _syncClient, _addonTalkHelper, logService);

        _configWindow = new ConfigWindow(configuration, _syncClient, logService);

        _commandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the config window"
        });

        _pluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        _pluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;

        _syncClient.Setup();
    }

    public void Dispose()
    {
        _pluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        _pluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;
        _addonTalkHelper.Dispose();
        _syncClient.Dispose();
        _readyStateTalkController.Dispose();
        _configWindow.Dispose();
        _readyStateWindow.Dispose();
        _commandManager.RemoveHandler(CommandName);
        KamiToolKitLibrary.Cleanup();
    }

    private void OnCommand(string command, string args) => ToggleConfigUi();

    private void ToggleConfigUi() => _nativeConfigWindow.Toggle();
}
