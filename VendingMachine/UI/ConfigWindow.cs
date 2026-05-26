using VendingMachine.Services;

namespace VendingMachine.UI;

public static class ConfigWindow
{
    public static void Draw(TradeableItemCache cache)
    {
        ImGuiEx.EzTabBar("VmTabs",
            ("General", GeneralTab.Draw, null, true),
            ("Sell settings", () => SellSettingsTab.Draw(cache), null, true),
            ("Buy settings", () => BuySettingsTab.Draw(), null, true),
            ("Debug", DebugTab.Draw, ImGuiColors.DalamudGrey, true));
    }
}
