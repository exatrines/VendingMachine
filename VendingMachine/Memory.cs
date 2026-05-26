using ECommons.EzHookManager;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace VendingMachine;

public unsafe class Memory
{
    private delegate void OfferItemTrade(nint tradeAddress, ushort slot, InventoryType type);

    [EzHook("48 89 6C 24 ?? 48 89 74 24 ?? 57 48 83 EC 30 83 B9 ?? ?? ?? ?? ?? 41 8B F0", false)]
    private EzHook<OfferItemTrade> OfferItemTradeHook = null!;

    public bool HookReady { get; private set; }

    public Memory()
    {
        try
        {
            EzSignatureHelper.Initialize(this);
            HookReady = true;
        }
        catch (Exception ex)
        {
            ex.Log();
            HookReady = false;
        }
    }

    private void OfferItemTradeDetour(nint tradeAddress, ushort slot, InventoryType type) =>
        OfferItemTradeHook.Original(tradeAddress, slot, type);

    public void SafeOfferItemTrade(InventoryType type, ushort slot)
    {
        if (!HookReady)
            throw new InvalidOperationException("OfferItemTrade hook is not available.");

        nint tradeAddress = ((nint)UIModule.Instance()->GetAgentModule()->GetAgentByInternalId(AgentId.Trade)) + 40;
        if (Utils.GetSlot(type, slot)->GetItemId() == 0)
            throw new InvalidOperationException($"Attempted to trade from empty slot {type}, {slot}");

        OfferItemTradeHook.Original(tradeAddress, slot, type);
    }
}
