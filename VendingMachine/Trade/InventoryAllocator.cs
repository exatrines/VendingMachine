using FFXIVClientStructs.FFXIV.Client.Game;
using VendingMachine.Services;

namespace VendingMachine.Trade;

public static unsafe class InventoryAllocator
{
    private struct SlotAvailability
    {
        public InventoryType Type;
        public int Slot;
        public uint BaseItemId;
        public uint Remaining;
    }

    private static List<SlotAvailability> BuildAvailability()
    {
        var slots = new List<SlotAvailability>();
        var im = InventoryManager.Instance();

        foreach (var inv in ItemSearchService.ValidInventories)
        {
            var cont = im->GetInventoryContainer(inv);
            for (var i = 0; i < cont->GetSize(); i++)
            {
                var item = cont->GetInventorySlot(i);
                var itemId = item->GetItemId();
                if (itemId == 0)
                    continue;

                slots.Add(new SlotAvailability
                {
                    Type = inv,
                    Slot = i,
                    BaseItemId = ItemIdHelper.Normalize(itemId),
                    Remaining = item->GetQuantity(),
                });
            }
        }

        return slots;
    }

    /// <summary>
    /// Maps each sell line to inventory. Multiple lines may use the same stack when quantity allows.
    /// </summary>
    public static SellQueueBuildResult BuildSellQueue(IReadOnlyList<SellEntry> entries)
    {
        var availability = BuildAvailability();
        var result = new SellQueueBuildResult();

        foreach (var entry in entries)
        {
            if (entry.Count <= 0 || entry.ItemId == 0)
                continue;

            var baseItemId = ItemIdHelper.Normalize(entry.ItemId);
            var need = (uint)entry.Count;
            var index = availability.FindIndex(s =>
                s.BaseItemId == baseItemId && s.Remaining >= need);

            if (index < 0)
            {
                var name = ItemSearchService.GetItemName(baseItemId);
                VmLog.Warning($"skipped sell line — no stack of {name} x{need} remaining in inventory.");
                continue;
            }

            var slot = availability[index];
            result.Queue.Add(new QueueEntry(slot.Type, slot.Slot, entry.Count));
            result.IncludedEntries.Add(entry);

            slot.Remaining -= need;
            availability[index] = slot;
        }

        if (result.IncludedEntries.Count < entries.Count(e => e.Count > 0 && e.ItemId != 0))
        {
            VmLog.Information(
                $"{result.IncludedEntries.Count} sell line(s) queued, " +
                $"{entries.Count(e => e.Count > 0 && e.ItemId != 0) - result.IncludedEntries.Count} skipped (insufficient inventory).");
        }

        return result;
    }
}
