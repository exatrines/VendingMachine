namespace VendingMachine.Trade;

internal static class VmLog
{
    private const string Prefix = "Vending Machine: ";

    public static void Debug(string message) => PluginLog.Debug(Prefix + message);

    public static void Information(string message) => PluginLog.Information(Prefix + message);

    public static void Warning(string message) => PluginLog.Warning(Prefix + message);
}
