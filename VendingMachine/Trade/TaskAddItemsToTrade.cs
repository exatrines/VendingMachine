using FFXIVClientStructs.FFXIV.Component.GUI;

namespace VendingMachine.Trade;

public static unsafe class TaskAddItemsToTrade
{
    private const int TaskTimeoutMs = 20_000;

    public static void EnqueueItems(IEnumerable<QueueEntry> entries, bool allowConfirmAfter)
    {
        TradeTask.ResetTradeSlotWait();

        Tasks.Enqueue(() =>
        {
            TradeTask.ConfirmAllowed = false;
            return true;
        }, TaskTimeoutMs, "ConfirmAllowed = false");

        Tasks.Enqueue(TradeTask.WaitUntilTradeOpen, TaskTimeoutMs);

        foreach (var entry in entries)
        {
            var quantity = entry.Quantity;
            var invQty = (int)Utils.GetSlot(entry.Type, entry.SlotId)->GetQuantity();

            Tasks.Enqueue(() =>
            {
                if (!TryGetAddonByName<AtkUnitBase>("Trade", out var addon) || !IsAddonReady(addon))
                    return false;

                if (TradeTask.IsNumericOpen())
                    return false;

                if (!TradeTask.GenericThrottle() || !EzThrottler.Throttle("VmOfferTrade", 300))
                    return false;

                if (!P.Memory.CanOfferItemTrade(entry.Type, (ushort)entry.SlotId))
                    return false;

                var slotsBefore = TradeTask.GetMyTradeSlotCount();
                if (!P.Memory.TryOfferItemTrade(entry.Type, (ushort)entry.SlotId))
                    return false;

                TradeTask.BeginNumericWait();
                TradeTask.SetPendingOfferSlotsBefore(slotsBefore);
                return true;
            }, TaskTimeoutMs, $"OfferItem {entry.Type}:{entry.SlotId}");

            if (quantity > 1 && invQty > 1)
            {
                Tasks.Enqueue(
                    () => TradeTask.WaitForNumericOrItemPlaced(TradeTask.GetPendingOfferSlotsBefore()),
                    TaskTimeoutMs);

                Tasks.Enqueue(
                    () => TradeTask.ApplyItemQuantityIfNeeded(quantity, TradeTask.GetPendingOfferSlotsBefore()),
                    TaskTimeoutMs,
                    $"ApplyQuantity {quantity}");

                Tasks.Enqueue(TradeTask.WaitUntilNumericClosed, TaskTimeoutMs);
            }

            Tasks.Enqueue(
                () => TradeTask.WaitForSlotCountAbove(TradeTask.GetPendingOfferSlotsBefore()),
                TaskTimeoutMs,
                $"WaitSlotCount>{TradeTask.GetPendingOfferSlotsBefore()}");

            Tasks.Enqueue(TradeTask.PauseBetweenOffers, TaskTimeoutMs, "PauseBetweenOffers");
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
