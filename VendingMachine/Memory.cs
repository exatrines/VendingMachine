using ECommons.EzHookManager;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using VendingMachine.Trade;

namespace VendingMachine;

public unsafe class Memory
{
    private const nint TradeAgentOffset = 0x28;

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

    public bool CanOfferItemTrade(InventoryType type, ushort slot)
    {
        if (!HookReady)
            return false;

        if (Utils.GetSlot(type, slot)->GetItemId() == 0)
            return false;

        return GetTradeAgentAddress() != 0;
    }

    public bool TryOfferItemTrade(InventoryType type, ushort slot)
    {
        if (!CanOfferItemTrade(type, slot))
        {
            VmLog.Warning($"cannot offer from {type} slot {slot} (hook={HookReady}).");
            return false;
        }

        OfferItemTradeHook.Original(GetTradeAgentAddress(), slot, type);
        return true;
    }

    /// <summary>Same trade-agent pointer Dropbox uses (AgentTrade + 0x28).</summary>
    private static nint GetTradeAgentAddress()
    {
        var trade = UIModule.Instance()->GetAgentModule()->GetAgentByInternalId(AgentId.Trade);
        return trade == null ? 0 : (nint)trade + TradeAgentOffset;
    }
}
