namespace VendingMachine.Trade;

/// <summary>One trade offer slot from TradeItemsLocal / TradeItemsRemote.</summary>
public readonly struct TradeAddonSlotDebugInfo
{
    public uint ItemId { get; init; }
    public string Name { get; init; }
}

/// <summary>Trade debug snapshot (game memory).</summary>
public sealed class TradeAddonDebugSnapshot
{
    public TradeAddonSlotDebugInfo MyOfferSlot1 { get; init; }
    public TradeAddonSlotDebugInfo MyOfferSlot2 { get; init; }
    public TradeAddonSlotDebugInfo MyOfferSlot3 { get; init; }
    public TradeAddonSlotDebugInfo MyOfferSlot4 { get; init; }
    public TradeAddonSlotDebugInfo MyOfferSlot5 { get; init; }
    public uint MyOfferGil { get; init; }
    public bool MyDone { get; init; }
    public string OpponentPlayerName { get; init; } = "";
    public bool OpponentDone { get; init; }
    public TradeAddonSlotDebugInfo OpponentOfferSlot1 { get; init; }
    public TradeAddonSlotDebugInfo OpponentOfferSlot2 { get; init; }
    public TradeAddonSlotDebugInfo OpponentOfferSlot3 { get; init; }
    public TradeAddonSlotDebugInfo OpponentOfferSlot4 { get; init; }
    public TradeAddonSlotDebugInfo OpponentOfferSlot5 { get; init; }
    public uint OpponentOfferGil { get; init; }
}
