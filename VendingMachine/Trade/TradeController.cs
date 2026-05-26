using ECommons.Throttlers;

namespace VendingMachine.Trade;

/// <summary>
/// Per-frame trade automation. Order while trade is open:
/// 1) Buy/Sell logic update  2) Yesno accept (before task gate)  3) Task queue  4) Lock button click
/// </summary>
public sealed unsafe class TradeController
{
    private readonly SellTradeLogic sellLogic = new();
    private readonly BuyTradeLogic buyLogic = new();

    private bool wasTradeOpen;
    private bool offerLockClicked;

    public BuySplitPaymentDebugInfo GetBuySplitPaymentDebugInfo() => buyLogic.GetSplitPaymentDebugInfo();

    public TradeAddonDebugSnapshot? GetTradeAddonDebugSnapshot()
    {
        if (!TradeAddonReader.IsTradeSessionActive())
            return null;

        return TradeAddonDebugReader.TryBuildSnapshot(out var snapshot)
            ? snapshot
            : TradeAddonDebugReader.CreateEmptySnapshotForDisplay();
    }

    public void Reset()
    {
        sellLogic.Reset();
        buyLogic.Reset();
        offerLockClicked = false;
    }

    public void Update()
    {
        if (!C.Enabled)
        {
            Reset();
            return;
        }

        var tradeOpen = Svc.Condition[ConditionFlag.TradeOpen];

        if (wasTradeOpen && !tradeOpen)
        {
            if ((C.Mode == TradeMode.Buy && buyLogic.HasActiveBuySession)
                || (C.Mode == TradeMode.Sell && sellLogic.HadActiveTrade))
                OnTradeComplete();
            else
                OnTradeCanceled();

            offerLockClicked = false;
        }

        wasTradeOpen = tradeOpen;

        if (C.Mode == TradeMode.Sell)
            sellLogic.Update(tradeOpen);
        else
            buyLogic.Update(tradeOpen);

        if (tradeOpen && C.Mode == TradeMode.Buy && buyLogic.ConsumeTradeOfferClickReset())
            offerLockClicked = false;

        if (!tradeOpen)
            return;

        if (TryAcceptTradeYesnoIfReady())
            return;

        if (TradeTask.IsActive)
            return;

        if (C.Mode == TradeMode.Sell)
        {
            if (sellLogic.ShouldConfirm())
                TryClickOfferLock();
            return;
        }

        if (buyLogic.ShouldClickTradeOfferLock())
            TryClickOfferLock();
    }

    public void OnTradeComplete()
    {
        if (C.Mode == TradeMode.Buy && buyLogic.TryCompleteTradeAndContinue())
        {
            offerLockClicked = false;
            return;
        }

        Reset();
    }

    public void OnTradeCanceled()
    {
        if (C.Mode == TradeMode.Buy && buyLogic.IsPaymentSessionActive)
            buyLogic.Reset();

        Reset();
    }

    private bool TryAcceptTradeYesnoIfReady()
    {
        var ready = C.Mode == TradeMode.Sell
            ? sellLogic.ShouldConfirm()
            : buyLogic.ShouldAcceptFinalConfirm();

        if (!ready)
            return false;

        TryAcceptTradeYesno();

        if (C.Mode == TradeMode.Sell
            && (TradeAddonReader.IsAwaitingFinalTradeConfirm() || TradeYesnoHelper.AnyTradeExecuteYesnoVisible()))
            return true;

        return C.Mode == TradeMode.Buy;
    }

    private void TryAcceptTradeYesno()
    {
        if (!TradeYesnoHelper.AnyTradeExecuteYesnoVisible())
        {
            if (TradeAddonReader.IsAwaitingFinalTradeConfirm()
                && EzThrottler.Throttle("VmYesnoMissing", 5000))
            {
                PluginLog.Debug("Vending Machine: awaiting trade execute but SelectYesno is not visible yet.");
            }

            return;
        }

        TradeYesnoHelper.TryAcceptTradeExecuteYesno();
    }

    private void TryClickOfferLock()
    {
        if (offerLockClicked || TradeAddonReader.IsLocalTradeLocked())
            return;

        if (!TradeAddonReader.TryGetTradeAddon(out var addon))
            return;

        if (!TradeTask.ConfirmAllowed && !TradeAddonReader.CanConfirmTrade(addon))
            return;

        if (!EzThrottler.Throttle("VmTradeOfferClick", 500))
            return;

        offerLockClicked = true;
        PluginLog.Information("Vending Machine: clicking trade offer button (条件提示)");

        if (!TradeAddonReader.TryClickTradeOfferButton(addon))
        {
            offerLockClicked = false;
            return;
        }

        if (C.Mode == TradeMode.Buy)
            buyLogic.OnSelfOfferLocked();
    }
}
