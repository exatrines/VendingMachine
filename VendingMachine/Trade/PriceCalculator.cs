using VendingMachine.Services;

namespace VendingMachine.Trade;

public static class PriceCalculator
{
    private const int StackSize = 999;

    /// <summary>Sum of priced opponent slots (live trade) or stored stacks.</summary>
    public static int CalculateBuyTotal(IReadOnlyList<BuyEntry> prices)
    {
        if (!TradeAddonReader.IsTradeSessionActive())
            return 0;

        return SumOpponentTradeSlots(prices);
    }

    public static int CalculateBuyTotal(IEnumerable<TradeAddonReader.OpponentItemStack> storedOffer, IReadOnlyList<BuyEntry> prices)
    {
        if (TradeAddonReader.IsTradeSessionActive())
            return SumOpponentTradeSlots(prices);

        return SumItemStacks(storedOffer, prices);
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

    private static int SumItemStacks(IEnumerable<TradeAddonReader.OpponentItemStack> items, IReadOnlyList<BuyEntry> prices)
    {
        var priceByItem = prices.ToDictionary(x => x.ItemId, x => x.PricePerStackGil);
        var total = 0;

        foreach (var stack in items)
        {
            var itemId = stack.ItemId % 1_000_000;
            if (priceByItem.TryGetValue(itemId, out var pricePerStack))
                total += GilForQuantity(stack.Quantity, pricePerStack);
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

    public static List<TradeAddonReader.OpponentItemStack> MergeBuyOrderItems(
        IEnumerable<TradeAddonReader.OpponentItemStack> baseline,
        IEnumerable<TradeAddonReader.OpponentItemStack> current)
    {
        var merged = baseline
            .GroupBy(s => s.ItemId % 1_000_000)
            .ToDictionary(
                g => g.Key,
                g => new TradeAddonReader.OpponentItemStack
                {
                    ItemId = g.First().ItemId,
                    Quantity = g.Sum(s => s.Quantity),
                });

        foreach (var stack in current)
        {
            var itemId = stack.ItemId % 1_000_000;
            if (!merged.TryGetValue(itemId, out var existing))
            {
                merged[itemId] = new TradeAddonReader.OpponentItemStack { ItemId = stack.ItemId, Quantity = stack.Quantity };
                continue;
            }

            merged[itemId] = new TradeAddonReader.OpponentItemStack
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
