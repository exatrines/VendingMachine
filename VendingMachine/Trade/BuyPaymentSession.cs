namespace VendingMachine.Trade;

/// <summary>Buy-mode payment state across one or more trades (plan.md Buy 処理見直し).</summary>
internal sealed class BuyPaymentSession
{
    public bool Active;
    public string PartnerName = "";
    public ulong PartnerContentId;
    public uint PartnerEntityId;

    /// <summary>Committed order total (updated on trade complete).</summary>
    public int TotalGil;

    /// <summary>Running order total for the open trade.</summary>
    public int CurrentTotalGil;

    public int PaidGil;

    /// <summary>Items purchased in this session (reserved for future use).</summary>
    public List<TradeItemStack> OrderItems = [];

    public int UnpaidGil => Math.Max(0, Math.Max(TotalGil, CurrentTotalGil) - PaidGil);

    public void Reset()
    {
        Active = false;
        PartnerName = "";
        PartnerContentId = 0;
        PartnerEntityId = 0;
        TotalGil = 0;
        CurrentTotalGil = 0;
        PaidGil = 0;
        OrderItems = [];
    }
}
