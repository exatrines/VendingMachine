namespace VendingMachine.Trade;

public sealed partial class BuyTradeLogic
{
    public bool TryCompleteTradeAndContinue()
    {
        if (!session.Active)
            return false;

        var paid = gilPaidThisTrade > 0 ? gilPaidThisTrade : payTargetGil;
        if (paid <= 0)
            return false;

        var runningForCommit = Math.Max(session.CurrentTotalGil, peakRunningTotalGil);
        var commitDelta = Math.Max(0, runningForCommit - session.TotalGil);
        if (commitDelta > 0)
            session.TotalGil += commitDelta;
        else if (runningForCommit > session.CurrentTotalGil
            && EzThrottler.Throttle("VmBuyCommitPeak", 5000))
        {
            VmLog.Debug(
                $"trade complete used peak running total {runningForCommit:N0} (current {session.CurrentTotalGil:N0}).");
        }

        if (offerGil > 0 && offerItems.Count > 0)
            session.OrderItems = PriceCalculator.MergeBuyOrderItems(session.OrderItems, offerItems);

        session.PaidGil += paid;
        ResetTradeWindowState();

        VmLog.Information(
            $"trade complete — total {session.TotalGil:N0}, paid {session.PaidGil:N0}, remaining {session.UnpaidGil:N0}.");

        if (session.UnpaidGil > 0)
        {
            phase = BuyPhase.AwaitingNextTrade;
            nextTradeRequestMs = Environment.TickCount64 + 1500;
            VmLog.Information("unpaid remains — requesting next trade.");
            return true;
        }

        VmLog.Information("buy payment session complete.");
        return false;
    }

    private void TryRequestNextTrade()
    {
        if (!session.Active || session.UnpaidGil <= 0)
            return;

        if (Environment.TickCount64 < nextTradeRequestMs)
            return;

        if (!EzThrottler.Throttle("VmBuyTradeRequest", 2000))
            return;

        if (session.PartnerContentId == 0 && session.PartnerEntityId == 0)
        {
            VmLog.Warning("payment partner unknown; ending session.");
            ResetSession();
            return;
        }

        TradePartnerHelper.TryRequestTradeWithPartner(session.PartnerContentId, session.PartnerEntityId);
        nextTradeRequestMs = Environment.TickCount64 + 3000;
    }

    public BuySplitPaymentDebugInfo GetSplitPaymentDebugInfo()
    {
        if (!session.Active)
            return new BuySplitPaymentDebugInfo { Active = false };

        var partnerName = session.PartnerName;
        if (string.IsNullOrEmpty(partnerName))
        {
            partnerName = TradePartnerHelper.TryResolvePartnerName(session.PartnerContentId, session.PartnerEntityId)
                ?? TradePartnerHelper.GetTradePartner()?.Name.ToString();
        }

        var a = lastAmounts;
        return new BuySplitPaymentDebugInfo
        {
            Active = true,
            PartnerName = partnerName,
            TotalGil = session.TotalGil,
            CurrentTotalGil = session.CurrentTotalGil,
            PaidGil = session.PaidGil,
            RemainingGil = session.UnpaidGil,
            OfferGil = a.OfferGil,
            PriorUnpaidGil = a.PriorUnpaidGil,
            CombinedGil = a.CombinedGil,
            ThisTradeTargetGil = a.PayTargetGil,
            Phase = phase,
        };
    }
}
