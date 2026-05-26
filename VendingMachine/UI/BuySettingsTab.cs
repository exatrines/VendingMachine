using VendingMachine.Services;
using VendingMachine.Trade;

namespace VendingMachine.UI;

public static class BuySettingsTab
{
    private static readonly ItemPickerState picker = new();

    public static void Draw()
    {
        ImGuiEx.TextWrapped(
            "Totals over 1,000,000 gil are paid across multiple trades with the same partner (up to 1,000,000 gil per trade).");
        ImGui.Separator();

        ImGuiEx.Text($"Buy price list ({C.BuyEntries.Count} items)");
        DrawItemPicker();
        ImGui.Separator();
        DrawPriceList();
    }

    private static void DrawItemPicker()
    {
        var results = ItemSearchService.SearchAllTradeable(picker.Filter);
        var entries = results.Select(r => new ItemPickerEntry { ItemId = r.ItemId, Name = r.Name }).ToList();

        if (!picker.TryAdd("buyPicker", entries))
            return;

        if (picker.HighlightedIndex < 0 || picker.HighlightedIndex >= entries.Count)
            return;

        var itemId = entries[picker.HighlightedIndex].ItemId;
        var existing = C.BuyEntries.FirstOrDefault(x => x.ItemId == itemId);
        if (existing != null)
            existing.PricePerStackGil = 0;
        else
            C.BuyEntries.Add(new BuyEntry { ItemId = itemId, PricePerStackGil = 0 });

        picker.Reset();
    }

    private static void DrawPriceList()
    {
        ImGui.BeginChild("##buyList", new System.Numerics.Vector2(0, 220), true);
        for (var i = 0; i < C.BuyEntries.Count; i++)
        {
            var entry = C.BuyEntries[i];
            ImGui.PushID(i);
            var name = ItemSearchService.GetItemName(entry.ItemId);
            var perItem = ItemSearchService.PricePerItemFromStack(entry.PricePerStackGil);

            ImGuiEx.Text(ItemPickerUI.FormatLabel(entry.ItemId, name));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(130);
            ImGui.DragInt("Price / stack##s", ref entry.PricePerStackGil, 100, 0, 1_000_000_000);
            ImGui.SameLine();
            ImGui.BeginDisabled(true);
            ImGui.SetNextItemWidth(100);
            var perItemDisplay = perItem;
            ImGui.DragInt("Price / item##i", ref perItemDisplay, 0, 0, 0);
            ImGui.EndDisabled();

            ImGui.SameLine();
            if (ImGui.Button("🗑"))
                C.BuyEntries.RemoveAt(i);
            ImGui.PopID();
        }

        ImGui.EndChild();
    }
}
