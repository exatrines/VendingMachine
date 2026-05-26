namespace VendingMachine.Trade;

public enum SellPhase
{
    Idle,
    PlacingItems,
    WaitingGil,
    ReadyToConfirm,
    Failed,
}

public sealed unsafe class SellTradeLogic
{
    private SellPhase phase = SellPhase.Idle;
    private bool itemsEnqueued;
    private bool emptyQueueReported;
    private List<SellEntry> activeSellEntries = [];
    private int expectedGil;

    public void Reset()
    {
        phase = SellPhase.Idle;
        itemsEnqueued = false;
        emptyQueueReported = false;
        activeSellEntries = [];
        expectedGil = 0;
        TradeTask.ConfirmAllowed = false;
    }

    public void OnTradeOpened()
    {
        if (phase != SellPhase.Idle || C.SellEntries.Count == 0)
            return;

        var build = InventoryAllocator.BuildSellQueue(C.SellEntries);
        activeSellEntries = build.IncludedEntries;
        expectedGil = PriceCalculator.SellExpectedTotal(activeSellEntries);

        if (expectedGil > TradeLimits.MaxGilPerTrade)
        {
            if (!emptyQueueReported)
            {
                Notify.Error(
                    $"Sell total {expectedGil:N0} gil exceeds 1,000,000. Lower prices or remove items.");
                PluginLog.Warning(
                    $"Vending Machine: sell total {expectedGil:N0} gil exceeds 1,000,000 — not placing items.");
                emptyQueueReported = true;
            }

            itemsEnqueued = true;
            phase = SellPhase.Failed;
            return;
        }

        if (build.Queue.Count == 0)
        {
            if (!emptyQueueReported)
            {
                PluginLog.Warning("Vending Machine: no sell lines could be queued (no matching inventory stacks).");
                emptyQueueReported = true;
            }

            itemsEnqueued = true;
            phase = SellPhase.Failed;
            return;
        }

        phase = SellPhase.PlacingItems;
        itemsEnqueued = true;
        TaskAddItemsToTrade.EnqueueItems(build.Queue, allowConfirmAfter: false);
    }

    public void Update(bool tradeOpen)
    {
        if (!tradeOpen)
        {
            if (phase != SellPhase.Idle)
                Reset();
            return;
        }

        if (!itemsEnqueued && phase == SellPhase.Idle)
            OnTradeOpened();

        if (phase == SellPhase.PlacingItems && !TradeTask.IsActive)
            phase = SellPhase.WaitingGil;

        if (phase is SellPhase.WaitingGil or SellPhase.ReadyToConfirm)
            UpdateWaitingGil();
    }

    private void UpdateWaitingGil()
    {
        if (!TradeAddonReader.TryGetTradeAddon(out var addon))
            return;

        if (TradeAddonReader.GetOpponentGil(addon) >= expectedGil)
        {
            phase = SellPhase.ReadyToConfirm;
            TradeTask.ConfirmAllowed = true;
        }
        else
        {
            TradeTask.ConfirmAllowed = false;
        }
    }

    public bool ShouldConfirm() => phase == SellPhase.ReadyToConfirm && TradeTask.ConfirmAllowed;

    public bool HadActiveTrade => itemsEnqueued && phase != SellPhase.Failed;
}
