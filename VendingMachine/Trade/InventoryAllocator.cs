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

        public bool Used;

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

                    BaseItemId = itemId % 1_000_000,

                    Remaining = item->GetQuantity(),

                });

            }

        }



        return slots;

    }



    /// <summary>

    /// Each sell list line must map to one distinct inventory stack with at least the configured quantity.

    /// Lines without a matching stack are skipped (logged) and excluded from the queue.

    /// </summary>

    public static SellQueueBuildResult BuildSellQueue(IReadOnlyList<SellEntry> entries)

    {

        var availability = BuildAvailability();

        var result = new SellQueueBuildResult();



        foreach (var entry in entries)

        {

            if (entry.Count <= 0 || entry.ItemId == 0)

                continue;



            var baseItemId = entry.ItemId % 1_000_000;

            var need = (uint)entry.Count;

            var index = availability.FindIndex(s =>

                !s.Used && s.BaseItemId == baseItemId && s.Remaining >= need);



            if (index < 0)

            {

                var name = ItemSearchService.GetItemName(baseItemId);

                PluginLog.Warning(

                    $"Vending Machine: skipped sell line — no inventory stack of {name} x{need} (each list line needs its own stack).");

                continue;

            }



            var slot = availability[index];

            result.Queue.Add(new QueueEntry(slot.Type, slot.Slot, entry.Count));

            result.IncludedEntries.Add(entry);



            slot.Used = true;

            availability[index] = slot;

        }



        if (result.IncludedEntries.Count < entries.Count(e => e.Count > 0 && e.ItemId != 0))

        {

            PluginLog.Information(

                $"Vending Machine: {result.IncludedEntries.Count} sell line(s) queued, " +

                $"{entries.Count(e => e.Count > 0 && e.ItemId != 0) - result.IncludedEntries.Count} skipped (no matching stack).");

        }



        return result;

    }

}


