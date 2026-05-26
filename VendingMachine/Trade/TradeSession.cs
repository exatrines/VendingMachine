namespace VendingMachine.Trade;

public sealed class TradeSession
{
    public required TradeMode Mode { get; init; }
    public required IReadOnlyList<TradeResultLine> Lines { get; init; }
    public required int TotalGil { get; init; }
}
