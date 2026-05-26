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
    private int expectedTradeSlots;
    private int placementAttempts;
    private List<SellEntry> activeSellEntries = [];
    private List<QueueEntry> pendingQueue = [];
    private int expectedGil;

    public void Reset()
    {
        phase = SellPhase.Idle;
        itemsEnqueued = false;
        emptyQueueReported = false;
        expectedTradeSlots = 0;
        placementAttempts = 0;
        activeSellEntries = [];
        pendingQueue = [];
        expectedGil = 0;
        TradeTask.ConfirmAllowed = false;
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
            BeginTradeWindow();

        if (phase == SellPhase.PlacingItems && !TradeTask.IsActive)
            OnPlacementTaskFinished();

        if (phase is SellPhase.WaitingGil or SellPhase.ReadyToConfirm)
            UpdateWaitingGil();
    }

    private void BeginTradeWindow()
    {
        if (phase != SellPhase.Idle || C.SellEntries.Count == 0)
            return;

        if (!P.Memory.HookReady)
        {
            FailSell("OfferItemTrade hook is not available. Rebuild the plugin or update Dalamud.");
            return;
        }

        var build = InventoryAllocator.BuildSellQueue(C.SellEntries);
        activeSellEntries = build.IncludedEntries;
        pendingQueue = build.Queue;
        expectedGil = PriceCalculator.SellExpectedTotal(activeSellEntries);
        expectedTradeSlots = pendingQueue.Count;

        if (expectedGil > TradeLimits.MaxGilPerTrade)
        {
            if (!emptyQueueReported)
            {
                Plugin.NotifyError(
                    $"Sell total {expectedGil:N0} gil exceeds 1,000,000. Lower prices or remove items.");
                VmLog.Warning($"sell total {expectedGil:N0} gil exceeds 1,000,000 — not placing items.");
                emptyQueueReported = true;
            }

            FailSell(null);
            return;
        }

        if (pendingQueue.Count == 0)
        {
            if (!emptyQueueReported)
            {
                VmLog.Warning("no sell lines could be queued (no matching inventory stacks).");
                emptyQueueReported = true;
            }

            FailSell(null);
            return;
        }

        placementAttempts = 0;
        BeginPlacingItems();
    }

    private void BeginPlacingItems()
    {
        phase = SellPhase.PlacingItems;
        itemsEnqueued = true;
        placementAttempts++;
        TaskAddItemsToTrade.EnqueueItems(pendingQueue, allowConfirmAfter: false);
    }

    private void OnPlacementTaskFinished()
    {
        var placed = TradeTask.GetMyTradeSlotCount();
        VmLog.Debug($"sell placement finished with {placed}/{expectedTradeSlots} trade slots.");

        if (placed < expectedTradeSlots && placementAttempts < 2)
        {
            VmLog.Warning($"placed {placed}/{expectedTradeSlots} item(s); retrying offer sequence.");
            BeginPlacingItems();
            return;
        }

        if (placed < expectedTradeSlots)
        {
            FailSell($"Could only place {placed} of {expectedTradeSlots} configured item(s) in the trade window.");
            return;
        }

        phase = SellPhase.WaitingGil;
        VmLog.Information($"placed {placed}/{expectedTradeSlots} item(s); waiting for {expectedGil:N0} gil.");
    }

    private void FailSell(string? userMessage)
    {
        if (!string.IsNullOrEmpty(userMessage))
            Plugin.NotifyError(userMessage);

        itemsEnqueued = true;
        phase = SellPhase.Failed;
        TradeTask.ConfirmAllowed = false;
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
