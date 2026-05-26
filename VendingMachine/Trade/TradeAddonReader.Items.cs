namespace VendingMachine.Trade;

public static partial class TradeAddonReader
{
    /// <summary>Opponent offer aggregated from the five trade slots (TradeItemsRemote).</summary>
    public static List<TradeItemStack> GetOpponentItems() => AggregateOpponentTradeSlots();

    /// <summary>Snapshot of all five opponent trade slots (for change detection).</summary>
    public static string SerializeOpponentTradeSlots()
    {
        var parts = new string[5];
        for (var i = 0; i < 5; i++)
        {
            GetTradeSlotFromMemory(local: false, i, out var itemId, out var quantity, out var isHq);
            parts[i] = $"{i}:{itemId}:{quantity}:{(isHq ? 1 : 0)}";
        }

        return string.Join("|", parts);
    }

    private static List<TradeItemStack> AggregateOpponentTradeSlots()
    {
        var merged = new Dictionary<uint, int>();
        for (var i = 0; i < 5; i++)
        {
            GetTradeSlotFromMemory(local: false, i, out var itemId, out var quantity, out _);
            if (itemId == 0 || quantity == 0)
                continue;

            merged[itemId] = merged.GetValueOrDefault(itemId) + (int)quantity;
        }

        return ToStacks(merged);
    }

    private static List<TradeItemStack> ToStacks(Dictionary<uint, int> merged) =>
        merged.Select(kv => new TradeItemStack { ItemId = kv.Key, Quantity = kv.Value }).ToList();
}
