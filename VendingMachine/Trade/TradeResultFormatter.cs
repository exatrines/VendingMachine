using VendingMachine.Services;

namespace VendingMachine.Trade;

public sealed class TradeResultLine
{
    public string? ItemName { get; init; }
    public int Quantity { get; init; }
    public int LineGil { get; init; }
}

public static class TradeResultFormatter
{
    public static string Format(TradeMode mode, IReadOnlyList<TradeResultLine> lines, int totalGil)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("--- Vending Result ---");
        sb.AppendLine($"Mode: {mode}");
        sb.AppendLine();

        for (var i = 0; i < 5; i++)
        {
            if (i < lines.Count && lines[i].ItemName != null)
            {
                sb.AppendLine($"{i + 1}. {lines[i].ItemName} * {lines[i].Quantity}: {lines[i].LineGil:N0}");
            }
            else
            {
                sb.AppendLine($"{i + 1}. None");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Total: {totalGil:N0}");
        return sb.ToString().TrimEnd();
    }

    public static List<TradeResultLine> DecomposeQuantity(uint itemId, int totalQty, int pricePerStack, int pricePerItem)
    {
        var lines = new List<TradeResultLine>();
        var name = ItemSearchService.GetItemName(itemId);
        var remaining = totalQty;

        while (remaining >= 999)
        {
            lines.Add(new TradeResultLine
            {
                ItemName = name,
                Quantity = 999,
                LineGil = pricePerStack,
            });
            remaining -= 999;
        }

        if (remaining > 0)
        {
            lines.Add(new TradeResultLine
            {
                ItemName = name,
                Quantity = remaining,
                LineGil = remaining * pricePerItem,
            });
        }

        return lines;
    }

    public static List<TradeResultLine> PadToFive(IReadOnlyList<TradeResultLine> lines)
    {
        var result = lines.Take(5).ToList();
        while (result.Count < 5)
            result.Add(new TradeResultLine());
        return result;
    }
}
