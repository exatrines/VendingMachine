using ECommons.ExcelServices;
using VendingMachine.Services;
using VendingMachine.Trade;

namespace VendingMachine.UI;

public static class SellSettingsTab
{
    private const int MaxEntries = 5;

    private static readonly ItemPickerState picker = new();
    private static List<ItemSearchService.InventorySearchResult> inventoryResults = [];

    public static void Draw(TradeableItemCache cache)
    {
        var atMax = C.SellEntries.Count >= MaxEntries;
        ImGuiEx.Text($"Sell list ({C.SellEntries.Count}/{MaxEntries})");

        ImGui.BeginDisabled(atMax);
        if (!atMax)
            DrawItemPicker(cache);
        else
            ImGuiEx.Text(ImGuiColors.DalamudGrey, "List full (5 items). Remove an entry to add more.");
        ImGui.EndDisabled();

        ImGui.Separator();
        DrawSellList();

        if (C.SellEntries.Count > 0)
        {
            ImGui.Separator();
            var total = C.SellEntries.Sum(e => e.PriceGil);
            if (total > TradeLimits.MaxGilPerTrade)
            {
                ImGuiEx.Text(ImGuiColors.DalamudRed,
                    $"Expected gil: {total:N0} — exceeds per-trade limit ({TradeLimits.MaxGilPerTrade:N0})");
            }
            else
            {
                ImGuiEx.Text($"Expected gil (minimum): {total:N0} / {TradeLimits.MaxGilPerTrade:N0}");
            }
        }
    }

    private static void DrawItemPicker(TradeableItemCache cache)
    {
        inventoryResults = ItemSearchService.SearchInventory(cache, picker.Filter);
        var entries = inventoryResults.Select(ToPickerEntry).ToList();

        if (!picker.TryAdd("sellPicker", entries))
            return;

        if (picker.HighlightedIndex < 0 || picker.HighlightedIndex >= inventoryResults.Count)
            return;

        var descriptor = inventoryResults[picker.HighlightedIndex].Descriptor;
        C.SellEntries.Add(new SellEntry
        {
            ItemId = (uint)descriptor.Id,
            Count = 1,
            PriceGil = 0,
        });

        picker.Reset();
    }

    private static ItemPickerEntry ToPickerEntry(ItemSearchService.InventorySearchResult result)
    {
        var name = ExcelItemHelper.GetName((uint)result.Descriptor.Id);
        if (result.Descriptor.HQ)
            name += "";

        return new ItemPickerEntry
        {
            ItemId = (uint)result.Descriptor.Id,
            Name = name,
            Detail = $"(x{result.Count})",
        };
    }

    private static void DrawSellList()
    {
        for (var i = 0; i < C.SellEntries.Count; i++)
        {
            var entry = C.SellEntries[i];
            ImGui.PushID(i);
            ImGuiEx.Text(ItemPickerUI.FormatLabel(entry.ItemId, ExcelItemHelper.GetName(entry.ItemId)));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            ImGui.DragInt("Count##c", ref entry.Count, 1, 1, 999);
            ImGui.SameLine();
            ImGui.SetNextItemWidth(120);
            ImGui.DragInt("Price (gil)##p", ref entry.PriceGil, 1000, 0, 1_000_000_000);
            ClampSellEntryPrice(i);
            ImGui.SameLine();
            if (ImGui.Button("🗑"))
                C.SellEntries.RemoveAt(i);
            ImGui.PopID();
        }
    }

    private static void ClampSellEntryPrice(int entryIndex)
    {
        var entry = C.SellEntries[entryIndex];
        var others = C.SellEntries.Where((_, i) => i != entryIndex).Sum(e => e.PriceGil);
        var maxForEntry = Math.Max(0, TradeLimits.MaxGilPerTrade - others);
        if (entry.PriceGil > maxForEntry)
            entry.PriceGil = maxForEntry;
    }
}
