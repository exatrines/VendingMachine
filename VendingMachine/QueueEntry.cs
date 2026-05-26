using FFXIVClientStructs.FFXIV.Client.Game;

namespace VendingMachine;

public readonly record struct QueueEntry(InventoryType Type, int SlotId, int Quantity);
