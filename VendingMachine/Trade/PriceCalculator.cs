using VendingMachine.Services;

namespace VendingMachine.Trade;

public static class PriceCalculator
{
    private const int StackSize = 999;

    /// <summary>Sum of priced opponent slots in the open trade window.</summary>
    public static int CalculateBuyTotal(IReadOnlyList<BuyEntry> prices)
    {
        if (!TradeAddonReader.IsTradeSessionActive())
            return 0;

        return SumOpponentTradeSlots(prices);
    }

    private static int SumOpponentTradeSlots(IReadOnlyList<BuyEntry> prices)
    {
        var priceByItem = prices.ToDictionary(x => x.ItemId, x => x.PricePerStackGil);
        var total = 0;

        for (var slot = 0; slot < 5; slot++)
        {
            TradeAddonReader.GetTradeSlotFromMemory(local: false, slot, out var itemId, out var quantity, out _);
            if (itemId == 0 || quantity == 0)
                continue;

            if (priceByItem.TryGetValue(itemId, out var pricePerStack))
                total += GilForQuantity((int)quantity, pricePerStack);
        }

        return total;
    }

    /// <summary>Full stacks (999) at stack price; remainder at per-item price (stack ÷ 1000, rounded up).</summary>
    public static int GilForQuantity(int quantity, int pricePerStack)
    {
        if (quantity <= 0)
            return 0;

        var pricePerItem = ItemSearchService.PricePerItemFromStack(pricePerStack);
        return quantity / StackSize * pricePerStack + quantity % StackSize * pricePerItem;
    }

    public static List<TradeItemStack> MergeBuyOrderItems(
        IEnumerable<TradeItemStack> baseline,
        IEnumerable<TradeItemStack> current)
    {
        var merged = baseline
            .GroupBy(s => ItemIdHelper.Normalize(s.ItemId))
            .ToDictionary(
                g => g.Key,
                g => new TradeItemStack
                {
                    ItemId = g.First().ItemId,
                    Quantity = g.Sum(s => s.Quantity),
                });

        foreach (var stack in current)
        {
            var itemId = ItemIdHelper.Normalize(stack.ItemId);
            if (!merged.TryGetValue(itemId, out var existing))
            {
                merged[itemId] = new TradeItemStack { ItemId = stack.ItemId, Quantity = stack.Quantity };
                continue;
            }

            merged[itemId] = new TradeItemStack
            {
                ItemId = existing.ItemId,
                Quantity = Math.Max(existing.Quantity, stack.Quantity),
            };
        }

        return merged.Values.OrderBy(s => s.ItemId).ToList();
    }

    public static int SellExpectedTotal(IReadOnlyList<SellEntry> entries) =>
        entries.Sum(e => e.PriceGil);
}
