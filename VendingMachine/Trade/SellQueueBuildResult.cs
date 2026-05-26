namespace VendingMachine.Trade;

public sealed class SellQueueBuildResult
{
    public List<QueueEntry> Queue { get; init; } = [];
    public List<SellEntry> IncludedEntries { get; init; } = [];
}
