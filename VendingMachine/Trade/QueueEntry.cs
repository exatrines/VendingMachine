using FFXIVClientStructs.FFXIV.Client.Game;

namespace VendingMachine.Trade;

public readonly record struct QueueEntry(InventoryType Type, int SlotId, int Quantity);
