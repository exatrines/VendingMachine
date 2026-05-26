using VendingMachine.Trade;

namespace VendingMachine.UI;

public static class DebugTab
{
    private static TradeAddonDebugSnapshot? cachedTradeSnapshot;
    private static long cachedTradeSnapshotMs;

    public static void Draw()
    {
        DrawTradeAddonDebug();
        ImGui.Separator();
        DrawSplitPaymentDebug();
    }

    private static void DrawTradeAddonDebug()
    {
        if (!ImGui.CollapsingHeader("Trade debug (memory)", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        if (!TradeAddonReader.IsTradeSessionActive())
        {
            cachedTradeSnapshot = null;
            ImGuiEx.Text(ImGuiColors.DalamudGrey, "Trade window is not open.");
            return;
        }

        var now = Environment.TickCount64;
        if (cachedTradeSnapshot == null || now - cachedTradeSnapshotMs >= 500)
        {
            cachedTradeSnapshot = P.Trade.GetTradeAddonDebugSnapshot();
            cachedTradeSnapshotMs = now;
        }

        var snap = cachedTradeSnapshot ?? TradeAddonDebugReader.CreateEmptySnapshotForDisplay();

        DrawOfferSection(
            "My Offer Info",
            snap.MyOfferGil,
            snap.MyDone,
            [
                snap.MyOfferSlot1,
                snap.MyOfferSlot2,
                snap.MyOfferSlot3,
                snap.MyOfferSlot4,
                snap.MyOfferSlot5,
            ]);

        var opponentTitle = string.IsNullOrWhiteSpace(snap.OpponentPlayerName)
            ? "Opponent Offer Info"
            : $"{snap.OpponentPlayerName} Offer Info";

        DrawOfferSection(
            opponentTitle,
            snap.OpponentOfferGil,
            snap.OpponentDone,
            [
                snap.OpponentOfferSlot1,
                snap.OpponentOfferSlot2,
                snap.OpponentOfferSlot3,
                snap.OpponentOfferSlot4,
                snap.OpponentOfferSlot5,
            ]);
    }

    private static void DrawOfferSection(string title, uint gil, bool done, TradeAddonSlotDebugInfo[] slots)
    {
        ImGui.Separator();
        ImGuiEx.Text(title);
        ImGuiEx.Text($"gil : {FormatGil(gil)}");
        ImGuiEx.Text($"Done : {done}");
        for (var i = 0; i < slots.Length; i++)
            ImGuiEx.Text($"itemslot#{i + 1} : {FormatSlot(slots[i])}");
    }

    private static string FormatGil(uint gil) => gil == 0 ? "(empty)" : $"{gil:N0}";

    private static string FormatSlot(TradeAddonSlotDebugInfo slot)
    {
        if (slot.ItemId == 0)
            return "(empty)";

        return $"{slot.Name}#{slot.ItemId}";
    }

    private static void DrawSplitPaymentDebug()
    {
        if (!ImGui.CollapsingHeader("Buy payment session", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var info = P.Trade.GetBuySplitPaymentDebugInfo();
        if (!info.Active)
        {
            ImGuiEx.Text(ImGuiColors.DalamudGrey, "No active split payment.");
            return;
        }

        ImGuiEx.Text($"Partner: {info.PartnerName ?? "(unknown)"}");
        ImGuiEx.Text($"1. offer (this trade): {info.OfferGil:N0}");
        ImGuiEx.Text($"2. prior unpaid: {info.PriorUnpaidGil:N0}");
        ImGuiEx.Text($"3. combined (1+2): {info.CombinedGil:N0}");
        ImGuiEx.Text($"→ gil to offer: {info.ThisTradeTargetGil:N0} (cap 1,000,000)");
        ImGuiEx.Text(ImGuiColors.DalamudGrey, $"committed total: {info.TotalGil:N0}  |  paid: {info.PaidGil:N0}  |  session unpaid: {info.RemainingGil:N0}");
        ImGuiEx.Text(ImGuiColors.DalamudGrey, $"Phase: {info.Phase}");
    }
}
