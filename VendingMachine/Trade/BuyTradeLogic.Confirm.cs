namespace VendingMachine.Trade;

public sealed partial class BuyTradeLogic
{
    private void UpdateReadyToConfirm(bool opponentLocked)
    {
        if (!TradeAddonReader.IsLocalTradeLocked() || !opponentLocked)
            return;

        if (payTargetGil > 0 && !IsMyGilAtTarget(payTargetGil))
            return;

        if (!TradeAddonReader.IsAwaitingFinalTradeConfirm()
            && !TradeYesnoHelper.AnyTradeExecuteYesnoVisible())
            return;

        if (phase != BuyPhase.ReadyToConfirm)
            VmLog.Information("both locked — ready for trade execute SelectYesno.");

        phase = BuyPhase.ReadyToConfirm;
        TradeTask.ConfirmAllowed = true;
    }

    public bool ShouldAcceptFinalConfirm() =>
        phase == BuyPhase.ReadyToConfirm
        && TradeTask.ConfirmAllowed
        && TradeAddonReader.IsLocalTradeLocked()
        && TradeAddonReader.IsOpponentTradeLocked()
        && (TradeYesnoHelper.AnyTradeExecuteYesnoVisible()
            || TradeAddonReader.IsAwaitingFinalTradeConfirm());

    public bool ShouldClickTradeOfferLock()
    {
        if (phase != BuyPhase.AwaitingSelfLock)
            return false;

        if (TradeTask.IsActive || gilSyncPending || TradeAddonReader.IsLocalTradeLocked())
            return false;

        if (!TradeAddonReader.IsOpponentTradeLocked())
            return false;

        return payTargetGil <= 0 || IsMyGilAtTarget(payTargetGil);
    }

    public void OnSelfOfferLocked()
    {
        phase = phase is BuyPhase.ReadyToConfirm ? BuyPhase.ReadyToConfirm : BuyPhase.WaitingOpponentConfirm;
        VmLog.Information("self locked — waiting for opponent.");
    }
}
