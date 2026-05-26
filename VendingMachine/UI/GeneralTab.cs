namespace VendingMachine.UI;

public static class GeneralTab
{
    public static void Draw()
    {
        ImGui.Checkbox("Enable", ref C.Enabled);
        ImGui.Separator();
        ImGui.Text("Auto Trade Mode");
        if (ImGui.RadioButton("Sell", C.Mode == TradeMode.Sell))
            C.Mode = TradeMode.Sell;
        ImGui.SameLine();
        if (ImGui.RadioButton("Buy", C.Mode == TradeMode.Buy))
            C.Mode = TradeMode.Buy;

        if (!P.Memory.HookReady)
            ImGuiEx.Text(EColor.RedBright, "Trade item hook failed to load. Sell automation is unavailable.");
    }
}
