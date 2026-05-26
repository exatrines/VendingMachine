using ECommons.Automation.UIInput;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace VendingMachine.Trade;

public static unsafe partial class TradeAddonReader
{
    public static bool TryGetTradeAddon(out AtkUnitBase* addon)
    {
        addon = null;
        return TryGetAddonByName("Trade", out addon) && IsAddonReady(addon) && IsTradeReady(addon);
    }

    /// <summary>True when a player trade window is open (condition flag or Trade addon visible).</summary>
    public static bool IsTradeSessionActive() =>
        Svc.Condition[ConditionFlag.TradeOpen] || IsTradeAddonVisible();

    /// <summary>True when the Trade addon exists and is visible.</summary>
    public static bool IsTradeAddonVisible()
    {
        if (!TryGetAddonByName("Trade", out AtkUnitBase* addon) || addon == null)
            return false;

        return addon->IsVisible;
    }

    public static bool IsTradeReady(AtkUnitBase* addon)
    {
        if (addon == null)
            return false;

        const int readyProbeNodeId = 31;
        if (readyProbeNodeId < 0 || readyProbeNodeId >= addon->UldManager.NodeListCount)
            return true;

        var node = addon->UldManager.NodeList[readyProbeNodeId];
        if (node == null)
            return true;

        var componentNode = node->GetAsAtkComponentNode();
        if (componentNode == null || componentNode->Component == null)
            return true;

        var uld = componentNode->Component->UldManager;
        if (uld.NodeListCount == 0)
            return true;

        var child = uld.NodeList[0];
        if (child == null)
            return true;

        var imageNode = child->GetAsAtkImageNode();
        if (imageNode == null)
            return true;

        return imageNode->AtkResNode.Color.A == 0xFF;
    }

    public static TradeState GetLocalTradeState() => InventoryManager.Instance()->TradeLocalState;

    public static TradeState GetRemoteTradeState() => InventoryManager.Instance()->TradeRemoteState;

    public static bool IsOpponentTradeLocked() =>
        GetRemoteTradeState() is TradeState.LockedIn or TradeState.WaitingForConfirmation or TradeState.Confirmed;

    public static bool IsLocalTradeLocked() =>
        GetLocalTradeState() is TradeState.LockedIn or TradeState.WaitingForConfirmation or TradeState.Confirmed;

    public static int CountMyTradeSlotsFromAddon(AtkUnitBase* addon) =>
        CountVisibleTradeSlots(addon, mySlots: true);

    public static int CountOpponentTradeSlotsFromAddon(AtkUnitBase* addon) =>
        CountVisibleTradeSlots(addon, mySlots: false);

    private static int CountVisibleTradeSlots(AtkUnitBase* addon, bool mySlots)
    {
        var count = 0;
        var baseIndex = mySlots ? 10 : 15;
        for (var i = 0; i < 5; i++)
        {
            var slotNode = addon->UldManager.NodeList[baseIndex + i];
            if (slotNode == null)
                continue;

            var componentNode = slotNode->GetAsAtkComponentNode();
            if (componentNode == null || componentNode->Component == null)
                continue;

            var uld = componentNode->Component->UldManager;
            if (uld.NodeListCount == 0)
                continue;

            var iconNode = uld.NodeList[0];
            if (iconNode != null && iconNode->IsVisible())
                count++;
        }

        return count;
    }

    public static int CountMyTradeSlotsFromInventory()
    {
        var count = 0;
        var tradeItems = InventoryManager.Instance()->TradeItemsLocal;
        for (var i = 0; i < 5; i++)
        {
            if (tradeItems[i].GetItemId() != 0)
                count++;
        }

        return count;
    }

    public static void GetTradeSlotFromMemory(bool local, int slotIndex, out uint itemId, out uint quantity, out bool isHq)
    {
        itemId = 0;
        quantity = 0;
        isHq = false;
        if (slotIndex is < 0 or > 4)
            return;

        var items = local
            ? InventoryManager.Instance()->TradeItemsLocal
            : InventoryManager.Instance()->TradeItemsRemote;

        var rawId = items[slotIndex].GetItemId();
        if (rawId == 0)
            return;

        isHq = ItemIdHelper.IsHq(rawId);
        itemId = ItemIdHelper.Normalize(rawId);
        quantity = items[slotIndex].GetQuantity();
    }

    public static bool CanConfirmTrade(AtkUnitBase* addon) =>
        IsTradeReady(addon) || TradeTask.ConfirmAllowed;

    public static bool IsAwaitingFinalTradeConfirm() =>
        GetLocalTradeState() is TradeState.WaitingForConfirmation or TradeState.Confirmed
        && GetRemoteTradeState() is TradeState.WaitingForConfirmation or TradeState.Confirmed;

    public static bool TryClickTradeOfferButton(AtkUnitBase* addon)
    {
        foreach (var nodeIndex in new[] { 3, 4, 2, 5, 6 })
        {
            if (TryClickTradeOfferButtonAtNode(addon, nodeIndex))
            {
                VmLog.Debug($"clicked trade offer button (node {nodeIndex}).");
                return true;
            }
        }

        VmLog.Warning("could not click trade offer button (条件提示).");
        return false;
    }

    private static bool TryClickTradeOfferButtonAtNode(AtkUnitBase* addon, int nodeIndex)
    {
        var node = addon->UldManager.NodeList[nodeIndex];
        if (node == null)
            return false;

        var componentNode = node->GetAsAtkComponentNode();
        if (componentNode == null)
            return false;

        var button = (AtkComponentButton*)componentNode->Component;
        if (button == null || !button->AtkResNode->IsVisible())
            return false;

        if (!button->IsEnabled)
        {
            var flagsPtr = (ushort*)&button->AtkComponentBase.OwnerNode->AtkResNode.NodeFlags;
            *flagsPtr ^= 1 << 5;
        }

        button->ClickAddonButton(addon);
        return true;
    }
}
