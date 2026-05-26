namespace VendingMachine.Trade;

/// <summary>Aggregated item id and quantity from trade offer slots.</summary>
public sealed class TradeItemStack
{
    public uint ItemId { get; init; }
    public int Quantity { get; init; }
}
