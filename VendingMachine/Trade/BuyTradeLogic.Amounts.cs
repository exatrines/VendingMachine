namespace VendingMachine.Trade;

public sealed partial class BuyTradeLogic
{
    private void ApplyAmountsToSession(List<TradeItemStack> opponentItems, BuyTradeAmounts amounts)
    {
        var running = session.TotalGil + amounts.OfferGil;
        session.CurrentTotalGil = running;
        peakRunningTotalGil = Math.Max(peakRunningTotalGil, running);
        payTargetGil = amounts.PayTargetGil;

        if (opponentItems.Count > 0)
        {
            offerItems = opponentItems
                .Select(s => new TradeItemStack { ItemId = s.ItemId, Quantity = s.Quantity })
                .ToList();
            offerGil = amounts.OfferGil;
        }
    }
}
