namespace VendingMachine.Trade;

public sealed partial class BuyTradeLogic
{
    private void HandleOpponentLockTransitions(
        List<TradeItemStack> opponentItems,
        bool opponentLocked,
        bool selfLocked)
    {
        if (!opponentLocked && opponentWasLocked)
        {
            opponentWasLocked = false;
            OnOpponentUnlocked(selfLocked);
            return;
        }

        if (!opponentLocked || opponentWasLocked)
            return;

        opponentWasLocked = true;
        VmLog.Information("opponent locked — preparing to lock.");
        OnOpponentLocked(opponentItems);
    }

    private void OnOpponentUnlocked(bool selfLocked)
    {
        if (phase is BuyPhase.WaitingOpponentConfirm or BuyPhase.ReadyToConfirm)
        {
            payTargetGil = 0;
            gilPaidThisTrade = 0;
            phase = BuyPhase.Negotiating;
            TradeTask.ConfirmAllowed = false;
            resetOfferLockClick = true;
            VmLog.Debug("opponent unlocked — back to negotiating.");
            return;
        }

        if (!selfLocked)
        {
            phase = BuyPhase.Negotiating;
            lastSyncedOfferSnapshot = "";
            lastSyncedPayTarget = -1;
            VmLog.Debug("opponent unlocked — negotiating.");
        }
    }

    private void OnOpponentLocked(List<TradeItemStack> opponentItems)
    {
        var amounts = BuyTradeAmounts.Compute(session, C.BuyEntries);
        ApplyAmountsToSession(opponentItems, amounts);
        gilPaidThisTrade = amounts.PayTargetGil;

        if (amounts.PayTargetGil <= 0)
        {
            phase = BuyPhase.AwaitingSelfLock;
            return;
        }

        if (IsMyGilAtTarget(amounts.PayTargetGil)
            && lastSyncedPayTarget == amounts.PayTargetGil
            && lastSyncedOfferSnapshot == offerSnapshot)
        {
            phase = BuyPhase.AwaitingSelfLock;
            return;
        }

        StartGilSync(amounts.PayTargetGil, lockAfter: true);
    }
}
