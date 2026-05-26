using ECommons.ExcelServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace VendingMachine.Services;

public static unsafe class ItemSearchService
{
    /// <summary>ItemUICategory row for 通貨 / Currency (Gil, tomestones, seals, etc.).</summary>
    private const uint CurrencyItemUiCategoryId = 63;

    public static readonly InventoryType[] ValidInventories =
    [
        InventoryType.Inventory1,
        InventoryType.Inventory2,
        InventoryType.Inventory3,
        InventoryType.Inventory4,
        InventoryType.Crystals,
    ];

    public sealed class InventorySearchResult
    {
        public required ItemDescriptor Descriptor { get; init; }
        public uint Count { get; init; }
        public bool CanStack { get; init; }
    }

    public sealed class AllItemSearchResult
    {
        public required uint ItemId { get; init; }
        public required string Name { get; init; }
        public bool CanStack { get; init; }
    }

    public static List<InventorySearchResult> SearchInventory(TradeableItemCache cache, string filter, int maxResults = 50)
    {
        var tradeable = cache.GetTradeableItemIds();
        var aggregated = new Dictionary<ItemDescriptor, (uint Count, bool CanStack)>();
        var im = InventoryManager.Instance();

        foreach (var inv in ValidInventories)
        {
            var cont = im->GetInventoryContainer(inv);
            for (var i = 0; i < cont->GetSize(); i++)
            {
                var slot = cont->GetInventorySlot(i);
                var itemId = slot->GetItemId();
                if (itemId == 0)
                    continue;

                var baseId = itemId % 1_000_000;
                if (!tradeable.Contains(baseId))
                    continue;

                var hq = itemId > 1_000_000;
                var desc = new ItemDescriptor(baseId, hq);
                if (aggregated.TryGetValue(desc, out var existing))
                    aggregated[desc] = (existing.Count + slot->GetQuantity(), existing.CanStack);
                else
                    aggregated[desc] = (slot->GetQuantity(), CanStack(baseId));
            }
        }

        var ret = aggregated
            .Where(kv => HasDisplayName((uint)kv.Key.Id))
            .Select(kv => new InventorySearchResult
            {
                Descriptor = kv.Key,
                Count = kv.Value.Count,
                CanStack = kv.Value.CanStack,
            })
            .ToList();

        if (!string.IsNullOrWhiteSpace(filter))
        {
            ret = ret.Where(x =>
            {
                var name = ExcelItemHelper.GetName((uint)x.Descriptor.Id);
                if (x.Descriptor.HQ)
                    name += "";
                return MatchesItemFilter(name, (uint)x.Descriptor.Id, filter);
            }).ToList();
        }

        return ret.OrderBy(x => x.Descriptor.Id).Take(maxResults).ToList();
    }

    public static List<AllItemSearchResult> SearchAllTradeable(string filter, int maxResults = 80)
    {
        var sheet = Svc.Data.GetExcelSheet<Item>();
        var query = sheet.Where(x => !x.IsUntradable && HasDisplayName(x.RowId) && !IsCurrencyItem(x.RowId));

        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(x => MatchesItemFilter(x.Name.ToString(), x.RowId, filter));
        }

        return query
            .OrderBy(x => x.RowId)
            .Take(maxResults)
            .Select(x => new AllItemSearchResult
            {
                ItemId = x.RowId,
                Name = x.Name.ToString(),
                CanStack = x.StackSize > 1,
            })
            .ToList();
    }

    /// <summary>Per-item buy price derived from stack price (stack price ÷ 1000, rounded up).</summary>
    public static int PricePerItemFromStack(int pricePerStack) =>
        Math.Max(0, (int)Math.Ceiling(pricePerStack / 1000.0));

    public static string GetItemName(uint itemId) => ExcelItemHelper.GetName(itemId, true);

    public static bool CanStack(uint itemId)
    {
        var row = Svc.Data.GetExcelSheet<Item>()!.GetRowOrDefault(itemId);
        return row != null && row.Value.StackSize > 1;
    }

    /// <summary>Currency items (通貨) such as Gil and tomestones.</summary>
    public static bool IsCurrencyItem(uint itemId)
    {
        var row = Svc.Data.GetExcelSheet<Item>()!.GetRowOrDefault(itemId);
        if (row == null)
            return false;

        return row.Value.ItemUICategory.RowId == CurrencyItemUiCategoryId;
    }

    /// <summary>Items with no localized name (ID-only rows) are hidden from pickers.</summary>
    public static bool HasDisplayName(uint itemId)
    {
        var row = Svc.Data.GetExcelSheet<Item>()!.GetRowOrDefault(itemId);
        if (row == null)
            return false;

        var name = row.Value.Name.GetText();
        return !string.IsNullOrWhiteSpace(name);
    }

    private static bool MatchesItemFilter(string name, uint itemId, string filter) =>
        name.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || itemId.ToString().Contains(filter, StringComparison.Ordinal);
}
