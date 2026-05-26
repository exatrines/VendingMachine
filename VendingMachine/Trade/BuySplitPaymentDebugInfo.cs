namespace VendingMachine.Trade;

public readonly struct BuySplitPaymentDebugInfo
{
    public bool Active { get; init; }
    public string? PartnerName { get; init; }
    /// <summary>Committed total (plan: total — updated on trade complete).</summary>
    public int TotalGil { get; init; }
    /// <summary>Running total for the current trade (plan: current_total).</summary>
    public int CurrentTotalGil { get; init; }
    public int PaidGil { get; init; }
    public int RemainingGil { get; init; }
    /// <summary>Current opponent offer in trade window (this frame).</summary>
    public int OfferGil { get; init; }
    /// <summary>Committed unpaid before this offer (TotalGil - PaidGil).</summary>
    public int PriorUnpaidGil { get; init; }
    /// <summary>OfferGil + PriorUnpaidGil.</summary>
    public int CombinedGil { get; init; }
    public int ThisTradeTargetGil { get; init; }
    public BuyPhase Phase { get; init; }
}
