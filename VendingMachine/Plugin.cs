using Dalamud.Plugin;
using ECommons.Configuration;
using ECommons.SimpleGui;
using VendingMachine.Services;
using VendingMachine.Trade;
using VendingMachine.UI;

namespace VendingMachine;

public unsafe class Plugin : IDalamudPlugin
{
    public string Name => "Vending Machine";

    internal static Configuration C = null!;
    internal static Plugin P = null!;
    internal static TaskManager Tasks = null!;

    private readonly TradeController tradeController = new();

    internal TradeController Trade => tradeController;
    private readonly TradeableItemCache tradeableCache = new();

    public Memory Memory { get; private set; } = null!;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        P = this;
        ECommonsMain.Init(pluginInterface, this);
        Tasks = new TaskManager { AbortOnTimeout = true, TimeLimitMS = 60_000 };

        C = EzConfig.Init<Configuration>();
        EzConfigGui.Init(DrawConfig);
        EzCmd.Add("/vendingmachine", EzConfigGui.Open);
        EzCmd.Add("/vm", EzConfigGui.Open);

        Memory = new Memory();

        Svc.Framework.Update += Framework_Update;
    }

    private void Framework_Update(object framework) => tradeController.Update();

    private void DrawConfig() => UI.ConfigWindow.Draw(tradeableCache);

    internal static void NotifyError(string message)
    {
        PluginLog.Warning(message);
        Notify.Error(message);
    }

    public void Dispose()
    {
        Svc.Framework.Update -= Framework_Update;
        ECommonsMain.Dispose();
        P = null!;
        C = null!;
    }
}
