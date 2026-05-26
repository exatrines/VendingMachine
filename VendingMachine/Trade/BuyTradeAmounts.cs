namespace VendingMachine.Trade;

internal readonly record struct BuyTradeAmounts(int OfferGil, int PriorUnpaidGil, int CombinedGil, int PayTargetGil)
{
    public static BuyTradeAmounts Compute(BuyPaymentSession session, IReadOnlyList<BuyEntry> buyEntries)
    {
        var offer = PriceCalculator.CalculateBuyTotal(buyEntries);
        var priorUnpaid = Math.Max(0, session.TotalGil - session.PaidGil);
        var combined = offer + priorUnpaid;
        var payTarget = combined <= 0 ? 0 : Math.Min(combined, TradeLimits.MaxGilPerTrade);
        return new BuyTradeAmounts(offer, priorUnpaid, combined, payTarget);
    }
}
