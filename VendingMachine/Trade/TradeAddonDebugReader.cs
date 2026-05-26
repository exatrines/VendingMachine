using ECommons.ExcelServices;

namespace VendingMachine.Trade;

/// <summary>Trade debug snapshot from game memory (TradeItemsLocal/Remote, trade state).</summary>
public static class TradeAddonDebugReader
{
    public static bool TryBuildSnapshot(out TradeAddonDebugSnapshot snapshot)
    {
        snapshot = CreateEmptySnapshot();
        if (!TradeAddonReader.IsTradeSessionActive())
            return false;

        var mySlots = ReadOfferSlots(local: true);
        var opponentSlots = ReadOfferSlots(local: false);

        snapshot = new TradeAddonDebugSnapshot
        {
            MyOfferSlot1 = mySlots[0],
            MyOfferSlot2 = mySlots[1],
            MyOfferSlot3 = mySlots[2],
            MyOfferSlot4 = mySlots[3],
            MyOfferSlot5 = mySlots[4],
            MyOfferGil = TradeAddonReader.GetMyTradeGilFromInventory(),
            MyDone = TradeAddonReader.IsLocalTradeLocked(),
            OpponentPlayerName = ResolveOpponentPlayerName(),
            OpponentDone = TradeAddonReader.IsOpponentTradeLocked(),
            OpponentOfferSlot1 = opponentSlots[0],
            OpponentOfferSlot2 = opponentSlots[1],
            OpponentOfferSlot3 = opponentSlots[2],
            OpponentOfferSlot4 = opponentSlots[3],
            OpponentOfferSlot5 = opponentSlots[4],
            OpponentOfferGil = TradeAddonReader.GetOpponentTradeGilFromInventory(),
        };

        return true;
    }

    public static TradeAddonDebugSnapshot CreateEmptySnapshotForDisplay() => CreateEmptySnapshot();

    private static TradeAddonDebugSnapshot CreateEmptySnapshot() =>
        new() { OpponentPlayerName = ResolveOpponentPlayerName() };

    private static TradeAddonSlotDebugInfo[] ReadOfferSlots(bool local)
    {
        var slots = new TradeAddonSlotDebugInfo[5];
        for (var i = 0; i < 5; i++)
            slots[i] = ReadOfferSlot(local, i);

        return slots;
    }

    private static TradeAddonSlotDebugInfo ReadOfferSlot(bool local, int slotIndex)
    {
        TradeAddonReader.GetTradeSlotFromMemory(local, slotIndex, out var itemId, out var quantity, out var isHq);
        if (itemId == 0)
            return new TradeAddonSlotDebugInfo { Name = "(empty)" };

        var name = SafeItemName(itemId, isHq);
        if (quantity > 1)
            name = $"{name} ×{quantity}";

        return new TradeAddonSlotDebugInfo { ItemId = itemId, Name = name };
    }

    private static string ResolveOpponentPlayerName()
    {
        var partner = TradePartnerHelper.GetTradePartner();
        if (partner != null)
        {
            var name = partner.Name.ToString();
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        var buyInfo = P.Trade.GetBuySplitPaymentDebugInfo();
        if (!string.IsNullOrWhiteSpace(buyInfo.PartnerName))
            return buyInfo.PartnerName;

        return "";
    }

    private static string SafeItemName(uint itemId, bool isHq)
    {
        try
        {
            var sheetId = isHq ? itemId + 1_000_000u : itemId;
            var name = ExcelItemHelper.GetName(sheetId);
            return string.IsNullOrWhiteSpace(name) ? $"item {itemId}" : name;
        }
        catch
        {
            return $"item {itemId}";
        }
    }
}
