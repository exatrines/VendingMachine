namespace VendingMachine.Trade;

public static unsafe class TaskAddItemsToTrade
{
    private const int TaskTimeoutMs = 15_000;

    public static void EnqueueItems(IEnumerable<QueueEntry> entries, bool allowConfirmAfter)
    {
        TradeTask.ResetTradeSlotWait();

        Tasks.Enqueue(() =>
        {
            TradeTask.ConfirmAllowed = false;
            return true;
        }, TaskTimeoutMs, "ConfirmAllowed = false");

        Tasks.Enqueue(TradeTask.WaitUntilTradeOpen, TaskTimeoutMs);

        var targetTradeSlots = 0;
        foreach (var entry in entries)
        {
            var quantity = entry.Quantity;
            targetTradeSlots++;

            Tasks.Enqueue(() =>
            {
                if (!TradeAddonReader.TryGetTradeAddon(out var addon) || !IsAddonReady(addon))
                    return false;

                if (TradeTask.IsNumericOpen())
                    return false;

                if (TradeTask.GenericThrottle() && EzThrottler.Throttle("VmOfferTrade", 100))
                {
                    P.Memory.SafeOfferItemTrade(entry.Type, (ushort)entry.SlotId);
                    return true;
                }

                return false;
            }, TaskTimeoutMs, $"OfferItem {entry.Type}:{entry.SlotId}");

            Tasks.Enqueue(() =>
            {
                TradeTask.BeginNumericWait();
                return true;
            }, TaskTimeoutMs, "BeginNumericWait");

            Tasks.Enqueue(TradeTask.WaitForNumericPrompt, TaskTimeoutMs);
            Tasks.Enqueue(() => TradeTask.ApplyNumericQuantity(quantity), TaskTimeoutMs, $"ApplyNumeric {quantity}");
            Tasks.Enqueue(TradeTask.WaitUntilNumericClosed, TaskTimeoutMs);
            Tasks.Enqueue(() => TradeTask.WaitForTradeSlots(targetTradeSlots), TaskTimeoutMs,
                $"WaitTradeSlots>={targetTradeSlots}");
        }

        if (allowConfirmAfter)
        {
            Tasks.Enqueue(() =>
            {
                TradeTask.ConfirmAllowed = true;
                return true;
            }, TaskTimeoutMs, "ConfirmAllowed = true");
        }
    }
}
