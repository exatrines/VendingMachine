using FFXIVClientStructs.FFXIV.Client.Game;

namespace VendingMachine;

public static unsafe class Utils
{
    public static InventoryItem* GetSlot(InventoryType type, int slot)
    {
        var im = InventoryManager.Instance();
        var cont = im->GetInventoryContainer(type);
        return cont->GetInventorySlot(slot);
    }
}
