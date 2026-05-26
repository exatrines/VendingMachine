using Lumina.Excel.Sheets;

namespace VendingMachine.Services;

public sealed class TradeableItemCache
{
    private uint[]? tradeableItemIds;

    public uint[] GetTradeableItemIds()
    {
        tradeableItemIds ??= Svc.Data.GetExcelSheet<Item>()
            .Where(x => !x.IsUntradable)
            .Select(x => x.RowId)
            .ToArray();

        return tradeableItemIds;
    }

    public bool IsTradeable(uint itemId) => GetTradeableItemIds().Contains(itemId);
}
